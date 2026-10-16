using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Краски для условий (вариант A).
///
/// На уровнях с цветными клетками среди кнопок команд появляются кнопки-краски
/// (по одной на каждый цвет уровня) и ластик. Тап по краске — она «берётся в руку»
/// (кнопка дышит, карточки хода подсвечиваются); следующий тап по карточке хода красит её
/// целиком. Покрашенная карточка срабатывает только на клетке своего цвета.
/// Краска одноразовая: после одной карточки рука снова пустая (меньше случайных покрасок).
///
/// Создаётся сама (MovingManager.Start) — в сцену ничего добавлять не нужно.
/// </summary>
public class PaintPalette : MonoBehaviour
{
    public static PaintPalette Instance { get; private set; }

    /// <summary>Что в руке: null — ничего, TileColor.None — ластик, иначе краска.</summary>
    public static TileColor? Selected { get; private set; }
    public static bool Active => Selected.HasValue;

    MovingManager _program;
    bool _built;
    readonly List<(GameObject go, TileColor color)> _buttons = new List<(GameObject, TileColor)>();

    public static PaintPalette Attach(MovingManager program)
    {
        if (program == null) return null;
        if (Instance == null) Instance = program.gameObject.AddComponent<PaintPalette>();
        Instance._program = program;
        Selected = null;
        return Instance;
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; Selected = null; }
    }

    /// <summary>Кнопка краски этого цвета (для туториала). null — нет.</summary>
    public static GameObject ButtonFor(TileColor color)
    {
        if (Instance == null) return null;
        foreach (var b in Instance._buttons)
            if (b.go != null && b.color == color && b.go.activeInHierarchy) return b.go;
        foreach (var b in Instance._buttons)
            if (b.go != null && b.color == color) return b.go;
        return null;
    }

    void Update()
    {
        // Цвета уровня известны только после загрузки карты — строим кнопки, как только они появились.
        if (!_built && ColorTiles.Any) Build();

        // Во время выполнения краска убирается из руки.
        if (Selected.HasValue && _program != null && !_program.CanEdit) Select(null);

        // Выбранная краска «дышит», остальные — спокойно.
        foreach (var b in _buttons)
        {
            if (b.go == null) continue;
            var juice = b.go.GetComponent<ButtonJuice>();
            if (juice != null) juice.breathe = Selected.HasValue && Selected.Value == b.color;
        }
    }

    void Build()
    {
        _built = true;
        foreach (var spawn in FindObjectsOfType<SpawnScript>(true))
        {
            if (spawn.objMethods == null || spawn.objMethods.Length == 0) continue;
            GameObject template = null;
            foreach (var o in spawn.objMethods) if (o != null) { template = o; break; }
            if (template == null || template.transform.parent == null) continue;

            foreach (var c in ColorTiles.UsedColors) AddButton(template, c);
            AddButton(template, TileColor.None); // ластик
        }
    }

    void AddButton(GameObject template, TileColor color)
    {
        var go = Instantiate(template, template.transform.parent);
        go.name = color == TileColor.None ? "PaintEraser" : "Paint" + color;
        go.SetActive(true);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = PaintSprites.Button(color);
            img.color = Color.white;
            img.preserveAspect = true;
        }
        var button = go.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent(); // убираем вызовы из шаблона
            button.onClick.AddListener(() => Toggle(color));
        }
        ButtonJuice.Ensure(go, wobble: true);
        _buttons.Add((go, color));
    }

    static void Toggle(TileColor color)
    {
        if (Instance != null && Instance._program != null && !Instance._program.CanEdit) return;
        bool same = Selected.HasValue && Selected.Value == color;
        Select(same ? (TileColor?)null : color);
        PixelSfx.Play(same ? Sfx.Close : Sfx.Open, 0.6f, 1.2f + (int)color * 0.08f);
    }

    public static void Select(TileColor? color)
    {
        Selected = color;
    }

    /// <summary>Карточку покрасили — краска израсходована.</summary>
    public static void Used() => Select(null);

    // ───────────── вид покрашенной карточки ─────────────

    /// <summary>Цветная рамка и заливка поверх карточки (или убрать, если None).</summary>
    public static void Decorate(InventoryItems card, TileColor color)
    {
        if (card == null) return;
        var existing = card.transform.Find("PaintFrame");
        if (color == TileColor.None)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }
        Image frame;
        if (existing != null) frame = existing.GetComponent<Image>();
        else
        {
            var go = new GameObject("PaintFrame", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(card.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-4f, -4f);
            rt.offsetMax = new Vector2(4f, 4f);
            frame = go.AddComponent<Image>();
            frame.sprite = LoopSprites.Frame;
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            var cardRt = card.transform as RectTransform;
            float px = cardRt != null && cardRt.rect.height > 1f ? cardRt.rect.height / 26f : 4f;
            frame.pixelsPerUnitMultiplier = 1f / px;
        }
        if (frame != null) frame.color = ColorTiles.Ui(color);
    }
}

