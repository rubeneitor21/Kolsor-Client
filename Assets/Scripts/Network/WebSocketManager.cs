using System;
using UnityEngine;
using NativeWebSocket;

public class WebSocketManager : MonoBehaviour
{
    private const string SERVER_URL = "wss://kolsor.garcalia.com";

    private WebSocket _socket;
    public bool IsConnected { get; private set; } = false;

    public static WebSocketManager Instance { get; private set; }

    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<string, string> OnMessageReceived;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
    }

    public async System.Threading.Tasks.Task Connect()
    {
        _socket = new WebSocket(SERVER_URL);

        _socket.OnOpen += () =>
        {
            IsConnected = true;
            Debug.Log("[WS] ✅ Conectado al servidor");
            OnConnected?.Invoke();
        };

        _socket.OnClose += (code) =>
        {
            IsConnected = false;
            Debug.Log("[WS] ❌ Desconectado. Código: " + code);
            OnDisconnected?.Invoke();
        };

        _socket.OnError += (error) =>
        {
            Debug.LogError("[WS] ⚠️ Error: " + error);
        };

        _socket.OnMessage += (bytes) =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("[WS] 📨 Recibido: " + json);
            HandleMessage(json);
        };

        await _socket.Connect();
    }

    private void HandleMessage(string json)
    {
        try
        {
            string type = ExtractStringField(json, "type");
            if (string.IsNullOrEmpty(type)) return;

            string body = ExtractBodyRaw(json);
            OnMessageReceived?.Invoke(type, body);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WS] Error procesando mensaje: " + e.Message);
        }
    }

    private string ExtractStringField(string json, string key)
    {
        string search = $"\"{key}\":\"";
        int start = json.IndexOf(search);
        if (start == -1) return "";
        start += search.Length;
        int end = json.IndexOf("\"", start);
        if (end == -1) return "";
        return json.Substring(start, end - start);
    }

    private string ExtractBodyRaw(string json)
    {
        string marker = "\"body\":";
        int start = json.IndexOf(marker);
        if (start == -1) return "";
        start += marker.Length;

        char first = json[start];

        if (first == '"')
        {
            start++;
            int end = start;
            while (end < json.Length)
            {
                if (json[end] == '"' && json[end - 1] != '\\') break;
                end++;
            }
            return json.Substring(start, end - start);
        }
        else if (first == '{' || first == '[')
        {
            char open = first;
            char close = first == '{' ? '}' : ']';
            int depth = 0, i = start;
            while (i < json.Length)
            {
                if (json[i] == open) depth++;
                else if (json[i] == close) { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
                i++;
            }
            return "";
        }
        else
        {
            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '}') end++;
            return json.Substring(start, end - start);
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _socket?.DispatchMessageQueue();
#endif
    }

    public async void Send(string type, string bodyJson = null)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("[WS] No conectado. No se puede enviar: " + type);
            return;
        }

        string json;
        if (bodyJson != null)
            json = $"{{\"type\":\"{type}\",\"body\":{bodyJson}}}";
        else
            json = $"{{\"type\":\"{type}\"}}";

        Debug.Log("[WS] 📤 Enviando: " + json);
        await _socket.SendText(json);
    }

    public void SendPing() => Send("ping");

    async void OnDestroy()
    {
        if (_socket != null)
            await _socket.Close();
    }
}