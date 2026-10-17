using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Скобки циклов над панелью программы.
///
/// Карточка While ставится ПОСЛЕ карточек, которые надо повторить. От неё влево тянется
/// цветная скобка со стрелкой: всё, что внутри, повторяется; что снаружи — выполняется один раз.
///   • тянешь стрелку влево/вправо — скобка охватывает больше/меньше карточек;
///   • значок ×N на углу — сколько раз выполнить содержимое (клик листает ×2 … ×6);
///   • во время Play скобка вспыхивает на каждом круге, а значок показывает «2/3».
///
/// Создаётся сам (MovingManager.Start) поверх каждой панели — в сцену ничего добавлять не нужно.
/// </summary>
public class LoopBrackets : MonoBehaviour
{
    static readonly Dictionary<SlotType, LoopBrackets> All = new Dictionary<SlotType, LoopBrackets>();

    public static LoopBrackets For(SlotType slot) =>
        All.TryGetValue(slot, out var b) && b != null ? b : null;

    static readonly Color[] Palette =
    {
        new Color(0.36f, 0.88f, 0.82f, 1f),  // бирюзовый
        new Color(1.00f, 0.47f, 0.40f, 1f),  // коралловый
        new Color(1.00f, 0.82f, 0.32f, 1f),  // золотой
        new Color(0.72f, 0.58f, 1.00f, 1f),  // сиреневый
    };
    static readonly Color HintColor = new Color(1f, 0.93f, 0.35f, 1f);

    MovingManager _program;
    InventoryManager _panel;
    SlotType _slot;
    RectTransform _rt;
    readonly Dictionary<int, Bracket> _brackets = new Dictionary<int, Bracket>();
    readonly List<int> _loops = new List<int>();
    readonly Vector3[] _corners = new Vector3[4];

