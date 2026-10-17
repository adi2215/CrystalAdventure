using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Пошаговый туториал поверх игры. Создаётся сам, если у текущего уровня есть LevelSt.tutorial
/// и уровень ещё ни разу не пройден.
///
///  • экран затемняется, «дырка» с пульсирующей рамкой — вокруг того, что нужно нажать
///    (кликнуть можно только туда);
///  • рядом покачивается пиксельная рука;
///  • персонаж в окне реплики «говорит»: текст печатается по буквам, буквы подпрыгивают
///    и чуть покачиваются (по целым пикселям), портрет подпрыгивает, пока идёт текст;
///  • шаг ждёт действия игрока: тап, карточку, вкладку, Play.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    const float TypeSpeed = 42f;     // символов в секунду
    const float HolePadding = 14f;
    const float DimAlpha = 0.62f;
    const float VoicePitch = 1.15f;  // высота «голоса» Элы

    static readonly Color FrameColor = new Color(1f, 0.82f, 0.3f, 1f);
    static readonly Color NameColor = new Color(0.55f, 0.36f, 0.75f, 1f);

    // ───────────── запуск ─────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Create();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Create();

    static void Create()
    {
        if (FindObjectOfType<TutorialDirector>() != null) return;
        if (FindObjectOfType<MovingManager>() == null) return;
        new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
    }

    /// <summary>Надо ли показывать туториал уровня (есть сценарий и уровень ещё не пройден).</summary>
    public static bool ShouldShow(LevelSt level, int levelIndex)
    {
        if (level == null || level.tutorial == null || level.tutorial.steps == null || level.tutorial.steps.Length == 0) return false;
        var db = SaveManager.Load();
        if (db != null && db.levels != null)
            foreach (var l in db.levels)
                if (l != null && l.levelNumber == levelIndex && l.levelCompleted && l.starsEarned > 0) return false;
        return true;
    }

    public static bool IsRunning { get; private set; }

    // ───────────── ссылки на игру ─────────────

    MovingManager _moving;
    PanelController _panels;

    // ───────────── UI ─────────────

    RectTransform _root;
    CanvasGroup _rootGroup;
    readonly Image[] _dim = new Image[4];
    RectTransform _frame;
    Image _frameImg;
    RectTransform _hand;
    Image _catcher;
    RectTransform _box;
    RectTransform _portrait;
    Image _portraitImg;
    TextMeshProUGUI _name;
    TextMeshProUGUI _text;
    RectTransform _next;
    TMP_FontAsset _font;

    // ───────────── состояние ─────────────

    TutorialStep _step;
    bool _tapped, _typing, _skip, _hasTarget;
    int _visible;
    float[] _revealTime = new float[0];
    Rect _hole;          // текущая (сглаженная) дырка в координатах _root
    Rect _holeTarget;
    float _dimAlpha;

    IEnumerator Start()
    {
        _moving = FindObjectOfType<MovingManager>();
        _panels = FindObjectOfType<PanelController>();

        // Ждём, пока уровень загрузится и куб приземлится.
        float timeout = 15f;
        while ((TilemapData.CurrentLevel == null || _moving == null || _moving.character == null || !_moving.character.IsReady) && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        var level = TilemapData.CurrentLevel;
        var data = _moving != null && _moving.character != null ? _moving.character.tran : null;
        int index = data != null ? data.NextLevel : -1;
        if (!ShouldShow(level, index)) { Destroy(gameObject); yield break; }

        var script = level.tutorial;
        IsRunning = true;
        Build(script);
        yield return ShowBox();

        foreach (var step in script.steps)
        {
            if (_skip) break;
            yield return RunStep(step);
            if (_moving != null && ActiveBlockManager.Instance != null && ActiveBlockManager.Instance.LevelComplete) break;
        }

        yield return HideAll();
        IsRunning = false;
        Destroy(gameObject);
    }

    void OnDestroy() => IsRunning = false;

    // ───────────── шаги ─────────────

    IEnumerator RunStep(TutorialStep step)
    {
        _step = step;
        _tapped = false;
        _scrolledFor = null;

        if (step.wait == TutorialWait.LevelComplete)
        {
            // Дальше игрок сам — просто убираем подсказки.
            _skip = true;
            yield break;
        }

        SetTarget(step);
        PlaceBox();
        StartCoroutine(Punch(_box, 0.05f));
        // На шагах «тап» любой клик сначала допечатывает текст, потом ведёт дальше.
        _catcher.raycastTarget = step.wait == TutorialWait.Tap;
        yield return TypeText(step.text);

        switch (step.wait)
        {
            case TutorialWait.Tap:
                _catcher.raycastTarget = true;
                _next.gameObject.SetActive(true);
                while (!_tapped && !_skip) yield return null;
                _next.gameObject.SetActive(false);
                _catcher.raycastTarget = false;
                break;

            case TutorialWait.AddCard:
            {
                int baseline = CountCards(step.slot, step.card);
                while (!_skip && CountCards(step.slot, step.card) < baseline + Mathf.Max(1, step.count)) yield return null;
                yield return Celebrate();
                break;
            }

            case TutorialWait.OpenTab:
                while (!_skip && !TabOpen(step.slot)) yield return null;
                yield return Celebrate();
                break;

            case TutorialWait.LoopSpan:
                while (!_skip && LoopSpanNow(step.slot) != Mathf.Max(1, step.count)) yield return null;
                yield return Celebrate();
                break;

            case TutorialWait.PaintSelected:
                while (!_skip && !PaintPalette.Active) yield return null;
                yield return Celebrate();
                break;

            case TutorialWait.PaintCard:
                while (!_skip && PaintedCard(step.slot, step.card) == null) yield return null;
                yield return Celebrate();
                break;

            case TutorialWait.LoopRepeats:
                while (!_skip && LoopRepeatsNow(step.slot) != Mathf.Max(1, step.count)) yield return null;
                yield return Celebrate();
                break;

            case TutorialWait.PressPlay:
                while (!_skip && (_moving == null || _moving.State != MovingManager.RunState.Playing)) yield return null;
                // Пока куб едет — ничего не загораживаем.
                yield return Fade(0f);
                while (_moving != null && _moving.State == MovingManager.RunState.Playing) yield return null;
                if (ActiveBlockManager.Instance != null && ActiveBlockManager.Instance.LevelComplete) yield break;
                yield return Fade(1f);
                break;
        }
    }

    IEnumerator Celebrate()
    {
        PixelSfx.Play(Sfx.Plus, 0.8f);
        if (_hand != null) StartCoroutine(Punch(_hand, 0.3f));
        if (_frame != null) PixelFX.BurstUI(_frame, PixelFX.StarColors, 10, 220f, 14f, 0.5f);
        yield return Wait(0.35f);
    }

    int CountCards(SlotType slot, ItemType type)
    {
        if (_moving == null) return 0;
        int n = 0;
        foreach (var item in _moving.Commands[slot]) if (item != null && item.type == type) n++;
        return n;
    }

    /// <summary>Первый While в панели (по порядку слотов), -1 — нет.</summary>
    int FirstWhile(SlotType slot)
    {
        if (_moving == null) return -1;
        var list = _moving.Commands[slot];
        for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].type == ItemType.While) return i;
        return -1;
    }

    /// <summary>Первая покрашенная карточка этого типа в панели (null — нет).</summary>
    InventoryItems PaintedCard(SlotType slot, ItemType type)
    {
        if (_moving == null) return null;
        var list = _moving.Commands[slot];
        var inv = slot == SlotType.Main ? _moving.Items : slot == SlotType.FunctionOne ? _moving.Items1 : _moving.Items2;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].type == type && _moving.GetColor(slot, i) != TileColor.None)
                return inv != null ? inv.FindCard(i) : null;
        return null;
    }

    /// <summary>Первая карточка этого типа в панели (для метки-условия).</summary>
    InventoryItems FirstCard(SlotType slot, ItemType type)
    {
        if (_moving == null) return null;
        var list = _moving.Commands[slot];
        var inv = slot == SlotType.Main ? _moving.Items : slot == SlotType.FunctionOne ? _moving.Items1 : _moving.Items2;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].type == type)
                return inv != null ? inv.FindCard(i) : null;
        return null;
    }

    int LoopSpanNow(SlotType slot)
    {
        int i = FirstWhile(slot);
        return i < 0 ? -1 : _moving.CardsInSpan(slot, i);
    }

    int LoopRepeatsNow(SlotType slot)
    {
        int i = FirstWhile(slot);
        return i < 0 ? -1 : _moving.GetIteration(slot, i) + 1;
    }

    bool TabOpen(SlotType slot)
    {
        var spawn = FindSpawn(slot);
        if (spawn == null || spawn.objMethods == null) return false;
        foreach (var o in spawn.objMethods) if (o != null && o.activeInHierarchy) return true;
        return false;
    }

    // ───────────── цели подсветки ─────────────

    void SetTarget(TutorialStep step)
    {
        _hasTarget = TryTargetRect(step, out _holeTarget);
        if (!_hasTarget) _holeTarget = new Rect(0f, 0f, 0f, 0f);
        _dimAlpha = step.dim ? DimAlpha : 0f;
    }

    bool TryTargetRect(TutorialStep step, out Rect rect)
    {
        rect = default;
        switch (step.target)
        {
            case TutorialTarget.Hero:
                return _moving != null && _moving.character != null && WorldRect(_moving.character.MainRenderer, out rect);
            case TutorialTarget.Crystal:
                foreach (var c in FindObjectsOfType<Crystal>())
                    if (c.gameObject.activeInHierarchy && WorldRect(c.GetComponentInChildren<SpriteRenderer>(), out rect)) return true;
                return false;
            case TutorialTarget.CommandButton:
            {
                var spawn = FindSpawn(step.slot);
                var button = spawn != null ? spawn.FindButton(step.card) : null;
                if (button != null && button.activeInHierarchy) return UIRect(button.transform as RectTransform, out rect);
                return TabRect(step.slot, out rect); // кнопка на другой вкладке — показать вкладку
            }
            case TutorialTarget.ProgramPanel:
            {
                var inv = step.slot == SlotType.Main ? _moving?.Items : step.slot == SlotType.FunctionOne ? _moving?.Items1 : _moving?.Items2;
                return inv != null && inv.gameObject.activeInHierarchy && UIRect(inv.transform as RectTransform, out rect);
            }
            case TutorialTarget.Tab:
                return TabRect(step.slot, out rect);
            case TutorialTarget.PlayButton:
                return _moving != null && _moving.Play != null && UIRect(_moving.Play.transform as RectTransform, out rect);
            case TutorialTarget.ClearButton:
                foreach (var b in FindObjectsOfType<Button>())
                {
                    var e = b.onClick;
                    for (int i = 0; i < e.GetPersistentEventCount(); i++)
                        if (e.GetPersistentMethodName(i) == "ClearButtons") return UIRect(b.transform as RectTransform, out rect);
                }
                return false;
            case TutorialTarget.HintButton:
            {
                var hint = FindObjectOfType<HintSystem>();
                return hint != null && UIRect(hint.transform as RectTransform, out rect);
            }
            case TutorialTarget.LevelsButton:
            {
                var go = GameObject.Find("LevelsButton");
                return go != null && UIRect(go.transform as RectTransform, out rect);
            }
            case TutorialTarget.LoopArrow:
            {
                var lb = LoopBrackets.For(step.slot);
                int i = FirstWhile(step.slot);
                if (lb == null || i < 0) return false;
                var parts = new List<RectTransform>();
                lb.PartsOf(i, parts);
                bool any = false;
                foreach (var part in parts)
                {
                    if (part == null || !part.gameObject.activeInHierarchy || !UIRect(part, out var r)) continue;
                    rect = any ? Rect.MinMaxRect(Mathf.Min(rect.xMin, r.xMin), Mathf.Min(rect.yMin, r.yMin), Mathf.Max(rect.xMax, r.xMax), Mathf.Max(rect.yMax, r.yMax)) : r;
                    any = true;
                }
                // Место, куда тянуть стрелку, тоже должно быть в «дырке»: добавляем весь ряд слотов.
                var inv = step.slot == SlotType.Main ? _moving?.Items : step.slot == SlotType.FunctionOne ? _moving?.Items1 : _moving?.Items2;
                if (any && inv != null && UIRect(inv.transform as RectTransform, out var panel))
                    rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, panel.xMin), Mathf.Min(rect.yMin, panel.yMin), Mathf.Max(rect.xMax, panel.xMax), Mathf.Max(rect.yMax, panel.yMax));
                return any;
            }
            case TutorialTarget.ColorTile:
            {
                var map = MapManager.Instance != null ? MapManager.Instance.map : null;
                if (map == null) return false;
                OverlayTile best = null;
                foreach (var t in map.Values)
                    if (t != null && ColorTiles.At(t) != TileColor.None &&
                        (best == null || t.gridLocation.y < best.gridLocation.y || (t.gridLocation.y == best.gridLocation.y && t.gridLocation.x < best.gridLocation.x)))
                        best = t;
                return best != null && WorldRect(best.GetComponent<SpriteRenderer>(), out rect);
            }
            case TutorialTarget.PaintButton:
            {
                var go = ColorTiles.UsedColors.Count > 0 ? PaintPalette.ButtonFor(ColorTiles.UsedColors[0]) : null;
                return go != null && go.activeInHierarchy && UIRect(go.transform as RectTransform, out rect);
            }
            case TutorialTarget.ProgramCard:
            {
                var card = FirstCard(step.slot, step.card);
                return card != null && card.gameObject.activeInHierarchy && UIRect(card.transform as RectTransform, out rect);
            }
            case TutorialTarget.LoopBadge:
            {
                var lb = LoopBrackets.For(step.slot);
                int i = FirstWhile(step.slot);
                var badge = lb != null && i >= 0 ? lb.BadgeOf(i) : null;
                return badge != null && badge.gameObject.activeInHierarchy && UIRect(badge, out rect);
            }
        }
        return false;
    }

    SpawnScript FindSpawn(SlotType slot)
    {
        foreach (var s in FindObjectsOfType<SpawnScript>(true))
            if (s.type == slot && s.objMethods != null && s.objMethods.Length > 0) return s;
        return null;
    }

    bool TabRect(SlotType slot, out Rect rect)
    {
        rect = default;
        if (_panels == null || _panels.spawnMethods == null || _panels.buttonPanels == null) return false;
        var spawn = FindSpawn(slot);
        int i = System.Array.IndexOf(_panels.spawnMethods, spawn);
        if (i < 0 || i >= _panels.buttonPanels.Length || _panels.buttonPanels[i] == null) return false;
        return UIRect(_panels.buttonPanels[i].transform as RectTransform, out rect);
    }

    /// <summary>Прямоугольник элемента интерфейса (любой канвас) в координатах нашего оверлея.</summary>
    bool UIRect(RectTransform target, out Rect rect)
    {
        rect = default;
        if (target == null) return false;
        var canvas = target.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null)
        {
            var root = canvas.rootCanvas;
            if (root.renderMode != RenderMode.ScreenSpaceOverlay) cam = root.worldCamera != null ? root.worldCamera : Camera.main;
        }
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        return CornersToRect(corners, cam, out rect);
    }

    bool WorldRect(SpriteRenderer sr, out Rect rect)
    {
        rect = default;
        if (sr == null || Camera.main == null) return false;
        var b = sr.bounds;
        var corners = new[] { new Vector3(b.min.x, b.min.y, b.center.z), new Vector3(b.min.x, b.max.y, b.center.z), new Vector3(b.max.x, b.max.y, b.center.z), new Vector3(b.max.x, b.min.y, b.center.z) };
        return CornersToRect(corners, Camera.main, out rect);
    }

    bool CornersToRect(Vector3[] corners, Camera cam, out Rect rect)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            Vector2 screen = cam != null ? (Vector2)cam.WorldToScreenPoint(c) : (Vector2)c;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        rect = Rect.MinMaxRect(min.x - HolePadding, min.y - HolePadding, max.x + HolePadding, max.y + HolePadding);
        return rect.width > 1f && rect.height > 1f;
    }

    bool LoopHandRect(out Rect rect)
    {
        rect = default;
        var lb = LoopBrackets.For(_step.slot);
        int i = FirstWhile(_step.slot);
        var handle = lb != null && i >= 0 ? lb.HandleOf(i) : null;
        if (handle == null || !UIRect(handle, out rect)) return false;
        rect = Rect.MinMaxRect(rect.xMin + HolePadding, rect.yMin + HolePadding, rect.xMax - HolePadding, rect.yMax - HolePadding);
        return true;
    }

    // ───────────── прокрутка к кнопке ─────────────

    GameObject _scrolledFor;

    /// <summary>Кнопка карточки может быть за краем прокручиваемой панели — плавно докручиваем к ней.</summary>
    void EnsureButtonVisible()
    {
        if (_step == null || (_step.target != TutorialTarget.CommandButton && _step.target != TutorialTarget.PaintButton)) return;
        GameObject button;
        if (_step.target == TutorialTarget.PaintButton)
            button = ColorTiles.UsedColors.Count > 0 ? PaintPalette.ButtonFor(ColorTiles.UsedColors[0]) : null;
        else
        {
            var spawn = FindSpawn(_step.slot);
            button = spawn != null ? spawn.FindButton(_step.card) : null;
        }
        if (button == null || !button.activeInHierarchy || button == _scrolledFor) return;
        _scrolledFor = button;
        var scroll = button.GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.content != null) StartCoroutine(ScrollTo(scroll, (RectTransform)button.transform));
    }

    static IEnumerator ScrollTo(ScrollRect scroll, RectTransform target)
    {
        var content = scroll.content;
        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        Canvas.ForceUpdateCanvases();

        Vector2 c = content.InverseTransformPoint(target.TransformPoint(target.rect.center));
        float fromX = scroll.horizontalNormalizedPosition, fromY = scroll.verticalNormalizedPosition;
        float toX = fromX, toY = fromY;

        float extraW = content.rect.width - viewport.rect.width;
        if (scroll.horizontal && extraW > 1f)
            toX = Mathf.Clamp01((c.x - content.rect.xMin - viewport.rect.width * 0.5f) / extraW);
        float extraH = content.rect.height - viewport.rect.height;
        if (scroll.vertical && extraH > 1f)
            toY = Mathf.Clamp01((c.y - content.rect.yMin - viewport.rect.height * 0.5f) / extraH);

        if (Mathf.Abs(toX - fromX) < 0.01f && Mathf.Abs(toY - fromY) < 0.01f) yield break;
        scroll.StopMovement();
        PixelSfx.Play(Sfx.Whoosh, 0.35f, 1.6f);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
        {
            if (scroll == null) yield break;
            float k = Mathf.SmoothStep(0f, 1f, t);
            if (scroll.horizontal) scroll.horizontalNormalizedPosition = Mathf.Lerp(fromX, toX, k);
            if (scroll.vertical) scroll.verticalNormalizedPosition = Mathf.Lerp(fromY, toY, k);
            yield return null;
        }
        if (scroll == null) yield break;
        if (scroll.horizontal) scroll.horizontalNormalizedPosition = toX;
        if (scroll.vertical) scroll.verticalNormalizedPosition = toY;
    }

    // ───────────── кадр ─────────────

    void Update()
    {
        if (_root == null) return;
        EnsureButtonVisible();

        // цель может двигаться (анимации) — пересчитываем
        if (_step != null && _hasTarget && TryTargetRect(_step, out var r)) _holeTarget = r;

        float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        _hole = new Rect(
            Mathf.Lerp(_hole.x, _holeTarget.x, k), Mathf.Lerp(_hole.y, _holeTarget.y, k),
            Mathf.Lerp(_hole.width, _holeTarget.width, k), Mathf.Lerp(_hole.height, _holeTarget.height, k));
        LayoutDim();
        LayoutFrameAndHand();
        AnimateText();
        AnimatePortrait();
    }

    void LayoutDim()
    {
        var full = _root.rect;
        var h = _hasTarget ? _hole : new Rect(full.center, Vector2.zero);
        // верх, низ, лево, право вокруг дырки
        SetRect(_dim[0], full.xMin, h.yMax, full.width, full.yMax - h.yMax);
        SetRect(_dim[1], full.xMin, full.yMin, full.width, h.yMin - full.yMin);
        SetRect(_dim[2], full.xMin, h.yMin, h.xMin - full.xMin, h.height);
        SetRect(_dim[3], h.xMax, h.yMin, full.xMax - h.xMax, h.height);
        foreach (var d in _dim)
        {
            var c = d.color;
            c.a = Mathf.MoveTowards(c.a, _dimAlpha, Time.unscaledDeltaTime * 3f);
            d.color = c;
        }
    }

    static void SetRect(Graphic g, float x, float y, float w, float h)
    {
        var rt = g.rectTransform;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
    }

    void LayoutFrameAndHand()
    {
        bool show = _hasTarget && _step != null && _step.target != TutorialTarget.None;
        _frame.gameObject.SetActive(show);
        _hand.gameObject.SetActive(show && _step.wait != TutorialWait.Tap);
        if (!show) return;

        float t = Time.unscaledTime;
        float pulse = Mathf.Round(Mathf.Sin(t * 5f) * 2f) * 2f; // пиксельное «дыхание» рамки
        _frame.anchoredPosition = new Vector2(_hole.x - pulse, _hole.y - pulse);
        _frame.sizeDelta = new Vector2(_hole.width + pulse * 2f, _hole.height + pulse * 2f);

        // Скобка цикла: рука «берёт» стрелку и показывает, куда её тянуть.
        if (_step.target == TutorialTarget.LoopArrow && LoopHandRect(out var arrow))
        {
            int want = Mathf.Max(1, _step.count), now = LoopSpanNow(_step.slot);
            float dir = now > want ? 1f : now < want ? -1f : 0f;          // сузить — вправо, расширить — влево
            float slotW = Mathf.Max(60f, arrow.width * 1.6f);
            float phase = (t * 0.8f) % 1f;
            float travel = dir * slotW * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 0.15f) / 0.6f));
            var grab = arrow.center + new Vector2(travel, 0f);
            _hand.anchoredPosition = new Vector2(Mathf.Round(grab.x / 2f) * 2f, Mathf.Round(grab.y / 2f) * 2f);
            _hand.localRotation = Quaternion.identity;
            return;
        }

        // Рука: снизу справа от цели, палец к центру; если не влезает — сверху слева, развёрнута.
        var full = _root.rect;
        bool flip = _hole.xMax + 90f > full.xMax || _hole.yMin - 90f < full.yMin;
        float bob = Mathf.Round(Mathf.Abs(Mathf.Sin(t * 4f)) * 6f) * 2f;
        if (!flip)
        {
            _hand.anchoredPosition = new Vector2(_hole.xMax - 18f + bob, _hole.yMin + 18f - bob);
            _hand.localRotation = Quaternion.identity;
        }
        else
        {
            _hand.anchoredPosition = new Vector2(_hole.xMin + 18f - bob, _hole.yMax - 18f + bob);
            _hand.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }
    }

    // ───────────── текст: печать и «живые» буквы ─────────────

    IEnumerator TypeText(string text)
    {
        _text.text = text ?? "";
        _text.ForceMeshUpdate();
        int total = _text.textInfo.characterCount;
        _revealTime = new float[total];
        for (int i = 0; i < total; i++) _revealTime[i] = float.MaxValue;
        _visible = 0;
        _text.maxVisibleCharacters = 0;
        _typing = true;
        _tapped = false;

        float acc = 0f;
        while (_visible < total)
        {
            if (_tapped || _skip) // тап во время печати — показать всё сразу
            {
                for (int i = _visible; i < total; i++) _revealTime[i] = Time.unscaledTime;
                _visible = total;
                _tapped = false;
                break;
            }
            acc += Time.unscaledDeltaTime * TypeSpeed;
            while (acc >= 1f && _visible < total)
            {
                acc -= 1f;
                char ch = _text.textInfo.characterInfo[_visible].character;
                _revealTime[_visible] = Time.unscaledTime;
                _visible++;
                // «голос»: пиксельный писк на каждую вторую букву, чуть разной высоты
                if (!char.IsWhiteSpace(ch) && _visible % 2 == 0)
                    PixelSfx.Play(Sfx.Blip, 0.5f, VoicePitch + Random.Range(-0.06f, 0.06f), 0.04f);
                if (ch == '.' || ch == '!' || ch == '?') acc -= 6f;   // пауза после фразы
                else if (ch == ',') acc -= 3f;
            }
            _text.maxVisibleCharacters = _visible;
            yield return null;
        }
        _text.maxVisibleCharacters = total;
        _typing = false;
        _tapped = false;
    }

    void AnimateText()
    {
        if (_text == null || string.IsNullOrEmpty(_text.text)) return;
        _text.ForceMeshUpdate();
        var info = _text.textInfo;
        float now = Time.unscaledTime;
        for (int i = 0; i < info.characterCount && i < _revealTime.Length; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible || i >= _visible) continue;
            float age = now - _revealTime[i];
            // появление: буква выскакивает снизу с отскоком
            float pop = age < 0.16f ? BackOut(age / 0.16f) : 1f;
            float jump = age < 0.22f ? (1f - age / 0.22f) * 12f : 0f;
            // постоянное лёгкое покачивание волной
            float wave = Mathf.Sin(now * 6f + i * 0.55f) * 1.6f;
            float dy = Mathf.Round((jump + wave) / 2f) * 2f; // целые «пиксели» по 2

            int mat = ch.materialReferenceIndex, v = ch.vertexIndex;
            var verts = info.meshInfo[mat].vertices;
            Vector3 center = (verts[v] + verts[v + 2]) * 0.5f;
            for (int j = 0; j < 4; j++)
                verts[v + j] = center + (verts[v + j] - center) * pop + new Vector3(0f, dy, 0f);
        }
        _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    void AnimatePortrait()
    {
        if (_portrait == null) return;
        float t = Time.unscaledTime;
        if (_typing)
        {
            // «говорит»: подпрыгивает и качает головой
            _portrait.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Abs(Mathf.Sin(t * 14f)) * 3f) * 2f);
            _portrait.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 4f);
        }
        else
        {
            _portrait.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Sin(t * 2f) * 1.5f) * 2f);
            _portrait.localRotation = Quaternion.identity;
        }
        if (_next != null && _next.gameObject.activeSelf)
            _next.anchoredPosition = new Vector2(-34f, 26f + Mathf.Round(Mathf.Abs(Mathf.Sin(t * 5f)) * 3f) * 2f);
    }

    // ───────────── построение интерфейса ─────────────

    void Build(TutorialScript script)
    {
        _font = GameFont.Main;

        var canvasGo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = NewRect("Root", canvasGo.transform);
        Stretch(_root);
        _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();

        // затемнение из 4 кусков (между ними — дырка, через неё можно нажать)
        for (int i = 0; i < 4; i++)
        {
            var rt = NewRect("Dim", _root);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
            _dim[i] = rt.gameObject.AddComponent<Image>();
            _dim[i].color = new Color(0.05f, 0.03f, 0.1f, 0f);
        }

        _frame = NewRect("Frame", _root);
        _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
        _frame.pivot = Vector2.zero;
        _frameImg = _frame.gameObject.AddComponent<Image>();
        _frameImg.sprite = PixelSprites.Frame;
        _frameImg.type = Image.Type.Sliced;
        _frameImg.pixelsPerUnitMultiplier = 0.25f;
        _frameImg.color = FrameColor;
        _frameImg.raycastTarget = false;

        _hand = NewRect("Hand", _root);
        _hand.anchorMin = _hand.anchorMax = new Vector2(0.5f, 0.5f);
        _hand.pivot = new Vector2(0.05f, 0.95f);
        _hand.sizeDelta = new Vector2(70f, 84f);
        var handImg = _hand.gameObject.AddComponent<Image>();
        handImg.sprite = PixelSprites.Hand;
        handImg.raycastTarget = false;

        // ловушка тапа (включается только на шагах «Tap»)
        var catcher = NewRect("TapCatcher", _root);
        Stretch(catcher);
        _catcher = catcher.gameObject.AddComponent<Image>();
        _catcher.color = new Color(0f, 0f, 0f, 0f);
        _catcher.raycastTarget = false;
        var catchButton = catcher.gameObject.AddComponent<Button>();
        catchButton.transition = Selectable.Transition.None;
        catchButton.onClick.AddListener(() => { _tapped = true; PixelSfx.Play(Sfx.Click, 0.6f); });

        BuildBox(script);
    }

    void BuildBox(TutorialScript script)
    {
        _box = NewRect("Dialog", _root);
        _box.anchorMin = _box.anchorMax = new Vector2(0f, 1f);
        _box.pivot = new Vector2(0f, 1f);
        _box.sizeDelta = new Vector2(860f, 230f);
        _box.anchoredPosition = new Vector2(40f, -40f);
        var bg = _box.gameObject.AddComponent<Image>();
        bg.sprite = PixelSprites.Panel;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 0.2f;
        var boxButton = _box.gameObject.AddComponent<Button>(); // тап по окну — тоже «дальше»
        boxButton.transition = Selectable.Transition.None;
        boxButton.onClick.AddListener(() => { _tapped = true; PixelSfx.Play(Sfx.Click, 0.6f); });

        // портрет
        var frame = NewRect("PortraitFrame", _box);
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(0f, 0.5f);
        frame.anchoredPosition = new Vector2(24f, -6f);
        frame.sizeDelta = new Vector2(170f, 170f);
        var frameImg = frame.gameObject.AddComponent<Image>();
        frameImg.sprite = PixelSprites.Tag;
        frameImg.type = Image.Type.Sliced;
        frameImg.pixelsPerUnitMultiplier = 0.2f;
        frameImg.color = new Color(0.82f, 0.74f, 0.95f, 1f);
        frameImg.raycastTarget = false;

        _portrait = NewRect("Portrait", frame);
        _portrait.anchorMin = new Vector2(0.1f, 0.1f);
        _portrait.anchorMax = new Vector2(0.9f, 0.9f);
        _portrait.offsetMin = _portrait.offsetMax = Vector2.zero;
        _portraitImg = _portrait.gameObject.AddComponent<Image>();
        _portraitImg.sprite = script.portrait != null ? script.portrait : ScenePortrait();
        _portraitImg.preserveAspect = true;
        _portraitImg.raycastTarget = false;
        if (_portraitImg.sprite == null) _portraitImg.color = new Color(0f, 0f, 0f, 0f);

        // табличка с именем
        var tag = NewRect("NameTag", _box);
        tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f);
        tag.pivot = new Vector2(0f, 0.5f);
        tag.anchoredPosition = new Vector2(30f, 0f);
        tag.sizeDelta = new Vector2(200f, 56f);
        var tagImg = tag.gameObject.AddComponent<Image>();
        tagImg.sprite = PixelSprites.Tag;
        tagImg.type = Image.Type.Sliced;
        tagImg.pixelsPerUnitMultiplier = 0.2f;
        tagImg.color = NameColor;
        tagImg.raycastTarget = false;
        _name = NewText(tag, script.speakerName, 32f, TextAlignmentOptions.Center);
        _name.color = Color.white;

        // текст
        var textRt = NewRect("Text", _box);
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(214f, 34f);
        textRt.offsetMax = new Vector2(-40f, -38f);
        _text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) _text.font = _font;
        _text.fontSize = 34f;
        _text.color = PixelSprites.Ink;
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.enableWordWrapping = true;
        _text.raycastTarget = false;

        // «дальше» ▼
        _next = NewRect("Next", _box);
        _next.anchorMin = _next.anchorMax = new Vector2(1f, 0f);
        _next.sizeDelta = new Vector2(34f, 26f);
        var nextImg = _next.gameObject.AddComponent<Image>();
        nextImg.sprite = PixelSprites.ArrowDown;
        nextImg.color = NameColor;
        nextImg.raycastTarget = false;
        _next.gameObject.SetActive(false);

        // «Пропустить»
        var skip = NewRect("Skip", _box);
        skip.anchorMin = skip.anchorMax = new Vector2(1f, 1f);
        skip.pivot = new Vector2(1f, 0.5f);
        skip.anchoredPosition = new Vector2(-24f, 0f);
        skip.sizeDelta = new Vector2(120f, 46f);
        var skipImg = skip.gameObject.AddComponent<Image>();
        skipImg.sprite = PixelSprites.Tag;
        skipImg.type = Image.Type.Sliced;
        skipImg.pixelsPerUnitMultiplier = 0.2f;
        skipImg.color = new Color(0.55f, 0.5f, 0.62f, 1f);
        var skipBtn = skip.gameObject.AddComponent<Button>();
        skipBtn.onClick.AddListener(() => _skip = true);
        NewText(skip, "Skip", 26f, TextAlignmentOptions.Center).color = Color.white;
        ButtonJuice.Ensure(skip.gameObject);

        _box.localScale = Vector3.zero;
    }

    /// <summary>Портрет из окна диалога в сцене (там уже нарисован персонаж).</summary>
    static Sprite ScenePortrait()
    {
        var dm = FindObjectOfType<DialogManager>(true);
        return dm != null && dm.actorImage != null ? dm.actorImage.sprite : null;
    }

    /// <summary>Окно сверху слева; если цель под ним — переезжает вниз.</summary>
    void PlaceBox()
    {
        if (_box == null) return;
        var full = _root.rect;
        var topLeft = new Rect(full.xMin + 40f, full.yMax - 40f - _box.sizeDelta.y, _box.sizeDelta.x, _box.sizeDelta.y);
        bool overlap = _hasTarget && topLeft.Overlaps(_holeTarget);
        _box.anchorMin = _box.anchorMax = overlap ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        _box.pivot = overlap ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        _box.anchoredPosition = overlap ? new Vector2(40f, 40f) : new Vector2(40f, -40f);
    }

    // ───────────── появление / исчезновение ─────────────

    IEnumerator ShowBox()
    {
        PixelSfx.Play(Sfx.Open, 0.8f);
        _rootGroup.alpha = 1f;
        yield return Tween(0.35f, k => _box.localScale = Vector3.one * BackOut(k));
        _box.localScale = Vector3.one;
    }

    IEnumerator Fade(float to)
    {
        float from = _rootGroup.alpha;
        _rootGroup.blocksRaycasts = to > 0.5f;
        yield return Tween(0.25f, k => _rootGroup.alpha = Mathf.Lerp(from, to, k));
    }

    IEnumerator HideAll()
    {
        _dimAlpha = 0f;
        _hasTarget = false;
        if (_box != null) yield return Tween(0.25f, k => _box.localScale = Vector3.one * (1f - k));
        yield return Fade(0f);
    }

    // ───────────── помощники ─────────────

    static IEnumerator Punch(RectTransform rt, float amount)
    {
        if (rt == null) yield break;
        Vector3 baseScale = rt.localScale;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
        {
            if (rt == null) yield break;
            rt.localScale = baseScale * (1f + amount * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        if (rt != null) rt.localScale = baseScale;
    }

    static IEnumerator Tween(float time, System.Action<float> step)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / time) { step(t); yield return null; }
        step(1f);
    }

    static IEnumerator Wait(float s)
    {
        for (float t = 0f; t < s; t += Time.unscaledDeltaTime) yield return null;
    }

    static float BackOut(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.6f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI NewText(RectTransform parent, string text, float size, TextAlignmentOptions align)
    {
        var rt = NewRect("Text", parent);
        Stretch(rt);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }
}