/// <summary>Пиксельные кнопки красок: кнопка в стиле команд, внутри — капля краски или ластик.</summary>
public static class PaintSprites
{
    static readonly Dictionary<TileColor, Sprite> Cache = new Dictionary<TileColor, Sprite>();

    public static Sprite Button(TileColor color)
    {
        if (Cache.TryGetValue(color, out var s) && s != null) return s;

        string[] box =
        {
            "....##################....",
            "..##hhhhhhhhhhhhhhhhhh##..",
            ".#hhbbbbbbbbbbbbbbbbbbhh#.",
            ".#hbbbbbbbbbbbbbbbbbbbbh#.",
            "#hbbbbbbbbbbbbbbbbbbbbbbh#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#bbbbbbbbbbbbbbbbbbbbbbbb#",
            "#dddddddddddddddddddddddd#",
            "#dddddddddddddddddddddddd#",
            ".#dddddddddddddddddddddd#.",
            "..##dddddddddddddddddd##..",
            "....##################....",
        };
        string[] drop =
        {
            ".....##.....",
            "....#cc#....",
            "....#cc#....",
            "...#cccc#...",
            "...#cccc#...",
            "..#cwcccc#..",
            ".#cwwccccc#.",
            ".#cwcccccc#.",
            "#ccccccccco#",
            "#ccccccccco#",
            ".#ccccccoo#.",
            "..##oooo##..",
            "....####....",
        };
        string[] eraser =
        {
            "............",
            ".......####.",
            "......#pppp#",
            ".....#pppp##",
            "....#pppp#w#",
            "...#pppp#ww#",
            "..#wwww#ww#.",
            ".#wwwwwww#..",
            "#wwwwwww#...",
            "#wwwwww#....",
            ".#####......",
            "............",
            "............",
        };
        var icon = color == TileColor.None ? eraser : drop;
        var rows = new string[box.Length];
        int ox = (box[0].Length - icon[0].Length) / 2, oy = 5;
        for (int y = 0; y < box.Length; y++)
        {
            var chars = box[y].ToCharArray();
            int iy = y - oy;
            if (iy >= 0 && iy < icon.Length)
                for (int x = 0; x < icon[iy].Length; x++)
                    if (icon[iy][x] != '.') chars[ox + x] = icon[iy][x];
            rows[y] = new string(chars);
        }

        var paint = color == TileColor.None ? new Color(1f, 0.55f, 0.7f, 1f) : ColorTiles.Ui(color);
        var pal = new Dictionary<char, Color>
        {
            { '#', PixelSprites.Ink },
            { 'h', new Color(0.47f, 0.5f, 0.75f, 1f) },
            { 'b', new Color(0.36f, 0.38f, 0.62f, 1f) },
            { 'd', new Color(0.27f, 0.29f, 0.5f, 1f) },
            { 'c', paint },
            { 'o', Color.Lerp(paint, Color.black, 0.3f) },
            { 'w', Color.white },
            { 'p', paint },
        };
        s = PixelSprites.Make(rows, pal);
        Cache[color] = s;
        return s;
    }
}
