using System.Collections.Generic;
using UnityEngine;

/// <summary>Пиксельные спрайты окна победы (рисуются кодом, в стиле панели программы).</summary>
public static class VictorySprites
{
    static Sprite _board, _row, _plus, _minus, _socket, _rays, _crystalOn, _crystalOff;
    static readonly Sprite[] _icons = new Sprite[3];

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static Dictionary<char, Color> Pal => new Dictionary<char, Color>
    {
        { '#', PixelSprites.Ink },
        { 'l', Hex("#B9B3E6") },   // светлый лавандовый кант
        { 'm', Hex("#8A84C2") },   // средний кант
        { 'n', Hex("#3A3F6E") },   // тёмно-синяя середина
        { 'r', Hex("#4B5189") },   // строка
        { 'h', Hex("#6067A6") },   // блик строки
        { 'd', Hex("#30355F") },   // тень строки
        { 's', Hex("#6F69A8") },   // тень канта
        { 'w', Color.white },
        { 'y', Hex("#FFD95A") },
        { 'o', Hex("#FFA53D") },
        { 'g', Hex("#9AA0B5") },
        { 'c', Hex("#D9DCF2") },
        { 'G', Hex("#58C95C") },   // зелёный значок
        { 'E', Hex("#8BE38A") },   // блик зелёного
        { 'R', Hex("#E85D5D") },   // красный значок
        { 'P', Hex("#F59A9A") },   // блик красного
        { 'k', Hex("#262A4D") },   // гнездо звезды
        { 'K', Hex("#1D2140") },
        { 'q', Hex("#343963") },
    };

    /// <summary>Доска итогов: светлый кант, тёмная середина. Sliced, рамка 5 px.</summary>
    public static Sprite Board => _board != null ? _board : _board = PixelSprites.Make(new[]
    {
        "..############..",
        ".#llllllllllll#.",
        "#llllllllllllll#",
        "#llmmmmmmmmmmll#",
        "#lm##########ml#",
        "#lm#nnnnnnnn#ml#",
        "#lm#nnnnnnnn#ml#",
        "#lm#nnnnnnnn#ml#",
        "#lm#nnnnnnnn#ml#",
        "#lm#nnnnnnnn#ml#",
        "#lm#nnnnnnnn#ml#",
        "#lm##########ml#",
        "#ssmmmmmmmmmmss#",
        "#ssssssssssssss#",
        ".#ssssssssssss#.",
        "..############..",
    }, Pal, new Vector4(5, 5, 5, 5));

    /// <summary>
    /// Строка итогов: плашка 8 пикселей высотой — тот же крупный пиксель, что у рамки консоли
    /// (FinishingLevel.artPixel). Sliced, рамка 2 px: контур + блик сверху / тень снизу.
    /// </summary>
    public static Sprite RowPlate => _row != null ? _row : _row = PixelSprites.Make(new[]
    {
        ".######.",
        "#hhhhhh#",
        "#rrrrrr#",
        "#rrrrrr#",
        "#rrrrrr#",
        "#rrrrrr#",
        "#dddddd#",
        ".######.",
    }, Pal, new Vector4(2, 2, 2, 2));

    /// <summary>Зелёный «+» 7×7 (крупный пиксель, как у консоли).</summary>
    public static Sprite Plus => _plus != null ? _plus : _plus = PixelSprites.Make(new[]
    {
        ".#####.",
        "#GGwGG#",
        "#GGwGG#",
        "#wwwww#",
        "#GGwGG#",
        "#GGwGG#",
        ".#####.",
    }, Pal);

    /// <summary>Красный «−» 7×7.</summary>
    public static Sprite Minus => _minus != null ? _minus : _minus = PixelSprites.Make(new[]
    {
        ".#####.",
        "#RRRRR#",
        "#RRRRR#",
        "#wwwww#",
        "#RRRRR#",
        "#RRRRR#",
        ".#####.",
    }, Pal);

