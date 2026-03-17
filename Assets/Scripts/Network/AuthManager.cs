using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class AuthManager : MonoBehaviour
{
    private const string SERVER_URL = "http://localhost:3000/api";

    public static AuthManager Instance { get; private set; }

    // Guardamos el token y datos del usuario tras el login
    public string JwtToken { get; private set; }
    public string UserId { get; private set; }
    public string Username { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(JwtToken);

    // Eventos para que la UI reaccione
    public static event Action<string> OnLoginSuccess;   // devuelve username
    public static event Action<string> OnLoginFailed;    // devuelve mensaje de error
    public static event Action<string> OnRegisterSuccess;
    public static event Action<string> OnRegisterFailed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── LOGIN ──────────────────────────────────────────────
    public void Login(string username, string password)
    {
        StartCoroutine(LoginCoroutine(username, password));
    }

    private IEnumerator LoginCoroutine(string username, string password)
    {
        string json = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(SERVER_URL + "/login", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
            JwtToken = response.token;
            UserId = response.user.id;
            Username = response.user.username;

            Debug.Log($"[Auth] ✅ Login correcto: {Username}");
            OnLoginSuccess?.Invoke(Username);

            // Autenticamos también el WebSocket automáticamente
            AuthenticateWebSocket();
        }
        else
        {
            var error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            Debug.LogWarning("[Auth] ❌ Login fallido: " + error.error);
            OnLoginFailed?.Invoke(error.error);
        }
    }

    // ── REGISTRO ───────────────────────────────────────────
    public void Register(string username, string password)
    {
        StartCoroutine(RegisterCoroutine(username, password));
    }

    private IEnumerator RegisterCoroutine(string username, string password)
    {
        string json = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(SERVER_URL + "/register", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[Auth] ✅ Registro correcto");
            OnRegisterSuccess?.Invoke("Cuenta creada. Ya puedes iniciar sesión.");
        }
        else
        {
            var error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            Debug.LogWarning("[Auth] ❌ Registro fallido: " + error.error);
            OnRegisterFailed?.Invoke(error.error);
        }
    }

    // ── AUTENTICAR WEBSOCKET ───────────────────────────────
    // Se llama automáticamente tras el login
    private void AuthenticateWebSocket()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            // Si el WS aún no está listo, esperamos a que se conecte
            WebSocketManager.OnConnected += SendAuthOnConnect;
            return;
        }
        SendAuth();
    }

    private void SendAuthOnConnect()
    {
        WebSocketManager.OnConnected -= SendAuthOnConnect;
        SendAuth();
    }

    private void SendAuth()
    {
        WebSocketManager.Instance.Send("auth", $"{{\"jwt\":\"{JwtToken}\"}}");
        Debug.Log("[Auth] 🔑 Token enviado por WebSocket");
    }
}

// ── Estructuras para parsear las respuestas ────────────────
[Serializable]
public class LoginResponse
{
    public string token;
    public UserInfo user;
}

[Serializable]
public class UserInfo
{
    public string id;
    public string username;
}

[Serializable]
public class ErrorResponse
{
    public string error;
}
