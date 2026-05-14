using UnityEngine;

/// Componente añadido a cada figura de dios en la mesa.
/// Gestiona hover, selección e invocación, y muestra/oculta el GodInfoCard.
[RequireComponent(typeof(Collider))]
public class GodFavorController : MonoBehaviour
{
    /// Nombre interno del dios: "ThorsStrike", "BrunhildsFury", etc.
    public string GodName;

    /// Solo true para las figuras del jugador local durante la fase god-favor.
    public bool IsInteractable;

    private static readonly Color HoverColor = new Color(1f, 0.85f, 0.2f) * 1.5f;
    private static readonly Color SelectedColor = new Color(1f, 0.65f, 0f) * 2f;
    private static readonly Color PassiveColor = new Color(0.4f, 0.3f, 0.05f) * 0.8f;

    private bool _selected;

    void Start()
    {
        if (!IsInteractable) SetEmission(PassiveColor);
    }

    public void OnHoverEnter()
    {
        // Mostrar card siempre en fase god-favor (interactuable o no)
        var gm = GameManager.Instance;
        if (gm != null && gm.CurrentState?.state == "god-favor")
            GodInfoCard.Instance?.Show(GodName, gm.MyEnergy, transform.position);

        if (IsInteractable && !_selected)
            SetEmission(HoverColor);
    }

    public void OnHoverExit()
    {
        GodInfoCard.Instance?.Hide();

        if (!_selected)
            SetEmission(IsInteractable ? Color.black : PassiveColor);
    }

    public void Select()
    {
        _selected = true;
        SetEmission(SelectedColor);
    }

    public void Deselect()
    {
        _selected = false;
        SetEmission(IsInteractable ? Color.black : PassiveColor);
    }

    public void SetPassed()
    {
        IsInteractable = false;
        _selected = false;
        SetEmission(PassiveColor);
    }

    private void SetEmission(Color color)
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) return;
        rend.material.EnableKeyword("_EMISSION");
        rend.material.SetColor("_EmissionColor", color);
    }
}