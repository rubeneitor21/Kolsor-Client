using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [Header("Turno / Ronda")]
    public TMP_Text turnText;
    public TMP_Text roundText;

    [Header("Vidas y energía")]
    public TMP_Text myLifeText;
    public TMP_Text opponentLifeText;
    public TMP_Text myEnergyText;
    public TMP_Text opponentEnergyText;

    [Header("Panel de favores divinos")]
    public GameObject godFavorPanel;
    public Button damageButton;       // "Daño" — requiere 3 energía
    public Button protectionButton;   // "Protección" — requiere 5 energía
    public TMP_Text godFavorWaitText; // "Esperando al rival..." tras elegir
    public TMP_Text godFavorTitle;    // "Elige tu favor divino"

    [Header("Panel de game-over")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;   // "¡Victoria!" / "¡Derrota!"

    void Start()
    {
        if (damageButton != null) damageButton.onClick.AddListener(() => OnFavorChosen("damage"));
        if (protectionButton != null) protectionButton.onClick.AddListener(() => OnFavorChosen("protection"));

        GameManager.OnRollsChanged += Refresh;
        GameManager.OnTurnChanged += Refresh;
        GameManager.OnGodFavorNeeded += ShowGodFavorPanel;
        GameManager.OnLifeUpdated += RefreshLife;

        if (godFavorPanel != null) godFavorPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (godFavorWaitText != null) godFavorWaitText.gameObject.SetActive(false);

        Refresh();
    }

    void OnDestroy()
    {
        GameManager.OnRollsChanged -= Refresh;
        GameManager.OnTurnChanged -= Refresh;
        GameManager.OnGodFavorNeeded -= ShowGodFavorPanel;
        GameManager.OnLifeUpdated -= RefreshLife;
    }

    // ── Turno / ronda ────────────────────────────────────────

    private void Refresh()
    {
        var gm = GameManager.Instance;

        // Game-over
        if (gm?.CurrentState?.state == "game-over")
        {
            ShowGameOver();
            return;
        }

        // God-favor: no actualizar turno mientras el panel esté abierto
        if (godFavorPanel != null && godFavorPanel.activeSelf) return;

        if (turnText != null)
        {
            if (gm == null || gm.CurrentState == null)
                turnText.text = "Esperando partida...";
            else if (gm.IsMyTurn)
                turnText.text = "TU TURNO";
            else
                turnText.text = $"Turno de {gm.OpponentName}";
        }

        if (roundText != null && gm?.CurrentState != null)
        {
            int r = gm.CurrentState.round;
            roundText.text = r > 0 ? $"Tirada {r}/3" : "";
        }

        RefreshLife();
    }

    // ── Vidas y energía ──────────────────────────────────────

    private void RefreshLife()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (myLifeText != null) myLifeText.text = $"❤ {gm.MyLife}";
        if (opponentLifeText != null) opponentLifeText.text = $"❤ {gm.OpponentLife}";
        if (myEnergyText != null) myEnergyText.text = $"⚡ {gm.MyEnergy}";
        if (opponentEnergyText != null) opponentEnergyText.text = $"⚡ {gm.OpponentEnergy}";
    }

    // ── Favores divinos ──────────────────────────────────────

    private void ShowGodFavorPanel()
    {
        if (godFavorPanel == null) return;
        godFavorPanel.SetActive(true);

        // Mostrar energía disponible en el título
        int energy = GameManager.Instance?.MyEnergy ?? 0;
        if (godFavorTitle != null)
            godFavorTitle.text = $"Elige tu favor divino\nEnergía: {energy}";

        // Indicar coste y disponibilidad de cada favor
        if (damageButton != null)
        {
            var label = damageButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = energy >= 3 ? "Daño\n(−3 energía)" : "Daño\n(sin energía)";
        }
        if (protectionButton != null)
        {
            var label = protectionButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = energy >= 5 ? "Protección\n(−5 energía)" : "Protección\n(sin energía)";
        }

        // Mostrar botones, ocultar mensaje de espera
        if (damageButton != null) damageButton.gameObject.SetActive(true);
        if (protectionButton != null) protectionButton.gameObject.SetActive(true);
        if (godFavorWaitText != null) godFavorWaitText.gameObject.SetActive(false);
        if (turnText != null) turnText.text = "Elige tu favor divino";
    }

    private void OnFavorChosen(string favor)
    {
        GameManager.Instance?.SendGodFavor(favor);

        // Ocultar botones, mostrar espera
        if (damageButton != null) damageButton.gameObject.SetActive(false);
        if (protectionButton != null) protectionButton.gameObject.SetActive(false);
        if (godFavorWaitText != null)
        {
            godFavorWaitText.gameObject.SetActive(true);
            string label = favor == "damage" ? "Daño" : "Protección";
            godFavorWaitText.text = $"Elegiste: {label}\nEsperando al rival...";
        }
    }

    // ── Game-over ────────────────────────────────────────────

    private void ShowGameOver()
    {
        if (godFavorPanel != null) godFavorPanel.SetActive(false);
        if (gameOverPanel == null) return;
        gameOverPanel.SetActive(true);

        bool iWon = GameData.WinnerId == GameData.MyId;
        if (gameOverText != null)
            gameOverText.text = iWon ? "¡Victoria!" : "¡Derrota!";
    }
}