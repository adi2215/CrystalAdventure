using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Меню уровней — «карта главы». Создаётся само при загрузке игровой сцены, в сцене ничего настраивать не нужно.
///
/// • Внизу слева кнопка «Levels» открывает карту.
/// • Уровни идут змейкой по главам (Forest / Winter / Sky — по 15), между ними — тропинка из точек.
/// • Пройденные — зелёные со звёздами, текущий — золотой, над ним прыгает куб, закрытые — серые с замком.
/// • Клик по уровню — карта закрывается, куб улетает, облака, уровень загружается.
/// • Старые кнопки «1 2 3 …» внизу слева прячутся автоматически.
///
/// В редакторе все уровни открыты (для проверки), в сборке — по порядку прохождения.
/// </summary>
public class LevelMapMenu : MonoBehaviour
{
    // ─────────── настройки (меняются здесь, в коде) ───────────
    static readonly string[] ChapterNames = { "Forest", "Winter", "Sky" };
    const int LevelsPerChapter = 15;
    const int Columns = 5;
    const float CellW = 250f, CellH = 230f, NodeSize = 130f;

    static readonly Color Backdrop = new Color(0.12f, 0.08f, 0.18f, 0.94f);
    static readonly Color NodeDone = new Color(0.47f, 0.86f, 0.55f, 1f);
    static readonly Color NodeCurrent = new Color(1f, 0.80f, 0.32f, 1f);
    static readonly Color NodeLocked = new Color(0.42f, 0.38f, 0.50f, 1f);
    static readonly Color StarOn = new Color(1f, 0.85f, 0.25f, 1f);
    static readonly Color StarOff = new Color(0.25f, 0.2f, 0.3f, 0.9f);
    static readonly Color Ink = new Color32(66, 38, 54, 255);

    // ─────────── запуск ───────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Create();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Create();

    static void Create()
    {
        if (FindObjectOfType<LevelMapMenu>() != null) return;
        if (FindObjectOfType<MovingManager>() == null) return; // только игровая сцена
        new GameObject("LevelMapMenu").AddComponent<LevelMapMenu>();
    }

    // ─────────── состояние ───────────

    Canvas _canvas;
    RectTransform _root;          // вся карта
    CanvasGroup _group;
    RectTransform _content;       // прокручиваемое содержимое
    GameObject _openButton;
    TMP_FontAsset _font;
    Sprite _heroSprite;
    bool _open, _busy;
    readonly List<RectTransform> _nodes = new List<RectTransform>();
    readonly List<Image> _dots = new List<Image>();
    RectTransform _heroIcon;

    void Start()
    {
        var anyText = FindObjectOfType<TextMeshProUGUI>(true);
        _font = anyText != null ? anyText.font : null;
        var hero = FindObjectOfType<CharacterCode>();
        _heroSprite = hero != null && hero.MainRenderer != null ? hero.MainRenderer.sprite : null;

        HideOldLevelButtons();
        BuildCanvas();
        BuildOpenButton();
    }

    void Update()
    {
        if (_openButton != null)
        {
            bool show = !_open && !Levelloader.IsLoading;
            if (_openButton.activeSelf != show) _openButton.SetActive(show);
        }
        if (_open && !_busy && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    /// <summary>Старая панель с кнопками «1 2 3 …» (они вызывают Levelloader.LoadLevelIndex2) — прячем.</summary>
    static void HideOldLevelButtons()
    {
        var parents = new HashSet<Transform>();
        foreach (var b in FindObjectsOfType<Button>(true))
        {
            var e = b.onClick;
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) is Levelloader && e.GetPersistentMethodName(i) == "LoadLevelIndex2")
                {
                    if (b.transform.parent != null) parents.Add(b.transform.parent);
                    b.gameObject.SetActive(false);
                }
        }
        foreach (var p in parents)
        {
            bool onlyLevelButtons = true;
            foreach (Transform child in p)
                if (child.gameObject.activeSelf) { onlyLevelButtons = false; break; }
            if (onlyLevelButtons) p.gameObject.SetActive(false);
        }
    }

    // ─────────── холст ───────────

