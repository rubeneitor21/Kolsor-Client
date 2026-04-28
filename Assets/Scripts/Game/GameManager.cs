using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public List<DiceData> MyDice { get; private set; } = new();

    // false hasta que el jugador pulse espacio para tirar.
    // Mientras es false, el cuenco propio muestra decorativos sincronizados.
    public bool MyDiceRolled { get; private set; } = false;

    public GameState CurrentState { get; private set; }
    public bool IsMyTurn => CurrentState?.activePlayer == GameData.MyId;
    public string OpponentName => GameData.OpponentName;

    public bool CanRoll => IsMyTurn
                           && CurrentState?.state == "select-rolls"
                           && MyDice != null && MyDice.Count > 0
                           && !MyDiceRolled
                           && !InputBlocked
                           && !_animating;

    public static event Action OnRollsChanged;
    public static event Action OnTurnChanged;

    public static bool InputBlocked = false;
    private bool _animating = false;

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
        NotifyBoardReady();
    }

    void Update()
    {
        if (Keyboard.current != null
            && Keyboard.current.spaceKey.wasPressedThisFrame
            && CanRoll)
        {
            Debug.Log("[Game] SPACE → Tirar mis dados");
            StartCoroutine(RollMyDiceAnimation());
        }
    }

    public void NotifyBoardReady()
    {
        var pending = LobbyManager.PendingRollsBodies;
        if (pending.Count == 0) return;

        Debug.Log($"[Game] Procesando {pending.Count} rolls pendientes");
        foreach (var body in pending) ParseRolls(body);
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
        string activeUser = ExtractStringValue(json, "user");
        bool isMe = (activeUser == GameData.MyId);

        string rollsArray = ExtractArray(json, "rolls");
        var currentRolls = string.IsNullOrEmpty(rollsArray)
            ? new List<DiceData>()
            : ParseDiceArray(rollsArray);

        Debug.Log($"[Game] game-rolls para {(isMe ? "YO" : "RIVAL")} | dados: {currentRolls.Count}");

        if (isMe)
        {
            MyDice = currentRolls;
            foreach (var d in MyDice) d.isMyDice = true;
            // Reseteamos: el jugador tendrá que pulsar espacio para tirar
            MyDiceRolled = false;
        }
        else
        {
            MyDice.Clear();
            MyDiceRolled = false;
        }

        string stateJson = ExtractObject(json, "state");
        if (!string.IsNullOrEmpty(stateJson))
        {
            CurrentState = ParseGameState(stateJson);
            Debug.Log($"[Game] Estado:{CurrentState?.state} | Turno:{CurrentState?.activePlayer} | Ronda:{CurrentState?.round}");
        }
        else if (!string.IsNullOrEmpty(activeUser))
        {
            CurrentState = new GameState
            {
                state = "select-rolls",
                round = 1,
                activePlayer = activeUser
            };
        }

        BoardManager.Instance?.RebuildAll();

        OnRollsChanged?.Invoke();
        OnTurnChanged?.Invoke();
    }

    private System.Collections.IEnumerator RollMyDiceAnimation()
    {
        _animating = true;
        OnTurnChanged?.Invoke();

        // Le pedimos al BoardManager que reproduzca la animación de tirada
        // sobre los dados visibles del cuenco propio. Al terminar, los dados
        // quedan con las caras reales de MyDice.
        yield return BoardManager.Instance?.AnimateMyRoll(MyDice);

        MyDiceRolled = true;
        _animating = false;

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