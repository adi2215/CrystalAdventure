using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Кнопки команд без прокрутки (идея обсуждена с автором): вместо длинного ряда, который надо листать, —
///
///   ↖ ↗ │ F1 F2 ⟳          компас 2×2: стрелки стоят так же, как куб ходит по полю
///   ↙ ↘ │ 🔴 🟡 ⌫          справа сверху — функции и цикл, снизу — краски и ластик
///
/// Раскладка всегда одна и та же: карточки, которых нет на уровне, показаны тёмными гнёздами (компас и
/// верхний правый ряд), чтобы игрок с первого уровня привыкал, где какая кнопка. Ряд красок — только на
/// уровнях с цветными клетками. Полоса прокрутки и стрелки ◄ ► прячутся, ScrollRect выключается.
///
/// Работает во время игры для каждой панели с кнопками (SpawnScript с HorizontalLayoutGroup внутри ScrollRect):
/// Main, F1, F2 — сцену менять не нужно. Раскладка обновляется сама, когда меняется набор видимых кнопок
/// (загрузка уровня, PaintPalette добавила краски, SpawnScript создал кнопку новой карточки).
/// Откат на старый ряд с прокруткой: CommandDock.Enabled = false (или убрать этот файл).
/// </summary>
public class CommandDock : MonoBehaviour
{
    public static bool Enabled = true;

    /// <summary>
    /// Насколько область кнопок шире прежнего ряда (Scroll View), в единицах канваса: слева, справа, снизу, сверху.
    /// Полоса прокрутки спрятана, поэтому место под ней и по бокам до рамки консоли свободно
    /// (рамка консоли — 5 пикселей рисунка ≈ 43 единицы по бокам и 50 сверху; ряд слотов начинается на 17 ниже ряда).
    /// Снизу больше: ряд слотов под доком уменьшается до размера кнопок (ShrinkSlots) и опускается.
    /// </summary>
    public static Vector4 Grow = new Vector4(10f, 10f, 26f, 14f);

    const float ArtPixel = 6f;     // пиксель гнёзд — как у рамки консоли
    const float ButtonArt = 13f;   // кнопки команд нарисованы 13×13 пикселей

    static readonly string[] Compass = { "Forward", "Right", "Left", "Bottom" };     // ↖ ↗ / ↙ ↘
    static readonly string[] Specials = { "FunctionOne", "FunctionTwo", "While" };
    static readonly string[] Paints = { "PaintRed", "PaintBlue", "PaintYellow", "PaintEraser" };

    SpawnScript _spawn;
    ScrollRect _scroll;
    RectTransform _area;
    readonly List<Image> _sockets = new List<Image>();
    Image _tray, _divider;
    string _signature;
    int _w, _h;
    Vector2 _areaSize;
    float _areaScale;
    static Sprite _socketSprite, _traySprite, _dividerSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Attach();

    static void Attach()
    {
        if (!Enabled) return;
        foreach (var spawn in FindObjectsOfType<SpawnScript>(true))
        {
            if (spawn.GetComponent<CommandDock>() != null) continue;
            var scroll = spawn.GetComponentInParent<ScrollRect>(true);
            if (scroll == null || spawn.GetComponent<HorizontalOrVerticalLayoutGroup>() == null) continue;
            var dock = spawn.gameObject.AddComponent<CommandDock>();
            dock._spawn = spawn;
            dock._scroll = scroll;
        }
    }

