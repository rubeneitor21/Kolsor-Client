using System;
using UnityEngine;
using NativeWebSocket;

public class WebSocketManager : MonoBehaviour
{
    // Cambia esto por la IP del servidor cuando lo tengáis en remoto
    private const string SERVER_URL = "wss://kolsor.garcalia.com";

    private WebSocket _socket;
    public bool IsConnected { get; private set; } = false;

    // Singleton: cualquier script puede acceder con WebSocketManager.Instance
    public static WebSocketManager Instance { get; private set; }

    // Eventos: otros scripts se suscriben para reaccionar a mensajes
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<string, string> OnMessageReceived; // (tipo, body)

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persiste entre escenas
    }

    async void Start()
    {
        await Connect();
    }

    private async System.Threading.Tasks.Task Connect()
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

            try
            {
                ServerMessage msg = JsonUtility.FromJson<ServerMessage>(json);
                if (msg != null && !string.IsNullOrEmpty(msg.type))
                    OnMessageReceived?.Invoke(msg.type, msg.body);
            }
            catch
            {
                Debug.LogWarning("[WS] Mensaje ignorado (no es JSON válido): " + json);
            }
        };

        await _socket.Connect();
    }

    void Update()
    {
        // Necesario para que NativeWebSocket procese mensajes
#if !UNITY_WEBGL || UNITY_EDITOR
        _socket?.DispatchMessageQueue();
#endif
    }

    // Envía un mensaje al servidor en formato JSON
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

    // Shortcut para el ping (el servidor lo exige cada 6 segundos)
    public void SendPing() => Send("ping");

    async void OnDestroy()
    {
        if (_socket != null)
            await _socket.Close();
    }
}

// Estructura del mensaje que llega del servidor
[Serializable]
public class ServerMessage
{
    public string type;
    public string body;
}