using UnityEngine;
using UnityEngine.UI;

public class LoadingDots : MonoBehaviour
{
    public Image dot1;
    public Image dot2;
    public Image dot3;

    private float _timer = 0f;
    private const float INTERVAL = 0.35f;

    private Color _bright;
    private Color _dim;

    void Start()
    {
        // Dorado brillante y dorado muy apagado
        ColorUtility.TryParseHtmlString("#C8960C", out _bright);
        ColorUtility.TryParseHtmlString("#3A2C10", out _dim);

        dot1.color = _bright;
        dot2.color = _dim;
        dot3.color = _dim;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= INTERVAL)
        {
            _timer = 0f;
            AdvanceDots();
        }
    }

    private void AdvanceDots()
    {
        // El punto brillante rota: 1→2→3→1→...
        if (dot1.color == _bright)
        {
            dot1.color = _dim;
            dot2.color = _bright;
            dot3.color = _dim;
        }
        else if (dot2.color == _bright)
        {
            dot1.color = _dim;
            dot2.color = _dim;
            dot3.color = _bright;
        }
        else
        {
            dot1.color = _bright;
            dot2.color = _dim;
            dot3.color = _dim;
        }
    }
}