    void Start()
    {
        if (_scroll == null) { enabled = false; return; }
        // Исправлено: область — сам Scroll View, а не Viewport. У Main в сцене Viewport нулевого размера
        // (его растягивал ScrollRect при раскладке, а мы выключаем ScrollRect раньше первой раскладки),
        // поэтому кнопки Main ставились в точку и обрезались маской — на панели ничего не было видно.
        _area = (RectTransform)_scroll.transform;
        var viewport = _scroll.viewport;
        if (viewport != null)
        {
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.sizeDelta = Vector2.zero; viewport.anchoredPosition = Vector2.zero;
            // Ряд больше не листается — маска не нужна, а кнопки могут выходить за прежний ряд (см. Grow).
            var mask = viewport.GetComponent<Mask>();
            if (mask != null) mask.enabled = false;
            var rectMask = viewport.GetComponent<RectMask2D>();
            if (rectMask != null) rectMask.enabled = false;
        }
        // Полупрозрачная скруглённая подложка ряда не совпадает с новой раскладкой — пустые места показывают гнёзда.
        var tray = _scroll.GetComponent<Image>();
        if (tray != null) tray.enabled = false;

        // Ряд больше не листается: выключаем раскладку ряда, прокрутку, полосу и стрелки ◄ ►.
        var layout = GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;
        if (_scroll.horizontalScrollbar != null) _scroll.horizontalScrollbar.gameObject.SetActive(false);
        if (_scroll.verticalScrollbar != null) _scroll.verticalScrollbar.gameObject.SetActive(false);
        _scroll.StopMovement();
        if (_scroll.content != null) _scroll.content.anchoredPosition = Vector2.zero;
        _scroll.enabled = false;
        // Туториал докручивает ряд к нужной кнопке через normalizedPosition — это сдвинуло бы разложенные кнопки.
        _scroll.horizontal = false;
        _scroll.vertical = false;
        HideArrows(_scroll.transform);
        if (_scroll.transform.parent != null) HideArrows(_scroll.transform.parent);
    }

