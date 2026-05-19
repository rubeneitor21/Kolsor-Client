using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Muestra mensajes centrados con fondo negro semiopaco.
///
/// SETUP EN UNITY:
///   1. En el Canvas de la GameScene, crear un Panel hijo:
///      - Anchor: center/middle, Pivot: 0.5/0.5
///      - Width: 420, Height: 70
///      - Image color: (0, 0, 0, 0.65)
///      - Asignar al campo "panel" de este script
///   2. Dentro del Panel, crear un TextMeshProUGUI:
///      - Anchor: stretch/stretch, todos los offsets a 0
///      - Alignment: center/middle, font size: 26
///      - Asignar al campo "label" de este script
///   3. Añadir este componente a un GameObject en GameScene.
public class NotificationUI : MonoBehaviour
{
    public static NotificationUI Instance { get; private set; }

    [Header("Referencias UI")]
    public GameObject panel;
    public TMP_Text label;

    [Header("Ajustes")]
    public float defaultDuration = 2.2f;

    private Coroutine _hideCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    /// Muestra el mensaje durante 'duration' segundos (0 = indefinido hasta la siguiente llamada).
    public void Show(string message, float duration = -1f)
    {
        if (panel == null || label == null) return;
        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);

        label.text = message;
        panel.SetActive(true);

        float d = duration < 0 ? defaultDuration : duration;
        if (d > 0) _hideCoroutine = StartCoroutine(HideAfter(d));
    }

    public void Hide()
    {
        if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); _hideCoroutine = null; }
        if (panel != null) panel.SetActive(false);
    }

    private IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (panel != null) panel.SetActive(false);
    }
}