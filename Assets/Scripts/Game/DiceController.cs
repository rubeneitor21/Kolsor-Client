using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DiceController : MonoBehaviour
{
    public DiceData Data { get; private set; }

    private Vector3 _restPosition;

    public void Init(DiceData data)
    {
        Data = data;
        _restPosition = transform.position;
    }

    /// Actualiza la posición de reposo y mueve el objeto al instante.
    /// BoardManager lo llama cada vez que recoloca el dado.
    /// Si el dado tiene Rigidbody, lo dejamos kinemático para que la física
    /// no lo tire al suelo ni lo amontone con otros dados.
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

    // El clic se detecta vía Physics.Raycast desde GameManager.Update.
    public void ToggleKeep()
    {
        if (Data == null || !Data.isMyDice) return;
        Data.kept = !Data.kept;
        ApplyVisual();
    }

    /// Aplica solo la emisión de color.
    /// La posición física la gestiona BoardManager (SetRestPosition).
    public void ApplyVisual()
    {
        if (Data == null) return;

        transform.position = _restPosition;

        var renderer = GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = renderer.material;
        if (Data.kept)
            mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 1.5f);
        else if (Data.energy)
            mat.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.0f) * 2f);
        else
            mat.SetColor("_EmissionColor", Color.black);
    }
}