    /// <summary>Стрелки ◄ ► у ряда (картинки Strelka) больше не нужны.</summary>
    static void HideArrows(Transform parent)
    {
        foreach (Transform t in parent)
        {
            var img = t.GetComponent<Image>();
            if (img != null && img.sprite != null && img.sprite.name.StartsWith("Strelka")) t.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (_area == null) return;
        // Исправлено: панель Main видна с первого кадра, когда раскладка интерфейса ещё не посчитана —
        // область была нулевой, кнопки получались крошечными и больше не пересчитывались.
        // Теперь ждём настоящий размер области и пересобираем, если он изменился.
        var size = AreaRect().size;
        if (size.x < 1f || size.y < 1f) return;
        // Консоль может появляться с анимацией масштаба (от 0): тогда размер кнопки в пикселях экрана
        // считался бы по почти нулевому масштабу и кнопки улетали за маску. Ждём нормальный масштаб
        // и пересобираем, когда он меняется.
        float scale = AreaScale();
        if (scale < 0.05f) return;
        bool sizeChanged = (size - _areaSize).sqrMagnitude > 0.25f || Mathf.Abs(scale - _areaScale) > 0.005f;
        // Пересобрать, только если изменился набор видимых кнопок, размер области или экрана.
        var sb = new StringBuilder();
        foreach (Transform t in transform) if (!t.name.StartsWith("Dock")) sb.Append(t.name).Append(t.gameObject.activeSelf ? '+' : '-');
        string sig = sb.ToString();
        if (sig == _signature && !sizeChanged && Screen.width == _w && Screen.height == _h) return;
        _signature = sig; _w = Screen.width; _h = Screen.height; _areaSize = size; _areaScale = scale;
        Layout();
    }

    void Layout()
    {
        var buttons = new Dictionary<string, RectTransform>();
        foreach (Transform t in transform)
            if (!t.name.StartsWith("Dock")) buttons[t.name] = (RectTransform)t;

        var paints = new List<RectTransform>();
        foreach (var n in Paints)
            if (buttons.TryGetValue(n, out var p) && p.gameObject.activeSelf) paints.Add(p);
        bool paintRow = paints.Count > 0;

        // Размер клетки: два ряда по высоте области; по ширине — компас (2) + промежуток + правый блок.
        var canvas = GetComponentInParent<Canvas>();
        float sf = canvas != null && canvas.rootCanvas.scaleFactor > 0.0001f ? canvas.rootCanvas.scaleFactor : 1f;
        float areaScale = Mathf.Max(0.0001f, AreaScale());
        var rect = AreaRect();
        int rightCols = Mathf.Max(Specials.Length, paints.Count);
        // u — пиксель рисунка подложки и разделителя (как у рамки консоли), целое число пикселей экрана.
        float px = areaScale * sf;
        float u = Mathf.Max(1f, Mathf.Round(ArtPixel * px)) / px;
        float gap = u;
        float spacer = u * 3f;   // между компасом и правым блоком: разделитель шириной u и по u воздуха
        float pad = u;           // поле подложки вокруг кнопок сверху и по бокам
        float padBottom = u * 2f; // снизу шире — у кнопок тень снизу, иначе ряд упирается в край подложки
        float cell = Mathf.Min((rect.height - gap - pad - padBottom) / 2f,
                               (rect.width - spacer - gap * rightCols - pad * 2f) / (2 + rightCols));
        // Кнопка — целое число пикселей экрана на пиксель рисунка 13×13.
        int k = Mathf.Max(1, Mathf.FloorToInt(cell * px / ButtonArt));
        cell = ButtonArt * k / px;

        float blockW = cell * (2 + rightCols) + gap * rightCols + spacer;
        float left = rect.center.x - blockW * 0.5f;
        // Подложка (с полями) — по центру области; блок кнопок внутри неё чуть выше из-за поля снизу.
        float blockCenterY = rect.center.y + (padBottom - pad) * 0.5f;
        float top = blockCenterY + (cell * 2f + gap) * 0.5f;
        Vector2 CellCenter(int col, int row, bool right) =>
            new Vector2(left + (right ? cell * 2f + gap + spacer : 0f) + col * (cell + gap) + cell * 0.5f,
                        top - row * (cell + gap) - cell * 0.5f);

        // Подложка под всем блоком и разделитель «ходы | функции».
        float blockH = cell * 2f + gap;
        var tray = Tray();
        Place(tray.rectTransform, rect.center, new Vector2(blockW + pad * 2f, blockH + pad + padBottom));
        tray.pixelsPerUnitMultiplier = 1f / u;
        var divider = Divider();
        float dividerX = left + cell * 2f + gap + spacer * 0.5f;
        Place(divider.rectTransform, new Vector2(dividerX, blockCenterY), new Vector2(u, blockH));
        divider.pixelsPerUnitMultiplier = 1f / u;

        ShrinkSlots(k, sf);

        int socket = 0;
        for (int i = 0; i < Compass.Length; i++)
            Put(buttons, Compass[i], CellCenter(i % 2, i / 2, false), cell, ref socket);
        for (int i = 0; i < Specials.Length; i++)
            Put(buttons, Specials[i], CellCenter(i, 0, true), cell, ref socket);
        if (paintRow)
            for (int i = 0; i < paints.Count; i++) Place(paints[i], CellCenter(i, 1, true), cell);
        for (int i = socket; i < _sockets.Count; i++) if (_sockets[i] != null) _sockets[i].gameObject.SetActive(false);
        // Порядок отрисовки: подложка, разделитель, гнёзда, кнопки.
        foreach (var sk in _sockets) if (sk != null) sk.transform.SetAsFirstSibling();
        divider.transform.SetAsFirstSibling();
        tray.transform.SetAsFirstSibling();

        // Прочие кнопки (неизвестные карточки) — в конец нижнего правого ряда, чтобы не пропали.
        int extra = paintRow ? paints.Count : 0;
        foreach (var kv in buttons)
        {
            string n = kv.Key;
            if (System.Array.IndexOf(Compass, n) >= 0 || System.Array.IndexOf(Specials, n) >= 0 || System.Array.IndexOf(Paints, n) >= 0) continue;
            if (!kv.Value.gameObject.activeSelf) continue;
            Place(kv.Value, CellCenter(extra++, 1, true), cell);
        }
    }

    GridLayoutGroup _slots;
    Vector3 _slotsBaseScale;
    float _slotsFactor = 1f;

    /// <summary>
    /// Ряд слотов программы под доком (GridLayoutGroup «Buttonsbar» той же панели) — уменьшить до размера
    /// кнопок дока: тот же пиксель рисунка (13×13), всё влезает и смотрится одним набором (просьба автора).
    /// Уменьшается масштабом всего ряда — вместе со слотами уменьшаются и карточки в них. Коэффициент —
    /// отношение целых «пикселей экрана на пиксель рисунка» (например 10/11 на 1080p), поэтому пиксели
    /// остаются ровными, а PixelSnapUI при пересчёте получит то же самое. Только уменьшение, никогда не больше.
    /// </summary>
    void ShrinkSlots(int k, float sf)
    {
        if (_slots == null)
        {
            for (var p = _area.parent; p != null && _slots == null; p = p.parent)
            {
                foreach (var g in p.GetComponentsInChildren<GridLayoutGroup>(true))
                    if (!g.transform.IsChildOf(transform)) { _slots = g; break; }
                if (p.GetComponent<Canvas>() != null) break;
            }
            if (_slots == null) return;
            _slotsBaseScale = _slots.transform.localScale;
        }
        var canvas = GetComponentInParent<Canvas>();
        float root = canvas != null ? canvas.rootCanvas.transform.lossyScale.x : 1f;
        if (Mathf.Abs(root) < 0.000001f || _slotsFactor <= 0f) return;
        // Масштаб ряда без нашего уменьшения, относительно канваса.
        float rel = Mathf.Abs(_slots.transform.lossyScale.x / root) / _slotsFactor;
        // Сколько пикселей экрана на пиксель рисунка слот получил бы сам (как считает PixelSnapUI).
        float raw = PixelSnapUI.OriginalCell(_slots).x * rel * sf / ButtonArt;
        int own = Mathf.FloorToInt(raw);
        float factor = own > k && k > 0 ? (float)k / own : 1f;
        if (Mathf.Abs(factor - _slotsFactor) < 0.0001f) return;
        _slotsFactor = factor;
        _slots.transform.localScale = new Vector3(_slotsBaseScale.x * factor, _slotsBaseScale.y * factor, _slotsBaseScale.z);
    }

    /// <summary>Область раскладки: прежний ряд, расширенный на Grow (в локальных координатах Scroll View).</summary>
    Rect AreaRect()
    {
        var r = _area.rect;
        return Rect.MinMaxRect(r.xMin - Grow.x, r.yMin - Grow.z, r.xMax + Grow.y, r.yMax + Grow.w);
    }

    /// <summary>Масштаб области относительно корневого канваса (1 — без анимаций и вложенных масштабов).</summary>
    float AreaScale()
    {
        var canvas = GetComponentInParent<Canvas>();
        float root = canvas != null ? canvas.rootCanvas.transform.lossyScale.x : 1f;
        if (Mathf.Abs(root) < 0.000001f) return 0f;
        return Mathf.Abs(_area.lossyScale.x / root);
    }

    /// <summary>Кнопка на своё место; если её нет на уровне — тёмное гнездо.</summary>
    void Put(Dictionary<string, RectTransform> buttons, string name, Vector2 center, float cell, ref int socket)
    {
        if (buttons.TryGetValue(name, out var b) && b.gameObject.activeSelf) { Place(b, center, cell); return; }
        var s = Socket(socket++);
        s.gameObject.SetActive(true);
        Place(s.rectTransform, center, cell * 0.86f);
    }

    /// <summary>Поставить элемент в точку области (её локальные координаты), размер — cell × cell.</summary>
    void Place(RectTransform rt, Vector2 areaPoint, float cell) => Place(rt, areaPoint, new Vector2(cell, cell));

    /// <summary>Поставить элемент в точку области (её локальные координаты), размер — в единицах области.</summary>
    void Place(RectTransform rt, Vector2 areaPoint, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = _area.TransformPoint(areaPoint);
        float rel = rt.parent != null && rt.parent.lossyScale.x > 0.0001f ? _area.lossyScale.x / rt.parent.lossyScale.x : 1f;
        rt.sizeDelta = size * rel;
        var img = rt.GetComponent<Image>();
        if (img != null) PixelSnapUI.Exclude(img); // размер задаёт док, не PixelSnapUI
    }

    /// <summary>Новая картинка дока (подложка, разделитель, гнездо) — дочерняя ряда кнопок, без нажатий.</summary>
    Image MakeImage(string name, Sprite sprite, Image.Type type)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = type;
        img.raycastTarget = false;
        PixelSnapUI.Exclude(img);
        return img;
    }

