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

    // Cola de game-rolls recibidos antes de que cargue GameScene.
    // Es una lista para que ambos rolls (el mío y el del rival) queden
    // almacenados aunque lleguen antes de que la escena esté lista.
    public static readonly System.Collections.Generic.List<string> PendingRollsBodies = new();

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
                PendingRollsBodies.Clear();
                OnGameStart?.Invoke(startData);
                break;
            case "game-rolls":
                // Acumulamos, no sobreescribimos: en el inicio de ronda el servidor
                // envía un game-rolls para cada jugador; queremos procesar los dos.
                PendingRollsBodies.Add(body);
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