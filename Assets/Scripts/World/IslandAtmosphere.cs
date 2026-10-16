using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Атмосфера уровня: «мы на летающем острове высоко в небе».
///
///  • Небо — градиент полосами с пиксельным дизерингом и сиянием солнца.
///  • Облака в несколько слоёв с параллаксом: дальние, ближние, море облаков ПОД островом,
///    изредка полупрозрачное облако проплывает перед камерой.
///  • Остров медленно покачивается вверх-вниз (весь GamLand: блоки, куб, кристаллы).
///  • Свет: глобальный свет сцены → холодный рассеянный, сверху слева — тёплое «солнце»
///    с картами нормалей (верх и левая грань светлые, правая в тени).
///  • Пылинки в воздухе.
///
/// Создаётся сам на сцене с картой (MapManager) — в сцену ничего добавлять не нужно.
/// Чтобы крутить значения, можно положить этот компонент на любой объект сцены: тогда используется он.
/// </summary>
[DefaultExecutionOrder(-50)]
public class IslandAtmosphere : MonoBehaviour
{
    public static IslandAtmosphere Instance { get; private set; }

    public enum Look { Morning, Sunset, Night }

    [Header("Вид")]
    public Look look = Look.Morning;
    [Tooltip("Своя палитра: если выключено — палитра берётся из «Вида»")]
    public bool customTheme;
    [Tooltip("Палитра. Заполняется по «Виду» — можно крутить прямо во время игры (включи «Своя палитра», чтобы не сбрасывалась)")]
    public IslandTheme theme;
    Look _lastLook;

    [Header("Остров парит")]
    public bool floating = true;
    [Tooltip("Насколько остров поднимается и опускается (единицы мира; 0.01 = 1 пиксель)")] public float floatAmplitude = 0.02f;
    [Tooltip("Период покачивания, сек")] public float floatPeriod = 5.5f;

    [Header("Облака")]
    [Tooltip("Выключить старые облака сцены (Spawner)")] public bool replaceOldClouds = true;
    public int farClouds = 6;
    public int nearClouds = 4;
    [Tooltip("Скорость ветра (единиц мира в секунду)")] public float windSpeed = 0.025f;
    [Tooltip("Море облаков внизу экрана")] public bool cloudSea = true;
    [Tooltip("Иногда полупрозрачное облако проплывает перед камерой")] public bool foregroundClouds = true;

    [Header("Свет")]
    [Tooltip("Солнце + рассеянный свет + карты нормалей")] public bool lighting = true;

    [Header("Пылинки")]
    public int motes = 16;

    /// <summary>Свет включён (по умолчанию — да, даже если атмосфера ещё не создана).</summary>
    public static bool LightingWanted => Instance == null || Instance.lighting;
    public static IslandTheme CurrentTheme => Instance != null && Instance.theme != null ? Instance.theme : IslandTheme.Morning();

