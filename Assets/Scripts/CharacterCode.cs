using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Куб-герой. Выполняет очередь команд, которую ему передаёт MovingManager.
///
/// Раньше движение жило в LateUpdate и само вытаскивало команды из списков,
/// поэтому Rewind не мог его по-настоящему остановить. Теперь вся программа —
/// одна корутина: StopRun() обрывает её в любой момент, StopAndReset() ставит куб на старт.
/// </summary>
public class CharacterCode : MonoBehaviour
{
    [SerializeField] private AnimationCurve _curve;

    public OverlayTile activeTile;

    private OverlayTile _tile;
    private OverlayTile startTile;

    private SpriteRenderer order;
    private Rigidbody2D rb;

    public float speed;
    public MovingManager List;
    public bool star1 = false;

    public Data tran;

    public Button button;

    public Vector3 targetPosition;
    public GameObject finishLevel;

    public float speedFalling;

    [Header("Куда смотрит куб")]
    [Tooltip("Необязательно: спрайт куба СПИНОЙ (смотрит вправо-вверх). Если пусто — для шагов вверх используется обычный спрайт, только зеркально по сторонам.")]
    public Sprite backSprite;
    [Tooltip("Повернуть лицо куба в сторону шага (вправо/влево по экрану)")]
    public bool faceMoveDirection = true;

    private Sprite _frontSprite;
    private SpriteRenderer[] _renderers;
    private Color[] _baseColors; // исходные цвета куба и его детей (тень и т.п.) — для вспышек урона и Rewind

    [Header("Край: «чуть не упал»")]
    [Tooltip("Насколько куб выходит к краю клетки (доля шага)")] [Range(0.1f, 0.6f)] public float edgeReach = 0.42f;
    [Tooltip("Сколько секунд куб балансирует на краю")] public float edgeTeeterTime = 0.7f;
    [Tooltip("Максимальный наклон при балансировании, градусы")] public float edgeTiltAngle = 14f;
    [Tooltip("Восклицательный знак, камешки, тряска камеры")] public bool edgeEffects = true;
    [Tooltip("Звук «ой!» на краю (необязательно)")] public AudioClip edgeSound;

    [Header("Прыжки")]
    [Tooltip("Начало уровня: куб запрыгивает на свою клетку")] public float jumpInTime = 0.7f;
    public float jumpInHeight = 0.55f;
    [Tooltip("Откуда запрыгивает, если стоит слишком близко к старту")] public Vector3 jumpInOffset = new Vector3(-0.5f, 0.35f, 0f);
    [Tooltip("Подпрыгнуть от радости, когда взял кристалл")] public float collectHopHeight = 0.2f;
    public float collectHopTime = 0.35f;
    [Tooltip("Сколько раз подпрыгнуть после последнего кристалла")] public int victoryHops = 2;

    [Header("Шаг: маленький прыжок с клетки на клетку")]
    [Tooltip("Минимальное время шага, сек")] public float stepTime = 0.2f;
    [Tooltip("Высота прыжка на шаге (0.01 = 1 пиксель)")] public float stepHop = 0.045f;
    [Tooltip("Присесть перед шагом, сек")] public float stepAnticipation = 0.04f;
    [Tooltip("Пыль из-под куба на каждом шаге")] public bool stepDust = true;

    [Header("Паузы (сек)")]
    public float pauseBetweenSteps = 0.1f;
    public float bumpTime = 0.2f;
    public float jumpTime = 0.5f;
    public float jumpHeight = 0.3f;
    public float waitOnMushroom = 0.3f;
    [Tooltip("Подскок куба на шляпке гриба после приземления")] public float mushroomHop = 0.1f;

    private Coroutine _run;
    private Coroutine _celebrate;
    private int _runToken;
    private bool _landed;
    private bool _introStarted;
    private bool _collectedNow;

    /// <summary>Высота куба над клеткой (прыжки) — по ней тень под кубом уменьшается.</summary>
    public float AirHeight { get; private set; }
    /// <summary>Видимость тени 0..1 (пока куб не запрыгнул на поле — тени нет).</summary>
    public float ShadowVisibility { get; private set; }

    private HeroJuice _juice;

    /// <summary>Сортировка куба над клеткой (тень — на 1 ниже).</summary>
    const int OrderAboveTile = 2;

    Vector3 ToLocal(Vector3 world) => transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;
    Vector3 ToWorld(Vector3 local) => transform.parent != null ? transform.parent.TransformPoint(local) : local;

    /// <summary>Куб приземлился, карта собрана, стартовая клетка известна — можно запускать.</summary>
    public bool IsReady => _landed && tran != null && !tran.FallingMap && _tile != null;

    public bool IsRunning => _run != null;

    public OverlayTile StartTile => startTile;
    /// <summary>Клетка, куда куб сейчас идёт / прыгает / скользит (null — стоит). Нужна грибу (MushroomObj): рисоваться ли поверх куба.</summary>
    public OverlayTile MoveTarget { get; private set; }

    /// <summary>Самый верхний sortingOrder куба вместе с дочерними спрайтами (Ela на голове и т. п.).</summary>
    public int TopSortingOrder
    {
        get
        {
            int top = order != null ? order.sortingOrder : 0;
            if (_renderers != null)
                foreach (var r in _renderers) if (r != null && r.enabled && r.sortingOrder > top) top = r.sortingOrder;
            return top;
        }
    }
    public SpriteRenderer MainRenderer => order;
    /// <summary>Спрайт «лицом к камере» (копии куба — враги и боссы — берут его, EnemyLayer).</summary>
    public Sprite FrontSprite => _frontSprite;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        order = GetComponent<SpriteRenderer>();
        if (order != null) _frontSprite = order.sprite;
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
        _baseOrders = new int[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseOrders[i] = _renderers[i] != null ? _renderers[i].sortingOrder : 0;
        _juice = GetComponent<HeroJuice>();
        if (_juice == null) _juice = gameObject.AddComponent<HeroJuice>();
    }

