using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour
{
    [Header("Panel: buscar partida")]
    public GameObject findMatchPanel;
    public TMP_Text usernameLabel;
    public Button findMatchButton;
    public Button exitButton;
    public TMP_Text errorText;

    [Header("Panel: buscando")]
    public GameObject matchmakingPanel;
    public TMP_Text statusText;
    public Button cancelButton;
    public Button exitButton2;

    void Start()
    {
        // Mostrar nombre del jugador
        usernameLabel.text = AuthManager.Instance.Username.ToUpper();

        // Estado inicial
        findMatchPanel.SetActive(true);
        matchmakingPanel.SetActive(false);
        errorText.text = "";

        // Botones
        findMatchButton.onClick.AddListener(OnFindMatchClick);
        cancelButton.onClick.AddListener(OnCancelClick);
        exitButton.onClick.AddListener(OnExitClick);
        exitButton2.onClick.AddListener(OnExitClick);

        // Eventos de red
        LobbyManager.OnSearchStarted += OnSearchStarted;
        LobbyManager.OnMatchmakingJoin += OnMatchmakingJoin;
        LobbyManager.OnGameStart += OnGameStart;

        WebSocketManager.OnDisconnected += OnDisconnected;
    }

    void OnDestroy()
    {
        LobbyManager.OnSearchStarted -= OnSearchStarted;
        LobbyManager.OnMatchmakingJoin -= OnMatchmakingJoin;
        LobbyManager.OnGameStart -= OnGameStart;
        WebSocketManager.OnDisconnected -= OnDisconnected;
    }

    // ── Botones ────────────────────────────────────────────
    private void OnFindMatchClick()
    {
        errorText.text = "";
        findMatchButton.interactable = false;
        LobbyManager.Instance.SearchMatch();
    }

    private void OnCancelClick()
    {
        // El servidor no tiene cancel por ahora — volvemos al panel de inicio
        matchmakingPanel.SetActive(false);
        findMatchPanel.SetActive(true);
        findMatchButton.interactable = true;
        errorText.text = "";
    }

    private void OnExitClick()
    {
        SceneManager.LoadScene("LoginScene");
    }

    // ── Eventos de red ─────────────────────────────────────
    private void OnSearchStarted()
    {
        findMatchPanel.SetActive(false);
        matchmakingPanel.SetActive(true);
        statusText.text = "Aguardando en la sala...";
    }

    private void OnMatchmakingJoin(string message)
    {
        statusText.text = message;
    }

    private void OnGameStart(GameStartData data)
    {
        SceneManager.LoadScene("GameScene");
    }

    private void OnDisconnected()
    {
        matchmakingPanel.SetActive(false);
        findMatchPanel.SetActive(true);
        findMatchButton.interactable = true;
        errorText.text = "Conexión perdida con el servidor.";
    }
}