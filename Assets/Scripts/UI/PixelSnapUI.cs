using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Чёткие пиксели в интерфейсе: каждый пиксель картинки — ровно целое число пикселей экрана.
///
/// Исправлено («мисс-пиксели» в UI): интерфейс сделан под 1280×720, на 1920×1080 он растягивается в 1,5 раза,
/// а у элементов к тому же разный «размер пикселя» (ячейки слотов 4,6 единицы на пиксель, кнопки меню 4,3,
/// карточки 10, Play 10,4 × 8,5, главная панель 7,8 × 9 — растянутая картинка 67×68). Пиксели выходили то по 1,
/// то по 2 пикселя экрана, края соседних элементов не совпадали по сетке.
///
/// Что делает (после загрузки сцены и при смене разрешения; сцену и префабы не меняет — всё во время игры):
///  • Простая картинка с фиксированным размером — размер подгоняется (каждая ось отдельно, по ближайшему)
///    так, чтобы пиксель картинки был целым числом пикселей экрана. Размер меняется чуть-чуть.
///  • Картинка, растянутая якорями на родителя (фон панели), — режется на 9 частей (копия спрайта
///    с рамкой в треть размера) и рисуется «Sliced»: углы и края чёткие при любом размере панели.
///  • Картинка Sliced (рамки кнопок, созданных кодом) — подбирается pixelsPerUnitMultiplier.
///  • Сетка карточек (GridLayoutGroup) — размер ячейки подгоняется вниз, чтобы не вылезти за панель.
///  • Канвасам включается pixelPerfect — края элементов встают на пиксели экрана.
/// Картинки высокого разрешения (пиксель картинки меньше 1,5 пикселя экрана) не трогаются.
/// Выключить: PixelSnapUI.Enabled = false до загрузки сцены.
/// </summary>
[DefaultExecutionOrder(1000)]
public class PixelSnapUI : MonoBehaviour
{
    public static bool Enabled = true;
    const float MinScreenPx = 1.5f; // меньше — это не пиксель-арт в этом масштабе

    static PixelSnapUI _inst;

    class State
    {
        public Vector2 size;     // исходный sizeDelta
        public float mult;       // исходный pixelsPerUnitMultiplier
        public Image.Type type;  // исходный тип
        public Sprite sprite;    // исходный спрайт
        public float rel;        // масштаб элемента относительно канваса (при первой встрече)
        public bool slice;       // фон из сцены, растянутый якорями — режем на 9 частей
    }

    readonly Dictionary<int, State> _images = new Dictionary<int, State>();
    readonly Dictionary<int, Vector2> _grids = new Dictionary<int, Vector2>();
    static readonly Dictionary<Sprite, Sprite> SlicedCopies = new Dictionary<Sprite, Sprite>();
    int _w, _h;
    float _nextScan;
    bool _firstScan = true; // первый проход после загрузки сцены — только объекты самой сцены

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();

    static void Ensure()
    {
        if (!Enabled) return;
        if (_inst == null)
        {
            var go = new GameObject("PixelSnapUI");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<PixelSnapUI>();
        }
        _inst._images.Clear(); // новая сцена — новые объекты
        _inst._grids.Clear();
        _inst._nextScan = 0f;
        _inst._firstScan = true;
    }

    void LateUpdate()
    {
        if (!Enabled) return;
        bool resized = Screen.width != _w || Screen.height != _h;
        if (!resized && Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + 0.5f; // интерфейс, созданный кодом, появляется позже — досматриваем
        _w = Screen.width; _h = Screen.height;
        Canvas.ForceUpdateCanvases(); // scaleFactor канваса уже под новый экран

        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) c.pixelPerfect = true;
        foreach (var g in FindObjectsOfType<GridLayoutGroup>(true)) SnapGrid(g, resized);
        foreach (var img in FindObjectsOfType<Image>(true)) SnapImage(img, resized);
        _firstScan = false;
    }

    static Canvas RootCanvas(Component c)
    {
        var canvas = c.GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.rootCanvas : null;
    }

    static float RelScale(RectTransform rt, Canvas canvas)
    {
        float cs = canvas.transform.lossyScale.x;
        return cs > 0.0001f ? Mathf.Abs(rt.lossyScale.x / cs) : 1f;
    }

    /// <summary>Размер элемента на родителе определяет раскладка (Grid или Horizontal/Vertical с контролем размера).</summary>
    static bool DrivenByLayout(RectTransform rt, out bool x, out bool y)
    {
        x = y = false;
        var p = rt.parent;
        if (p == null) return false;
        if (p.GetComponent<GridLayoutGroup>() != null) { x = y = true; return true; }
        var hv = p.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (hv != null && hv.enabled) { x = hv.childControlWidth; y = hv.childControlHeight; }
        var fitter = rt.GetComponent<ContentSizeFitter>();
        if (fitter != null && fitter.enabled)
        {
            x |= fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained;
            y |= fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained;
        }
        if (rt.GetComponent<AspectRatioFitter>() != null) x = y = true;
        return x || y;
    }