    void Squash(float amount)
    {
        if (_juice != null) _juice.Kick(amount);
    }

    // ============================================================
    //                    КУДА СМОТРИТ КУБ
    // ============================================================

    /// <summary>
    /// Изометрия: Forward (+Y) — влево-вверх, Right (+X) — вправо-вверх,
    /// Left (−X) — влево-вниз, Bottom (−Y) — вправо-вниз (так нарисован спрайт).
    /// </summary>
    public static bool FacesScreenLeft(ItemType t) => t == ItemType.Forward || t == ItemType.Left;
    public static bool FacesUp(ItemType t) => t == ItemType.Forward || t == ItemType.Right;

    public void Face(ItemType direction)
    {
        if (!faceMoveDirection || WayTiles.Direction(direction) == Vector3Int.zero) return;
        ApplyFacing(_renderers, order, _frontSprite, backSprite, direction);
    }

    public void FaceDefault() => Face(ItemType.Bottom);

    /// <summary>Общий код поворота — им же пользуется «призрак» подсказки.</summary>
    public static void ApplyFacing(SpriteRenderer[] renderers, SpriteRenderer main, Sprite front, Sprite back, ItemType direction)
    {
        bool left = FacesScreenLeft(direction);
        if (renderers != null)
            foreach (var r in renderers)
                if (r != null) r.flipX = left;
        if (main != null && front != null)
            main.sprite = FacesUp(direction) && back != null ? back : front;
    }

    void Update()
    {
        if (tran == null || !tran.FallingCube || _introStarted) return;
        // Вступление уровня: когда карта опустилась, куб запрыгивает на свою клетку.
        if (tran.FallingMap) return;
        _introStarted = true;
        StartCoroutine(JumpIn());
    }

