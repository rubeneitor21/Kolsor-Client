using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GodSelectionUI : MonoBehaviour
{
    [Header("Contenedor de botones (Grid Layout Group)")]
    public Transform godButtonContainer;
    public GameObject godButtonPrefab;

    [Header("Botón de confirmación")]
    public Button confirmButton;
    public TMP_Text confirmLabel;

    [Header("Textos")]
    public TMP_Text waitingText;
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public TMP_Text timerText;   // Texto separado para el contador

    // Colores exactos del lobby
    private static readonly Color BtnNormal = new Color(0.2902f, 0.2078f, 0.0627f, 1f);
    private static readonly Color BtnHover = new Color(0.2275f, 0.1569f, 0.0627f, 1f);
    private static readonly Color BorderCol = new Color(0.4196f, 0.3098f, 0.1020f, 1f);
    private static readonly Color GoldBright = new Color(0.7843f, 0.5882f, 0.0471f, 1f);
    private static readonly Color GoldMid = new Color(0.4784f, 0.3765f, 0.2510f, 1f);
    private static readonly Color GoldDim = new Color(0.2275f, 0.1725f, 0.0627f, 1f);
    // Hover para botón ya seleccionado: dorado oscuro, no amarillo brillante
    private static readonly Color SelHover = new Color(0.5490f, 0.4000f, 0.0627f, 1f);

    private readonly List<string> _selected = new();
    private readonly List<Button> _buttons = new();
    private readonly List<TMP_Text> _labels = new();
    private string[] _availableGods;
    private bool _confirmed;
    private Coroutine _timerCoroutine;

    // ── Punto de entrada ─────────────────────────────────────────────────────

    public void Show(string[] availableGods)
    {
        _availableGods = availableGods;
        _confirmed = false;
        _selected.Clear();

        if (titleText != null) { titleText.text = "KOLSOR"; titleText.color = GoldBright; }
        if (subtitleText != null) { subtitleText.text = "ELIGE TUS DIOSES"; subtitleText.color = GoldMid; }
        if (waitingText != null) waitingText.gameObject.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(true);
        if (confirmButton != null) confirmButton.gameObject.SetActive(true);

        BuildButtons();
        RefreshConfirmButton();

        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(SelectionTimer(60));
    }

    // ── Timer ─────────────────────────────────────────────────────────────────

    private IEnumerator SelectionTimer(int seconds)
    {
        for (int t = seconds; t > 0; t--)
        {
            if (_confirmed) yield break;
            if (timerText != null) timerText.text = $"{t}s";
            yield return new WaitForSeconds(1f);
        }
        if (!_confirmed) AutoSelectDefaults();
    }

    private void AutoSelectDefaults()
    {
        _selected.Clear();
        string def1 = System.Array.IndexOf(_availableGods, "ThorsStrike") >= 0 ? "ThorsStrike" : _availableGods[0];
        string def2 = System.Array.IndexOf(_availableGods, "BragisVerve") >= 0 ? "BragisVerve" : _availableGods[1];
        _selected.Add(def1);
        _selected.Add(def2);
        RefreshButtonColors();
        RefreshConfirmButton();
        if (timerText != null) timerText.text = "";
        StartCoroutine(ConfirmAfterDelay(1.5f));
    }

    private IEnumerator ConfirmAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        OnConfirmClick();
    }

    // ── Construcción de botones ───────────────────────────────────────────────

    private void BuildButtons()
    {
        foreach (var b in _buttons) if (b != null) Destroy(b.gameObject);
        _buttons.Clear(); _labels.Clear();
        if (godButtonPrefab == null || godButtonContainer == null) return;

        foreach (var godName in _availableGods)
        {
            var obj = Instantiate(godButtonPrefab, godButtonContainer);
            var btn = obj.GetComponent<Button>();
            var img = obj.GetComponent<Image>();

            // Image en blanco para que el ColorBlock haga el tinting
            if (img != null) img.color = Color.white;
            ApplyButtonColors(btn, BtnNormal, BtnHover, BtnHover);

            var lbl = obj.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.text = $"<b>{FriendlyName(godName)}</b>\n<size=65%>{CostLine(godName)}</size>";
                lbl.color = GoldMid;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontSize = 28;
            }
            _labels.Add(lbl);

            string name = godName;
            btn.onClick.AddListener(() => OnToggle(name));
            _buttons.Add(btn);
        }
    }

    // ── Interacción ───────────────────────────────────────────────────────────

    private void OnToggle(string godName)
    {
        if (_confirmed) return;
        if (_selected.Contains(godName)) _selected.Remove(godName);
        else { if (_selected.Count >= 2) return; _selected.Add(godName); }
        RefreshButtonColors();
        RefreshConfirmButton();
    }

    public void OnConfirmClick()
    {
        if (_selected.Count != 2 || _confirmed) return;
        _confirmed = true;
        if (_timerCoroutine != null) { StopCoroutine(_timerCoroutine); _timerCoroutine = null; }

        LobbyManager.Instance.SendGodSelection(_selected[0], _selected[1]);

        foreach (var b in _buttons) b.interactable = false;
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (waitingText != null)
        {
            waitingText.gameObject.SetActive(true);
            waitingText.enabled = true;
            waitingText.text = "Esperando al rival...";
            waitingText.color = GoldMid;
        }

    }

    // ── Colores ───────────────────────────────────────────────────────────────

    private void RefreshButtonColors()
    {
        for (int i = 0; i < _buttons.Count && i < _availableGods.Length; i++)
        {
            string name = _availableGods[i];
            bool sel = _selected.Contains(name);
            bool full = _selected.Count >= 2 && !sel;
            var img = _buttons[i].GetComponent<Image>();
            var lbl = _labels[i];

            if (img != null) img.color = Color.white;

            if (sel)
            {
                // Seleccionado: fondo dorado, hover más oscuro para que se lea
                ApplyButtonColors(_buttons[i], BorderCol, SelHover, SelHover);
                if (lbl != null) lbl.color = GoldBright;
            }
            else if (full)
            {
                // Ya hay 2 elegidos, este no: apagado
                var dim = new Color(BtnNormal.r * 0.5f, BtnNormal.g * 0.5f, BtnNormal.b * 0.5f);
                ApplyButtonColors(_buttons[i], dim, dim, dim);
                if (lbl != null) lbl.color = GoldDim;
            }
            else
            {
                ApplyButtonColors(_buttons[i], BtnNormal, BtnHover, BtnHover);
                if (lbl != null) lbl.color = GoldMid;
            }
        }
    }

    private void RefreshConfirmButton()
    {
        if (confirmButton == null) return;
        bool ready = _selected.Count == 2;

        var img = confirmButton.GetComponent<Image>();
        if (img != null) img.color = Color.white;

        // Cuando no está listo, usamos BtnNormal como disabled para evitar el blanco default
        ApplyButtonColors(confirmButton,
            ready ? BorderCol : BtnNormal,
            ready ? SelHover : BtnHover,
            ready ? SelHover : BtnHover,
            disabledColor: BtnNormal);  // evita el gris blancuzco de Unity

        if (confirmLabel != null)
        {
            confirmLabel.text = ready ? "ELEGIR DIOSES" : $"ELIGE {2 - _selected.Count} M\u00C1S";
            confirmLabel.color = ready ? GoldBright : GoldMid;
        }
        confirmButton.interactable = ready;
    }

    private static void ApplyButtonColors(Button btn, Color normal, Color hover, Color pressed,
                                           Color? disabledColor = null)
    {
        if (btn == null) return;
        var cb = btn.colors;
        cb.normalColor = normal;
        cb.highlightedColor = hover;
        cb.pressedColor = pressed;
        cb.selectedColor = normal;
        cb.disabledColor = disabledColor ?? new Color(normal.r, normal.g, normal.b, 0.5f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;
    }

    // ── Helpers de texto ──────────────────────────────────────────────────────

    private static string FriendlyName(string g) => g switch
    {
        "ThorsStrike" => "Thor",
        "BrunhildsFury" => "Brunhild",
        "SkadisHunt" => "Skadi",
        "LokisTrick" => "Loki",
        "BragisVerve" => "Bragi",
        "IdunsRejuvenat" => "Idun",
        "MimirsWisdom" => "Mimir",
        "VarsBond" => "Var",
        _ => g
    };

    private static string CostLine(string g) => g switch
    {
        "ThorsStrike" => "Da\u00F1o directo 2/5/8 vidas \u2022 Coste: 4/8/12",
        "BrunhildsFury" => "Tus Hachas \u00D71.5/2/3 \u2022 Coste: 6/10/18",
        "SkadisHunt" => "Tus Flechas \u00D72/3/4 \u2022 Coste: 6/10/14",
        "LokisTrick" => "Cancela 1/2/3 dados rivales \u2022 Coste: 3/6/9",
        "BragisVerve" => "+2/3/4 fichas por Mano \u2022 Coste: 4/8/12",
        "IdunsRejuvenat" => "Cura 2/4/6 vidas \u2022 Coste: 4/7/10",
        "MimirsWisdom" => "+1/2/3 fichas por da\u00F1o recibido \u2022 Coste: 3/5/7",
        "VarsBond" => "+1/2/3 vida por ficha rival gastada \u2022 Coste: 10/14/18",
        _ => ""
    };
}