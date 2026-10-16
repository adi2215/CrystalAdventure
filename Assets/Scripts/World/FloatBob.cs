using UnityEngine;

/// <summary>Покачивание вверх-вниз (обломки острова). Появляется плавно, когда карта собралась.</summary>
public class FloatBob : MonoBehaviour
{
    public float amplitude = 0.015f;
    public float period = 4f;
    public float phase;

    Vector3 _base;
    SpriteRenderer _sr;
    float _alpha;

    void Start()
    {
        _base = transform.localPosition;
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) { var c = _sr.color; c.a = 0f; _sr.color = c; }
    }

    void Update()
    {
        float t = Time.time * Mathf.PI * 2f / Mathf.Max(0.3f, period) + phase;
        transform.localPosition = _base + new Vector3(0f, Mathf.Sin(t) * amplitude, 0f);
        if (_sr != null && _alpha < 1f)
        {
            var mm = MapManager.Instance;
            bool ready = mm == null || mm.data == null || !mm.data.FallingMap;
            if (ready) _alpha = Mathf.MoveTowards(_alpha, 1f, Time.deltaTime / 1.2f);
            var c = _sr.color; c.a = _alpha; _sr.color = c;
        }
    }
}
