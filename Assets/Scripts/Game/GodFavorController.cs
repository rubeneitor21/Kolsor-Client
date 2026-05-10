using UnityEngine;

/// Componente que se añade dinámicamente a cada figura de dios en la mesa.
/// Gestiona el hover, la selección y el envío del favor divino al servidor.
[RequireComponent(typeof(Collider))]
public class GodFavorController : MonoBehaviour
{
    /// "damage" o "protection"
    public string FavorType;

    /// Solo true para las figuras del jugador local durante la fase god-favor.
    public bool IsInteractable;

    private static readonly Color HoverColor = new Color(1f, 0.85f, 0.2f) * 1.5f;
    private static readonly Color SelectedColor = new Color(1f, 0.65f, 0f) * 2f;

    private bool _selected;

    // ── Hover (llamado desde GameManager via raycast) ────────

    public void OnHoverEnter()
    {
        if (!_selected) SetEmission(HoverColor);
    }

    public void OnHoverExit()
    {
        if (!_selected) SetEmission(Color.black);
    }

    // ── Selección ────────────────────────────────────────────

    public void Select()
    {
        _selected = true;
        SetEmission(SelectedColor);
    }

    public void Deselect()
    {
        _selected = false;
        SetEmission(Color.black);
    }

    // ── Helper ───────────────────────────────────────────────

    private void SetEmission(Color color)
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) return;
        rend.material.EnableKeyword("_EMISSION");
        rend.material.SetColor("_EmissionColor", color);
    }
}