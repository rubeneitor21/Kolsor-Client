using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DiceController : MonoBehaviour
{
    public DiceData Data { get; private set; }

    private Vector3 _restPosition;
    private const float KEPT_LIFT = 0.35f;

    public void Init(DiceData data)
    {
        Data = data;
        _restPosition = transform.position;
    }

    // El clic se detecta vía Physics.Raycast desde GameManager.Update (más
    // fiable que OnMouseDown). Este método aplica el toggle externo.
    public void ToggleKeep()
    {
        if (Data == null || !Data.isMyDice) return;
        Data.kept = !Data.kept;
        ApplyVisual();
    }

    public void ApplyVisual()
    {
        if (Data == null) return;

        Vector3 pos = _restPosition;
        if (Data.kept) pos += Vector3.up * KEPT_LIFT;
        transform.position = pos;

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = renderer.material;
            if (Data.kept)
                mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 1.5f);
            else if (Data.energy)
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.0f) * 2f);
            else
                mat.SetColor("_EmissionColor", Color.black);
        }
    }
}
