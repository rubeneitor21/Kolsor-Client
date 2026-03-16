using UnityEngine;

public class PingManager : MonoBehaviour
{
    // Cada 4 segundos enviamos ping (el servidor corta a los 6)
    private const float PING_INTERVAL = 4f;
    private float _timer = 0f;

    void Start()
    {
        // Nos suscribimos para saber cuándo estamos conectados
        WebSocketManager.OnConnected += OnConnected;
        WebSocketManager.OnDisconnected += OnDisconnected;
    }

    void OnDestroy()
    {
        WebSocketManager.OnConnected -= OnConnected;
        WebSocketManager.OnDisconnected -= OnDisconnected;
    }

    private bool _active = false;

    private void OnConnected()
    {
        _active = true;
        _timer = 0f;
        Debug.Log("[Ping] Sistema de ping activado");
    }

    private void OnDisconnected()
    {
        _active = false;
        Debug.Log("[Ping] Sistema de ping desactivado");
    }

    void Update()
    {
        if (!_active) return;

        _timer += Time.deltaTime;

        if (_timer >= PING_INTERVAL)
        {
            _timer = 0f;
            WebSocketManager.Instance.SendPing();
            Debug.Log("[Ping] 🏓 Ping enviado");
        }
    }
}