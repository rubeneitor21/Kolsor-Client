using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public List<DiceData> MyDice { get; private set; } = new();
    public List<DiceData> MySurvivors { get; private set; } = new();
    public List<DiceData> EnemyCurrentBowl { get; private set; } = new();
    public List<DiceData> MyConfirmed { get; private set; } = new();
    public List<DiceData> EnemyConfirmed { get; private set; } = new();

    public int MyEnergyPreHands { get; private set; } = 0;
    public int OpponentEnergyPreHands { get; private set; } = 0;
    // Energía base de god-favor (sin dados de energía) — usada para calcular el tier
    public int MyGodFavorEnergy { get; private set; } = 0;
    public int OpponentGodFavorEnergy { get; private set; } = 0;
    public string MyInvokedGod { get; private set; } = "";
    public string OpponentInvokedGod { get; private set; } = "";
    public int MyInvokedGodTier { get; private set; } = 0;
    public int OpponentInvokedGodTier { get; private set; } = 0;
    public GameState CurrentState { get; private set; }
    public int MyLife { get; private set; } = 15;
    public int OpponentLife { get; private set; } = 15;
    public int MyEnergy { get; private set; } = 0;
    public int OpponentEnergy { get; private set; } = 0;
    public bool IsMyTurn => CurrentState?.activePlayer == GameData.MyId;
    public string OpponentName => GameData.OpponentName;

    public bool MyDiceRolled { get; private set; } = false;
    public bool GameStarted { get; private set; } = false;

    public bool CanRoll => IsMyTurn
                           && CurrentState?.state == "select-rolls"
                           && !MyDiceRolled && !InputBlocked
                           && !_animating && !_waitingServer
                           && GameStarted;

    public bool CanConfirm => IsMyTurn
                           && CurrentState?.state == "select-rolls"
                           && MyDiceRolled
                           && MyDice != null && MyDice.Count > 0
                           && !InputBlocked && !_animating && !_waitingServer;

    public bool CanClickDice => CanConfirm && _myRollCount < 3;

    public static event Action OnRollsChanged;
    public static event Action OnTurnChanged;
    public static event Action OnGodFavorNeeded;
    public static event Action OnLifeUpdated;

    public static bool InputBlocked = false;
    private bool _animating = false;
    private bool _waitingServer = false;
    private bool _godFavorSelected = false;
    private int _myRollCount = 0;
    private bool _resolutionAnimated = false;

    private DiceController _hoveredDice = null;
    private GodFavorController _hoveredGod = null;

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

        GameStarted = true;
        CurrentState = new GameState
        {
            state = "select-rolls",
            round = 1,
            activePlayer = GameData.PlayerStartId
        };

        MyConfirmed.Clear(); EnemyConfirmed.Clear();
        MySurvivors.Clear(); EnemyCurrentBowl.Clear();

        BoardManager.Instance?.RebuildAll();
        BoardManager.Instance?.SpawnGodFigures();
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
            else if (CurrentState?.state == "god-favor" && !_godFavorSelected)
            {
                Debug.Log("[Game] SPACE → pasar favor divino");
                _godFavorSelected = true;
                GodInfoCard.Instance?.ForceHide();
                BoardManager.Instance?.DisableGodFavorInteraction();
                SendGodFavor("");
            }
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (CurrentState?.state == "god-favor" && !_godFavorSelected)
                    HandleGodClick();
                else
                {
                    Debug.Log($"[Game] CLICK izquierdo | CanClickDice={CanClickDice}");
                    if (CanClickDice) HandleDiceClick();
                }
            }
            HandleHover();
        }
        else
        {
            _hoveredDice?.OnHoverExit(); _hoveredDice = null;
            _hoveredGod?.OnHoverExit(); _hoveredGod = null;
        }
    }

    private void HandleHover()
    {
        var mousePos = Mouse.current.position.ReadValue();

        var newDice = BoardManager.Instance?.GetHoveredDie(mousePos);
        if (newDice != _hoveredDice)
        {
            if (_hoveredDice != null) _hoveredDice.OnHoverExit();
            newDice?.OnHoverEnter();
            _hoveredDice = newDice;
        }

        GodFavorController newGod = null;
        if (CurrentState?.state == "god-favor" && !_godFavorSelected)
            newGod = BoardManager.Instance?.GetHoveredGod(mousePos);
        if (newGod != _hoveredGod)
        {
            if (_hoveredGod != null) _hoveredGod.OnHoverExit();
            newGod?.OnHoverEnter();
            _hoveredGod = newGod;
        }
    }

    private void HandleGodClick()
    {
        var ctrl = BoardManager.Instance?.GetHoveredGod(Mouse.current.position.ReadValue());
        if (ctrl == null || !ctrl.IsInteractable) return;
        ctrl.Select();
        GodInfoCard.Instance?.Lock(); // mantener el card abierto
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
            if (h.distance < closestDist) { closestDist = h.distance; closest = dice; }
        }

        if (closest != null)
            closest.ToggleKeep();
        else
            Debug.Log("[Game] No se encontró dado válido para clicar");
    }

    private void HandleMessage(string type, string body)
    {
        switch (type)
        {
            case "dice-rolled": HandleDiceRolled(body); break;
            case "selection-confirmed": HandleSelectionConfirmed(body); break;
            case "round-start": HandleRoundStart(body); break;
            case "god-favor": HandleGodFavor(body); break;
            case "resolution-attack-first": HandleResolution(body, isSecond: false); break;
            case "resolution-attack-second": HandleResolution(body, isSecond: true); break;
            case "game-over": HandleGameOver(body); break;
        }
    }

    private void HandleDiceRolled(string body)
    {
        string user = ExtractStringValue(body, "user");
        string rollsArray = ExtractArray(body, "rolls");
        var rolls = string.IsNullOrEmpty(rollsArray) ? new List<DiceData>() : ParseDiceArray(rollsArray);
        bool isMe = (user == GameData.MyId);

        Debug.Log($"[Game] dice-rolled de {(isMe ? "YO" : "RIVAL")} | {rolls.Count} dados");

        if (isMe)
        {
            MySurvivors.Clear();
            MyDice = rolls;
            foreach (var d in MyDice) { d.isMyDice = true; d.kept = false; }
            _myRollCount++;
            StartCoroutine(AnimateMyRollAndUnlock());
        }
        else
        {
            EnemyCurrentBowl = rolls;
            foreach (var d in EnemyCurrentBowl) { d.isMyDice = false; d.kept = false; }
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

        if (_myRollCount >= 3)
        {
            Debug.Log("[Game] Tirada 3 → auto-confirmando todos los dados");
            foreach (var d in MyDice) d.kept = true;
            yield return new WaitForSeconds(0.4f);
            ConfirmSelection();
        }
    }

    private IEnumerator AnimateEnemyRollAndUnlock(List<DiceData> enemyRolls)
    {
        InputBlocked = true;
        yield return BoardManager.Instance?.AnimateEnemyRoll(enemyRolls);
        InputBlocked = false;
    }

    private void HandleSelectionConfirmed(string body)
    {
        string user = ExtractStringValue(body, "user");
        bool isMe = (user == GameData.MyId);

        string stateJson = ExtractObject(body, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = ParseGameState(stateJson);
            MyConfirmed = ParseSelectedRolls(stateJson, GameData.MyId);
            EnemyConfirmed = ParseSelectedRolls(stateJson, GameData.OpponentId);
            foreach (var d in MyConfirmed) { d.isMyDice = true; d.kept = true; }
            foreach (var d in EnemyConfirmed) { d.isMyDice = false; d.kept = true; }

            MyEnergy = ParseIntField(stateJson, GameData.MyId, "energy");
            OpponentEnergy = ParseIntField(stateJson, GameData.OpponentId, "energy");
            Debug.Log($"[Game] selection-confirmed → MyEnergy:{MyEnergy} OppEnergy:{OpponentEnergy}");
            BoardManager.Instance?.SpawnTokens(MyEnergy, OpponentEnergy);
        }

        if (isMe)
        {
            MySurvivors = MyDice.FindAll(d => !d.kept);
            foreach (var d in MySurvivors) { d.isMyDice = false; d.kept = false; }
        }
        else
        {
            string selectedArray = ExtractArray(body, "selected");
            var selected = string.IsNullOrEmpty(selectedArray) ? new List<DiceData>() : ParseDiceArray(selectedArray);
            var survivors = new List<DiceData>(EnemyCurrentBowl);
            foreach (var sel in selected)
            {
                var match = survivors.Find(d => d.face == sel.face && d.energy == sel.energy);
                if (match != null) survivors.Remove(match);
            }
            EnemyCurrentBowl = survivors;
        }

        MyDice.Clear();
        MyDiceRolled = false;
        _waitingServer = false;

        Debug.Log($"[Game] selection-confirmed por {(isMe ? "YO" : "RIVAL")} | Mis confirmados:{MyConfirmed.Count} | Rival:{EnemyConfirmed.Count}");

        StartCoroutine(AnimateConfirmAndRebuild(isMe));

        if (IsMyTurn && MyConfirmed.Count >= 6)
            StartCoroutine(AutoSkipTurn());

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

    private void HandleGodFavor(string body)
    {
        string stateJson = ExtractObject(body, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = ParseGameState(stateJson);
            MyConfirmed = ParseSelectedRolls(stateJson, GameData.MyId);
            EnemyConfirmed = ParseSelectedRolls(stateJson, GameData.OpponentId);
            foreach (var d in MyConfirmed) { d.isMyDice = true; d.kept = true; }
            foreach (var d in EnemyConfirmed) { d.isMyDice = false; d.kept = true; }

            MyLife = ParseIntField(stateJson, GameData.MyId, "life");
            OpponentLife = ParseIntField(stateJson, GameData.OpponentId, "life");
            MyEnergy = ParseIntField(stateJson, GameData.MyId, "energy");
            OpponentEnergy = ParseIntField(stateJson, GameData.OpponentId, "energy");

            // Energía base (lo que usó el servidor para calcular el tier del dios)
            MyGodFavorEnergy = MyEnergy;
            OpponentGodFavorEnergy = OpponentEnergy;

            // Energía PRE-manos: base + dados con energy:true de esta ronda
            int myEnergyDice = 0;
            if (MyConfirmed != null) foreach (var d in MyConfirmed) if (d.energy) myEnergyDice++;
            int oppEnergyDice = 0;
            if (EnemyConfirmed != null) foreach (var d in EnemyConfirmed) if (d.energy) oppEnergyDice++;
            MyEnergyPreHands = MyEnergy + myEnergyDice;
            OpponentEnergyPreHands = OpponentEnergy + oppEnergyDice;
        }

        MySurvivors.Clear();
        MyDice.Clear();
        EnemyCurrentBowl.Clear();
        MyDiceRolled = false;
        _waitingServer = false;
        _godFavorSelected = false;

        bool canAffordAny = false;
        if (GameData.MySelectedGods != null)
        {
            foreach (var god in GameData.MySelectedGods)
            {
                if (GodData.CanAffordAny(god, MyEnergy))
                {
                    canAffordAny = true;
                    break;
                }
            }
        }

        if (!canAffordAny)
        {
            Debug.Log($"[Game] god-favor: sin tokens suficientes (MyEnergy:{MyEnergy}) → auto-skip");
            BoardManager.Instance?.RebuildAll();
            StartCoroutine(SendGodFavorDelayed("", 3f));
            return;
        }

        Debug.Log($"[Game] god-favor: activando selección | MyEnergy:{MyEnergy}");
        CurrentState = new GameState
        {
            state = "god-favor",
            round = CurrentState?.round ?? 0,
            activePlayer = CurrentState?.activePlayer ?? ""
        };

        NotificationUI.Instance?.Show("Selecciona dioses u omite con espacio.", 2f);
        BoardManager.Instance?.RebuildAll();
        BoardManager.Instance?.EnableGodFavorInteraction();
        OnGodFavorNeeded?.Invoke();
        OnTurnChanged?.Invoke();
    }

    private IEnumerator SendGodFavorDelayed(string godName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SendGodFavor(godName);
    }

    /// Auto-skip si el jugador no interactúa en el tiempo límite.
    private IEnumerator GodFavorAutoSkip(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (!_godFavorSelected)
        {
            Debug.Log("[Game] god-favor: auto-skip por tiempo agotado");
            _godFavorSelected = true;
            GodInfoCard.Instance?.ForceHide();
            BoardManager.Instance?.DisableGodFavorInteraction();
            SendGodFavor("");
        }
    }

    /// Llamado desde GodInfoCard cuando el jugador pulsa "Elegir".
    public void InvokeGodFromCard(string godName)
    {
        if (_godFavorSelected) return;
        _godFavorSelected = true;
        if (_hoveredGod != null) { _hoveredGod.OnHoverExit(); _hoveredGod = null; }
        GodInfoCard.Instance?.ForceHide();
        BoardManager.Instance?.DisableGodFavorInteraction();
        Debug.Log($"[Game] InvokeGodFromCard → {godName}");
        SendGodFavor(godName);
        OnTurnChanged?.Invoke();
    }

    private void HandleResolution(string body, bool isSecond)
    {
        string stateJson = ExtractObject(body, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            MyLife = ParseIntField(stateJson, GameData.MyId, "life");
            OpponentLife = ParseIntField(stateJson, GameData.OpponentId, "life");
            MyEnergy = ParseIntField(stateJson, GameData.MyId, "energy");
            OpponentEnergy = ParseIntField(stateJson, GameData.OpponentId, "energy");
        }

        if (!isSecond)
        {
            MyInvokedGod = ParseStringField(stateJson, GameData.MyId, "godFavor");
            OpponentInvokedGod = ParseStringField(stateJson, GameData.OpponentId, "godFavor");

            if (!string.IsNullOrEmpty(MyInvokedGod) && GodData.All.TryGetValue(MyInvokedGod, out var myInfo))
            {
                int tier = GodData.GetAffordableTier(MyInvokedGod, MyGodFavorEnergy);
                MyInvokedGodTier = tier;
                if (tier > 0) MyEnergyPreHands = Mathf.Max(0, MyEnergyPreHands - myInfo.Tiers[tier - 1].Cost);
            }
            if (!string.IsNullOrEmpty(OpponentInvokedGod) && GodData.All.TryGetValue(OpponentInvokedGod, out var oppInfo))
            {
                int tier = GodData.GetAffordableTier(OpponentInvokedGod, OpponentGodFavorEnergy);
                OpponentInvokedGodTier = tier;
                if (tier > 0) OpponentEnergyPreHands = Mathf.Max(0, OpponentEnergyPreHands - oppInfo.Tiers[tier - 1].Cost);
            }
            return;
        }

        Debug.Log($"[Game] Resolution | MyLife:{MyLife} OppLife:{OpponentLife}");

        _resolutionAnimationStarted = true;

        if (ResolutionAnimator.Instance != null)
            ResolutionAnimator.Instance.OnAnimationComplete = () =>
            {
                BoardManager.Instance?.SpawnStones(MyLife, OpponentLife);
                if (_pendingGameOverBody != null)
                {
                    ExecuteGameOver();
                }
                else if (_pendingRoundStartBody != null)
                {
                    StartCoroutine(ExecuteRoundStart(_pendingRoundStartBody));
                    _pendingRoundStartBody = null;
                }
            };

        var board = BoardManager.Instance;
        if (board != null) StartCoroutine(board.AnimateResolution());
        OnLifeUpdated?.Invoke();
    }

    private IEnumerator ExecuteRoundStart(string body)
    {
        yield return new WaitForSeconds(0.5f); // pequeña pausa tras las piedras

        string stateJson = ExtractObject(body, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = ParseGameState(stateJson);
            MyConfirmed = ParseSelectedRolls(stateJson, GameData.MyId);
            EnemyConfirmed = ParseSelectedRolls(stateJson, GameData.OpponentId);
            MyEnergy = ParseIntField(stateJson, GameData.MyId, "energy");
            OpponentEnergy = ParseIntField(stateJson, GameData.OpponentId, "energy");
            BoardManager.Instance?.SpawnTokens(MyEnergy, OpponentEnergy);
        }
        MyDice.Clear(); MySurvivors.Clear(); EnemyCurrentBowl.Clear();
        MyDiceRolled = false;
        _waitingServer = false;
        _myRollCount = 0;
        _godFavorSelected = false;
        _resolutionAnimationStarted = false;
        MyInvokedGod = "";
        OpponentInvokedGod = "";
        MyInvokedGodTier = 0;
        OpponentInvokedGodTier = 0;
        BoardManager.Instance?.RebuildAll();

        bool isMyTurn = CurrentState?.activePlayer == GameData.MyId;
        string turnMsg = isMyTurn ? "Tu turno" : "Turno del rival";
        NotificationUI.Instance?.Show(turnMsg);

        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    private IEnumerator DelayedStoneUpdate()
    {
        yield return new WaitForSeconds(1.5f);
        BoardManager.Instance?.SpawnStones(MyLife, OpponentLife);
        BoardManager.Instance?.SpawnTokens(MyEnergy, OpponentEnergy);
    }

    private void HandleGameOver(string body)
    {
        string winner = ExtractStringValue(body, "winner");
        Debug.Log($"[Game] game-over | winner:{winner} | iWon:{winner == GameData.MyId}");
        GameData.WinnerId = winner;
        _pendingGameOverBody = body;

        if (_resolutionAnimationStarted)
        {
            // Caso normal: animación ya en marcha (game-over tras resolution-attack-second).
            // OnAnimationComplete llamará a ExecuteGameOver cuando termine.
            return;
        }

        // Caso especial: game-over tras resolution-attack-first (el rival murió en el primer ataque).
        // Arrancamos la animación ahora para que las piedras bajen antes de mostrar el panel.
        if (ResolutionAnimator.Instance != null)
        {
            ResolutionAnimator.Instance.OnAnimationComplete = () =>
            {
                BoardManager.Instance?.SpawnStones(MyLife, OpponentLife);
                ExecuteGameOver();
            };
            var board = BoardManager.Instance;
            if (board != null) { StartCoroutine(board.AnimateResolution()); return; }
        }

        // Fallback si no hay animador ni tablero: mostrar panel directamente.
        ExecuteGameOver();
    }

    private void ExecuteGameOver()
    {
        _pendingGameOverBody = null;
        CurrentState = new GameState { state = "game-over", round = 0, activePlayer = "" };
        OnTurnChanged?.Invoke();
    }

    private string _pendingRoundStartBody = null;
    private string _pendingGameOverBody = null;
    private bool _resolutionAnimationStarted = false;

    private void HandleRoundStart(string body)
    {
        _pendingRoundStartBody = body;
        if (ResolutionAnimator.Instance == null)
            StartCoroutine(ExecuteRoundStart(body));
    }

    public void SendGodFavor(string godName)
    {
        Debug.Log($"[Game] SendGodFavor: '{godName}'");
        WebSocketManager.Instance.Send("select-favor", "{\"godName\":\"" + godName + "\"}");
    }

    public string[] MySelectedGods => GameData.MySelectedGods;
    public string[] OpponentSelectedGods => GameData.OpponentSelectedGods;

    private IEnumerator AutoSkipTurn()
    {
        yield return new WaitForSeconds(0.4f);
        Debug.Log("[Game] AutoSkipTurn → enviando select-rolls vacío");
        WebSocketManager.Instance.Send("select-rolls", "{\"rolls\":[]}");
        _waitingServer = true;
        OnTurnChanged?.Invoke();
    }

    public void ConfirmSelection()
    {
        if (!CanConfirm) return;

        var sb = new System.Text.StringBuilder();
        bool first = true; int keptCount = 0;
        sb.Append('[');
        foreach (var d in MyDice)
        {
            if (!d.kept) continue;
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"face\":\"").Append(d.face).Append("\",\"energy\":").Append(d.energy ? "true" : "false").Append('}');
            keptCount++;
        }
        sb.Append(']');

        WebSocketManager.Instance.Send("select-rolls", $"{{\"rolls\":{sb}}}");
        Debug.Log($"[Game] Enviada selección: {keptCount} dados guardados");
        _waitingServer = true;
        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    // ── Helpers de parseo ──────────────────────────────────────────────────────

    private List<DiceData> ParseDiceArray(string arrayJson)
    {
        var result = new List<DiceData>();
        int i = 0;
        while (i < arrayJson.Length)
        {
            int start = arrayJson.IndexOf('{', i); if (start == -1) break;
            int end = arrayJson.IndexOf('}', start); if (end == -1) break;
            string obj = arrayJson.Substring(start, end - start + 1);
            string faceStr = ExtractStringValue(obj, "face");
            bool energy = obj.Contains("\"energy\":true");
            if (Enum.TryParse<DiceFace>(faceStr, out DiceFace face))
                result.Add(new DiceData { face = face, energy = energy, kept = false, isMyDice = false });
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
        int depth = 0, i = userStart + userMarker.Length - 1, userObjStart = i;
        while (i < stateJson.Length)
        {
            if (stateJson[i] == '{') depth++;
            else if (stateJson[i] == '}') { depth--; if (depth == 0) break; }
            i++;
        }
        if (i >= stateJson.Length) return new List<DiceData>();
        string userObj = stateJson.Substring(userObjStart, i - userObjStart + 1);
        string rollsArray = ExtractArray(userObj, "selectedRolls");
        return string.IsNullOrEmpty(rollsArray) ? new List<DiceData>() : ParseDiceArray(rollsArray);
    }

    private GameState ParseGameState(string stateJson) => new GameState
    {
        state = ExtractStringValue(stateJson, "state"),
        activePlayer = ExtractStringValue(stateJson, "activePlayer"),
        round = ParseRoundField(stateJson)
    };

    private int ParseRoundField(string stateJson)
    {
        string marker = "\"round\":";
        int rStart = stateJson.IndexOf(marker);
        if (rStart == -1) return 0;
        rStart += marker.Length;
        int rEnd = rStart;
        while (rEnd < stateJson.Length && (char.IsDigit(stateJson[rEnd]) || stateJson[rEnd] == '-')) rEnd++;
        return int.TryParse(stateJson.Substring(rStart, rEnd - rStart), out int v) ? v : 0;
    }

    private int ParseIntField(string stateJson, string userId, string field)
    {
        if (string.IsNullOrEmpty(userId)) return 0;
        string userMarker = "\"" + userId + "\":{";
        int uStart = stateJson.IndexOf(userMarker);
        if (uStart == -1) return 0;
        int depth = 0, i = uStart + userMarker.Length - 1, uEnd = i;
        while (i < stateJson.Length)
        {
            if (stateJson[i] == '{') depth++;
            else if (stateJson[i] == '}') { depth--; if (depth == 0) { uEnd = i; break; } }
            i++;
        }
        string userObj = stateJson.Substring(uStart + userMarker.Length - 1, uEnd - (uStart + userMarker.Length - 1) + 1);
        string marker = "\"" + field + "\":";
        int fStart = userObj.IndexOf(marker);
        if (fStart == -1) return 0;
        fStart += marker.Length;
        int fEnd = fStart;
        while (fEnd < userObj.Length && (char.IsDigit(userObj[fEnd]) || userObj[fEnd] == '-')) fEnd++;
        return int.TryParse(userObj.Substring(fStart, fEnd - fStart), out int val) ? val : 0;
    }

    private string ParseStringField(string stateJson, string userId, string field)
    {
        if (string.IsNullOrEmpty(userId)) return "";
        string userMarker = "\"" + userId + "\":{";
        int uStart = stateJson.IndexOf(userMarker);
        if (uStart == -1) return "";
        int depth = 0, i = uStart + userMarker.Length - 1, uEnd = i;
        while (i < stateJson.Length)
        {
            if (stateJson[i] == '{') depth++;
            else if (stateJson[i] == '}') { depth--; if (depth == 0) { uEnd = i; break; } }
            i++;
        }
        string userObj = stateJson.Substring(uStart + userMarker.Length - 1, uEnd - (uStart + userMarker.Length - 1) + 1);
        return ExtractStringValue(userObj, field);
    }

    private string ExtractArray(string json, string key)
    {
        string marker = $"\"{key}\":[";
        int start = json.IndexOf(marker); if (start == -1) return "";
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
        int start = json.IndexOf(marker); if (start == -1) return "";
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
        int start = json.IndexOf(search); if (start == -1) return "";
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