    public static LoopBrackets Attach(MovingManager program, InventoryManager panel, SlotType slot)
    {
        if (program == null || panel == null) return null;
        var existing = panel.GetComponentInChildren<LoopBrackets>(true);
        if (existing == null)
        {
            var go = new GameObject("LoopBrackets", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(panel.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true; // в Grid/Horizontal Layout не участвует
            rt.SetAsLastSibling();
            existing = go.AddComponent<LoopBrackets>();
        }
        existing._program = program;
        existing._panel = panel;
        existing._slot = slot;
        existing._rt = (RectTransform)existing.transform;
        All[slot] = existing;
        return existing;
    }

    void OnDestroy()
    {
        if (All.TryGetValue(_slot, out var b) && b == this) All.Remove(_slot);
    }

    // ───────────── кадр ─────────────

    void LateUpdate()
    {
        if (_program == null || _panel == null) return;
        var parent = _rt.parent;
        if (parent != null && _rt.GetSiblingIndex() != parent.childCount - 1) _rt.SetAsLastSibling();

        // Какие While сейчас в панели
        _loops.Clear();
        var list = _program.Commands[_slot];
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].type == ItemType.While && _panel.FindCard(i) != null) _loops.Add(i);

        var stale = new List<int>();
        foreach (var kv in _brackets) if (!_loops.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (var k in stale) { _brackets[k].Destroy(); _brackets.Remove(k); }

        bool idle = _program.CanEdit;
        float dt = Time.unscaledDeltaTime;
        for (int n = 0; n < _loops.Count; n++)
        {
            int index = _loops[n];
            if (!_brackets.TryGetValue(index, out var b))
            {
                b = new Bracket(this, index);
                _brackets[index] = b;
            }
            b.BaseColor = Palette[n % Palette.Length];
            UpdateBracket(b, idle, dt);
        }
    }

    void UpdateBracket(Bracket b, bool idle, float dt)
    {
        int index = b.Index;
        var loopRect = SlotLocal(index);
        if (!loopRect.HasValue) { b.SetVisible(false); return; }
        b.SetVisible(true);

        int span = _program.GetSpan(_slot, index);
        int first = index - span;

        // Сегменты по рядам: подряд идущие слоты одного ряда — один прямоугольник.
        var segments = new List<Rect>();
        Rect? current = null;
        float rowY = float.NaN;
        for (int j = first; j <= index; j++)
        {
            var r = SlotLocal(j);
            if (!r.HasValue) continue;
            var rect = r.Value;
            bool sameRow = !float.IsNaN(rowY) && Mathf.Abs(rect.center.y - rowY) < rect.height * 0.5f;
            if (current.HasValue && sameRow) current = Union(current.Value, rect);
            else
            {
                if (current.HasValue) segments.Add(current.Value);
                current = rect;
                rowY = rect.center.y;
            }
        }
        if (current.HasValue) segments.Add(current.Value);

        float slotH = loopRect.Value.height;
        float px = Mathf.Max(1f, slotH / 26f);          // размер «пикселя» рамки
        float pad = px * 1.5f;                          // соседние ряды сетки — через 15 px
        for (int s = 0; s < segments.Count; s++)
        {
            var r = segments[s];
            segments[s] = Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
        }

        // Мягкое движение скобки
        float k = 1f - Mathf.Exp(-dt * 18f);
        b.Pulse = Mathf.Max(0f, b.Pulse - dt * 2.5f);
        b.HintTime = Mathf.Max(0f, b.HintTime - dt);
        Color color = b.BaseColor;
        if (b.HintTime > 0f) color = Color.Lerp(color, HintColor, 0.5f + 0.5f * Mathf.Sin(b.HintTime * 14f));
        color = Color.Lerp(color, Color.white, b.Pulse * 0.6f);
        b.SyncFrames(segments, k, px, color);

        // Разделитель между телом цикла и самой карточкой While
        var prev = span > 0 ? SlotLocal(index - 1) : null;
        bool divider = prev.HasValue && Mathf.Abs(prev.Value.center.y - loopRect.Value.center.y) < slotH * 0.5f;
        float divX = divider ? (prev.Value.xMax + loopRect.Value.xMin) * 0.5f : loopRect.Value.xMin;
        b.PlaceDivider(divider, divX, loopRect.Value.center.y, slotH * 0.78f, px * 2f, color, k);

        // Стрелка-ручка — у левого края первого сегмента
        var head = segments.Count > 0 ? segments[0] : loopRect.Value;
        float bob = 0f;
        if (idle && !b.Dragging)
        {
            float amp = span == 0 ? px * 2.5f : px * 1.2f;
            bob = -Mathf.Abs(Mathf.Sin(Time.unscaledTime * (span == 0 ? 5f : 2.6f))) * amp;
        }
        b.PlaceHandle(new Vector2(head.xMin + bob, head.center.y), slotH * 0.62f, color, idle, k);

        // Значок ×N
        int extra = Mathf.Max(1, _program.GetIteration(_slot, index));
        string label = !idle && b.RunPass >= 0 ? $"{b.RunPass + 1}/{b.RunTotal}" : $"x{extra + 1}";
        if (idle) b.RunPass = -1;
        var lr = loopRect.Value;
        b.PlaceBadge(new Vector2(lr.xMax - px, lr.yMax - px), slotH * 0.42f, color, label, idle, k);
    }

    Rect? SlotLocal(int index)
    {
        var slot = _panel.SlotRect(index);
        if (slot == null) return null;
        slot.GetWorldCorners(_corners);
        Vector2 a = _rt.InverseTransformPoint(_corners[0]);
        Vector2 c = _rt.InverseTransformPoint(_corners[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
    }

    static Rect Union(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    // ───────────── для туториала ─────────────

    /// <summary>Первая (самая левая) карточка While с видимой скобкой, -1 — нет.</summary>
    public int FirstLoop
    {
        get
        {
            int best = -1;
            foreach (var kv in _brackets) if (kv.Value.Visible && (best < 0 || kv.Key < best)) best = kv.Key;
            return best;
        }
    }

    public RectTransform HandleOf(int index) => _brackets.TryGetValue(index, out var b) ? b.HandleRect : null;
    public RectTransform BadgeOf(int index) => _brackets.TryGetValue(index, out var b) ? b.BadgeRect : null;

    /// <summary>Все видимые части скобки (рамки, стрелка, значок) — чтобы подсветить её целиком.</summary>
    public void PartsOf(int index, List<RectTransform> into)
    {
        if (_brackets.TryGetValue(index, out var b)) b.CollectParts(into);
    }

    // ───────────── события ─────────────

    /// <summary>Во время выполнения: шаг из цикла index, круг pass из total.</summary>
    public void OnLoopStep(int index, int pass, int total)
    {
        if (!_brackets.TryGetValue(index, out var b)) return;
        if (b.RunPass != pass)
        {
            b.RunPass = pass;
            b.RunTotal = total;
            b.Pulse = 1f;
            b.PunchBadge();
            if (pass > 0) PixelSfx.Play(Sfx.Blip, 0.6f, 0.9f + pass * 0.1f, 0.05f);
        }
    }

    /// <summary>Подсказка: у этого цикла не та скобка или не то число повторов.</summary>
    public void HintLoop(int index)
    {
        if (_brackets.TryGetValue(index, out var b)) b.HintTime = 1.4f;
    }

    void DragTo(Bracket b, Vector2 screen, Camera cam)
    {
        if (!_program.CanEdit) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, cam, out var local)) return;

        int index = b.Index;
        int min = index - _program.MaxSpan(_slot, index);
        int best = index;
        float bestDist = float.MaxValue;
        for (int j = min; j <= index; j++)
        {
            var r = SlotLocal(j);
            if (!r.HasValue) continue;
            // Чуть сильнее учитываем горизонталь — тянем ведь вбок.
            var d = r.Value.center - local;
            float dist = d.x * d.x + d.y * d.y * 2.5f;
            if (dist < bestDist) { bestDist = dist; best = j; }
        }

        int span = index - best;
        if (_program.SetSpan(_slot, index, span))
        {
            PixelSfx.Play(Sfx.Blip, 0.9f, 0.85f + span * 0.08f, 0f);
            b.Pulse = 0.6f;
        }
    }

    void CycleRepeats(Bracket b)
    {
        if (!_program.CanEdit) return;
        var card = _panel.FindCard(b.Index);
        if (card != null) card.CycleIterations();
        else
        {
            int n = _program.GetIteration(_slot, b.Index);
            _program.SetIteration(_slot, b.Index, n < InventoryItems.MaxIterations ? n + 1 : 1);
            PixelSfx.Play(Sfx.Blip, 0.8f);
        }
        b.PunchBadge();
    }

    // ───────────── одна скобка ─────────────

    sealed class Bracket
    {
        readonly LoopBrackets _owner;
        public readonly int Index;
        public Color BaseColor = Color.white;
        public float Pulse;
        public float HintTime;
        public int RunPass = -1;
        public int RunTotal;
        public bool Dragging;

        readonly RectTransform _root;
        readonly List<Image> _frames = new List<Image>();
        readonly Image _divider;
        readonly RectTransform _handle;
        readonly Image _handleImage;
        readonly RectTransform _badge;
        readonly Image _badgeImage;
        readonly TextMeshProUGUI _badgeText;
        float _badgePunch;

        public Bracket(LoopBrackets owner, int index)
        {
            _owner = owner;
            Index = index;
            var go = new GameObject("Loop" + index, typeof(RectTransform));
            _root = (RectTransform)go.transform;
            _root.SetParent(owner._rt, false);
            Stretch(_root);

            _divider = NewImage("Divider", _root, LoopSprites.Solid, false);

            _handleImage = NewImage("Arrow", _root, LoopSprites.Arrow, true);
            _handle = _handleImage.rectTransform;
            _handle.pivot = new Vector2(0.75f, 0.5f);
            _handleImage.raycastPadding = new Vector4(-14f, -14f, -14f, -14f);
            var drag = _handle.gameObject.AddComponent<LoopHandle>();
            drag.OnDragged = (screen, cam) => _owner.DragTo(this, screen, cam);
            drag.OnState = d => { Dragging = d; if (d) PixelSfx.Play(Sfx.Click, 0.7f); };

            _badgeImage = NewImage("Badge", _root, LoopSprites.Pill, true);
            _badge = _badgeImage.rectTransform;
            _badge.pivot = new Vector2(0.5f, 0.5f);
            _badgeImage.type = Image.Type.Sliced;
            _badgeImage.raycastPadding = new Vector4(-8f, -8f, -8f, -8f);
            var click = _badge.gameObject.AddComponent<LoopHandle>();
            click.OnClicked = () => _owner.CycleRepeats(this);

            var tgo = new GameObject("Count", typeof(RectTransform));
            _badgeText = tgo.AddComponent<TextMeshProUGUI>();
            var trt = _badgeText.rectTransform;
            trt.SetParent(_badge, false);
            Stretch(trt);
            _badgeText.alignment = TextAlignmentOptions.Center;
            _badgeText.enableAutoSizing = true;
            _badgeText.fontSizeMin = 6;
            _badgeText.fontSizeMax = 40;
            _badgeText.fontStyle = FontStyles.Bold;
            _badgeText.color = PixelSprites.Ink;
            _badgeText.raycastTarget = false;
            _badgeText.enableWordWrapping = false;
            var font = GameFont.Main;
            if (font != null) _badgeText.font = font;

            // Скобка «вырастает» при появлении
            _root.localScale = new Vector3(1f, 0.6f, 1f);
            Pulse = 1f;
        }

        public bool Visible => _root != null && _root.gameObject.activeInHierarchy;
        public RectTransform HandleRect => _handle;
        public RectTransform BadgeRect => _badge;

        public void CollectParts(List<RectTransform> into)
        {
            foreach (var f in _frames) if (f != null && f.gameObject.activeSelf) into.Add(f.rectTransform);
            into.Add(_handle);
            into.Add(_badge);
        }

        public void SetVisible(bool v)
        {
            if (_root.gameObject.activeSelf != v) _root.gameObject.SetActive(v);
        }

        public void SyncFrames(List<Rect> segments, float k, float px, Color color)
        {
            _root.localScale = Vector3.Lerp(_root.localScale, Vector3.one, k);
            while (_frames.Count < segments.Count)
            {
                var img = NewImage("Frame", _root, LoopSprites.Frame, false);
                img.type = Image.Type.Sliced;
                img.rectTransform.SetAsFirstSibling();
                _frames.Add(img);
            }
            for (int i = 0; i < _frames.Count; i++)
            {
                var img = _frames[i];
                bool on = i < segments.Count;
                if (img.gameObject.activeSelf != on) img.gameObject.SetActive(on);
                if (!on) continue;
                img.pixelsPerUnitMultiplier = 1f / px;
                img.color = color;
                var rt = img.rectTransform;
                var r = segments[i];
                var curPos = rt.anchoredPosition;
                var curSize = rt.sizeDelta;
                bool fresh = curSize.sqrMagnitude < 1f;
                rt.anchoredPosition = fresh ? r.center : Vector2.Lerp(curPos, r.center, k);
                rt.sizeDelta = fresh ? r.size : Vector2.Lerp(curSize, r.size, k);
            }
        }

        public void PlaceDivider(bool on, float x, float y, float h, float w, Color color, float k)
        {
            if (_divider.gameObject.activeSelf != on) _divider.gameObject.SetActive(on);
            if (!on) return;
            var rt = _divider.rectTransform;
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(x, y), k);
            rt.sizeDelta = new Vector2(w, h);
            _divider.color = color;
        }

        public void PlaceHandle(Vector2 pos, float size, Color color, bool idle, float k)
        {
            _handle.anchoredPosition = _handle.anchoredPosition.sqrMagnitude < 1f ? pos : Vector2.Lerp(_handle.anchoredPosition, pos, Mathf.Max(k, 0.5f));
            float grow = Dragging ? 1.2f : 1f;
            _handle.sizeDelta = new Vector2(size * 0.8f, size) * grow;
            _handleImage.color = idle ? color : new Color(color.r, color.g, color.b, 0.55f);
            _handleImage.raycastTarget = idle;
        }

        public void PlaceBadge(Vector2 pos, float size, Color color, string label, bool idle, float k)
        {
            _badgePunch = Mathf.Max(0f, _badgePunch - Time.unscaledDeltaTime * 3f);
            _badge.anchoredPosition = pos;
            _badge.sizeDelta = new Vector2(size * (label.Length > 2 ? 1.9f : 1.5f), size);
            _badge.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(_badgePunch * Mathf.PI));
            _badgeImage.color = color;
            _badgeImage.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.5f, size * 0.1f);
            _badgeImage.raycastTarget = idle;
            if (_badgeText.text != label) _badgeText.text = label;
        }

