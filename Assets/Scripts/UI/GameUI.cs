using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [Header("Turno")]
    public TMP_Text turnText;
    public TMP_Text roundText;

    [Header("Selección de dados")]
    public Button confirmButton;

    void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClick);

        GameManager.OnRollsChanged += Refresh;
        GameManager.OnTurnChanged += Refresh;

        Refresh();
    }

    void OnDestroy()
    {
        GameManager.OnRollsChanged -= Refresh;
        GameManager.OnTurnChanged -= Refresh;
    }

    private void OnConfirmClick()
    {
        // GameManager.Instance?.ConfirmSelection();
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;

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
            roundText.text = $"Ronda {gm.CurrentState.round}";

        // if (confirmButton != null)
        //    confirmButton.interactable = gm != null && gm.CanSelect;
    }
}
