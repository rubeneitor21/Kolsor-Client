using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CoinFlipUI : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject coinCanvas;
    public RectTransform coin;
    public TMP_Text coinFace;        // el símbolo encima de la moneda
    public TMP_Text resultText;

    [Header("Caras de la moneda")]
    public string headSymbol = "ᚦ"; // Thor — sale si empiezas tú
    public string tailSymbol = "ᛏ"; // Tyr  — sale si empieza el rival

    [Header("Tiempos (segundos)")]
    public float spinDuration = 2.0f;
    public float resultDuration = 1.8f;

    void Start()
    {
        if (coinCanvas != null) coinCanvas.SetActive(false);
        StartCoroutine(PlayCoinFlip());
    }

    private IEnumerator PlayCoinFlip()
    {
        if (coinCanvas == null) yield break;

        GameManager.InputBlocked = true;
        coinCanvas.SetActive(true);
        resultText.text = "";

        bool iStart = (GameData.PlayerStartId == GameData.MyId);
        string finalSymbol = iStart ? headSymbol : tailSymbol;

        // ── Fase 1: girar la moneda ──────────────────────────
        // Alternamos los símbolos durante el giro para simular las dos caras.
        float t = 0f;
        float spinSpeed = 8f;
        bool showingHead = true;
        coinFace.text = headSymbol;

        while (t < spinDuration)
        {
            t += Time.deltaTime;
            float scaleX = Mathf.Cos(t * spinSpeed * Mathf.PI * 2f);
            coin.localScale = new Vector3(scaleX, 1f, 1f);

            // Cuando la moneda está "de canto" (scaleX ~ 0), invertimos la cara.
            // Detectamos el cruce por cero comparando con la escala anterior.
            if (showingHead && scaleX < 0f)
            {
                coinFace.text = tailSymbol;
                showingHead = false;
            }
            else if (!showingHead && scaleX > 0f)
            {
                coinFace.text = headSymbol;
                showingHead = true;
            }

            // El texto también se escala con la moneda para que parezca "pegado"
            coinFace.transform.localScale = new Vector3(Mathf.Abs(scaleX), 1f, 1f);

            yield return null;
        }

        // Aseguramos que termina con la cara correcta visible y a tamaño normal
        coin.localScale = Vector3.one;
        coinFace.transform.localScale = Vector3.one;
        coinFace.text = finalSymbol;

        // ── Fase 2: mostrar resultado ────────────────────────
        resultText.text = iStart
            ? "¡EMPIEZAS TÚ!"
            : $"EMPIEZA {GameData.OpponentName.ToUpper()}";

        yield return new WaitForSeconds(resultDuration);

        // ── Fase 3: ocultar todo ─────────────────────────────
        coinCanvas.SetActive(false);
        GameManager.InputBlocked = false;
    }
}