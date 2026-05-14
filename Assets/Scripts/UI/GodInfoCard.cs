using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Panel UI que aparece al hacer hover sobre una figura de dios durante la fase god-favor.
/// Muestra los 3 tiers con su coste y efecto, resaltando el máximo que puedes pagar.
///
/// Jerarquía en Canvas:
///   GodInfoCard (este componente + Image fondo oscuro, desactivado al inicio)
///   ├── TitleText          (TMP_Text)
///   ├── DescText           (TMP_Text)
///   ├── PriorityText       (TMP_Text)
///   ├── TierRow0           (GameObject + Image)
///   │   ├── CostText       (TMP_Text)
///   │   └── EffectText     (TMP_Text)
///   ├── TierRow1           (igual)
///   ├── TierRow2           (igual)
///   └── ElegirButton       (Button)
///       └── ElegirLabel    (TMP_Text)
public class GodInfoCard : MonoBehaviour
{
    public static GodInfoCard Instance { get; private set; }

    private bool _locked = false;

    public void Lock() => _locked = true;

    [Header("Textos principales")]
    public TMP_Text titleText;
    public TMP_Text descText;
    public TMP_Text priorityText;

    [Header("Filas de tiers (3 elementos)")]
    public TierRowUI[] tierRows;

    [Header("Botón elegir")]
    public Button elegirButton;
    public TMP_Text elegirLabel;

    [System.Serializable]
    public class TierRowUI
    {
        public GameObject root;
        public TMP_Text costText;
        public TMP_Text effectText;
    }

    // Colores que coinciden con la paleta del juego
    private static readonly Color BgNormal = new Color(0.18f, 0.13f, 0.04f, 1f);  // #2E2109
    private static readonly Color BgAffordable = new Color(0.29f, 0.21f, 0.06f, 1f);  // #4A3610
    private static readonly Color BgSelected = new Color(0.42f, 0.30f, 0.06f, 1f);  // #6B4D0F — tier activo
    private static readonly Color TextDim = new Color(0.45f, 0.40f, 0.32f, 1f);
    private static readonly Color TextBright = new Color(0.85f, 0.75f, 0.50f, 1f);

    private string _currentGodName;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.SetActive(false);
    }

    /// Muestra el card para un dios concreto con la energía actual del jugador.
    /// godWorldPos se usa para posicionar el card cerca de la figura en pantalla.
    public void Show(string godName, int myEnergy, Vector3 godWorldPos)
    {
        if (!GodData.All.TryGetValue(godName, out var info)) return;
        _currentGodName = godName;

        // Textos de cabecera
        titleText.text = info.DisplayName;
        descText.text = info.Description;
        priorityText.text = $"Prioridad: {info.Priority}";

        // Tier más alto que puedo pagar (0 = ninguno)
        int activeTier = GodData.GetAffordableTier(godName, myEnergy);

        for (int i = 0; i < tierRows.Length && i < info.Tiers.Length; i++)
        {
            var row = tierRows[i];
            var tier = info.Tiers[i];
            bool canPay = myEnergy >= tier.Cost;
            bool isActive = (i + 1 == activeTier);

            row.costText.text = tier.Cost.ToString();
            row.effectText.text = tier.EffectText;

            // Color de fondo de la fila
            var img = row.root.GetComponent<Image>();
            if (img != null)
                img.color = isActive ? BgSelected : (canPay ? BgAffordable : BgNormal);

            // Color del texto
            Color textCol = canPay ? TextBright : TextDim;
            row.costText.color = textCol;
            row.effectText.color = textCol;
        }

        // Botón Elegir: solo activo si puedo pagar algo
        bool canUse = activeTier > 0;
        elegirButton.interactable = canUse;
        if (elegirLabel != null)
            elegirLabel.color = canUse ? TextBright : TextDim;

        elegirButton.onClick.RemoveAllListeners();
        if (canUse)
        {
            elegirButton.onClick.AddListener(() =>
            {
                Hide();
                GameManager.Instance?.InvokeGodFromCard(godName);
            });
        }

        // Posicionar el card en pantalla cerca de la figura
        PositionNearWorldPoint(godWorldPos);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_locked) return; // no cerrar si está bloqueado
        gameObject.SetActive(false);
        _currentGodName = null;
    }

    public void ForceHide()
    {
        _locked = false;
        gameObject.SetActive(false);
        _currentGodName = null;
    }

    public bool IsShowingFor(string godName)
        => gameObject.activeSelf && _currentGodName == godName;

    /// Mueve el RectTransform del card para que aparezca a la derecha de la figura,
    /// sin salirse de los bordes de la pantalla.
    private void PositionNearWorldPoint(Vector3 worldPos)
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        var rt = GetComponent<RectTransform>();
        var canvas = GetComponentInParent<Canvas>();
        if (rt == null || canvas == null) return;

        // Convertir a coordenadas del canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out Vector2 localPoint
        );

        // Desplazar a la derecha de la figura
        localPoint += new Vector2(rt.rect.width * 0.55f, 0f);

        // Mantener dentro del canvas
        var canvasRect = canvas.GetComponent<RectTransform>().rect;
        float halfW = rt.rect.width * 0.5f;
        float halfH = rt.rect.height * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, canvasRect.xMin + halfW, canvasRect.xMax - halfW);
        localPoint.y = Mathf.Clamp(localPoint.y, canvasRect.yMin + halfH, canvasRect.yMax - halfH);

        rt.localPosition = localPoint;
    }


}