    // ─────────────────────────── запуск ───────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (scene, mode) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (Instance != null) return;
        var existing = FindObjectOfType<IslandAtmosphere>();
        if (existing != null) return;
        if (FindObjectOfType<MapManager>() == null) return;
        new GameObject("IslandAtmosphere").AddComponent<IslandAtmosphere>();
    }

    // ─────────────────────────── состояние ──────────────────────────

    class Drifter
    {
        public SpriteRenderer sr;
        public Vector2 pos;       // в «экранных» единицах при исходном зуме, (0,0) — центр экрана
        public float speed;
        public float halfWidth;
        public float phase;
        public float baseAlpha;
        public float scale = 1f;
    }

    class Layer
    {
        public string name;
        public float follow;      // 1 — стоит на экране, меньше — сильнее уезжает при сдвиге камеры
        public readonly List<Drifter> items = new List<Drifter>();
    }

    Camera _cam;
    Vector3 _camStart;
    float _refViewH = 1.35f;
    bool _built;

    SpriteRenderer _sky;
    readonly List<Layer> _layers = new List<Layer>();
    Layer _far, _near, _seaBack, _seaFront, _front, _motesBack, _motesFront;
    float _nextFront;

    Transform _island;
    Vector3 _islandBase;
    float _floatRamp, _floatTime, _readyCheck;
    bool _floatReady;

    Component _global, _sun;
    Color _globalColor0;
    float _globalIntensity0;

    Material _unlit;
    System.Random _rng;
    readonly List<Sprite> _farSprites = new List<Sprite>();
    readonly List<Sprite> _nearSprites = new List<Sprite>();
    readonly List<Sprite> _fgSprites = new List<Sprite>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (!customTheme || theme == null) theme = Preset(look);
        _lastLook = look;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
    }

    public static IslandTheme Preset(Look l) =>
        l == Look.Sunset ? IslandTheme.Sunset() : l == Look.Night ? IslandTheme.Night() : IslandTheme.Morning();

    /// <summary>Сменить вид прямо в игре (например, по главе).</summary>
    public void SetLook(Look l)
    {
        look = l;
        _lastLook = l;
        customTheme = false;
        theme = Preset(l);
        if (_built) Rebuild();
    }

    void Start()
    {
        _cam = Camera.main;
        if (_cam == null) { enabled = false; return; }
        _camStart = _cam.transform.position;
        _refViewH = _cam.orthographic ? _cam.orthographicSize * 2f : 1.35f;
        _rng = new System.Random(1234);

        _unlit = WorldArt.Unlit;

        if (replaceOldClouds) DisableOldClouds();

        var mm = FindObjectOfType<MapManager>();
        if (mm != null)
        {
            var root = mm.transform.root;
            if (root != null && _cam.transform.root != root) { _island = root; _islandBase = root.position; }
        }

        Rebuild();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void DisableOldClouds()
    {
        foreach (var s in FindObjectsOfType<SpawnCloud>()) s.enabled = false;
        foreach (var c in FindObjectsOfType<Clouds>()) Destroy(c.gameObject);
    }

    void OnValidate()
    {
        if (!customTheme && look != _lastLook) { theme = Preset(look); _lastLook = look; }
        if (Application.isPlaying && _built) Rebuild();
    }

    // ─────────────────────────── постройка ──────────────────────────

    void Rebuild()
    {
        if (theme == null) theme = Preset(look);
        foreach (Transform child in transform) Destroy(child.gameObject);
        _layers.Clear();
        _farSprites.Clear();
        _nearSprites.Clear();
        _fgSprites.Clear();
        _sky = null;

        float viewW = _refViewH * Mathf.Max(1f, _cam.aspect);
        var th = theme;

        // Небо
        int skyH = Mathf.Max(64, Mathf.RoundToInt(_refViewH * WorldArt.PPU));
        int skyW = Mathf.Max(64, Mathf.RoundToInt(skyH * Mathf.Max(1f, _cam.aspect)));
        _sky = MakeRenderer("Sky", WorldArt.Sky(th, skyW, skyH, 7), -32000);
        _cam.backgroundColor = th.skyHorizon;

        // Дальние облака: маленькие, бледные, медленные
        _far = AddLayer("FarClouds", 0.96f);
        for (int i = 0; i < farClouds; i++)
        {
            var sr = MakeRenderer("FarCloud", FarCloudSprite(), -31000);
            var d = AddDrifter(_far, sr, windSpeed * (0.30f + 0.15f * Rand()), 1f);
            d.pos = new Vector2(Rand(-0.6f, 0.6f) * viewW, Rand(-0.12f, 0.42f) * _refViewH);
        }

        // Ближние облака
        _near = AddLayer("NearClouds", 0.88f);
        for (int i = 0; i < nearClouds; i++)
        {
            var sr = MakeRenderer("NearCloud", NearCloudSprite(), -30000);
            var d = AddDrifter(_near, sr, windSpeed * (0.8f + 0.4f * Rand()), 1f);
            d.pos = new Vector2(Rand(-0.6f, 0.6f) * viewW, Rand(-0.30f, 0.36f) * _refViewH);
        }

        // Море облаков под островом: две полосы, задняя бледнее и медленнее
        if (cloudSea)
        {
            _seaBack = AddLayer("CloudSeaBack", 0.9f);
            AddSea(_seaBack, 11, Tint(th.cloudLight, 0.25f), Tint(th.cloudBase, 0.3f), Tint(th.cloudShadow, 0.3f), Tint(th.cloudSeaDeep, 0.3f),
                   -29000, -0.5f + 0.34f, windSpeed * 0.55f, viewW);
            _seaFront = AddLayer("CloudSeaFront", 0.78f);
            AddSea(_seaFront, 23, th.cloudLight, th.cloudBase, th.cloudShadow, th.cloudSeaDeep,
                   -28900, -0.5f + 0.2f, windSpeed * 1.25f, viewW);
        }

        // Облако перед камерой (одно, изредка)
        _front = AddLayer("ForegroundCloud", 0.45f);
        _nextFront = Time.time + 8f + Rand() * 10f;

        // Пылинки: половина позади острова, половина спереди
        var pixel = WorldArt.Pixel();
        _motesBack = AddLayer("MotesBack", 0.9f);
        _motesFront = AddLayer("MotesFront", 0.7f);
        for (int i = 0; i < motes; i++)
        {
            bool front = i % 2 == 1;
            var sr = MakeRenderer("Mote", pixel, front ? 8 : -100);
            var d = AddDrifter(front ? _motesFront : _motesBack, sr, 0f, front ? 0.55f : 0.8f);
            d.pos = new Vector2(Rand(-0.5f, 0.5f) * viewW, Rand(-0.5f, 0.5f) * _refViewH);
            d.speed = 0.006f + Rand() * 0.012f;
            d.phase = Rand() * 10f;
            var c = th.mote; c.a = d.baseAlpha; sr.color = c;
            if (front && Rand() < 0.4f) d.scale = 2f;
        }

        ApplyLight();
        _built = true;
        UpdateLayers();
    }

    Sprite FarCloudSprite()
    {
        if (_farSprites.Count >= 6) return _farSprites[_rng.Next(_farSprites.Count)];
        var th = theme;
        int w = 30 + _rng.Next(28), h = 12 + _rng.Next(6);
        var s = WorldArt.Cloud(_rng.Next(), w, h, Tint(th.cloudLight, 0.45f), Tint(th.cloudBase, 0.5f), Tint(th.cloudShadow, 0.5f));
        _farSprites.Add(s);
        return s;
    }

    Sprite NearCloudSprite()
    {
        if (_nearSprites.Count >= 6) return _nearSprites[_rng.Next(_nearSprites.Count)];
        var th = theme;
        int w = 52 + _rng.Next(40), h = 18 + _rng.Next(10);
        var s = WorldArt.Cloud(_rng.Next(), w, h, Tint(th.cloudLight, 0.1f), Tint(th.cloudBase, 0.15f), Tint(th.cloudShadow, 0.15f));
        _nearSprites.Add(s);
        return s;
    }

    /// <summary>Смешать цвет облака с цветом неба (дальше — бледнее).</summary>
    Color Tint(Color c, float towardSky)
    {
        var sky = Color.Lerp(theme.skyMid, theme.skyHorizon, 0.5f);
        var r = Color.Lerp(c, sky, towardSky);
        r.a = 1f;
        return r;
    }

    void AddSea(Layer layer, int seed, Color light, Color mid, Color shadow, Color deep, int order, float topFrac, float speed, float viewW)
    {
        const int w = 320, h = 72;
        var sprite = WorldArt.CloudSea(seed, w, h, light, mid, shadow, deep);
        float tileW = w / WorldArt.PPU;
        int copies = Mathf.CeilToInt(viewW * 1.6f / tileW) + 1;
        for (int i = 0; i < copies; i++)
        {
            var sr = MakeRenderer(layer.name, sprite, order);
            var d = AddDrifter(layer, sr, speed, 1f);
            d.halfWidth = tileW * 0.5f;
            d.pos = new Vector2(-viewW * 0.8f + i * tileW, topFrac * _refViewH);
            d.phase = -1f; // признак полосы моря: переносится вплотную к соседке
        }
    }

    Layer AddLayer(string name, float follow)
    {
        var l = new Layer { name = name, follow = follow };
        _layers.Add(l);
        return l;
    }

    Drifter AddDrifter(Layer layer, SpriteRenderer sr, float speed, float alpha)
    {
        var d = new Drifter
        {
            sr = sr,
            speed = speed,
            halfWidth = sr.sprite != null ? sr.sprite.bounds.extents.x : 0.3f,
            baseAlpha = alpha,
        };
        layer.items.Add(d);
        return d;
    }

    SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (_unlit != null) sr.sharedMaterial = _unlit;
        sr.sortingLayerID = 0;
        sr.sortingOrder = order;
        return sr;
    }

    float Rand() => (float)_rng.NextDouble();
    float Rand(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

    // ──────────────────────────── свет ─────────────────────────────

    void ApplyLight()
    {
        if (!lighting) return;
        if (_global == null)
        {
            _global = IslandLighting.FindGlobal();
            if (_global == null) return;
        }
        IslandLighting.SetColor(_global, theme.ambient, theme.ambientIntensity);

        if (_sun == null)
        {
            // Далёкий источник сверху слева: направление на него почти одинаковое для всей карты.
            var pos = _camStart + new Vector3(-30f, 30f, 0f);
            // Отдельный объект сцены (не ребёнок атмосферы — Rebuild его не удаляет).
            _sun = IslandLighting.CreatePoint(_global, "IslandSun", null, new Vector3(pos.x, pos.y, 0f),
                theme.sun, theme.sunIntensity, 400f, 420f, 0f, 42f);
        }
        else IslandLighting.SetColor(_sun, theme.sun, theme.sunIntensity);
    }

    // ──────────────────────────── кадр ─────────────────────────────

    void Update()
    {
        if (!_built) return;
        Float();
        Drift(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (_built) UpdateLayers();
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (_built && cam == _cam) UpdateLayers();
    }

    void Float()
    {
        if (_island == null || !floating) return;
        if (!_floatReady)
        {
            if (Time.time < _readyCheck) return;
            _readyCheck = Time.time + 0.25f;
            _floatReady = IntroFinished();
            if (!_floatReady) return;
            _islandBase = _island.position;
        }
        _floatRamp = Mathf.MoveTowards(_floatRamp, 1f, Time.deltaTime / 2.5f);
        _floatTime += Time.deltaTime;
        float y = Mathf.Sin(_floatTime * Mathf.PI * 2f / Mathf.Max(0.5f, floatPeriod)) * floatAmplitude * Mathf.SmoothStep(0f, 1f, _floatRamp);
        _island.position = _islandBase + new Vector3(0f, y, 0f);
    }

    /// <summary>Карта собралась, куб приземлился, блоки доехали — можно качать остров.</summary>
    static bool IntroFinished()
    {
        var mm = MapManager.Instance;
        if (mm == null || mm.data == null || mm.data.FallingMap) return false;
        var hero = FindObjectOfType<CharacterCode>();
        if (hero != null && !hero.IsLanded) return false;
        foreach (var s in FindObjectsOfType<Sine>())
            if (s.enabled) return false;
        return true;
    }

    void Drift(float dt)
    {
        float viewW = _refViewH * Mathf.Max(1f, _cam.aspect);
        float t = Time.time;

        foreach (var layer in _layers)
        {
            bool motesLayer = layer == _motesBack || layer == _motesFront;
            for (int i = 0; i < layer.items.Count; i++)
            {
                var d = layer.items[i];
                if (d.sr == null) continue;
                if (motesLayer)
                {
                    d.pos.y += d.speed * dt;
                    d.pos.x += Mathf.Sin(t * 0.6f + d.phase) * 0.004f * dt - windSpeed * 0.2f * dt;
                    if (d.pos.y > _refViewH * 0.55f) { d.pos.y = -_refViewH * 0.55f; d.pos.x = Rand(-0.5f, 0.5f) * viewW; }
                    if (d.pos.x < -viewW * 0.55f) d.pos.x += viewW * 1.1f;
                    var c = theme.mote;
                    c.a = d.baseAlpha * (0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 1.7f + d.phase * 3f)));
                    d.sr.color = c;
                    continue;
                }

                d.pos.x -= d.speed * dt;
                // у полос моря пивот слева (pos.x — левый край), у облаков — по центру
                float rightEdge = d.phase < 0f ? d.pos.x + d.halfWidth * 2f : d.pos.x + d.halfWidth;
                if (rightEdge < -viewW * 0.5f - 0.1f)
                {
                    if (d.phase < 0f)
                    {
                        // полоса моря: встаёт сразу за самой правой соседкой
                        float maxRight = float.MinValue;
                        foreach (var o in layer.items) if (o != d) maxRight = Mathf.Max(maxRight, o.pos.x);
                        d.pos.x = maxRight + d.halfWidth * 2f - 0.01f;
                    }
                    else if (layer == _front)
                    {
                        d.sr.gameObject.SetActive(false);
                    }
                    else
                    {
                        d.sr.sprite = layer == _far ? FarCloudSprite() : NearCloudSprite();
                        d.halfWidth = d.sr.sprite.bounds.extents.x;
                        d.pos.x = viewW * 0.5f + d.halfWidth + Rand(0f, 0.4f) * viewW;
                        d.pos.y = layer == _far ? Rand(-0.12f, 0.42f) * _refViewH : Rand(-0.30f, 0.36f) * _refViewH;
                    }
                }
            }
        }

        if (foregroundClouds && Time.time >= _nextFront)
        {
            _nextFront = Time.time + 22f + Rand() * 18f;
            SpawnForeground(viewW);
        }
    }

    void SpawnForeground(float viewW)
    {
        Drifter d = null;
        foreach (var o in _front.items) if (o.sr != null && !o.sr.gameObject.activeSelf) { d = o; break; }
        if (d == null)
        {
            if (_front.items.Count >= 2) return;
            var sr = MakeRenderer("ForegroundCloud", null, 9);
            d = AddDrifter(_front, sr, 0f, 0.22f);
        }
        var th = theme;
        if (_fgSprites.Count < 2)
            _fgSprites.Add(WorldArt.Cloud(_rng.Next(), 110 + _rng.Next(40), 36 + _rng.Next(10), th.cloudLight, th.cloudBase, th.cloudShadow));
        d.sr.sprite = _fgSprites[_rng.Next(_fgSprites.Count)];
        d.scale = 1.6f;
        d.halfWidth = d.sr.sprite.bounds.extents.x * 1.6f;
        d.speed = windSpeed * 3.2f;
        d.pos = new Vector2(viewW * 0.5f + d.halfWidth + 0.05f, Rand(-0.45f, 0.1f) * _refViewH);
        var c = Color.white; c.a = d.baseAlpha; d.sr.color = c;
        d.sr.gameObject.SetActive(true);
    }

    /// <summary>Поставить всё, что «висит» на экране, относительно камеры (параллакс и зум).</summary>
    void UpdateLayers()
    {
        if (_cam == null) return;
        Vector3 cam = _cam.transform.position;
        float viewH = _cam.orthographic ? _cam.orthographicSize * 2f : _refViewH;
        float zoom = viewH / Mathf.Max(0.01f, _refViewH);
        float viewW = viewH * _cam.aspect;
        float z = cam.z + Mathf.Max(1f, _cam.nearClipPlane + 5f);
        Vector2 pan = new Vector2(cam.x - _camStart.x, cam.y - _camStart.y);

        if (_sky != null && _sky.sprite != null)
        {
            var size = _sky.sprite.bounds.size;
            _sky.transform.position = new Vector3(cam.x, cam.y, z + 1f);
            _sky.transform.localScale = new Vector3(viewW / size.x, viewH / size.y, 1f);
        }

        foreach (var layer in _layers)
        {
            Vector2 parallax = -pan * (1f - layer.follow);
            foreach (var d in layer.items)
            {
                if (d.sr == null) continue;
                var p = new Vector2(cam.x, cam.y) + d.pos * zoom + parallax;
                d.sr.transform.position = new Vector3(p.x, p.y, z);
                d.sr.transform.localScale = Vector3.one * d.scale * zoom;
            }
        }
    }
}
