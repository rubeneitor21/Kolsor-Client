using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Dados de la tirada actual (cuenco propio).
    // Se llena cuando llega dice-rolled dirigido a mí.
    public List<DiceData> MyDice { get; private set; } = new();

    // Dados sobrantes tras confirmar (los que NO guardé).
    // Se muestran en el cuenco propio mientras es el turno del rival.
    // Se limpian cuando vuelvo a tirar o empieza nueva ronda.
    public List<DiceData> MySurvivors { get; private set; } = new();

    // Dados actuales en el cuenco del rival.
    // Se establece cuando el rival tira, se reduce a los sobrantes cuando confirma.
    // Se usa en RebuildAll para que el cuenco rival no quede vacío.
    public List<DiceData> EnemyCurrentBowl { get; private set; } = new();

    // Dados confirmados acumulados (la fila lateral).
    // Se reconstruyen desde state.users[id].selectedRolls cuando confirmamos.
    public List<DiceData> MyConfirmed { get; private set; } = new();
    public List<DiceData> EnemyConfirmed { get; private set; } = new();

    // Estado del juego
    public GameState CurrentState { get; private set; }
    public bool IsMyTurn => CurrentState?.activePlayer == GameData.MyId;
    public string OpponentName => GameData.OpponentName;

    // Banderas de control
    public bool MyDiceRolled { get; private set; } = false;
    public bool GameStarted { get; private set; } = false;

    public bool CanRoll => IsMyTurn
                           && CurrentState?.state == "select-rolls"
                           && !MyDiceRolled
                           && !InputBlocked
                           && !_animating
                           && !_waitingServer
                           && GameStarted;

    public bool CanConfirm => IsMyTurn
                              && CurrentState?.state == "select-rolls"
                              && MyDiceRolled
                              && MyDice != null && MyDice.Count > 0
                              && !InputBlocked
                              && !_animating
                              && !_waitingServer;

    public bool CanClickDice => CanConfirm;

    public static event Action OnRollsChanged;
    public static event Action OnTurnChanged;

    public static bool InputBlocked = false;
    private bool _animating = false;
    private bool _waitingServer = false;
    private DiceController _hoveredDice = null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable() { WebSocketManager.OnMessageReceived += HandleMessage; }
    void OnDisable() { WebSocketManager.OnMessageReceived -= HandleMessage; }

    void Start()
    {
        Debug.Log("[Game] GameManager.Start()");
        Debug.Log($"[Game] MyId:'{GameData.MyId}' | OpponentId:'{GameData.OpponentId}' | RoomId:'{GameData.RoomId}'");

        // Al cargar GameScene, marcamos que el juego ha empezado y
        // construimos el estado inicial: ronda 1, turno del playerStart.
        GameStarted = true;
        CurrentState = new GameState
        {
            state = "select-rolls",
            round = 1,
            activePlayer = GameData.PlayerStartId
        };
        MyConfirmed.Clear();
        EnemyConfirmed.Clear();
        MySurvivors.Clear();
        EnemyCurrentBowl.Clear();

        BoardManager.Instance?.RebuildAll();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (CanRoll)
            {
                Debug.Log("[Game] SPACE → roll-dice");
                WebSocketManager.Instance.Send("roll-dice");
                _waitingServer = true;
                OnTurnChanged?.Invoke();
            }
            else if (CanConfirm)
            {
                Debug.Log("[Game] SPACE → Confirmar selección");
                ConfirmSelection();
            }
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log($"[Game] CLICK izquierdo | CanClickDice={CanClickDice}");
                if (CanClickDice) HandleDiceClick();
            }

            // Hover: resalta el dado bajo el cursor usando raycast.
            // OnMouseEnter/Exit no son fiables con el New Input System en modo exclusivo.
            HandleHover();
        }
        else
        {
            // Sin ratón (táctil / sin dispositivo): limpiar hover si lo había.
            if (_hoveredDice != null)
            {
                _hoveredDice.OnHoverExit();
                _hoveredDice = null;
            }
        }
    }

    private void HandleHover()
    {
        var newHover = BoardManager.Instance?.GetHoveredDie(Mouse.current.position.ReadValue());
        if (newHover != _hoveredDice)
        {
            _hoveredDice?.OnHoverExit();
            newHover?.OnHoverEnter();
            _hoveredDice = newHover;
        }
    }

    private void HandleDiceClick()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[Game] Camera.main es NULL — la cámara no tiene tag MainCamera");
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        Debug.Log($"[Game] Raycast desde ({mousePos.x:F0},{mousePos.y:F0}) → {hits.Length} hits");

        DiceController closest = null;
        float closestDist = float.MaxValue;

        foreach (var h in hits)
        {
            var dice = h.collider.GetComponent<DiceController>();
            Debug.Log($"[Game]   hit: {h.collider.gameObject.name} | dice={(dice != null ? "OK" : "NULL")}");

            if (dice == null || dice.Data == null || !dice.Data.isMyDice) continue;
            if (h.distance < closestDist)
            {
                closestDist = h.distance;
                closest = dice;
            }
        }

        if (closest != null)
        {
            Debug.Log($"[Game] ToggleKeep en dado: {closest.Data.face}");
            closest.ToggleKeep();
        }
        else
        {
            Debug.Log("[Game] No se encontró dado válido para clicar");
        }
    }

    private void HandleMessage(string type, string body)
    {
        switch (type)
        {
            case "dice-rolled":
                HandleDiceRolled(body);
                break;
            case "selection-confirmed":
                HandleSelectionConfirmed(body);
                break;
            case "round-start":
                HandleRoundStart(body);
                break;
        }
    }

    /// El servidor confirma que un jugador (yo o el rival) ha tirado.
    /// Body: { user, rolls: [...], round }
    private void HandleDiceRolled(string body)
    {
        string user = ExtractStringValue(body, "user");
        string rollsArray = ExtractArray(body, "rolls");
        var rolls = string.IsNullOrEmpty(rollsArray)
            ? new List<DiceData>()
            : ParseDiceArray(rollsArray);

        bool isMe = (user == GameData.MyId);
        Debug.Log($"[Game] dice-rolled de {(isMe ? "YO" : "RIVAL")} | {rolls.Count} dados");
        Debug.Log($"[Game] HandleDiceRolled: InputBlocked={InputBlocked} | _animating={_animating} | _waitingServer={_waitingServer}");

        if (isMe)
        {
            // Al tirar de nuevo, los sobrantes de la tirada anterior ya no son necesarios.
            // BoardManager reutilizará sus GameObjects como base para la nueva animación.
            MySurvivors.Clear();

            MyDice = rolls;
            foreach (var d in MyDice) { d.isMyDice = true; d.kept = false; }
            Debug.Log($"[Game] Antes de animar mi tirada: MyDiceRolled={MyDiceRolled}");
            StartCoroutine(AnimateMyRollAndUnlock());
        }
        else
        {
            // El rival tira. Guardamos sus dados para RebuildAll.
            EnemyCurrentBowl = rolls;
            foreach (var d in EnemyCurrentBowl) { d.isMyDice = false; d.kept = false; }
            Debug.Log($"[Game] Antes de animar tirada rival: rolls.Count={rolls.Count}");
            StartCoroutine(AnimateEnemyRollAndUnlock(rolls));
        }
    }

    private IEnumerator AnimateMyRollAndUnlock()
    {
        _animating = true;
        OnTurnChanged?.Invoke();
        yield return BoardManager.Instance?.AnimateMyRoll(MyDice);
        MyDiceRolled = true;
        _animating = false;
        _waitingServer = false;
        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();

        Debug.Log($"[Game] Mi animación terminada | MyDiceRolled={MyDiceRolled} | CanClickDice={CanClickDice} | _waitingServer={_waitingServer}");
    }

    private IEnumerator AnimateEnemyRollAndUnlock(List<DiceData> enemyRolls)
    {
        InputBlocked = true;
        yield return BoardManager.Instance?.AnimateEnemyRoll(enemyRolls);
        InputBlocked = false;

        Debug.Log($"[Game] Animación rival terminada | InputBlocked={InputBlocked}");
    }

    /// El servidor avisa de que un jugador acaba de confirmar.
    /// Body: { user, selected: [...], state: {...} }
    private void HandleSelectionConfirmed(string body)
    {
        string user = ExtractStringValue(body, "user");
        bool isMe = (user == GameData.MyId);

        // Actualizamos el estado completo
        string stateJson = ExtractObject(body, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = ParseGameState(stateJson);
            Debug.Log($"[Game] Estado tras confirmación: {CurrentState?.state} | Turno: {CurrentState?.activePlayer} | Ronda: {CurrentState?.round}");

            // Reconstruimos las filas confirmadas desde el state
            MyConfirmed = ParseSelectedRolls(stateJson, GameData.MyId);
            EnemyConfirmed = ParseSelectedRolls(stateJson, GameData.OpponentId);

            foreach (var d in MyConfirmed) { d.isMyDice = true; d.kept = true; }
            foreach (var d in EnemyConfirmed) { d.isMyDice = false; d.kept = true; }
        }

        // Guardamos los dados sobrantes antes de limpiar.
        if (isMe)
        {
            // Mis sobrantes: los que no guardé en esta tirada.
            MySurvivors = MyDice.FindAll(d => !d.kept);
            foreach (var d in MySurvivors) { d.isMyDice = false; d.kept = false; }
            Debug.Log($"[Game] MySurvivors guardados: {MySurvivors.Count} dados");
        }
        else
        {
            // Sobrantes del rival: restamos los que guardó de su cuenco actual.
            // Así sabemos qué dados le quedan visibles en el cuenco rival.
            string selectedArray = ExtractArray(body, "selected");
            var selected = string.IsNullOrEmpty(selectedArray)
                ? new List<DiceData>()
                : ParseDiceArray(selectedArray);
            var survivors = new List<DiceData>(EnemyCurrentBowl);
            foreach (var sel in selected)
            {
                var match = survivors.Find(d => d.face == sel.face && d.energy == sel.energy);
                if (match != null) survivors.Remove(match);
            }
            EnemyCurrentBowl = survivors;
            Debug.Log($"[Game] EnemyCurrentBowl tras confirmación rival: {EnemyCurrentBowl.Count} sobrantes");
        }

        // Reseteamos el cuenco propio: la próxima vez que sea mi turno
        // tendré que pulsar espacio para tirar de nuevo.
        MyDice.Clear();
        MyDiceRolled = false;
        _waitingServer = false;

        Debug.Log($"[Game] selection-confirmed por {(isMe ? "YO" : "RIVAL")} | Mis confirmados:{MyConfirmed.Count} | Rival:{EnemyConfirmed.Count}");

        // Animación de los dados moviéndose a la fila correspondiente.
        // El BoardManager se encarga de que sea visible en ambos clientes.
        StartCoroutine(AnimateConfirmAndRebuild(isMe));

        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    private IEnumerator AnimateConfirmAndRebuild(bool wasMe)
    {
        InputBlocked = true;
        yield return BoardManager.Instance?.AnimateConfirmation(wasMe);
        BoardManager.Instance?.RebuildAll();
        InputBlocked = false;
        OnTurnChanged?.Invoke();
    }

    /// El servidor avisa del inicio de una nueva ronda tras la resolución.
    private void HandleRoundStart(string body)
    {
        string stateJson = ExtractObject(body, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = ParseGameState(stateJson);
            MyConfirmed = ParseSelectedRolls(stateJson, GameData.MyId);
            EnemyConfirmed = ParseSelectedRolls(stateJson, GameData.OpponentId);
        }
        MyDice.Clear();
        MySurvivors.Clear();
        EnemyCurrentBowl.Clear();
        MyDiceRolled = false;
        _waitingServer = false;
        BoardManager.Instance?.RebuildAll();
        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    public void ConfirmSelection()
    {
        if (!CanConfirm) return;

        var sb = new System.Text.StringBuilder();
        bool first = true;
        int keptCount = 0;
        sb.Append('[');
        foreach (var d in MyDice)
        {
            if (!d.kept) continue;
            if (!first) sb.Append(',');
            first = false;
            string energy = d.energy ? "true" : "false";
            sb.Append("{\"face\":\"").Append(d.face).Append("\",\"energy\":").Append(energy).Append('}');
            keptCount++;
        }
        sb.Append(']');

        string body = $"{{\"rolls\":{sb}}}";
        WebSocketManager.Instance.Send("select-rolls", body);
        Debug.Log($"[Game] Enviada selección al servidor: {keptCount} dados guardados");

        _waitingServer = true;
        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    // ── Helpers de parseo ─────────────────────────────────

    private List<DiceData> ParseDiceArray(string arrayJson)
    {
        var result = new List<DiceData>();
        int i = 0;
        while (i < arrayJson.Length)
        {
            int start = arrayJson.IndexOf('{', i);
            if (start == -1) break;
            int end = arrayJson.IndexOf('}', start);
            if (end == -1) break;

            string obj = arrayJson.Substring(start, end - start + 1);
            string faceStr = ExtractStringValue(obj, "face");
            bool energy = obj.Contains("\"energy\":true");

            if (Enum.TryParse<DiceFace>(faceStr, out DiceFace face))
            {
                result.Add(new DiceData
                {
                    face = face,
                    energy = energy,
                    kept = false,
                    isMyDice = false
                });
            }
            i = end + 1;
        }
        return result;
    }

    private List<DiceData> ParseSelectedRolls(string stateJson, string userId)
    {
        if (string.IsNullOrEmpty(userId)) return new List<DiceData>();

        string userMarker = $"\"{userId}\":{{";
        int userStart = stateJson.IndexOf(userMarker);
        if (userStart == -1) return new List<DiceData>();

        int depth = 0;
        int i = userStart + userMarker.Length - 1;
        int userObjStart = i;
        while (i < stateJson.Length)
        {
            if (stateJson[i] == '{') depth++;
            else if (stateJson[i] == '}') { depth--; if (depth == 0) break; }
            i++;
        }
        if (i >= stateJson.Length) return new List<DiceData>();

        string userObj = stateJson.Substring(userObjStart, i - userObjStart + 1);
        string rollsArray = ExtractArray(userObj, "selectedRolls");
        if (string.IsNullOrEmpty(rollsArray)) return new List<DiceData>();

        return ParseDiceArray(rollsArray);
    }

    private GameState ParseGameState(string stateJson)
    {
        var s = new GameState();
        s.state = ExtractStringValue(stateJson, "state");
        s.activePlayer = ExtractStringValue(stateJson, "activePlayer");

        string roundMarker = "\"round\":";
        int rStart = stateJson.IndexOf(roundMarker);
        if (rStart != -1)
        {
            rStart += roundMarker.Length;
            int rEnd = rStart;
            while (rEnd < stateJson.Length && (char.IsDigit(stateJson[rEnd]) || stateJson[rEnd] == '-')) rEnd++;
            int.TryParse(stateJson.Substring(rStart, rEnd - rStart), out s.round);
        }
        return s;
    }

    private string ExtractArray(string json, string key)
    {
        string marker = $"\"{key}\":[";
        int start = json.IndexOf(marker);
        if (start == -1) return "";
        start += marker.Length - 1;
        int depth = 0, i = start;
        while (i < json.Length)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
            i++;
        }
        return "";
    }

    private string ExtractObject(string json, string key)
    {
        string marker = $"\"{key}\":{{";
        int start = json.IndexOf(marker);
        if (start == -1) return "";
        start += marker.Length - 1;
        int depth = 0, i = start;
        while (i < json.Length)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
            i++;
        }
        return "";
    }

    private string ExtractStringValue(string json, string key)
    {
        string search = $"\"{key}\":\"";
        int start = json.IndexOf(search);
        if (start == -1) return "";
        start += search.Length;
        int end = json.IndexOf("\"", start);
        return end == -1 ? "" : json.Substring(start, end - start);
    }
}

[System.Serializable]
public class GameState
{
    public string state;
    public int round;
    public string activePlayer;
}