    /// <summary>0 — карточки, 1 — подсказка (лампочка), 2 — край (знак внимания).</summary>
    public static Sprite Icon(int index)
    {
        index = Mathf.Clamp(index, 0, 2);
        if (_icons[index] != null) return _icons[index];
        string[] rows;
        switch (index)
        {
            case 1: // лампочка — «без подсказок»
                rows = new[]
                {
                    ".#####.",
                    "#ywyyy#",
                    "#wyyyy#",
                    "#yyyyy#",
                    ".#yyy#.",
                    ".#ggg#.",
                    "..###..",
                };
                break;
            case 2: // знак внимания — «край»
                rows = new[]
                {
                    "...#...",
                    "..#o#..",
                    "..#w#..",
                    ".#owo#.",
                    ".#ooo#.",
                    "#oowoo#",
                    "#######",
                };
                break;
            default: // карточка со стрелкой — «карточки»
                rows = new[]
                {
                    "#######",
                    "#ww#ww#",
                    "#w###w#",
                    "#ww#ww#",
                    "#ww#ww#",
                    "#ccccc#",
                    "#######",
                };
                break;
        }
        return _icons[index] = PixelSprites.Make(rows, Pal);
    }

    /// <summary>
    /// Пиксельный кристалл — «звезда» оценки (окно победы, карта уровней, конец главы).
    /// lit — заработан (сиреневый, как кристаллы на поле), иначе — тёмный силуэт того же рисунка.
    /// Исправлено: в окне победы звёзды были картинкой 1280×1280, уменьшенной в 10 раз, — не пиксельные.
    /// </summary>
    public static Sprite Crystal(bool lit)
    {
        if (lit && _crystalOn != null) return _crystalOn;
        if (!lit && _crystalOff != null) return _crystalOff;
        string[] rows =
        {
            ".....#.....",
            "....#w#....",
            "...#wwm#...",
            "..#wllmm#..",
            ".#wlllmmd#.",
            "#wllllmmdd#",
            "#lllllmmdd#",
            "#llllmmmdd#",
            "#mmmmmdddd#",
            ".#mmmmddd#.",
            "..#mmmdd#..",
            "...#mdd#...",
            "....#d#....",
            ".....#.....",
        };
        var pal = lit
            ? new Dictionary<char, Color>
            {
                { '#', PixelSprites.Ink }, { 'w', Color.white }, { 'l', Hex("#F6CEFA") }, { 'm', Hex("#DE96EC") },
                { 'd', Hex("#AA68CE") }, { 'g', Hex("#FFECFF") },
            }
            : new Dictionary<char, Color>
            {
                { '#', PixelSprites.Ink }, { 'w', Hex("#6E6A8C") }, { 'l', Hex("#5A5678") }, { 'm', Hex("#464264") },
                { 'd', Hex("#363250") }, { 'g', Hex("#646082") },
            };
        var sprite = PixelSprites.Make(rows, pal);
        if (lit) _crystalOn = sprite; else _crystalOff = sprite;
        return sprite;
    }

    /// <summary>Тёмное «гнездо» под звездой: круг с контуром, тенью сверху и бликом снизу.</summary>
    public static Sprite Socket
    {
        get
        {
            if (_socket != null) return _socket;
            const int size = 28;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            var p = Pal;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (c + 0.5f);
                    Color col;
                    if (r > 1f) col = Color.clear;
                    else if (r > 0.9f) col = p['#'];
                    else if (r > 0.76f) col = dy > 0 ? p['K'] : p['q'];   // верх темнее, низ светлее (y вверх)
                    else col = p['k'];
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            return _socket = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>Лучи за звёздами: 16 клиньев, к краю тают. Пиксельные (Point).</summary>
    public static Sprite Rays
    {
        get
        {
            if (_rays != null) return _rays;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f;
                    bool ray = Mathf.FloorToInt(a * 16f) % 2 == 0;
                    float fade = Mathf.Clamp01(1f - r);
                    float alpha = r > 1f ? 0f : (ray ? 0.9f : 0.22f) * fade * fade;
                    // ступеньки вместо плавного градиента — пиксельный вид
                    alpha = Mathf.Floor(alpha * 6f) / 6f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            return _rays = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