    Image Tray()
    {
        if (_tray == null) _tray = MakeImage("DockTray", TraySprite, Image.Type.Sliced);
        _tray.gameObject.SetActive(true);
        return _tray;
    }

    Image Divider()
    {
        if (_divider == null) _divider = MakeImage("DockDivider", DividerSprite, Image.Type.Tiled);
        _divider.gameObject.SetActive(true);
        return _divider;
    }

    Image Socket(int index)
    {
        while (_sockets.Count <= index)
        {
            _sockets.Add(MakeImage("DockSocket" + _sockets.Count, SocketSprite, Image.Type.Sliced));
        }
        var s = _sockets[index];
        var canvas = GetComponentInParent<Canvas>();
        float sf = canvas != null && canvas.rootCanvas.scaleFactor > 0.0001f ? canvas.rootCanvas.scaleFactor : 1f;
        float u = Mathf.Max(1f, Mathf.Round(ArtPixel * sf)) / sf;
        s.pixelsPerUnitMultiplier = 1f / u;
        return s;
    }

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    /// <summary>
    /// Подложка дока — утопленная в консоль тёмная плита: тёмный контур, тень под верхним краем,
    /// светлая кромка снизу (свет консоли падает сверху). Цвета — холодные, из палитры консоли (#3B445C).
    /// Sliced, рамка 2 px.
    /// </summary>
    static Sprite TraySprite
    {
        get
        {
            if (_traySprite != null) return _traySprite;
            var pal = new Dictionary<char, Color>
            {
                { '#', Hex("#1F2433") }, { 't', Hex("#262C3E") }, { 'k', Hex("#2D3448") }, { 'q', Hex("#4A5570") },
            };
            _traySprite = PixelSprites.Make(new[]
            {
                ".#####.",
                "#ttttt#",
                "#tkkkk#",
                "#kkkkk#",
                "#kkkkk#",
                "#qqqqq#",
                ".#####.",
            }, pal, new Vector4(2, 2, 2, 2));
            return _traySprite;
        }
    }

