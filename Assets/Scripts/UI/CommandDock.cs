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

    const float ArtPixel = 6f;     // пиксель гнёзд — как у рамки консоли
    const float ButtonArt = 13f;   // кнопки команд нарисованы 13×13 пикселей

    static readonly string[] Compass = { "Forward", "Right", "Left", "Bottom" };     // ↖ ↗ / ↙ ↘
    static readonly string[] Specials = { "FunctionOne", "FunctionTwo", "While" };
    static readonly string[] Paints = { "PaintRed", "PaintBlue", "PaintYellow", "PaintEraser" };

    SpawnScript _spawn;
    ScrollRect _scroll;
    RectTransform _area;
    readonly List<Image> _sockets = new List<Image>();
    string _signature;
    int _w, _h;
    Vector2 _areaSize;
    float _areaScale;
    static Sprite _socketSprite;

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
        _area = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;

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
        var size = _area.rect.size;
        if (size.x < 1f || size.y < 1f) return;
        // Консоль может появляться с анимацией масштаба (от 0): тогда размер кнопки в пикселях экрана
        // считался бы по почти нулевому масштабу и кнопки улетали за маску. Ждём нормальный масштаб
        // и пересобираем, когда он меняется.
        float scale = AreaScale();
        if (scale < 0.05f) return;
        bool sizeChanged = (size - _areaSize).sqrMagnitude > 0.25f || Mathf.Abs(scale - _areaScale) > 0.005f;
        // Пересобрать, только если изменился набор видимых кнопок, размер области или экрана.
        var sb = new StringBuilder();
        foreach (Transform t in transform) if (!t.name.StartsWith("DockSocket")) sb.Append(t.name).Append(t.gameObject.activeSelf ? '+' : '-');
        string sig = sb.ToString();
        if (sig == _signature && !sizeChanged && Screen.width == _w && Screen.height == _h) return;
        _signature = sig; _w = Screen.width; _h = Screen.height; _areaSize = size; _areaScale = scale;
        Layout();
    }

    void Layout()
    {
        var buttons = new Dictionary<string, RectTransform>();
        foreach (Transform t in transform)
            if (!t.name.StartsWith("DockSocket")) buttons[t.name] = (RectTransform)t;

        var paints = new List<RectTransform>();
        foreach (var n in Paints)
            if (buttons.TryGetValue(n, out var p) && p.gameObject.activeSelf) paints.Add(p);
        bool paintRow = paints.Count > 0;

        // Размер клетки: два ряда по высоте области; по ширине — компас (2) + промежуток + правый блок.
        var canvas = GetComponentInParent<Canvas>();
        float sf = canvas != null && canvas.rootCanvas.scaleFactor > 0.0001f ? canvas.rootCanvas.scaleFactor : 1f;
        float areaScale = Mathf.Max(0.0001f, AreaScale());
        var rect = _area.rect;
        int rightCols = Mathf.Max(Specials.Length, paints.Count);
        float gap = rect.height * 0.06f, spacer = gap * 3f;
        float cell = Mathf.Min((rect.height - gap) / 2f, (rect.width - spacer - gap * (rightCols)) / (2 + rightCols));
        // Кнопка — целое число пикселей экрана на пиксель рисунка 13×13.
        int k = Mathf.Max(1, Mathf.FloorToInt(cell * areaScale * sf / ButtonArt));
        cell = ButtonArt * k / (areaScale * sf);

        float blockW = cell * (2 + rightCols) + gap * rightCols + spacer;
        float left = rect.center.x - blockW * 0.5f;
        float top = rect.center.y + (cell * 2f + gap) * 0.5f;
        Vector2 CellCenter(int col, int row, bool right) =>
            new Vector2(left + (right ? cell * 2f + gap + spacer : 0f) + col * (cell + gap) + cell * 0.5f,
                        top - row * (cell + gap) - cell * 0.5f);

        int socket = 0;
        for (int i = 0; i < Compass.Length; i++)
            Put(buttons, Compass[i], CellCenter(i % 2, i / 2, false), cell, ref socket);
        for (int i = 0; i < Specials.Length; i++)
            Put(buttons, Specials[i], CellCenter(i, 0, true), cell, ref socket);
        if (paintRow)
            for (int i = 0; i < paints.Count; i++) Place(paints[i], CellCenter(i, 1, true), cell);
        for (int i = socket; i < _sockets.Count; i++) if (_sockets[i] != null) _sockets[i].gameObject.SetActive(false);

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
    void Place(RectTransform rt, Vector2 areaPoint, float cell)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = _area.TransformPoint(areaPoint);
        float rel = rt.parent != null && rt.parent.lossyScale.x > 0.0001f ? _area.lossyScale.x / rt.parent.lossyScale.x : 1f;
        rt.sizeDelta = new Vector2(cell * rel, cell * rel);
        var img = rt.GetComponent<Image>();
        if (img != null) PixelSnapUI.Exclude(img); // размер задаёт док, не PixelSnapUI
    }

    Image Socket(int index)
    {
        while (_sockets.Count <= index)
        {
            var go = new GameObject("DockSocket" + _sockets.Count, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling(); // под кнопками
            var img = go.AddComponent<Image>();
            img.sprite = SocketSprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            _sockets.Add(img);
        }
        var s = _sockets[index];
        var canvas = GetComponentInParent<Canvas>();
        float sf = canvas != null && canvas.rootCanvas.scaleFactor > 0.0001f ? canvas.rootCanvas.scaleFactor : 1f;
        float u = Mathf.Max(1f, Mathf.Round(ArtPixel * sf)) / sf;
        s.pixelsPerUnitMultiplier = 1f / u;
        return s;
    }

    /// <summary>Тёмное утопленное гнездо: контур, тень сверху, чуть светлее снизу. Sliced, рамка 2 px.</summary>
    static Sprite SocketSprite
    {
        get
        {
            if (_socketSprite != null) return _socketSprite;
            ColorUtility.TryParseHtmlString("#1D2140", out var top);
            ColorUtility.TryParseHtmlString("#262A4D", out var mid);
            ColorUtility.TryParseHtmlString("#343963", out var low);
            var pal = new Dictionary<char, Color> { { '#', PixelSprites.Ink }, { 't', top }, { 'k', mid }, { 'q', low } };
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
