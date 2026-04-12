using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Los 6 dados de cada jugador (de los 10 que manda el servidor, usamos los primeros 6)
    public List<DiceData> MyDice { get; private set; } = new List<DiceData>();
    public List<DiceData> EnemyDice { get; private set; } = new List<DiceData>();

    public string OpponentName => GameData.OpponentName;
    public bool IsMyTurn => GameData.PlayerStartId == GameData.MyId;

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

        if (!string.IsNullOrEmpty(LobbyManager.PendingRollsBody))
        {
            Debug.Log("[Game] Procesando rolls pendientes...");
            ParseRolls(LobbyManager.PendingRollsBody);
            LobbyManager.PendingRollsBody = "";
        }
    }

    void OnEnable()
    {
        WebSocketManager.OnMessageReceived += HandleMessage;
    }

    void OnDisable()
    {
        WebSocketManager.OnMessageReceived -= HandleMessage;
    }

    private void HandleMessage(string type, string body)
    {
        if (type == "game-rolls")
            ParseRolls(body);
    }

    private bool _rollsPending = false;

    private void ParseRolls(string json)
    {
        MyDice.Clear();
        EnemyDice.Clear();

        var myDiceJson = ExtractArrayForId(json, GameData.MyId);
        var enemyDiceJson = ExtractArrayForId(json, GameData.OpponentId);

        MyDice = ParseDiceArray(myDiceJson, isMyDice: true);
        EnemyDice = ParseDiceArray(enemyDiceJson, isMyDice: false);

        if (MyDice.Count > 6) MyDice = MyDice.GetRange(0, 6);
        if (EnemyDice.Count > 6) EnemyDice = EnemyDice.GetRange(0, 6);

        Debug.Log($"[Game] Dados míos: {MyDice.Count} | Dados rival: {EnemyDice.Count}");

        if (BoardManager.Instance != null)
            BoardManager.Instance.SpawnDice();
        else
            _rollsPending = true;  // BoardManager no está listo, lo guardaremos
    }

    // Añade este método para que BoardManager lo llame cuando esté listo
    public void NotifyBoardReady()
    {
        if (_rollsPending && MyDice.Count > 0)
        {
            _rollsPending = false;
            BoardManager.Instance.SpawnDice();
        }
    }

    private string ExtractArrayForId(string json, string id)
    {
        string search = $"\"{id}\":[";
        int start = json.IndexOf(search);
        if (start == -1) return "[]";
        start += search.Length - 1; // apunta al [

        int depth = 0, i = start;
        while (i < json.Length)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
            i++;
        }
        return "[]";
    }

    private List<DiceData> ParseDiceArray(string arrayJson, bool isMyDice)
    {
        var result = new List<DiceData>();

        // Cada dado tiene forma: {"face":"Axe","energy":false}
        int i = 0;
        while (i < arrayJson.Length)
        {
            int objStart = arrayJson.IndexOf('{', i);
            if (objStart == -1) break;

            int objEnd = arrayJson.IndexOf('}', objStart);
            if (objEnd == -1) break;

            string obj = arrayJson.Substring(objStart, objEnd - objStart + 1);

            string faceStr = ExtractStringValue(obj, "face");
            bool energy = obj.Contains("\"energy\":true");

            if (Enum.TryParse<DiceFace>(faceStr, out DiceFace face))
            {
                result.Add(new DiceData
                {
                    face = face,
                    energy = energy,
                    kept = false,
                    isMyDice = isMyDice
                });
            }

            i = objEnd + 1;
        }

        return result;
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