    /// <summary>Разделитель «ходы | функции»: пунктир из светлых точек через одну (Tiled, 1×2).</summary>
    static Sprite DividerSprite
    {
        get
        {
            if (_dividerSprite != null) return _dividerSprite;
            var pal = new Dictionary<char, Color> { { 'o', Hex("#56627F") } };
            _dividerSprite = PixelSprites.Make(new[] { "o", "." }, pal);
            return _dividerSprite;
        }
    }

    /// <summary>Тёмное утопленное гнездо: контур, тень сверху, чуть светлее снизу. Sliced, рамка 2 px.</summary>
    static Sprite SocketSprite
    {
        get
        {
            if (_socketSprite != null) return _socketSprite;
            Color top = Hex("#1B2030"), mid = Hex("#222837"), low = Hex("#353E55");
            var pal = new Dictionary<char, Color> { { '#', Hex("#161A26") }, { 't', top }, { 'k', mid }, { 'q', low } };
            _socketSprite = PixelSprites.Make(new[]
            {
                ".#####.",
                "#ttttt#",
                "#tkkkt#",
                "#kkkkk#",
                "#kkkkk#",
                "#qqqqq#",
                ".#####.",
            }, pal, new Vector4(2, 2, 2, 2));
            return _socketSprite;
        }
    }
}
