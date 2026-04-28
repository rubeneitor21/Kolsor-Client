using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Dados de la tirada ACTUAL del jugador activo (solo se llena para él)
    public List<DiceData> MyDice { get; private set; } = new();

    // Dados ya confirmados por cada jugador a lo largo de la ronda.
    // Se reconstruyen desde state.users[id].selectedRolls en cada game-rolls.
    public List<DiceData> MyConfirmed { get; private set; } = new();
    public List<DiceData> EnemyConfirmed { get; private set; } = new();

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

    // Bloquea input mientras se reproduce alguna animación (moneda, etc.)
    public static bool InputBlocked = false;

    private bool _waitingServer = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        WebSocketManager.OnMessageReceived += HandleMessage;
    }

    void OnDisable()
    {
        WebSocketManager.OnMessageReceived -= HandleMessage;
    }

    void Start()
    {
        Debug.Log("[Game] GameManager.Start()");
        Debug.Log($"[Game] MyId: '{GameData.MyId}' | OpponentId: '{GameData.OpponentId}'");
        NotifyBoardReady();
    }

    void Update()
    {
        if (InputBlocked) return;

        // Confirmar selección con espacio
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && CanSelect)
        {
            Debug.Log("[Game] SPACE → ConfirmSelection");
            ConfirmSelection();
        }

        // Clic sobre los dados de mi cuenco
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

            if (closest != null)
            {
                closest.ToggleKeep();
                BoardManager.Instance?.RefreshKeptRows();
            }
        }
    }

    public void NotifyBoardReady()
    {
        var pending = LobbyManager.PendingRollsBodies;
        if (pending.Count == 0) return;

        Debug.Log($"[Game] NotifyBoardReady: procesando {pending.Count} rolls pendientes...");
        foreach (var body in pending)
            ParseRolls(body);

        pending.Clear();
    }

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
        // El "user" lo inyecta WebSocketManager dentro del body.
        // "user" = jugador activo al que van dirigidos estos dados.
        string activeUser = ExtractStringValue(json, "user");

        // Dados de la tirada ACTUAL (los del cuenco)
        string rollsArray = ExtractArray(json, "rolls");
        var currentRollDice = string.IsNullOrEmpty(rollsArray) ? new List<DiceData>() : ParseDiceArray(rollsArray);

        bool isMe = (activeUser == GameData.MyId);

        Debug.Log($"[Game] game-rolls para {(isMe ? "YO" : "RIVAL")} | dados nuevos: {currentRollDice.Count}");

        // ── Tirada actual ──────────────────────────────────────
        // Solo el jugador activo recibe dados visibles en su cuenco.
        if (isMe)
        {
            MyDice = currentRollDice;
            foreach (var d in MyDice) d.isMyDice = true;
        }
        else
        {
            // No es mi turno: limpiamos mi cuenco
            MyDice.Clear();
        }

        // ── Estado del servidor ────────────────────────────────
        // El primer game-rolls de la partida no trae state. Los siguientes sí.
        string stateJson = ExtractObject(json, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = ParseGameState(stateJson);
            Debug.Log($"[Game] Estado: {CurrentState?.state} | Turno: {CurrentState?.activePlayer} | Ronda: {CurrentState?.round}");

            // Reconstruimos las filas confirmadas desde state.users[id].selectedRolls
            MyConfirmed = ParseSelectedRolls(stateJson, GameData.MyId);
            EnemyConfirmed = ParseSelectedRolls(stateJson, GameData.OpponentId);

            foreach (var d in MyConfirmed) { d.isMyDice = true; d.kept = true; }
            foreach (var d in EnemyConfirmed) { d.isMyDice = false; d.kept = true; }

            Debug.Log($"[Game] Confirmados → Yo:{MyConfirmed.Count} | Rival:{EnemyConfirmed.Count}");
        }
        else if (!string.IsNullOrEmpty(activeUser))
        {
            // Primer game-rolls de la partida: no hay confirmados todavía
            CurrentState = new GameState
            {
                state = "select-rolls",
                round = 1,
                activePlayer = activeUser
            };
            MyConfirmed.Clear();
            EnemyConfirmed.Clear();
        }

        _waitingServer = false;

        // Reconstruimos visualmente todo
        BoardManager.Instance?.RebuildAll();

        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    public void ConfirmSelection()
    {
        if (!CanSelect)
        {
            Debug.LogWarning("[Game] ConfirmSelection ignorado");
            return;
        }

        // Construimos el JSON con los dados kept
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

        if (keptCount == 0)
        {
            Debug.LogWarning("[Game] No has guardado ningún dado");
            return;
        }

        string body = $"{{\"rolls\":{sb}}}";
        WebSocketManager.Instance.Send("select-rolls", body);

        // Movemos optimistamente los dados kept de la tirada actual a la fila
        // confirmada propia mientras esperamos al servidor. Cuando llegue el
        // próximo game-rolls, RebuildAll los reemplazará por los del state real
        // (que coincidirán) en la misma posición, así no hay salto raro.
        foreach (var d in MyDice)
        {
            if (d.kept) MyConfirmed.Add(d);
        }
        MyDice.Clear();

        _waitingServer = true;

        BoardManager.Instance?.RebuildAll();

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

    /// Extrae el array selectedRolls de un usuario concreto desde state.users[userId]
    private List<DiceData> ParseSelectedRolls(string stateJson, string userId)
    {
        if (string.IsNullOrEmpty(userId)) return new List<DiceData>();

        // Buscamos "userId":{ ... "selectedRolls":[...] ... }
        string userMarker = $"\"{userId}\":{{";
        int userStart = stateJson.IndexOf(userMarker);
        if (userStart == -1) return new List<DiceData>();

        // Encontramos el cierre del objeto del usuario
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

        // round es número, no string
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