    void BuildCanvas()
    {
        var go = new GameObject("LevelMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 400;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = NewRect("Map", go.transform);
        Stretch(_root);
        _group = _root.gameObject.AddComponent<CanvasGroup>();
        _root.gameObject.SetActive(false);
    }

    void BuildOpenButton()
    {
        var btn = NewRect("LevelsButton", _canvas.transform);
        btn.anchorMin = btn.anchorMax = new Vector2(0f, 0f);
        btn.pivot = new Vector2(0f, 0f);
        btn.anchoredPosition = new Vector2(28f, 28f);
        btn.sizeDelta = new Vector2(210f, 84f);
        var img = btn.gameObject.AddComponent<Image>();
        SetPixelPanel(img, NodeLocked * 1.15f);
        var b = btn.gameObject.AddComponent<Button>();
        b.onClick.AddListener(Open);

        var icon = NewRect("Icon", btn);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(48f, 2f);
        icon.sizeDelta = new Vector2(42f, 42f);
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.sprite = GridIcon();
        iconImg.raycastTarget = false;

        var label = Text(btn, "Levels", 34f, TextAlignmentOptions.MidlineLeft);
        label.rectTransform.offsetMin = new Vector2(84f, 0f);
        ButtonJuice.Ensure(btn.gameObject);
        _openButton = btn.gameObject;
    }

    // ─────────── открыть / закрыть ───────────

    public void Open()
    {
        if (_open || _busy || Levelloader.IsLoading) return;
        var moving = FindObjectOfType<MovingManager>();
        if (moving != null && moving.State == MovingManager.RunState.Playing) return; // куб едет — не мешаем
        StartCoroutine(OpenRoutine());
    }

    public void Close()
    {
        if (!_open || _busy) return;
        StartCoroutine(CloseRoutine(-1));
    }

    IEnumerator OpenRoutine()
    {
        PixelSfx.Play(Sfx.Open);
        _busy = true;
        _open = true;
        _group.alpha = 0f;
        _root.gameObject.SetActive(true); // сначала включить: компоненты должны проснуться с нормальным масштабом
        BuildMap();

        yield return Tween(0.25f, k => _group.alpha = k);

        // Узлы выскакивают по очереди, тропинка дорисовывается следом.
        int dot = 0;
        for (int i = 0; i < _nodes.Count; i++)
        {
            StartCoroutine(Pop(_nodes[i], 0.35f));
            PixelSfx.Play(Sfx.Blip, 0.35f, 0.8f + 0.04f * (i % 12), 0.03f);
            int until = Mathf.RoundToInt((i + 1f) / _nodes.Count * _dots.Count);
            for (; dot < until; dot++) StartCoroutine(Pop(_dots[dot].rectTransform, 0.2f));
            yield return Wait(0.035f);
        }
        if (_heroIcon != null) StartCoroutine(HeroLand(_heroIcon));
        _busy = false;
    }

    IEnumerator CloseRoutine(int openLevel)
    {
        PixelSfx.Play(Sfx.Close);
        _busy = true;
        yield return Tween(0.2f, k => _group.alpha = 1f - k);
        _root.gameObject.SetActive(false);
        _open = false;
        _busy = false;
        if (openLevel >= 0)
        {
            var loader = FindObjectOfType<Levelloader>();
            if (loader != null) loader.OpenLevel(openLevel);
        }
    }

    // ─────────── карта ───────────

    void BuildMap()
    {
        foreach (Transform child in _root) Destroy(child.gameObject);
        _nodes.Clear();
        _dots.Clear();
        _heroIcon = null;

        var data = FindObjectOfType<TilemapData>();
        int count = data != null && data.tilemapData != null ? data.tilemapData.Length : 0;
        var progress = LoadProgress(count);
        int current = 0;
        while (current < count - 1 && progress[current] > 0) current++;

        // Фон
        var bg = NewRect("Backdrop", _root);
        Stretch(bg);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = Backdrop;
        StartCoroutine(FloatingSparkles(bg));

        // Заголовок
        int chapter = Mathf.Clamp(current / LevelsPerChapter, 0, ChapterNames.Length - 1);
        var title = Text(_root, ChapterNames[chapter], 88f, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 120f));
        title.color = NodeCurrent;
        var sub = Text(_root, "Choose a level", 34f, TextAlignmentOptions.Center);
        Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -165f), new Vector2(900f, 50f));
        sub.color = new Color(1f, 1f, 1f, 0.7f);

        // Звёзды всего
        int total = 0;
        foreach (var s in progress) total += s;
        var starBox = NewRect("Stars", _root);
        Anchor(starBox, new Vector2(1f, 1f), new Vector2(-250f, -70f), new Vector2(260f, 70f));
        var starIcon = NewRect("Icon", starBox);
        starIcon.anchorMin = starIcon.anchorMax = new Vector2(0f, 0.5f);
        starIcon.anchoredPosition = new Vector2(30f, 0f);
        starIcon.sizeDelta = new Vector2(48f, 48f);
        var si = starIcon.gameObject.AddComponent<Image>();
        si.sprite = StarSprite();
        si.color = StarOn;
        var starText = Text(starBox, $"{total} / {count * 3}", 38f, TextAlignmentOptions.MidlineLeft);
        starText.rectTransform.offsetMin = new Vector2(66f, 0f);

        // Закрыть
        var close = NewRect("Close", _root);
        Anchor(close, new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(84f, 84f));
        SetPixelPanel(close.gameObject.AddComponent<Image>(), NodeLocked);
        close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        Text(close, "X", 44f, TextAlignmentOptions.Center);
        ButtonJuice.Ensure(close.gameObject);

        // Прокручиваемая область с узлами
        var view = NewRect("View", _root);
        view.anchorMin = new Vector2(0f, 0f);
        view.anchorMax = new Vector2(1f, 1f);
        view.offsetMin = new Vector2(80f, 40f);
        view.offsetMax = new Vector2(-80f, -220f);
        view.gameObject.AddComponent<RectMask2D>();
        var viewImg = view.gameObject.AddComponent<Image>();
        viewImg.color = new Color(0f, 0f, 0f, 0f); // ловит прокрутку
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        _content = NewRect("Content", view);
        _content.anchorMin = new Vector2(0.5f, 1f);
        _content.anchorMax = new Vector2(0.5f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        scroll.content = _content;
        scroll.viewport = view;

        // Позиции: главы по 15 уровней, внутри — змейка по 5 в ряд.
        var positions = new List<Vector2>();
        float y = -60f;
        for (int i = 0; i < count; i++)
        {
            int inChapter = i % LevelsPerChapter;
            if (inChapter == 0 && i > 0)
            {
                y -= CellH * 0.6f;
                var header = Text(_content, ChapterNames[Mathf.Min(i / LevelsPerChapter, ChapterNames.Length - 1)], 48f, TextAlignmentOptions.Center);
                Anchor(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y + 40f), new Vector2(600f, 70f));
                header.color = NodeCurrent;
                y -= 70f;
            }
            int row = inChapter / Columns, col = inChapter % Columns;
            if (row % 2 == 1) col = Columns - 1 - col; // змейка
            if (inChapter > 0 && col == (row % 2 == 1 ? Columns - 1 : 0)) y -= CellH;
            float x = (col - (Columns - 1) * 0.5f) * CellW;
            positions.Add(new Vector2(x, y - NodeSize * 0.5f));
        }
        float height = positions.Count > 0 ? -positions[positions.Count - 1].y + NodeSize + 80f : 200f;
        _content.sizeDelta = new Vector2(Columns * CellW, height);

        // Тропинка из точек (под узлами)
        for (int i = 1; i < positions.Count; i++)
        {
            Vector2 a = positions[i - 1], b = positions[i];
            float dist = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.FloorToInt((dist - NodeSize) / 28f));
            for (int d = 1; d <= n; d++)
            {
                var p = Vector2.Lerp(a, b, (NodeSize * 0.5f + d * (dist - NodeSize) / (n + 1)) / dist);
                var dotRt = NewRect("Dot", _content);
                Anchor(dotRt, new Vector2(0.5f, 1f), p, new Vector2(12f, 12f));
                var img = dotRt.gameObject.AddComponent<Image>();
                img.sprite = DotSprite();
                img.raycastTarget = false;
                img.color = progress[i - 1] > 0 ? new Color(NodeDone.r, NodeDone.g, NodeDone.b, 0.9f) : new Color(1f, 1f, 1f, 0.25f);
                dotRt.localScale = Vector3.zero;
                _dots.Add(img);
            }
        }

        // Узлы
        for (int i = 0; i < count; i++)
        {
            bool done = progress[i] > 0;
            bool unlocked = i == 0 || progress[i - 1] > 0 || done || UnlockAll;
            bool isCurrent = i == current;
            _nodes.Add(BuildNode(i, positions[i], progress[i], done, unlocked, isCurrent));
        }

        // Прокрутить к текущему уровню
        if (current < positions.Count)
        {
            float target = Mathf.Max(0f, -positions[current].y - 250f);
            _content.anchoredPosition = new Vector2(0f, target);
        }
    }

    RectTransform BuildNode(int index, Vector2 pos, int stars, bool done, bool unlocked, bool current)
    {
        var node = NewRect($"Level{index + 1}", _content);
        Anchor(node, new Vector2(0.5f, 1f), pos, new Vector2(NodeSize, NodeSize));
        var img = node.gameObject.AddComponent<Image>();
        SetPixelPanel(img, !unlocked ? NodeLocked : current ? NodeCurrent : done ? NodeDone : new Color(0.95f, 0.75f, 0.45f, 1f));

        if (unlocked)
        {
            var num = Text(node, (index + 1).ToString(), 64f, TextAlignmentOptions.Center);
            num.rectTransform.offsetMin = new Vector2(0f, 26f);
            num.color = Ink;
            num.fontStyle = FontStyles.Bold;

            // три звезды под номером
            for (int s = 0; s < 3; s++)
            {
                var st = NewRect("Star", node);
                st.anchorMin = st.anchorMax = new Vector2(0.5f, 0f);
                st.anchoredPosition = new Vector2((s - 1) * 34f, 26f + (s == 1 ? 6f : 0f));
                st.sizeDelta = new Vector2(30f, 30f);
                var si = st.gameObject.AddComponent<Image>();
                si.sprite = StarSprite();
                si.color = s < stars ? StarOn : StarOff;
                si.raycastTarget = false;
            }

            var button = node.gameObject.AddComponent<Button>();
            int level = index;
            button.onClick.AddListener(() => OnLevelClicked(level, node));
            var juice = ButtonJuice.Ensure(node.gameObject, wobble: current);
            juice.breathe = current;
        }
        else
        {
            var lockRt = NewRect("Lock", node);
            lockRt.anchorMin = lockRt.anchorMax = new Vector2(0.5f, 0.5f);
            lockRt.sizeDelta = new Vector2(56f, 72f);
            var li = lockRt.gameObject.AddComponent<Image>();
            li.sprite = LockSprite();
            li.color = new Color(0.2f, 0.17f, 0.26f, 1f);
            li.raycastTarget = false;
            var button = node.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => { var j = node.GetComponent<ButtonJuice>(); if (j != null) j.Deny(); });
            ButtonJuice.Ensure(node.gameObject);
        }

        // Спрятать до анимации появления (ButtonJuice выключен, иначе он сразу вернёт масштаб).
        var nodeJuice = node.GetComponent<ButtonJuice>();
        if (nodeJuice != null) nodeJuice.enabled = false;
        node.localScale = Vector3.zero;

        if (current && _heroSprite != null)
        {
            var hero = NewRect("Hero", _content);
            Anchor(hero, new Vector2(0.5f, 1f), pos + new Vector2(0f, NodeSize * 0.5f + 40f), new Vector2(70f, 70f));
            var hi = hero.gameObject.AddComponent<Image>();
            hi.sprite = _heroSprite;
            hi.preserveAspect = true;
            hi.raycastTarget = false;
            hero.localScale = Vector3.zero;
            _heroIcon = hero;
        }
        return node;
    }

    void OnLevelClicked(int level, RectTransform node)
    {
        if (_busy) return;
        StartCoroutine(PickLevel(level, node));
    }

    IEnumerator PickLevel(int level, RectTransform node)
    {
        _busy = true;
        PixelFX.BurstUI(node, PixelFX.StarColors, 12, 300f, 18f, 0.6f);
        PixelSfx.Play(Sfx.Crystal, 0.8f);
        yield return Wait(0.25f);
        _busy = false;
        yield return CloseRoutine(level);
    }

    // ─────────── прогресс ───────────

