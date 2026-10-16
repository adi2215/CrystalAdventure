using UnityEngine;

/// <summary>
/// «Сочность» куба-героя поверх его анимации:
///  • сплющивание/вытягивание на пружинке (присел перед шагом, вытянулся в прыжке, сплющился при приземлении);
///  • мягкая тень под кубом, которая уменьшается и бледнеет, когда он в воздухе.
///
/// Масштаб домножается в LateUpdate — анимация «дыхания» (Animator) продолжает работать.
/// Добавляется сам из CharacterCode.
/// </summary>
public class HeroJuice : MonoBehaviour
{
    [Tooltip("Жёсткость пружинки (больше — быстрее возвращается)")] public float stiffness = 260f;
    [Tooltip("Затухание (меньше — сильнее «пружинит»)")] public float damping = 13f;
    [Tooltip("Насколько сужается по ширине, когда вытягивается")] [Range(0f, 1f)] public float keepVolume = 0.6f;

    [Header("Тень")]
    public bool shadow = true;
    [Range(0f, 1f)] public float shadowAlpha = 0.32f;
    [Tooltip("Смещение тени от «ног» куба (центр верхней грани клетки)")] public Vector2 shadowOffset = new Vector2(0f, 0.045f);

    CharacterCode _hero;
    SpriteRenderer _main;
    SpriteRenderer _shadow;
    static Sprite _shadowSprite;

    float _s, _v;
    Vector3 _base, _written;
    bool _hasWritten;

    void Awake()
    {
        _hero = GetComponent<CharacterCode>();
        _main = GetComponent<SpriteRenderer>();
    }

    /// <summary>Толчок: &gt;0 — вытянуться вверх, &lt;0 — сплющиться.</summary>
    public void Kick(float amount)
    {
        _s = Mathf.Clamp(amount, -0.45f, 0.45f);
        _v = 0f;
    }

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        _v += (-stiffness * _s - damping * _v) * dt;
        _s += _v * dt;
        if (Mathf.Abs(_s) < 0.0005f && Mathf.Abs(_v) < 0.005f) { _s = 0f; _v = 0f; }

        // Если Animator в этом кадре не писал масштаб, берём прошлую «чистую» базу — без накопления.
        var cur = transform.localScale;
        var b = _hasWritten && cur == _written ? _base : cur;
        _base = b;
        float sy = 1f + _s, sx = 1f - _s * keepVolume;
        _written = new Vector3(b.x * sx, b.y * sy, b.z);
        transform.localScale = _written;
        _hasWritten = true;

        UpdateShadow();
    }

    void UpdateShadow()
    {
        if (!shadow) { if (_shadow != null) _shadow.enabled = false; return; }
        if (_shadow == null) CreateShadow();
        if (_shadow == null) return;

        float air = _hero != null ? _hero.AirHeight : 0f;
        float vis = _hero != null ? _hero.ShadowVisibility : 1f;
        float heroAlpha = _main != null ? _main.color.a : 1f;
        float k = Mathf.Clamp01(air / 0.5f);

        var p = transform.position;
        _shadow.transform.position = new Vector3(p.x + shadowOffset.x, p.y + shadowOffset.y - air, p.z + 0.002f);
        float scale = Mathf.Lerp(1f, 0.55f, k) * (1f - _s * 0.35f);
        _shadow.transform.localScale = new Vector3(scale, scale, 1f);
        var c = IslandDecor.ShadowColor;
        c.a = shadowAlpha * Mathf.Lerp(1f, 0.35f, k) * vis * heroAlpha;
        _shadow.color = c;
        _shadow.enabled = c.a > 0.01f;
        if (_main != null)
        {
            _shadow.sortingLayerID = _main.sortingLayerID;
            _shadow.sortingOrder = _main.sortingOrder - 1;
        }
    }

    void CreateShadow()
    {
        if (_shadowSprite == null) _shadowSprite = WorldArt.ShadowBlob(20, 10);
        var go = new GameObject("HeroShadow");
        go.transform.SetParent(transform.parent, false);
        _shadow = go.AddComponent<SpriteRenderer>();
        _shadow.sprite = _shadowSprite;
        if (WorldArt.Unlit != null) _shadow.sharedMaterial = WorldArt.Unlit;
    }

    void OnDestroy()
    {
        if (_shadow != null) Destroy(_shadow.gameObject);
    }
}