        public void PunchBadge() => _badgePunch = 1f;

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        static Image NewImage(string name, RectTransform parent, Sprite sprite, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = raycast;
            var rt = img.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}

/// <summary>Стрелка скобки (тянуть) и значок ×N (клик).</summary>
public class LoopHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public System.Action<Vector2, Camera> OnDragged;
    public System.Action<bool> OnState;
    public System.Action OnClicked;
    bool _dragging;

    public void OnPointerDown(PointerEventData e)
    {
        if (OnDragged == null) return;
        OnState?.Invoke(true);
        OnDragged(e.position, e.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (OnDragged != null && !_dragging) OnState?.Invoke(false);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (OnDragged == null) return;
        _dragging = true;
        OnState?.Invoke(true);
    }

    public void OnDrag(PointerEventData e) => OnDragged?.Invoke(e.position, e.pressEventCamera);

    public void OnEndDrag(PointerEventData e)
    {
        _dragging = false;
        OnState?.Invoke(false);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!e.dragging) OnClicked?.Invoke();
    }
}

/// <summary>Пиксельные спрайты скобки цикла (рисуются кодом).</summary>
public static class LoopSprites
{
    static Sprite _frame, _arrow, _pill, _solid;

    static Dictionary<char, Color> Pal => new Dictionary<char, Color>
    {
        { '#', PixelSprites.Ink },
        { 'w', Color.white },
        { 'f', new Color(1f, 1f, 1f, 0.16f) },
        { 's', new Color(0.78f, 0.78f, 0.78f, 1f) },
    };