#if UNITY_EDITOR
    const bool UnlockAll = true;   // в редакторе можно открыть любой уровень для проверки
#else
    const bool UnlockAll = false;
#endif

    static int[] LoadProgress(int count)
    {
        var stars = new int[count];
        var db = SaveManager.Load();
        if (db != null && db.levels != null)
            foreach (var l in db.levels)
                if (l != null && l.levelNumber >= 0 && l.levelNumber < count && l.levelCompleted)
                    stars[l.levelNumber] = Mathf.Max(stars[l.levelNumber], l.starsEarned);
        return stars;
    }

    // ─────────── анимации ───────────

    IEnumerator Pop(RectTransform rt, float time)
    {
        if (rt == null) yield break;
        var juice = rt.GetComponent<ButtonJuice>();
        if (juice != null) juice.enabled = false;
        yield return Tween(time, k => { if (rt != null) rt.localScale = Vector3.one * BackOut(k); });
        if (rt != null) rt.localScale = Vector3.one;
        if (juice != null)
        {
            juice.RebaseScale(Vector3.one);
            juice.enabled = true;
        }
    }

    IEnumerator HeroLand(RectTransform hero)
    {
        Vector2 target = hero.anchoredPosition;
        hero.localScale = Vector3.one;
        yield return Tween(0.4f, k =>
        {
            float h = (1f - k) * 220f;
            hero.anchoredPosition = target + new Vector2(0f, h);
        });
        // подпрыгивает на месте, пока карта открыта
        while (hero != null && _open)
        {
            for (float t = 0f; t < 1f && hero != null; t += Time.unscaledDeltaTime / 0.6f)
            {
                hero.anchoredPosition = target + new Vector2(0f, 18f * 4f * t * (1f - t));
                yield return null;
            }
            yield return Wait(0.5f);
        }
    }

    IEnumerator FloatingSparkles(RectTransform parent)
    {
        var parts = new List<(RectTransform rt, float speed, float phase)>();
        for (int i = 0; i < 18; i++)
        {
            var rt = NewRect("Sparkle", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(Random.value, Random.value);
            float s = Random.Range(10f, 22f);
            rt.sizeDelta = new Vector2(s, s);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PixelFX.Sparkle;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, Random.Range(0.08f, 0.25f));
            parts.Add((rt, Random.Range(10f, 30f), Random.value * 10f));
        }
        while (parent != null)
        {
            float t = Time.unscaledTime;
            foreach (var p in parts)
            {
                if (p.rt == null) continue;
                p.rt.anchoredPosition = new Vector2(Mathf.Sin(t * 0.5f + p.phase) * 20f, (t * p.speed + p.phase * 50f) % 120f - 60f);
            }
            yield return null;
        }
    }

    static IEnumerator Tween(float time, System.Action<float> step)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / time) { step(t); yield return null; }
        step(1f);
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    static float BackOut(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.5f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ─────────── UI-помощники ───────────

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

    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    TextMeshProUGUI Text(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var rt = NewRect("Text", parent);
        Stretch(rt);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    // ─────────── пиксельные спрайты (рисуются кодом) ───────────

    static Sprite _panel, _star, _lock, _grid, _dot;

    static void SetPixelPanel(Image img, Color color)
    {
        img.sprite = PanelSprite();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.2f; // каждый пиксель рисунка — 5 пикселей экрана
        img.color = color;
    }

    static Sprite Make(string[] rows, Dictionary<char, Color> palette, Vector4 border = default)
    {
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, palette.TryGetValue(rows[h - 1 - y][x], out var c) ? c : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    static Dictionary<char, Color> Pal => new Dictionary<char, Color>
    {
        { '#', new Color(0.26f, 0.15f, 0.21f, 1f) },  // контур
        { 'w', Color.white },                         // заливка (тонируется цветом узла)
        { 'h', new Color(1f, 1f, 1f, 1f) },
        { 's', new Color(0.72f, 0.72f, 0.72f, 1f) },  // тень снизу
    };

    static Sprite PanelSprite()
    {
        if (_panel != null) return _panel;
        string[] rows =
        {
            "..########..",
            ".#wwwwwwww#.",
            "#wwwwwwwwww#",
            "#wwwwwwwwww#",
            "#wwwwwwwwww#",
            "#wwwwwwwwww#",
            "#wwwwwwwwww#",
            "#wwwwwwwwww#",
            "#ssssssssss#",
            "#ssssssssss#",
            ".#ssssssss#.",
            "..########..",
        };
        _panel = Make(rows, Pal, new Vector4(4, 4, 4, 4));
        return _panel;
    }

    static Sprite StarSprite()
    {
        if (_star != null) return _star;
        string[] rows =
        {
            "....#....",
            "...#w#...",
            "...#w#...",
            "####w####",
            "#wwwwwww#",
            ".#wwwww#.",
            "..#w#w#..",
            ".#w#.#w#.",
            ".##...##.",
        };
        _star = Make(rows, Pal);
        return _star;
    }

    static Sprite LockSprite()
    {
        if (_lock != null) return _lock;
        string[] rows =
        {
            "..###..",
            ".#...#.",
            ".#...#.",
            "#######",
            "###.###",
            "###.###",
            "#######",
            "#######",
        };
        _lock = Make(rows, new Dictionary<char, Color> { { '#', Color.white } });
        return _lock;
    }

    static Sprite GridIcon()
    {
        if (_grid != null) return _grid;
        string[] rows =
        {
            "###.###",
            "#w#.#w#",
            "###.###",
            ".......",
            "###.###",
            "#w#.#w#",
            "###.###",
        };
        _grid = Make(rows, new Dictionary<char, Color> { { '#', Color.white }, { 'w', new Color(1f, 0.8f, 0.32f, 1f) } });
        return _grid;
    }

    static Sprite DotSprite()
    {
        if (_dot != null) return _dot;
        _dot = Make(new[] { ".##.", "####", "####", ".##." }, new Dictionary<char, Color> { { '#', Color.white } });
        return _dot;
    }
}
