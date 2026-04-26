using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public List<DiceData> MyDice { get; private set; } = new();
    public List<DiceData> EnemyDice { get; private set; } = new();

    // Estado del juego recibido del servidor
    public GameState CurrentState { get; private set; }
    public bool IsMyTurn => CurrentState?.activePlayer == GameData.MyId;
    public bool CanSelect => IsMyTurn
                              && CurrentState?.state == "select-rolls"
                              && MyDice != null && MyDice.Count > 0
                              && !_waitingServer;

    public string OpponentName => GameData.OpponentName;

    public static event Action OnRollsChanged;
    public static event Action OnTurnChanged;

    private bool _waitingServer = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        Debug.Log("[Game] GameManager.Start() ejecutado");
        Debug.Log($"[Game] MyId: '{GameData.MyId}'");
        Debug.Log($"[Game] OpponentId: '{GameData.OpponentId}'");
        Debug.Log($"[Game] PendingRolls vacío: {string.IsNullOrEmpty(LobbyManager.PendingRollsBody)}");

        NotifyBoardReady();
    }

    void Update()
    {
        // Atajo temporal hasta tener botón en UI
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && CanSelect)
        {
            Debug.Log("[Game] SPACE → ConfirmSelection");
            ConfirmSelection();
        }

        // Detección de clic sobre los dados vía RaycastAll: el cuenco también
        // tiene Collider y se interpondría delante de los dados. Recorremos
        // todos los impactos y elegimos el dado mío más cercano a la cámara.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && CanSelect)
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);

            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            DiceController closest = null;
            float closestDist = float.MaxValue;

            foreach (var h in hits)
            {
                var dice = h.collider.GetComponent<DiceController>();
                if (dice == null || dice.Data == null || !dice.Data.isMyDice) continue;
                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    closest = dice;
                }
            }

            if (closest != null) closest.ToggleKeep();
        }
    }

    public void NotifyBoardReady()
    {
        if (!string.IsNullOrEmpty(LobbyManager.PendingRollsBody))
        {
            Debug.Log("[Game] NotifyBoardReady: procesando rolls pendientes...");
            ParseRolls(LobbyManager.PendingRollsBody);
            LobbyManager.PendingRollsBody = "";
        }
    }

    void OnEnable() => WebSocketManager.OnMessageReceived += HandleMessage;
    void OnDisable() => WebSocketManager.OnMessageReceived -= HandleMessage;

    private void HandleMessage(string type, string body)
    {
        switch (type)
        {
            case "game-rolls":
                ParseRolls(body);
                break;
        }
    }

    public void ParseRolls(string json)
    {
        // El JSON tiene forma:
        // {"rolls":[{"face":"Axe","energy":false},...]}
        // Y el "user" está fuera del body, así que LobbyManager lo pasa junto

        // Extraemos los dados del array "rolls"
        string rollsArray = ExtractArray(json, "rolls");
        if (string.IsNullOrEmpty(rollsArray))
        {
            Debug.LogWarning("[Game] No se encontró el array 'rolls' en: " + json);
            return;
        }

        // Extraemos el ID del usuario (viene en el mismo JSON del mensaje completo)
        string userId = ExtractStringValue(json, "user");

        var dice = ParseDiceArray(rollsArray);

        Debug.Log($"[Game] Rolls para {userId}: {dice.Count} dados");

        if (userId == GameData.MyId)
        {
            MyDice = dice;
            foreach (var d in MyDice) d.isMyDice = true;
            // Cuando me toca a mí, los dados del rival ya no son la tirada
            // actual: los limpiamos para no confundir al jugador.
            EnemyDice = new List<DiceData>();
        }
        else
        {
            EnemyDice = dice;
            foreach (var d in EnemyDice) d.isMyDice = false;
            // Cuando es turno del rival, mis dados de la tirada anterior
            // ya no son seleccionables: limpiamos para evitar confusión.
            MyDice = new List<DiceData>();
        }

        // Actualizar estado si viene incluido
        string stateJson = ExtractObject(json, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = JsonUtility.FromJson<GameState>(stateJson);
            Debug.Log($"[Game] Estado: {CurrentState?.state} | Turno: {CurrentState?.activePlayer}");
        }
        else if (!string.IsNullOrEmpty(userId))
        {
            // El primer game-rolls no incluye "state". Reconstruimos uno mínimo
            // a partir del campo "user" para que CanSelect funcione.
            CurrentState = new GameState
            {
                state = "select-rolls",
                round = CurrentState?.round > 0 ? CurrentState.round : 1,
                activePlayer = userId
            };
        }

        _waitingServer = false;

        BoardManager.Instance?.SpawnDice();
        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    // Llamado por la UI cuando el jugador pulsa "Confirmar selección"
    public void ConfirmSelection()
    {
        if (!CanSelect)
        {
            Debug.LogWarning("[Game] ConfirmSelection ignorado: no puedes seleccionar ahora");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var d in MyDice)
        {
            if (!d.kept) continue;
            if (!first) sb.Append(',');
            first = false;
            string energy = d.energy ? "true" : "false";
            sb.Append("{\"face\":\"").Append(d.face).Append("\",\"energy\":").Append(energy).Append('}');
        }
        sb.Append(']');

        string body = $"{{\"rolls\":{sb}}}";
        WebSocketManager.Instance.Send("select-rolls", body);

        // Vaciamos los dados localmente y esperamos al próximo game-rolls
        MyDice = new List<DiceData>();
        _waitingServer = true;

        BoardManager.Instance?.SpawnDice();
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
                    isMyDice = false // se asigna después
                });
            }
            i = end + 1;
        }
        return result;
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

// ── Estructuras de estado ──────────────────────────────────
[System.Serializable]
public class GameState
{
    public string state;
    public int round;
    public string activePlayer;
}