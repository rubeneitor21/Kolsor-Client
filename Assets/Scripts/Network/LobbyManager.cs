using System;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public static event Action OnSearchStarted;
    public static event Action<string> OnMatchmakingJoin;
    public static event Action<string[]> OnGodSelectionStart; // lista de 8 dioses disponibles
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

            case "god-selection-start":
                // Guardamos los datos de la partida y mostramos la selección de dioses
                SaveGameData(body);
                var gods = ParseAvailableGods(body);
                OnGodSelectionStart?.Invoke(gods);
                break;

            case "game-start":
                // La partida empieza: guardamos los dioses seleccionados y navegamos al juego
                SaveUserGods(body);
                PendingRollsBodies.Clear();
                OnGameStart?.Invoke(null);
                break;

            case "game-rolls":
                PendingRollsBodies.Add(body);
                break;
        }
    }

    // ── Métodos públicos ──────────────────────────────────────────────────────

    public void SearchMatch()
    {
        WebSocketManager.Instance.Send("matchmaking-search");
    }

    /// Envía los 2 dioses elegidos por el jugador al servidor.
    public void SendGodSelection(string god1, string god2)
    {
        string body = $"{{\"gods\":[\"{god1}\",\"{god2}\"]}}";
        WebSocketManager.Instance.Send("select-gods", body);
        Debug.Log($"[Lobby] SendGodSelection: {god1}, {god2}");
    }

    // ── Parseo ────────────────────────────────────────────────────────────────

    private void SaveGameData(string body)
    {
        GameData.MyId = AuthManager.Instance != null ? AuthManager.Instance.UserId : "";
        GameData.PlayerStartId = ExtractStringValue(body, "playerStart");
        GameData.RoomId = ExtractStringValue(body, "roomId");

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
        Debug.Log($"[Lobby] god-selection-start | Yo:{GameData.MyId} | Rival:{GameData.OpponentName} | Room:{GameData.RoomId}");
    }

    /// Parsea "availableGods":["God1","God2",...] del mensaje god-selection-start.
    private string[] ParseAvailableGods(string body)
    {
        var gods = new System.Collections.Generic.List<string>();
        string marker = "\"availableGods\":[";
        int start = body.IndexOf(marker);
        if (start == -1) return gods.ToArray();
        start += marker.Length;
        int end = body.IndexOf("]", start);
        if (end == -1) return gods.ToArray();
        string arr = body.Substring(start, end - start);
        // arr = "\"God1\",\"God2\",..."
        int i = 0;
        while (i < arr.Length)
        {
            int q1 = arr.IndexOf("\"", i);
            if (q1 == -1) break;
            int q2 = arr.IndexOf("\"", q1 + 1);
            if (q2 == -1) break;
            gods.Add(arr.Substring(q1 + 1, q2 - q1 - 1));
            i = q2 + 1;
        }
        return gods.ToArray();
    }

    /// Parsea "userGods":{"id1":["G1","G2"],"id2":["G3","G4"]} del mensaje game-start.
    private void SaveUserGods(string body)
    {
        // Buscar los dioses de cada jugador en el bloque userGods
        string marker = "\"userGods\":{";
        int start = body.IndexOf(marker);
        if (start == -1) return;
        start += marker.Length - 1; // apuntamos a la {

        // Extraer el bloque completo hasta la } que cierra userGods
        int depth = 0, i = start;
        while (i < body.Length)
        {
            if (body[i] == '{') depth++;
            else if (body[i] == '}') { depth--; if (depth == 0) break; }
            i++;
        }
        string godsBlock = body.Substring(start, i - start + 1);

        GameData.MySelectedGods = ParseGodsForUser(godsBlock, GameData.MyId);
        GameData.OpponentSelectedGods = ParseGodsForUser(godsBlock, GameData.OpponentId);

        Debug.Log($"[Lobby] Mis dioses: {string.Join(", ", GameData.MySelectedGods)}");
        Debug.Log($"[Lobby] Dioses rival: {string.Join(", ", GameData.OpponentSelectedGods)}");
    }

    private string[] ParseGodsForUser(string godsBlock, string userId)
    {
        var result = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(userId)) return result.ToArray();
        string userMarker = "\"" + userId + "\":[";
        int start = godsBlock.IndexOf(userMarker);
        if (start == -1) return result.ToArray();
        start += userMarker.Length;
        int end = godsBlock.IndexOf("]", start);
        if (end == -1) return result.ToArray();
        string arr = godsBlock.Substring(start, end - start);
        int i = 0;
        while (i < arr.Length)
        {
            int q1 = arr.IndexOf("\"", i);
            if (q1 == -1) break;
            int q2 = arr.IndexOf("\"", q1 + 1);
            if (q2 == -1) break;
            result.Add(arr.Substring(q1 + 1, q2 - q1 - 1));
            i = q2 + 1;
        }
        return result.ToArray();
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
public class MatchmakingJoinBody { public string id; public string message; }

[System.Serializable]
public class GameStartData
{
    public string playerStart;
    public string roomId;
    public PlayerInfo[] players;
}

[System.Serializable]
public class PlayerInfo { public string id; public string username; }