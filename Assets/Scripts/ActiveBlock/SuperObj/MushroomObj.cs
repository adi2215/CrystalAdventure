using UnityEngine;

/// <summary>
/// Гриб на клетке: в покое «дышит» и покачивается, на прыжок героя пружинит шляпкой.
///
/// Покой: мягкое желе (вытягивается вверх — чуть худеет, приседает — чуть толстеет) и наклон
/// туда-сюда. У каждого гриба своя фаза — на поле они качаются вразнобой.
/// Удар (Bounce): пружина с затуханием — гриб сплющивается, распрямляется с перелётом
/// и затихает («боинг»). CharacterCode зовёт Bounce, когда герой приземляется на гриб
/// и когда отталкивается с него, а по CapDelta куб опускается и поднимается вместе со шляпкой.
///
/// Спрайт гриба с опорной точкой внизу по центру (mushroom.png, pivot 0.5, 0), поэтому
/// масштаб и наклон идут «от земли» — гриб не отрывается от клетки.
/// Покой — плавный (без округления до пикселя: по пикселю «дыхание» шло рывками).
///
/// Порядок отрисовки: на обычных уровнях у всех клеток один sortingOrder, а куб всегда «клетка + 2»,
/// гриб — 1, поэтому куб, стоящий ЗА грибом, рисовался перед ним. Теперь гриб, который стоит ближе
/// к камере, чем куб (меньше x + y в изометрии — и клетки куба, и клетки, куда он идёт), поднимается
/// над кубом (TopSortingOrder + 1); иначе — свой порядок из префаба. Остальные объекты не трогаются.
///
/// Исправлено: раньше originalScale никогда не задавался (= 0,0,0) и Squash() сжимал гриб в точку;
/// сам Squash() при этом нигде не вызывался — гриб стоял неподвижно.
/// </summary>
public class MushroomObj : MonoBehaviour
{
    // Старые поля префаба — оставлены, чтобы не терять сохранённые значения.
    public float bounceForce = 2f;
    public float bounceDelay = 0.5f;
    public float topOffset = 0.1f;
    public float squashAmount = 0.3f;
    public float squashDuration = 0.2f;

    [Header("Покой")]
    [Tooltip("Период «дыхания», сек")] public float idlePeriod = 2.2f;
    [Tooltip("Насколько вытягивается вверх в покое (доля высоты)")] public float idleStretch = 0.07f;
    [Tooltip("Наклон туда-сюда, градусы")] public float idleSway = 3f;

    [Header("Пружина")]
    [Tooltip("Жёсткость: больше — быстрее «боинг»")] public float springStiffness = 260f;
    [Tooltip("Затухание: больше — меньше колебаний")] public float springDamping = 11f;
    [Tooltip("Ширина при сплющивании относительно высоты")] public float squashWiden = 0.8f;

    private bool objAttached = false;
    private OverlayTile _tile;
    private SpriteRenderer _sr;
    private int _baseOrder;
    private static CharacterCode _hero;
    private Vector3 originalScale;
    private float _phase;
    private float _spring;     // > 0 — сплющен, < 0 — вытянут
    private float _springVel;

    /// <summary>Насколько шляпка сейчас выше (+) или ниже (−) своего обычного места, в мире.</summary>
    public float CapDelta { get; private set; }

    private void Awake()
    {
        originalScale = transform.localScale;
        _phase = Random.value * Mathf.PI * 2f;
        var sr = GetComponent<SpriteRenderer>();
        _sr = sr;
        if (sr != null) _baseOrder = sr.sortingOrder;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (objAttached || !other.CompareTag("Cube")) return;
        var tile = other.GetComponent<OverlayTile>();
        if (tile == null) return;
        tile.attachedObject = gameObject;
        tile.block = SuperBlocks.Mushroom;
        objAttached = true;
        _tile = tile;
    }

    public Vector3 GetTopPosition()
    {
        return transform.position + Vector3.up * topOffset;
    }

    /// <summary>Прежний вызов «сплющиться» — теперь это удар пружины.</summary>
    public void Squash() => Bounce(1f);

    /// <summary>
    /// Толкнуть пружину: 1 — герой приземлился с прыжка, поменьше — отталкивается или
    /// мягко подскакивает. Удары складываются.
    /// </summary>
    public void Bounce(float strength)
    {
        _springVel += squashAmount * 14f * strength;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

        // Пружина (полунеявный Эйлер — устойчив при любом шаге кадра).
        _springVel += (-springStiffness * _spring - springDamping * _springVel) * dt;
        _spring += _springVel * dt;
        if (Mathf.Abs(_spring) < 0.0005f && Mathf.Abs(_springVel) < 0.005f) { _spring = 0f; _springVel = 0f; }

        // Покой: дыхание и наклон. Пока пружина работает, покой стихает — не спорит с ударом.
        float calm = 1f - Mathf.Clamp01(Mathf.Abs(_spring) * 6f);
        float t = Time.time * Mathf.PI * 2f / Mathf.Max(0.2f, idlePeriod) + _phase;
        float breath = Mathf.Sin(t) * idleStretch * calm;
        float sway = Mathf.Sin(t * 0.5f) * idleSway * calm;

        float sy = 1f + breath - _spring;
        float sx = 1f - breath * 0.6f + _spring * squashWiden;
        sy = Mathf.Max(0.4f, sy);
        sx = Mathf.Max(0.4f, sx);

        transform.localScale = new Vector3(originalScale.x * sx, originalScale.y * sy, originalScale.z);
        transform.localRotation = Quaternion.Euler(0f, 0f, sway);
        // Куб стоит на высоте topOffset от низа гриба — она и сжимается вместе со спрайтом.
        CapDelta = (sy - 1f) * topOffset;
    }

    private void LateUpdate()
    {
        if (_sr == null || _tile == null) return;
        if (_hero == null) _hero = FindObjectOfType<CharacterCode>();
        if (_hero == null) return;

        int mine = _tile.gridLocation.x + _tile.gridLocation.y;
        bool inFront = InFrontOf(_hero.activeTile, mine) && InFrontOf(_hero.MoveTarget, mine, ifNone: true);
        int want = inFront ? Mathf.Max(_baseOrder, _hero.TopSortingOrder + 1) : _baseOrder;
        if (_sr.sortingOrder != want) _sr.sortingOrder = want;
    }

    /// <summary>Гриб ближе к камере, чем клетка (меньше x + y). Нет клетки — ifNone.</summary>
    static bool InFrontOf(OverlayTile tile, int mushroomDepth, bool ifNone = false)
    {
        if (tile == null) return ifNone;
        return mushroomDepth < tile.gridLocation.x + tile.gridLocation.y;
    }

    private void OnDisable()
    {
        if (originalScale != Vector3.zero) transform.localScale = originalScale;
        transform.localRotation = Quaternion.identity;
        _spring = _springVel = 0f;
        CapDelta = 0f;
    }
}
