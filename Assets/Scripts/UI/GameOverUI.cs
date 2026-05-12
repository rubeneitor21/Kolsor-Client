using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Panel de fin de partida. Escucha OnTurnChanged del GameManager y se muestra
/// cuando el estado es "game-over".
public class GameOverUI : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject panel;        // el panel completo (desactivado al inicio)
    public TMP_Text resultText;   // "VICTORIA" o "DERROTA"
    public TMP_Text subtitleText; // "Has ganado la batalla" / "Has perdido la batalla"
    public Button lobbyButton;  // volver al lobby

    // Colores del juego
    private static readonly Color GoldBright = new Color(0.7843f, 0.5882f, 0.0471f);
    private static readonly Color GoldMid = new Color(0.4784f, 0.3765f, 0.2510f);
    private static readonly Color RedColor = new Color(0.7843f, 0.1882f, 0.1176f);

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (lobbyButton != null) lobbyButton.onClick.AddListener(OnLobbyClick);
        GameManager.OnTurnChanged += CheckGameOver;
    }

    void OnDestroy()
    {
        GameManager.OnTurnChanged -= CheckGameOver;
    }

    private void CheckGameOver()
    {
        if (GameManager.Instance?.CurrentState?.state != "game-over") return;

        bool iWon = GameData.WinnerId == GameData.MyId;

        if (resultText != null)
        {
            resultText.text = iWon ? "VICTORIA" : "DERROTA";
            resultText.color = iWon ? GoldBright : RedColor;
        }

        if (subtitleText != null)
        {
            subtitleText.text = iWon ? "Has ganado la batalla" : "Has perdido la batalla";
            subtitleText.color = GoldMid;
        }

        if (panel != null) panel.SetActive(true);
    }

    private void OnLobbyClick()
    {
        SceneManager.LoadScene("LobbyScene");
    }
}