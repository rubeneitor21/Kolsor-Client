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

    public void ToggleKeep()
    {
        if (Data == null || !Data.isMyDice) return;
        Data.kept = !Data.kept;
        ApplyVisual();
    }

    public void ApplyVisual()
    {
        if (Data == null) return;
        transform.position = _restPosition;

        var renderer = GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = renderer.material;
        mat.EnableKeyword("_EMISSION");

        if (Data.kept)
        {
            // Marcado para guardar: emisión amarilla brillante en el dado.
            // Como el halo de energy es más tenue y solo está alrededor, se distingue.
            mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 1.5f);
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
        }
    }
}