    void SnapImage(Image img, bool resized)
    {
        if (img == null || img.sprite == null && !_images.ContainsKey(img.GetInstanceID())) return;
        int id = img.GetInstanceID();
        bool known = _images.TryGetValue(id, out var st);
        if (known && !resized) return;

        var rt = img.rectTransform;
        var canvas = RootCanvas(img);
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
        if (img.GetComponent<Animator>() != null) return;
        float s = canvas.scaleFactor;
        if (s <= 0.0001f) return;

        if (!known)
        {
            if (img.type != Image.Type.Simple && img.type != Image.Type.Sliced) return;
            st = new State { size = rt.sizeDelta, mult = img.pixelsPerUnitMultiplier, type = img.type, sprite = img.sprite, rel = RelScale(rt, canvas) };
            // Резать на 9 частей — только фоны, которые были в сцене с самого начала: картинки, созданные кодом
            // позже (затемнение туториала, «дырка», виньетки), растягиваются намеренно.
            st.slice = _firstScan && img.type == Image.Type.Simple && (rt.anchorMin.x != rt.anchorMax.x || rt.anchorMin.y != rt.anchorMax.y);
            _images[id] = st;
        }
        var sprite = st.sprite;
        if (sprite == null) return;
        float ref2sprite = canvas.referencePixelsPerUnit / Mathf.Max(0.01f, sprite.pixelsPerUnit);

        if (st.type == Image.Type.Sliced)
        {
            // Пиксель рамки = ref2sprite / mult единиц канваса.
            float px = ref2sprite / Mathf.Max(0.0001f, st.mult) * s * st.rel;
            if (px < MinScreenPx) return;
            int k = Mathf.Max(1, Mathf.RoundToInt(px));
            img.pixelsPerUnitMultiplier = ref2sprite * s * st.rel / k;
            return;
        }

        float sw = sprite.rect.width, sh = sprite.rect.height;
        bool stretchX = rt.anchorMin.x != rt.anchorMax.x, stretchY = rt.anchorMin.y != rt.anchorMax.y;

        if (stretchX || stretchY)
        {
            if (!st.slice) return;
            // Фон панели, растянутый якорями: режем на 9 частей — размер панели не меняется, пиксели рамки целые.
            var rect = rt.rect;
            float pxX = rect.width * st.rel * s / sw, pxY = rect.height * st.rel * s / sh;
            float px = Mathf.Min(pxX, pxY);
            if (px < MinScreenPx || sw > 256 || sh > 256) return;
            var sliced = SlicedCopy(sprite);
            if (sliced == null) return;
            int k = Mathf.Max(1, Mathf.RoundToInt(px));
            img.sprite = sliced;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = ref2sprite * s * st.rel / k;
            return;
        }

        DrivenByLayout(rt, out bool drivenX, out bool drivenY);
        var size = st.size;
        float pxW = size.x * st.rel * s / sw, pxH = size.y * st.rel * s / sh;
        if (Mathf.Min(pxW, pxH) < MinScreenPx) return;
        if (!drivenX) size.x = sw * Mathf.Max(1, Mathf.RoundToInt(pxW)) / (s * st.rel);
        if (!drivenY) size.y = sh * Mathf.Max(1, Mathf.RoundToInt(pxH)) / (s * st.rel);
        rt.sizeDelta = size;
    }

    /// <summary>
    /// Сетка карточек: ячейка подгоняется под целый пиксель картинки карточки (вниз — чтобы ряд
    /// не вылез за панель). Размер пикселя берётся по первой картинке в сетке.
    /// </summary>
    void SnapGrid(GridLayoutGroup g, bool resized)
    {
        int id = g.GetInstanceID();
        bool known = _grids.TryGetValue(id, out var orig);
        if (known && !resized) return;
        var canvas = RootCanvas(g);
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
        float s = canvas.scaleFactor;
        if (s <= 0.0001f) return;

        Image sample = null;
        foreach (Transform child in g.transform)
        {
            var img = child.GetComponentInChildren<Image>(true);
            if (img != null && img.sprite != null && img.type == Image.Type.Simple) { sample = img; break; }
        }
        if (sample == null) return; // пустая сетка — посмотрим в следующий раз
        if (!known) { orig = g.cellSize; _grids[id] = orig; }

        var rt = (RectTransform)g.transform;
        float rel = RelScale(rt, canvas);
        float sw = sample.sprite.rect.width, sh = sample.sprite.rect.height;
        float pxW = orig.x * rel * s / sw, pxH = orig.y * rel * s / sh;
        if (Mathf.Min(pxW, pxH) < MinScreenPx) return;
        g.cellSize = new Vector2(sw * Mathf.Max(1, Mathf.FloorToInt(pxW)) / (s * rel),
                                 sh * Mathf.Max(1, Mathf.FloorToInt(pxH)) / (s * rel));
    }

    /// <summary>Копия спрайта с рамкой для 9-частей (треть меньшей стороны, если своей рамки нет).</summary>
    static Sprite SlicedCopy(Sprite src)
    {
        if (src.border.sqrMagnitude > 0f) return src;
        if (SlicedCopies.TryGetValue(src, out var copy) && copy != null) return copy;
        if (src.texture == null || src.packed) return null;
        var r = src.rect;
        float b = Mathf.Floor(Mathf.Min(r.width, r.height) / 3f);
        var pivot = new Vector2(src.pivot.x / r.width, src.pivot.y / r.height);
        copy = Sprite.Create(src.texture, r, pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        copy.name = src.name + " (sliced)";
        SlicedCopies[src] = copy;
        return copy;
    }
}
