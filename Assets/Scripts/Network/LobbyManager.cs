using System;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public static event Action OnSearchStarted;
    public static event Action<string> OnMatchmakingJoin;
    public static event Action<GameStartData> OnGameStart;

    public static readonly System.Collections.Generic.List<string> PendingRollsBodies = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() { WebSocketManager.OnMessageReceived += HandleMessage; }
    void OnDisable() { WebSocketManager.OnMessageReceived -= HandleMessage; }

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
                SaveGameData(body);
                PendingRollsBodies.Clear();
                OnGameStart?.Invoke(null);
                break;

            case "game-rolls":
                PendingRollsBodies.Add(body);
                break;
        }
    }

    /// JsonUtility no parsea bien arrays de objetos al nivel raíz.
    /// Hacemos parseo manual de los players para no perder OpponentName.
    private void SaveGameData(string body)
    {
        GameData.MyId = AuthManager.Instance != null ? AuthManager.Instance.UserId : "";
        GameData.PlayerStartId = ExtractStringValue(body, "playerStart");
        GameData.RoomId = ExtractStringValue(body, "roomId");

        // Recorremos los objetos {"id":"...","username":"..."} dentro de "players":[...]
        int searchFrom = 0;
        while (true)
        {
            int idStart = body.IndexOf("\"id\":\"", searchFrom);
            if (idStart == -1) break;
            idStart += 6;
            int idEnd = body.IndexOf("\"", idStart);
            if (idEnd == -1) break;
            string id = body.Substring(idStart, idEnd - idStart);

            int unameMarker = body.IndexOf("\"username\":\"", idEnd);
            if (unameMarker == -1) break;
            unameMarker += 12;
            int unameEnd = body.IndexOf("\"", unameMarker);
            if (unameEnd == -1) break;
            string username = body.Substring(unameMarker, unameEnd - unameMarker);

            if (id != GameData.MyId)
            {
                GameData.OpponentId = id;
                GameData.OpponentName = username;
            }

            searchFrom = unameEnd + 1;
        }

        Debug.Log($"[Lobby] game-start | Yo:{GameData.MyId} | Rival:{GameData.OpponentName} | Empieza:{GameData.PlayerStartId} | Room:{GameData.RoomId}");
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

    public void SearchMatch()
    {
        WebSocketManager.Instance.Send("matchmaking-search");
    }
}

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
    public string roomId;
    public PlayerInfo[] players;
}

[System.Serializable]
public class PlayerInfo
{
    public string id;
    public string username;
}