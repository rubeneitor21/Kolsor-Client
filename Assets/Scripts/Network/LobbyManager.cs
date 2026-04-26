using System;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public static event Action OnSearchStarted;
    public static event Action<string> OnMatchmakingJoin;  // mensaje del servidor
    public static event Action<GameStartData> OnGameStart;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        WebSocketManager.OnMessageReceived += HandleMessage;
    }

    void OnDisable()
    {
        WebSocketManager.OnMessageReceived -= HandleMessage;
    }

    // Añade esta variable estática
    public static string PendingRollsBody { get; set; } = "";

    // En HandleMessage, añade el case game-rolls:
    private void HandleMessage(string type, string body)
    {
        switch (type)
        {
            case "matchmaking-search":
                OnSearchStarted?.Invoke();
                break;
            case "matchmaking-join":
                var joinData = JsonUtility.FromJson<MatchmakingJoinBody>(body);
                OnMatchmakingJoin?.Invoke(joinData?.message ?? "En sala...");
                break;
            case "game-start":
                var startData = JsonUtility.FromJson<GameStartData>(body);
                SaveGameData(startData);
                PendingRollsBody = ""; // limpia rolls anteriores
                OnGameStart?.Invoke(startData);
                break;
            case "game-rolls":
                // Guardamos el body para que GameManager lo procese cuando
                // GameScene termine de cargar. Si GameManager ya existe,
                // será él mismo quien procese el evento (está suscrito a
                // OnMessageReceived), así que aquí no llamamos a ParseRolls
                // para evitar procesarlo dos veces.
                PendingRollsBody = body;
                break;
        }
    }

    private void SaveGameData(GameStartData data)
    {
        GameData.MyId = AuthManager.Instance.UserId;
        GameData.PlayerStartId = data.playerStart;

        foreach (var p in data.players)
        {
            if (p.id != GameData.MyId)
            {
                GameData.OpponentId = p.id;
                GameData.OpponentName = p.username;
            }
        }
    }

    public void SearchMatch()
    {
        WebSocketManager.Instance.Send("matchmaking-search");
    }
}

// ── Estructuras JSON ────────────────────────────────────────
[System.Serializable]
public class MatchmakingJoinBody
{
    public string id;
    public string message;
}

[System.Serializable]
public class GameStartData
{
    public string playerStart;
    public PlayerInfo[] players;
}

[System.Serializable]
public class PlayerInfo
{
    public string id;
    public string username;
}