    IEnumerator JumpIn()
    {
        var tile = FindNearestTile(targetPosition);
        Vector3 end = tile != null ? TopOf(tile) : targetPosition;
        Vector3 start = transform.position;
        if ((new Vector2(start.x, start.y) - new Vector2(end.x, end.y)).sqrMagnitude < 0.09f) start = end + jumpInOffset;
        float height = jumpInHeight + Vector3.Distance(start, end) * 0.15f;

        if (tile != null) ApplyOrder(tile);

        PixelSfx.Play(Sfx.Jump, 0.7f, 0.9f);
        Squash(0.25f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, jumpInTime))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            float h = 4f * height * t * (1f - t);           // дуга по времени — «подлёт» и падение
            transform.position = Vector3.Lerp(start, end, k) + Vector3.up * h;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 0f, k) * (start.x < end.x ? 1f : -1f));
            AirHeight = h + (1f - k) * Mathf.Max(0f, start.y - end.y);
            ShadowVisibility = Mathf.SmoothStep(0f, 1f, (k - 0.35f) / 0.65f);
            yield return null;
        }
        transform.position = end;
        transform.rotation = Quaternion.identity;
        AirHeight = 0f;
        ShadowVisibility = 1f;
        Squash(-0.32f);

        // Приземление: пыль из-под куба и лёгкое вздрагивание.
        PixelSfx.Play(Sfx.Land, 0.9f);
        if (order != null)
            PixelFX.Burst(end + Vector3.down * 0.03f, PixelFX.DustColors, 8, 0.45f, 0.1f, 1.2f, order.sortingLayerID, order.sortingOrder + 1, PixelFX.Dot, 1.2f, 0.5f);

        tran.FallingCube = false;
        OnLanded();
    }

    void OnLanded()
    {
        if (_landed) return;
        _landed = true;
        if (startTile == null) startTile = FindNearestTile(targetPosition);
        if (startTile != null) PositionCharacterOnLine(startTile, collect: false);
    }

    /// <summary>Стартовая клетка = ближайшая к точке старта уровня (не «первая, которой коснулись при падении»).</summary>
    OverlayTile FindNearestTile(Vector3 worldPos)
    {
        var mm = MapManager.Instance;
        if (mm == null || mm.map == null) return null;
        OverlayTile best = null;
        float bestDist = float.MaxValue;
        foreach (var tile in mm.map.Values)
        {
            if (tile == null) continue;
            var p = tile.transform.position;
            float d = (new Vector2(p.x, p.y) - new Vector2(worldPos.x, worldPos.y)).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = tile; }
        }
        return best;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Запасной вариант, если карта ещё не построена к моменту приземления.
        if (startTile != null || !other.CompareTag("Cube")) return;
        if (tran != null && tran.FallingCube) return;
        startTile = other.GetComponent<OverlayTile>();
        _tile = startTile;
    }

    public void TransitionScene()
    {
        if (finishLevel != null) finishLevel.SetActive(true);
    }

    // ============================================================
    //                    ВЫПОЛНЕНИЕ ПРОГРАММЫ
    // ============================================================

    /// <param name="onStep">перед каждым шагом (номер шага) — подсветить карточку</param>
    /// <param name="onEdge">куб упёрся в край — «чуть не упал»</param>
    public void RunCommands(List<Item> queue, Action onDone, Action<int> onStep = null, Action<int> onEdge = null)
        => RunCommands(queue, onDone, onStep, onEdge, null, null);

    /// <param name="canRun">условие шага (цветная клетка); false — шаг пропускается</param>
    /// <param name="onSkip">шаг пропущен</param>
    public void RunCommands(List<Item> queue, Action onDone, Action<int> onStep, Action<int> onEdge,
                            Func<int, OverlayTile, bool> canRun, Action<int> onSkip)
    {
        StopRun();
        if (button != null) button.enabled = true;
        _run = StartCoroutine(RunRoutine(queue, onDone, onStep, onEdge, canRun, onSkip));
    }

    /// <summary>
    /// То же, но КУДА идти, уже решило новое ядро (MovingManager.useCrystalCore → CoreRunBridge):
    /// шаг i плана = шаг i очереди queue. Анимации, кристаллы и победа — прежние.
    /// </summary>
    public void RunPlan(List<Item> queue, IReadOnlyList<CrystalAdventure.UnityLayer.CorePlannedStep> plan, Action onDone,
                        Action<int> onStep, Action<int> onEdge, Action<int> onSkip)
    {
        StopRun();
        if (button != null) button.enabled = true;
        _run = StartCoroutine(RunRoutine(queue, onDone, onStep, onEdge, null, onSkip, plan));
    }

    [Tooltip("Пауза, когда карточка пропущена (условие не выполнено), сек")]
    public float skipPause = 0.22f;

    /// <summary>Оборвать выполнение в любой момент (анимация шага, прыжок, радость — всё, кроме вступления).</summary>
    public void StopRun()
    {
        _runToken++;
        if (_run != null) StopCoroutine(_run);
        if (_celebrate != null) StopCoroutine(_celebrate);
        _run = null;
        _celebrate = null;
        _collectedNow = false;
        AirHeight = 0f;
    }

    public void StopAndReset()
    {
        if (!_landed) return; // ещё запрыгивает на поле — сбрасывать нечего
        StopRun();
        RestoreLook();
        transform.rotation = Quaternion.identity; // мог остаться наклон после края
        MoveToStartTile();
        FaceDefault();
    }

    public void MoveToStartTile()
    {
        if (startTile == null) startTile = FindNearestTile(targetPosition);
        if (startTile == null) return;
        PositionCharacterOnLine(startTile, collect: false);
    }

    /// <param name="plan">null — старый путь (WayTiles решает сам); иначе шаги, посчитанные ядром.</param>
    IEnumerator RunRoutine(List<Item> queue, Action onDone, Action<int> onStep, Action<int> onEdge,
                           Func<int, OverlayTile, bool> canRun = null, Action<int> onSkip = null,
                           IReadOnlyList<CrystalAdventure.UnityLayer.CorePlannedStep> plan = null)
    {
        var map = MapManager.Instance != null ? MapManager.Instance.map : null;
        int token = _runToken;
        int count = plan != null ? plan.Count : queue.Count;

        for (int i = 0; i < count; i++)
        {
            if (token != _runToken) yield break;
            var cmd = i < queue.Count ? queue[i] : null;
            bool skipped = plan != null ? plan[i].Skipped : canRun != null && !canRun(i, _tile);
            if (skipped)
            {
                // Условие не выполнено (не тот цвет клетки) — карточка пропускается.
                onSkip?.Invoke(i);
                PixelSfx.Play(Sfx.Blip, 0.35f, 0.7f, 0.05f);
                if (skipPause > 0f) yield return new WaitForSeconds(skipPause);
                continue;
            }
            if (cmd != null) Face(cmd.type);
            onStep?.Invoke(i);
            bool core = plan != null;
            var planned = core ? plan[i] : default;
            EnemyLayer.BeginTurn(i); // враги шагают одновременно с кубом
            if (_shieldFx != null) _shieldFx.SetActive(false);

            var step = core ? PlannedStep(planned, map) : WayTiles.Resolve(_tile, cmd, map);
            var leftTile = _tile;

            if (core && !planned.IsMove)
            {
                // Не ход: удар, супер-удар, щит, ожидание (уровни с врагами).
                yield return DoAction(planned);
            }
            else if (core && planned.Blocked)
            {
                // Упёрся во врага (или стену) — это не край: толкнулся и остался, «чуть не упал» не считается.
                yield return Bump(new Vector3Int(planned.Facing.x, planned.Facing.y, 0));
            }
            else if (core && planned.HeroDied && !planned.Moved)
            {
                // Коснулся врага в этом шаге — ядро шаг не засчитало. Это не край: рывок к врагу, дальше телепорт.
                yield return Bump(cmd != null ? WayTiles.Direction(cmd.type) : Vector3Int.zero);
            }
            else if (step.target == null)
            {
                // Дальше пусто — куб доходит до края, балансирует и возвращается. Ход потрачен.
                onEdge?.Invoke(i);
                yield return EdgeTeeter(cmd != null ? WayTiles.Direction(cmd.type) : Vector3Int.zero);
            }
            else if (step.slid && step.via != null)
            {
                // Лёд: встал на первую ледяную клетку (шагом или прыжком с гриба) и скользит дальше.
                if (step.fromMushroom) yield return JumpToTile(step.via);
                else yield return MoveToTile(step.via);
                if (token != _runToken) yield break;
                if (SpecialTiles.IsCrumbling(leftTile)) SpecialTiles.Crumble(leftTile);
                yield return SlideToTile(step.target);
                if (step.onMushroom) yield return BounceOnMushroom(step.target);
            }
            else if (step.fromMushroom || step.onMushroom)
            {
                yield return JumpToTile(step.target);
                if (step.onMushroom) yield return BounceOnMushroom(step.target);
            }
            else
            {
                yield return MoveToTile(step.target);
            }
            if (step.target != null && SpecialTiles.IsCrumbling(step.target)) SpecialTiles.Tremble(step.target); // плита дрожит под кубом
            if (token != _runToken) yield break; // победа или Rewind прямо на этом шаге

            // Клетки неба: после шага сдуло ветром или перенёс телепорт.
            if (core && planned.Special != CrystalAdventure.UnityLayer.SpecialMove.None)
            {
                yield return SpecialMove(planned, map);
                if (token != _runToken) yield break;
            }

            // Ушёл с рассыпающейся плиты — она падает (Winter). Повторный вызов безопасен.
            if (step.target != null && leftTile != null && leftTile != _tile && SpecialTiles.IsCrumbling(leftTile))
                SpecialTiles.Crumble(leftTile);

            // Удары врагов, урон, смерти, новые красные клетки — после шага куба.
            yield return EnemyLayer.FinishTurn(i);
            if (core) SpecialTiles.AfterCoreStep(i); // шипы поднимаются / опускаются по ходу мира
            if (token != _runToken) yield break;

            if (_collectedNow)
            {
                _collectedNow = false;
                yield return Hop(collectHopHeight, collectHopTime, 0f); // взял кристалл — подпрыгнул
            }

            if (core && planned.HeroDied)
            {
                // Коснулся врага — телепорт на старт: программа обрывается, уровень сбрасывается (как Rewind).
                yield return Defeat();
                if (token != _runToken) yield break;
                _run = null;
                if (List != null) List.OnHeroDefeated();
                yield break;
            }
            if (core && planned.Won && EnemyLayer.Active && ActiveBlockManager.Instance != null)
            {
                // На уровнях с врагами победу объявляет ядро (кристаллы + босс повержен).
                ActiveBlockManager.Instance.ForceVictory();
                yield break;
            }

            if (pauseBetweenSteps > 0f) yield return new WaitForSeconds(pauseBetweenSteps);
        }

        _run = null;
        onDone?.Invoke();
    }

    // ============================================================
    //              ВРАГИ: УДАР, ЩИТ, УРОН, ПОРАЖЕНИЕ
    // ============================================================

    GameObject _shieldFx;

    /// <summary>Действие без шага (карточки Атака / Супер-атака / Щит): удар — рывок туда, куда смотрит куб.</summary>
    IEnumerator DoAction(CrystalAdventure.UnityLayer.CorePlannedStep planned)
    {
        var dir = new Vector3Int(planned.Facing.x, planned.Facing.y, 0);
        switch (planned.Action)
        {
            case CrystalAdventure.Core.ActionType.Attack:
            case CrystalAdventure.Core.ActionType.SuperAttack:
            {
                bool super = planned.Action == CrystalAdventure.Core.ActionType.SuperAttack;
                Vector3 start = transform.localPosition;
                Vector3 reach = StepVector(dir) * (super ? 0.45f : 0.32f);
                PixelSfx.Play(Sfx.Jump, 0.7f, super ? 0.7f : 1.3f);
                Squash(-0.15f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / (super ? 0.3f : 0.2f))
                {
                    float k = Mathf.Sin(t * Mathf.PI);
                    var p = start + reach * k;
                    transform.localPosition = new Vector3(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f, p.z);
                    yield return null;
                }
                transform.localPosition = start;
                if (order != null)
                    PixelFX.Burst(ToWorld(start + reach) + Vector3.up * 0.08f,
                                  super ? new[] { new Color(1f, 0.85f, 0.3f, 1f), Color.white, new Color(0.6f, 0.9f, 1f, 1f) } : PixelFX.StarColors,
                                  super ? 14 : 6, super ? 0.8f : 0.45f, 0.2f, 1.2f, order.sortingLayerID, order.sortingOrder + 3, PixelFX.Dot, 1f, 0.45f);
                Squash(0.15f);
                break;
            }
            case CrystalAdventure.Core.ActionType.Shield:
            {
                if (_shieldFx == null)
                {
                    var sr = new GameObject("ShieldFx").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(transform, false);
                    sr.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                    sr.sprite = EnemyArt.Bubble;
                    if (order != null) { sr.sortingLayerID = order.sortingLayerID; sr.sortingOrder = order.sortingOrder + 2; }
                    _shieldFx = sr.gameObject;
                }
                if (order != null && _shieldFx.TryGetComponent<SpriteRenderer>(out var shieldSr)) shieldSr.sortingOrder = order.sortingOrder + 2;
                _shieldFx.SetActive(true);
                PixelSfx.Play(Sfx.Pop, 0.6f, 1.2f);
                yield return new WaitForSeconds(0.2f);
                break;
            }
            default:
                yield return new WaitForSeconds(0.2f); // ожидание
                break;
        }
    }

    /// <summary>Куба ударили: вспышка и дрожь (из EnemyLayer).</summary>
    public void PlayHurt() => StartCoroutine(HurtRoutine());

    IEnumerator HurtRoutine()
    {
        // Только вспышка и сжатие: позицию не трогаем — следующий шаг может начаться, пока вспышка гаснет.
        PixelSfx.Play(Sfx.Edge, 0.8f, 1.2f);
        Squash(-0.2f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            if (order != null) order.color = Color.Lerp(new Color(1f, 0.35f, 0.35f, 1f), BaseColorOf(order), t);
            yield return null;
        }
        if (order != null) order.color = BaseColorOf(order);
    }

    static readonly Color[] TeleportColors = { new Color(0.55f, 0.85f, 1f, 1f), new Color(0.85f, 0.95f, 1f, 1f), Color.white };

    /// <summary>
    /// Поражение (коснулся врага): куб вытягивается лучом и исчезает в искрах — телепорт.
    /// Дальше MovingManager.OnHeroDefeated сбрасывает уровень, и куб появляется на старте (PlayTeleportIn).
    /// </summary>
    IEnumerator Defeat()
    {
        PixelSfx.Play(Sfx.Remove, 1f, 0.8f);
        TeleportSparkles(0.6f);
        Squash(0.45f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            SetAlpha(1f - t);
            yield return null;
        }
        SetAlpha(0f);
        yield return new WaitForSeconds(0.15f);
    }

    /// <summary>Появление на старте после поражения: искры и проявление (вызывает MovingManager).</summary>
    public void PlayTeleportIn()
    {
        if (!_landed) return;
        StartCoroutine(TeleportInRoutine());
    }

    IEnumerator TeleportInRoutine()
    {
        PixelSfx.Play(Sfx.Pop, 0.8f, 1.3f);
        TeleportSparkles(0.4f);
        Squash(0.35f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            if (IsRunning) { SetAlpha(1f); yield break; } // уже снова нажали Play
            SetAlpha(t);
            yield return null;
        }
        SetAlpha(1f);
    }

    void TeleportSparkles(float up)
    {
        if (order == null) return;
        PixelFX.Burst(transform.position + Vector3.up * 0.1f, TeleportColors, 14, 0.5f, up, 0.3f,
                      order.sortingLayerID, order.sortingOrder + 5, PixelFX.Dot, 1f, 0.6f);
    }

    /// <summary>Прозрачность всех спрайтов куба (тень — HeroJuice — гаснет сама по альфе тела).</summary>
    void SetAlpha(float a)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) { var c = _baseColors[i]; c.a *= a; _renderers[i].color = c; }
    }

    /// <summary>Вернуть вид после поражения / удара / щита (Rewind).</summary>
    void RestoreLook()
    {
        if (_renderers != null)
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].color = _baseColors[i];
        if (_shieldFx != null) _shieldFx.SetActive(false);
        transform.rotation = Quaternion.identity;
    }

    Color BaseColorOf(SpriteRenderer r)
    {
        int i = System.Array.IndexOf(_renderers, r);
        return i >= 0 ? _baseColors[i] : Color.white;
    }


    /// <summary>Шаг ядра → тот же Step, что дал бы WayTiles (прыжок — если стоим на грибе или встаём на гриб).</summary>
    WayTiles.Step PlannedStep(CrystalAdventure.UnityLayer.CorePlannedStep planned, Dictionary<Vector3Int, OverlayTile> map)
    {
        var step = new WayTiles.Step();
        if (!planned.Moved || map == null) return step;
        // Сдуло ветром / перенёс телепорт: сам шаг — только до клетки, где это случилось (SpecialMove — потом).
        var dest = planned.Special != CrystalAdventure.UnityLayer.SpecialMove.None ? planned.SpecialFrom : planned.To;
        if (_tile != null && dest.x == _tile.gridLocation.x && dest.y == _tile.gridLocation.y) return step;
        if (!map.TryGetValue(new Vector3Int(dest.x, dest.y, 0), out var target) || target == null) return step;
        step.target = target;
        step.fromMushroom = WayTiles.IsMushroom(_tile);
        step.onMushroom = WayTiles.IsMushroom(target);
        if (planned.Slid && map.TryGetValue(new Vector3Int(planned.Via.x, planned.Via.y, 0), out var via) && via != null && via != target)
        {
            step.via = via;
            step.slid = true;
        }
        return step;
    }

    [Header("Лёд (Winter)")]
    [Tooltip("Сколько секунд скользит одна клетка")] public float slideTimePerCell = 0.11f;

    /// <summary>
    /// Скольжение по льду: без прыжка, равномерно, с наклоном вперёд и ледяной крошкой из-под куба.
    /// В конце — как обычный шаг: встал на клетку (кристалл, если он там, берётся).
    /// </summary>
    IEnumerator SlideToTile(OverlayTile target)
    {
        RaiseOrderFor(target);
        Vector3 start = transform.localPosition;
        Vector3 end = ToLocal(TopOf(target));
        float cells = Mathf.Max(1f, Vector2.Distance(start, end) / 0.18f);
        float time = Mathf.Max(0.12f, slideTimePerCell * cells);
        float lean = end.x >= start.x ? -6f : 6f;
        PixelSfx.Play(Sfx.Step, 0.45f, 1.5f);
        float nextChip = 0f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            transform.localPosition = Vector3.Lerp(start, end, t);
            transform.rotation = Quaternion.Euler(0f, 0f, lean * Mathf.Sin(t * Mathf.PI));
            if (t >= nextChip && order != null)
            {
                nextChip += 0.25f;
                PixelFX.Burst(transform.position + Vector3.up * 0.02f, IceChips, 3, 0.2f, 0.05f, 0.9f,
                              order.sortingLayerID, order.sortingOrder + 1, PixelFX.Dot, 0.7f, 0.3f);
            }
            yield return null;
        }
        transform.localPosition = end;
        transform.rotation = Quaternion.identity;
        Squash(-0.12f);
        PositionCharacterOnLine(target, collect: true);
    }

    // ============================================================
    //              КЛЕТКИ НЕБА: ВЕТЕР И ТЕЛЕПОРТ
    // ============================================================

    static readonly Color[] WindStreaks = { new Color(0.9f, 0.97f, 1f, 1f), new Color(0.7f, 0.88f, 1f, 1f), Color.white };

    IEnumerator SpecialMove(CrystalAdventure.UnityLayer.CorePlannedStep planned, Dictionary<Vector3Int, OverlayTile> map)
    {
        if (map == null || !map.TryGetValue(new Vector3Int(planned.To.x, planned.To.y, 0), out var target) || target == null) yield break;
        if (planned.Special == CrystalAdventure.UnityLayer.SpecialMove.Teleported) yield return TeleportTo(target);
        else yield return WindPush(target);
    }

    /// <summary>
    /// Порыв ветра — своя анимация, не похожая на скольжение по льду: поток воздуха подхватывает куб
    /// (присел, вокруг взлетают завитки), подбрасывает вверх, на лету куб делает оборот вокруг себя и
    /// по дуге переносится на соседнюю клетку, где плюхается с облачком воздуха.
    /// Исправлено (по отзыву автора): раньше куб просто ехал по прямой с наклоном — выглядело как лёд.
    /// </summary>
    IEnumerator WindPush(OverlayTile target)
    {
        RaiseOrderFor(target);
        Vector3 start = transform.localPosition;
        Vector3 end = ToLocal(TopOf(target));
        float dir = end.x >= start.x ? -1f : 1f; // крутится «по ветру»
        int layer = order != null ? order.sortingLayerID : 0, sort = order != null ? order.sortingOrder + 1 : 0;

        // 1) Подхватило: присел, из-под куба поднимаются завитки воздуха.
        PixelSfx.Play(Sfx.Whoosh, 0.7f, 1.25f);
        Squash(-0.22f);
        PixelFX.Burst(transform.position, WindStreaks, 8, 0.25f, 0.55f, -0.2f, layer, sort, PixelFX.Dot, 0.8f, 0.45f);
        yield return new WaitForSeconds(0.07f);

        // 2) Подбросило и понесло по дуге, с оборотом в воздухе.
        Squash(0.35f);
        const float time = 0.46f, lift = 0.24f;
        float nextPuff = 0f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            float h = lift * Mathf.Sin(t * Mathf.PI) + 0.012f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t);
            transform.localPosition = Vector3.Lerp(start, end, k) + Vector3.up * h;
            AirHeight = h;
            float spin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.85f, t)) * 360f;
            transform.rotation = Quaternion.Euler(0f, 0f, dir * spin);
            if (t >= nextPuff)
            {
                nextPuff += 0.2f;
                PixelFX.Burst(transform.position, WindStreaks, 3, 0.3f, 0.05f, 0f, layer, sort, PixelFX.Dot, 0.7f, 0.35f);
            }
            yield return null;
        }
        transform.localPosition = end;
        transform.rotation = Quaternion.identity;
        AirHeight = 0f;

        // 3) Плюхнулся: сплющился, облачко воздуха в стороны.
        Squash(-0.3f);
        PixelSfx.Play(Sfx.Land, 0.6f, 1.2f);
        PixelFX.Burst(transform.position + Vector3.up * 0.02f, WindStreaks, 10, 0.5f, 0.04f, 0f, layer, sort, PixelFX.Dot, 0.9f, 0.4f);
        PositionCharacterOnLine(target, collect: true);
    }

    /// <summary>Телепорт: куб растворяется в искрах на одной площадке и проявляется на парной.</summary>
    IEnumerator TeleportTo(OverlayTile target)
    {
        PixelSfx.Play(Sfx.Ghost, 0.8f, 1.3f);
        TeleportSparkles(0.4f);
        Squash(0.35f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f) { SetAlpha(1f - t); yield return null; }
        SetAlpha(0f);
        PositionCharacterOnLine(target, collect: true);
        TeleportSparkles(0.4f);
        Squash(0.3f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f) { SetAlpha(t); yield return null; }
        SetAlpha(1f);
    }

    static readonly Color[] IceChips = { new Color(0.85f, 0.97f, 1f, 1f), new Color(0.6f, 0.85f, 0.98f, 1f), Color.white };

    /// <summary>
    /// Шаг на соседнюю клетку маленьким прыжком: присел → вытянулся и перелетел → сплющился при
    /// приземлении, пыль из-под куба. Позиции — относительно острова (он покачивается).
    /// </summary>
    IEnumerator MoveToTile(OverlayTile target)
    {
        RaiseOrderFor(target);
        Vector3 start = transform.localPosition;
        Vector3 end = ToLocal(TopOf(target));
        float dist = Vector2.Distance(start, end);
        float time = Mathf.Max(stepTime, dist / Mathf.Max(0.01f, speed));

        // присел
        Squash(-0.14f);
        if (stepAnticipation > 0f) yield return new WaitForSeconds(stepAnticipation);

        Squash(0.16f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            float h = 4f * stepHop * t * (1f - t);
            transform.localPosition = Vector3.Lerp(start, end, k) + Vector3.up * h;
            AirHeight = h;
            yield return null;
        }
        transform.localPosition = end;
        AirHeight = 0f;
        Squash(-0.2f);
        PixelSfx.Play(Sfx.Step, 0.6f, UnityEngine.Random.Range(0.9f, 1.1f));
        if (stepDust && order != null)
            PixelFX.Burst(transform.position + Vector3.up * 0.02f, PixelFX.DustColors, 4, 0.22f, 0.06f, 0.9f,
                          order.sortingLayerID, order.sortingOrder + 1, PixelFX.Dot, 0.8f, 0.35f);
        PositionCharacterOnLine(target, collect: true);
    }

    /// <summary>
    /// Куб подходит к краю, пятится, раскачивается («ой-ой!»), с края сыплются камешки,
    /// над головой «!», камера чуть вздрагивает — и он возвращается в центр клетки.
    /// </summary>
    IEnumerator EdgeTeeter(Vector3Int dir)
    {
        Vector3 start = transform.localPosition;
        Quaternion baseRot = Quaternion.identity;
        Vector3 step = StepVector(dir);
        Vector3 edge = start + step * edgeReach;
        float lean = step.x >= 0f ? -1f : 1f; // наклон в сторону обрыва (по часовой — вправо)

        // 1) Подходит к краю.
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
        {
            transform.localPosition = Vector3.Lerp(start, edge, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.localPosition = edge;
        Squash(-0.12f);

        if (edgeEffects) EdgeFX.Play(this, ToWorld(edge + step * 0.15f), step, order);
        if (edgeSound != null && Camera.main != null) AudioSource.PlayClipAtPoint(edgeSound, Camera.main.transform.position);
        else PixelSfx.Play(Sfx.Edge, 0.9f);

        // 2) Балансирует: наклоняется к обрыву и раскачивается всё слабее.
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, edgeTeeterTime))
        {
            float swing = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t);
            float angle = lean * edgeTiltAngle * (0.35f + 0.65f * swing);
            transform.rotation = baseRot * Quaternion.Euler(0f, 0f, angle);
            transform.localPosition = edge + step * (0.06f * swing);
            yield return null;
        }

        // 3) Отскакивает назад в центр клетки.
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            float h = 0.05f * Mathf.Sin(k * Mathf.PI);
            transform.localPosition = Vector3.Lerp(edge, start, k) + Vector3.up * h;
            transform.rotation = Quaternion.Slerp(transform.rotation, baseRot, k);
            AirHeight = h;
            yield return null;
        }
        transform.localPosition = start;
        transform.rotation = baseRot;
        AirHeight = 0f;
        Squash(-0.15f);
    }

    /// <summary>Смещение на одну клетку в мире (по настоящей сетке, если она есть).</summary>
    Vector3 StepVector(Vector3Int dir)
    {
        var mm = MapManager.Instance;
        if (mm != null && mm.tilemap != null && _tile != null)
        {
            var from = _tile.gridLocation;
            var to = new Vector3Int(from.x + dir.x, from.y + dir.y, from.z);
            var v = mm.tilemap.GetCellCenterWorld(to) - mm.tilemap.GetCellCenterWorld(from);
            v.z = 0f;
            if (v.sqrMagnitude > 0.0001f) return v;
        }
        // Изометрия: +X клетки → вправо-вверх, +Y → влево-вверх.
        return new Vector3((dir.x - dir.y) * 0.16f, (dir.x + dir.y) * 0.08f, 0f);
    }

    IEnumerator Bump(Vector3Int dir)
    {
        Vector3 start = transform.localPosition;
        // Изометрия: +X клетки → вправо-вверх, +Y → влево-вверх.
        var offset = new Vector3((dir.x - dir.y) * 0.5f, (dir.x + dir.y) * 0.25f, 0f) * 0.2f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / bumpTime)
        {
            transform.localPosition = start + offset * Mathf.Sin(t * Mathf.PI);
            yield return null;
        }
        transform.localPosition = start;
    }

    private IEnumerator JumpToTile(OverlayTile targetTile)
    {
        RaiseOrderFor(targetTile);
        PixelSfx.Play(Sfx.Jump, 0.8f);
        Vector3 startPos = transform.localPosition;
        Vector3 endPos = ToLocal(TopOf(targetTile));
        var launchPad = MushroomOf(_tile);
        if (launchPad != null) launchPad.Bounce(0.6f); // оттолкнулся от шляпки — гриб пружинит
        Squash(-0.18f);
        yield return new WaitForSeconds(0.05f);
        Squash(0.3f);

        for (float t = 0f; t < 1f; t += Time.deltaTime / jumpTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t);
            float height = 4 * jumpHeight * progress * (1 - progress);
            transform.localPosition = Vector3.Lerp(startPos, endPos, progress) + Vector3.up * height;
            AirHeight = height;
            yield return null;
        }
        AirHeight = 0f;
        Squash(-0.3f);
        PixelSfx.Play(Sfx.Land, 0.6f, 1.1f);

        PositionCharacterOnLine(targetTile, collect: true);
    }

    static MushroomObj MushroomOf(OverlayTile tile)
    {
        if (tile == null || tile.attachedObject == null) return null;
        return tile.attachedObject.TryGetComponent<MushroomObj>(out var m) ? m : null;
    }

    /// <summary>
    /// Приземлился на гриб: шляпка сплющивается, куб проседает вместе с ней, потом гриб
    /// «выстреливает» — куб подскакивает на месте и снова мягко встаёт на шляпку.
    /// Заменяет прежнюю паузу waitOnMushroom (длится не меньше неё).
    /// </summary>
    IEnumerator BounceOnMushroom(OverlayTile tile)
    {
        float total = Mathf.Max(0.36f, waitOnMushroom);
        var mushroom = MushroomOf(tile);
        if (mushroom == null) { yield return new WaitForSeconds(total); yield break; }

        Vector3 basePos = transform.localPosition;
        mushroom.Bounce(1f);

        // 1) Проседает вместе со шляпкой.
        float sink = total * 0.3f;
        for (float t = 0f; t < sink; t += Time.deltaTime)
        {
            transform.localPosition = basePos + Vector3.up * mushroom.CapDelta;
            yield return null;
        }

        // 2) Шляпка распрямляется — подбрасывает куб.
        Squash(0.25f);
        PixelSfx.Play(Sfx.Jump, 0.45f, 1.35f);
        float hop = total - sink;
        for (float t = 0f; t < 1f; t += Time.deltaTime / hop)
        {
            float h = 4f * mushroomHop * t * (1f - t);
            transform.localPosition = basePos + Vector3.up * (h + mushroom.CapDelta * (1f - t));
            AirHeight = h;
            yield return null;
        }
        transform.localPosition = basePos;
        AirHeight = 0f;
        Squash(-0.18f);
        mushroom.Bounce(0.35f);
    }

    int[] _baseOrders;

    /// <summary>
    /// Порядок отрисовки куба на клетке. На уровнях с врагами — по глубине клетки (World/SortDepth),
    /// чтобы куб и враги не перекрывали друг друга как попало; дочерние спрайты куба идут следом
    /// с теми же сдвигами, что и в сцене. На обычных уровнях — как раньше (клетка + 2).
    /// </summary>
    void ApplyOrder(OverlayTile tile)
    {
        if (order == null || tile == null) return;
        var tr = tile.GetComponent<SpriteRenderer>();
        int legacy = (tr != null ? tr.sortingOrder : 0) + OrderAboveTile;
        if (EnemyLayer.Active) SetBodyOrder(SortDepth.Character(tile), legacy);
        else
        {
            order.sortingOrder = legacy;
            // Вернуть дочерним спрайтам их порядок из сцены (если до этого был уровень с врагами).
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i] != order) _renderers[i].sortingOrder = _baseOrders[i];
        }
    }

    void SetBodyOrder(int body, int legacy)
    {
        order.sortingOrder = body;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null && _renderers[i] != order) _renderers[i].sortingOrder = body + (_baseOrders[i] - legacy);
    }

    /// <summary>
    /// Шаг к камере: новый (больший) порядок — сразу, иначе куб на полпути уйдёт под врага на клетке
    /// перед ним; шаг от камеры — порядок обновится по прибытии (ApplyOrder).
    /// </summary>
    void RaiseOrderFor(OverlayTile target)
    {
        MoveTarget = target;
        if (!EnemyLayer.Active || order == null || target == null) return;
        int to = SortDepth.Character(target);
        if (to <= order.sortingOrder) return;
        var tr = target.GetComponent<SpriteRenderer>();
        SetBodyOrder(to, (tr != null ? tr.sortingOrder : 0) + OrderAboveTile);
    }

    /// <summary>Пересчитать порядок на текущей клетке (EnemyLayer: уровень с врагами загружен или сброшен).</summary>
    public void RefreshSortOrder()
    {
        if (_tile != null) ApplyOrder(_tile);
    }

    public static Vector3 TopOf(OverlayTile tile)
    {
        if (tile.attachedObject != null && tile.attachedObject.TryGetComponent<MushroomObj>(out var mushroom))
        {
            var pos = mushroom.GetTopPosition();
            pos.z += 0.7f;
            return pos;
        }
        var p = tile.transform.position;
        p.z += 0.001f;
        return p;
    }

    private void PositionCharacterOnLine(OverlayTile tile, bool collect)
    {
        if (tile == null) return;

        transform.position = TopOf(tile);
        AirHeight = 0f;
        MoveTarget = null;
        ApplyOrder(tile);

        activeTile = tile;
        _tile = tile;

        if (collect && ActiveBlockManager.Instance != null)
        {
            var crystalObj = tile.attachedObject;
            var juice = crystalObj != null ? crystalObj.GetComponent<CrystalJuice>() : null;
            var crystalPos = juice != null ? juice.VisualCenter : crystalObj != null ? crystalObj.transform.position : transform.position;
            var crystalSprite = juice != null ? juice.VisualSprite
                              : crystalObj != null && crystalObj.TryGetComponent<SpriteRenderer>(out var csr) ? csr.sprite : null;
            if (ActiveBlockManager.Instance.TryCollectCrystal(tile))
            {
                _collectedNow = true;
                PixelSfx.Play(Sfx.Crystal, 0.9f);
                if (order != null)
                {
                    CrystalJuice.PlayCollect(crystalPos, crystalSprite, order.sortingLayerID, order.sortingOrder + 4);
                    PixelFX.Burst(crystalPos, PixelFX.CrystalColors, 12, 0.9f, 0.6f, 1.6f, order.sortingLayerID, order.sortingOrder + 5, null, 1.6f, 0.9f);
                }
            }
        }
    }

    // ============================================================
    //                    РАДОСТЬ: ПОДПРЫГНУТЬ
    // ============================================================

    /// <summary>Подпрыгнуть на месте. wiggle — покачивание в воздухе (градусы).</summary>
    IEnumerator Hop(float height, float time, float wiggle)
    {
        PixelSfx.Play(Sfx.Jump, 0.45f, 1.25f);
        Vector3 basePos = transform.localPosition;
        Squash(0.22f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, time))
        {
            float h = 4f * height * t * (1f - t);
            transform.localPosition = basePos + Vector3.up * h;
            AirHeight = h;
            if (wiggle != 0f) transform.rotation = Quaternion.Euler(0f, 0f, wiggle * Mathf.Sin(t * Mathf.PI * 2f));
            yield return null;
        }
        transform.localPosition = basePos;
        transform.rotation = Quaternion.identity;
        AirHeight = 0f;
        Squash(-0.25f);
    }

    // ============================================================
    //                 УЛЁТ В НЕБО (переход на следующий уровень)
    // ============================================================

    [Header("Улёт в небо (кнопка «Дальше»)")]
    public float flyAwayTime = 1.0f;
    [Tooltip("Насколько высоко улетает (единицы мира)")] public float flyAwayHeight = 3f;

    public bool IsLanded => _landed;

    /// <summary>
    /// Присел → оттолкнулся → улетает вверх с ускорением, тает и оставляет искристый след.
    /// Вызывает Levelloader перед облаками перехода.
    /// </summary>
    public IEnumerator FlyAway()
    {
        StopRun();
        transform.rotation = Quaternion.identity;
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        var baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
        Vector3 start = transform.localPosition;
        int layer = order != null ? order.sortingLayerID : 0;
        int sort = order != null ? order.sortingOrder : 0;

        // Приседает (подготовка к прыжку)
        Squash(-0.3f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
        {
            transform.localPosition = start + Vector3.down * (0.04f * Mathf.Sin(t * Mathf.PI * 0.5f));
            yield return null;
        }
        Squash(0.4f);

        PixelFX.Burst(ToWorld(start) + Vector3.down * 0.03f, PixelFX.DustColors, 10, 0.6f, 0.05f, 1.2f, layer, sort + 1, PixelFX.Dot, 1.3f, 0.5f);
        PixelSfx.Play(Sfx.Jump, 0.8f, 0.85f);
        PixelSfx.Play(Sfx.Whoosh, 0.9f);

        // Улетает вверх с ускорением, тает во второй половине, за ним — искры
        float nextTrail = 0f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.2f, flyAwayTime))
        {
            float k = t * t * (1.6f - 0.6f * t);   // разгон
            transform.localPosition = start + Vector3.up * (flyAwayHeight * k);
            AirHeight = flyAwayHeight * k;
            transform.rotation = Quaternion.Euler(0f, 0f, 12f * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t));
            float alpha = t < 0.4f ? 1f : Mathf.Clamp01(1f - (t - 0.4f) / 0.6f);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = baseColors[i];
                c.a *= alpha;
                renderers[i].color = c;
            }
            if (t >= nextTrail)
            {
                nextTrail += 0.08f;
                PixelFX.Burst(transform.position + Vector3.down * 0.05f, PixelFX.StarColors, 3, 0.25f, -0.2f, 0.5f, layer, sort + 6, null, 1.2f * alpha + 0.3f, 0.6f);
            }
            yield return null;
        }
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) { var c = baseColors[i]; c.a = 0f; renderers[i].color = c; }
    }

    /// <summary>Все кристаллы собраны — радостно прыгает (вызывает MovingManager.OnLevelComplete).</summary>
    public void Celebrate()
    {
        if (_celebrate != null) StopCoroutine(_celebrate);
        _celebrate = StartCoroutine(CelebrateRoutine());
    }

    IEnumerator CelebrateRoutine()
    {
        PixelSfx.Play(Sfx.Win, 0.9f);
        for (int i = 0; i < Mathf.Max(1, victoryHops); i++)
        {
            if (order != null && i == 0)
                PixelFX.Burst(transform.position + Vector3.up * 0.15f, PixelFX.StarColors, 14, 1.1f, 0.8f, 1.4f, order.sortingLayerID, order.sortingOrder + 5, null, 1.8f, 1f);
            yield return Hop(collectHopHeight * (i == 0 ? 1.6f : 1.2f), collectHopTime * 1.1f, i % 2 == 0 ? 10f : -10f);
        }
        _celebrate = null;
    }
}
