using System.Collections;
using UnityEngine;

/// <summary>
/// Живой кристалл: парит и покачивается над клеткой, светится (ореол + свет на соседние блоки),
/// по нему пробегают искорки-блики, под ним мягкая тень. При сборе — вспышка, кристалл
/// подлетает, увеличивается и тает (PlayCollect).
///
/// Картинку рисует дочерний «Visual» — сам объект кристалла (коллайдер, логика) не двигается.
/// Добавляется сам из ActiveBlockManager.
/// </summary>
public class CrystalJuice : MonoBehaviour
{
    [Header("Парит")]
    [Tooltip("Насколько кристалл приподнят над клеткой")] public float lift = 0.015f;
    public float bobAmplitude = 0.018f;
    public float bobPeriod = 2.2f;

    [Header("Свечение")]
    public bool glow = true;
    [Tooltip("Свет на соседние блоки")] public bool castLight = true;
    public float lightRadius = 0.5f;

    [Header("Блики")]
    [Tooltip("Раз в сколько секунд пробегает искорка")] public float twinkleEvery = 1.3f;

    const float CenterHeight = 0.08f; // середина картинки кристалла над его пивотом (снизу)

    SpriteRenderer _src, _vis, _glow, _shadow, _twinkle;
    Component _light;
    float _phase, _nextTwinkle, _twinkleT = -1f, _lightBase;
    Vector3 _twinkleLocal;
    static Sprite _glowSprite, _shadowSprite;

    public static void Attach(GameObject crystal)
    {
        if (crystal != null && crystal.GetComponent<CrystalJuice>() == null) crystal.AddComponent<CrystalJuice>();
    }

    /// <summary>Где сейчас нарисован кристалл (середина картинки).</summary>
    public Vector3 VisualCenter => _vis != null ? _vis.transform.position + Vector3.up * CenterHeight : transform.position + Vector3.up * CenterHeight;
    public Sprite VisualSprite => _vis != null ? _vis.sprite : (_src != null ? _src.sprite : null);

    void Start()
    {
        _src = GetComponent<SpriteRenderer>();
        if (_src == null) { enabled = false; return; }
        _phase = transform.position.x * 3.1f + transform.position.y * 1.7f;
        int layer = _src.sortingLayerID;
        int order = Mathf.Max(_src.sortingOrder, 2);

        _vis = MakeChild("Visual", _src.sprite, _src.sharedMaterial, layer, order);
        _vis.color = _src.color;
        _vis.flipX = _src.flipX;
        _src.enabled = false;

        if (_shadowSprite == null) _shadowSprite = WorldArt.ShadowBlob(14, 7);
        _shadow = MakeChild("Shadow", _shadowSprite, WorldArt.Unlit, layer, 1);

        var theme = IslandAtmosphere.CurrentTheme;
        if (glow)
        {
            if (_glowSprite == null) _glowSprite = WorldArt.Glow(30);
            _glow = MakeChild("Glow", _glowSprite, WorldArt.Unlit, layer, order - 1);
            var c = theme.crystalLight; c.a = 0.5f; _glow.color = c;
        }

        _twinkle = MakeChild("Twinkle", PixelFX.Sparkle, WorldArt.Unlit, layer, order + 1);
        _twinkle.enabled = false;
        _nextTwinkle = Time.time + 0.5f + Mathf.Repeat(_phase, 1f) * twinkleEvery;

        if (castLight && IslandAtmosphere.LightingWanted)
        {
            _lightBase = theme.crystalLightIntensity;
            _light = IslandLighting.CreatePoint(IslandLighting.FindGlobal(), "CrystalLight", transform,
                transform.position + Vector3.up * CenterHeight, theme.crystalLight, _lightBase, 0.05f, lightRadius, 0.55f, 0f);
        }
    }

    SpriteRenderer MakeChild(string name, Sprite sprite, Material mat, int layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat != null) sr.sharedMaterial = mat;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        if (_vis == null) return;
        float t = Time.time;
        float inv = 1f / Mathf.Max(0.01f, transform.lossyScale.y);
        float bob = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.3f, bobPeriod) + _phase) * bobAmplitude;
        float height = lift + bobAmplitude + bob;                 // над пивотом, в мире
        _vis.transform.localPosition = new Vector3(0f, height * inv, -0.001f);

        float k = (bob + bobAmplitude) / Mathf.Max(0.0001f, 2f * bobAmplitude); // 0 — внизу, 1 — вверху
        _shadow.transform.localPosition = new Vector3(0f, 0.012f * inv, 0.001f);
        _shadow.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, k);
        var sc = IslandDecor.ShadowColor; sc.a = Mathf.Lerp(0.30f, 0.18f, k); _shadow.color = sc;

        if (_glow != null)
        {
            _glow.transform.localPosition = new Vector3(0f, (height + CenterHeight) * inv, 0f);
            _glow.transform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * 2.6f + _phase));
            var c = _glow.color; c.a = 0.42f + 0.14f * Mathf.Sin(t * 2.6f + _phase); _glow.color = c;
        }

        if (_light != null)
            IslandLighting.SetColor(_light, IslandAtmosphere.CurrentTheme.crystalLight, _lightBase * (0.9f + 0.1f * Mathf.Sin(t * 2.6f + _phase)));

        Twinkle(t, height, inv);
    }

    void Twinkle(float t, float height, float inv)
    {
        if (_twinkleT < 0f && t >= _nextTwinkle)
        {
            _twinkleT = 0f;
            _nextTwinkle = t + twinkleEvery * (0.6f + 0.8f * Mathf.Repeat(t * 7.13f, 1f));
            _twinkleLocal = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0.04f, 0.13f), -0.002f);
        }
        if (_twinkleT < 0f) return;
        _twinkleT += Time.deltaTime / 0.4f;
        if (_twinkleT >= 1f) { _twinkleT = -1f; _twinkle.enabled = false; return; }
        float s = Mathf.Sin(_twinkleT * Mathf.PI);
        _twinkle.enabled = true;
        _twinkle.transform.localPosition = new Vector3(_twinkleLocal.x * inv, (height + _twinkleLocal.y) * inv, _twinkleLocal.z);
        _twinkle.transform.localScale = Vector3.one * (0.4f + 0.7f * s) * inv;
        _twinkle.transform.localRotation = Quaternion.Euler(0f, 0f, _twinkleT * 45f);
        _twinkle.color = new Color(1f, 1f, 1f, s);
    }

    // ───────────────────────── сбор ─────────────────────────

    /// <summary>Вспышка при сборе: кристалл подлетает, растёт и тает, от него расходится кольцо света.</summary>
    public static void PlayCollect(Vector3 center, Sprite sprite, int layer, int order)
    {
        if (sprite == null) return;
        var go = new GameObject("CrystalCollect");
        go.transform.position = center;
        var fx = go.AddComponent<CrystalCollectFX>();
        fx.Play(sprite, layer, order);
    }
}
