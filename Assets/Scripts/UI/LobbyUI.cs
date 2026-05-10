using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("Panel: selección de dioses")]
    public GameObject godSelectionPanel; // contiene GodSelectionUI
    public GodSelectionUI godSelectionUI;  // ref directa para llamar Show()

    void Start()
    {
        usernameLabel.text = AuthManager.Instance.Username.ToUpper();

        findMatchPanel.SetActive(true);
        matchmakingPanel.SetActive(false);
        if (godSelectionPanel != null) godSelectionPanel.SetActive(false);
        errorText.text = "";

        findMatchButton.onClick.AddListener(OnFindMatchClick);
        cancelButton.onClick.AddListener(OnCancelClick);
        exitButton.onClick.AddListener(OnExitClick);
        exitButton2.onClick.AddListener(OnExitClick);

        LobbyManager.OnSearchStarted += OnSearchStarted;
        LobbyManager.OnMatchmakingJoin += OnMatchmakingJoin;
        LobbyManager.OnGodSelectionStart += OnGodSelectionStart;
        LobbyManager.OnGameStart += OnGameStart;
        WebSocketManager.OnDisconnected += OnDisconnected;
    }

    void OnDestroy()
    {
        LobbyManager.OnSearchStarted -= OnSearchStarted;
        LobbyManager.OnMatchmakingJoin -= OnMatchmakingJoin;
        LobbyManager.OnGodSelectionStart -= OnGodSelectionStart;
        LobbyManager.OnGameStart -= OnGameStart;
        WebSocketManager.OnDisconnected -= OnDisconnected;
    }

    // ── Botones ───────────────────────────────────────────────────────────────

    private void OnFindMatchClick()
    {
        errorText.text = "";
        findMatchButton.interactable = false;
        LobbyManager.Instance.SearchMatch();
    }

    private void OnCancelClick()
    {
        matchmakingPanel.SetActive(false);
        findMatchPanel.SetActive(true);
        findMatchButton.interactable = true;
        errorText.text = "";
    }

    private void OnExitClick()
    {
        SceneManager.LoadScene("LoginScene");
    }

    // ── Eventos de red ────────────────────────────────────────────────────────

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

    /// Se encontró rival: ocultar "buscando" y mostrar selección de dioses.
    private void OnGodSelectionStart(string[] gods)
    {
        matchmakingPanel.SetActive(false);
        if (godSelectionPanel != null) godSelectionPanel.SetActive(true);
        godSelectionUI?.Show(gods); // llamada directa, sin depender de eventos
    }

    /// Ambos jugadores eligieron dioses: ir al juego.
    private void OnGameStart(GameStartData data)
    {
        SceneManager.LoadScene("GameScene");
    }

    private void OnDisconnected()
    {
        if (godSelectionPanel != null) godSelectionPanel.SetActive(false);
        matchmakingPanel.SetActive(false);
        findMatchPanel.SetActive(true);
        findMatchButton.interactable = true;
        errorText.text = "Conexión perdida con el servidor.";
    }
}