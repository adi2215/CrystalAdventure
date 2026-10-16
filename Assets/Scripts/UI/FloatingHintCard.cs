using UnityEngine;

/// <summary>Всплывающая карточка живёт сама по себе и сама исчезает.</summary>
public class FloatingHintCard : MonoBehaviour
{
    SpriteRenderer _sr;
    float _scale, _alpha, _t;
    Vector3 _start;
    const float Life = 1.4f;

    public void Init(SpriteRenderer sr, float scale, float alpha)
    {
        _sr = sr; _scale = scale; _alpha = alpha;
        _start = transform.position;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        _t += Time.deltaTime / Life;
        if (_t >= 1f) { Destroy(gameObject); return; }
        // Мягкое появление с небольшим отскоком, потом плавно уплывает вверх и тает.
        float pop = _t < 0.25f ? Mathf.SmoothStep(0f, 1.1f, _t / 0.25f) : Mathf.Lerp(1.1f, 1f, Mathf.Clamp01((_t - 0.25f) / 0.15f));
        transform.localScale = Vector3.one * (_scale * pop);
        transform.position = _start + Vector3.up * (0.18f * _t);
        var c = Color.white;
        c.a = _alpha * (_t < 0.6f ? 1f : 1f - (_t - 0.6f) / 0.4f);
        _sr.color = c;
    }
}