    /// <summary>Рамка: тёмный контур, цветная полоса, лёгкая заливка внутри. Sliced.</summary>
    public static Sprite Frame => _frame != null ? _frame : _frame = PixelSprites.Make(new[]
    {
        "...######...",
        "..#wwwwww#..",
        ".#ww####ww#.",
        "#ww#ffff#ww#",
        "#w#ffffff#w#",
        "#w#ffffff#w#",
        "#w#ffffff#w#",
        "#w#ffffff#w#",
        "#ww#ffff#ww#",
        ".#ww####ww#.",
        "..#wwwwww#..",
        "...######...",
    }, Pal, new Vector4(4, 4, 4, 4));

    /// <summary>Стрелка влево — за неё тянут скобку.</summary>
    public static Sprite Arrow => _arrow != null ? _arrow : _arrow = PixelSprites.Make(new[]
    {
        "....##...",
        "...#w#...",
        "..#ww#...",
        ".#www####",
        "#wwwwwww#",
        "#wwwwwww#",
        ".#sss####",
        "..#ss#...",
        "...#s#...",
        "....##...",
    }, Pal);

    /// <summary>Пилюля значка ×N. Sliced.</summary>
    public static Sprite Pill => _pill != null ? _pill : _pill = PixelSprites.Make(new[]
    {
        "..####..",
        ".#wwww#.",
        "#wwwwww#",
        "#wwwwww#",
        "#ssssss#",
        ".#ssss#.",
        "..####..",
    }, Pal, new Vector4(3, 3, 3, 3));

    public static Sprite Solid => _solid != null ? _solid : _solid = PixelSprites.Make(new[] { "ww", "ww" }, Pal);
}
