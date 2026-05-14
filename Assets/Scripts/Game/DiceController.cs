using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DiceController : MonoBehaviour
{
    public DiceData Data { get; private set; }

    private Vector3 _restPosition;
    public Vector3 RestPosition => _restPosition;

    public void Init(DiceData data)
    {
        Data = data;
        _restPosition = transform.position;
    }

    public void SetRestPosition(Vector3 pos)
    {
        _restPosition = pos;
        transform.position = pos;
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    /// Marca o desmarca el dado para guardar y pide al tablero que actualice posiciones.
    public void ToggleKeep()
    {
        if (Data == null || !Data.isMyDice) return;
        Data.kept = !Data.kept;
        BoardManager.Instance?.RefreshSelectionRow();
    }

    /// Limpia emision residual y hace snap a la posicion de reposo.
    /// Llamado desde BoardManager tras la animacion de tirada.
    public void ApplyVisual()
    {
        if (Data == null) return;
        transform.position = _restPosition;
        SetEmission(Color.black);
    }

    // Hover (llamado desde GameManager via raycast).
    // No usamos OnMouseEnter/OnMouseExit: no son fiables con
    // el New Input System en modo exclusivo.

    public void OnHoverEnter()
    {
        SetEmission(new Color(1f, 0.85f, 0.2f) * 1.5f);
    }

    public void OnHoverExit()
    {
        SetEmission(Color.black);
    }

    private void SetEmission(Color color)
    {
        var rend = GetComponent<Renderer>()
                ?? GetComponentInChildren<Renderer>();
        if (rend == null) return;
        foreach (var mat in rend.materials)
        {
            if (mat == null) continue;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color);
        }
    }
}