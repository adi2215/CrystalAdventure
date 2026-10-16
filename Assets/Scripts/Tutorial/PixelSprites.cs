using System.Collections.Generic;
using UnityEngine;

/// <summary>Пиксельные спрайты для туториала, нарисованные кодом (без ассетов).</summary>
public static class PixelSprites
{
    static Sprite _panel, _frame, _hand, _arrowDown, _tag;

    public static readonly Color Ink = new Color(0.26f, 0.15f, 0.21f, 1f);

    public static Sprite Make(string[] rows, Dictionary<char, Color> palette, Vector4 border = default, Vector2? pivot = null)
    {
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, palette.TryGetValue(rows[h - 1 - y][x], out var c) ? c : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    static Dictionary<char, Color> Pal(Color fill, Color shade) => new Dictionary<char, Color>
    {
        { '#', Ink }, { 'w', fill }, { 's', shade }, { 'h', Color.white },
    };

    /// <summary>Окно реплики: тёмный контур, светлая заливка, тень снизу. Растягивается (Sliced).</summary>
    public static Sprite Panel
    {
        get
        {
            if (_panel != null) return _panel;
            string[] rows =
            {
                "..########..",
                ".#hhhhhhhh#.",
                "#hwwwwwwwwh#",
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
            _panel = Make(rows, Pal(new Color(1f, 0.96f, 0.88f), new Color(0.85f, 0.78f, 0.7f)), new Vector4(4, 4, 4, 4));
            return _panel;
        }
    }

    /// <summary>Табличка с именем.</summary>
    public static Sprite Tag
    {
        get
        {
            if (_tag != null) return _tag;
            string[] rows =
            {
                ".######.",
                "#wwwwww#",
                "#wwwwww#",
                "#wwwwww#",
                "#ssssss#",
                ".######.",
            };
            _tag = Make(rows, Pal(Color.white, new Color(0.8f, 0.8f, 0.8f)), new Vector4(3, 3, 3, 3));
            return _tag;
        }
    }

    /// <summary>Рамка подсветки: только контур (тонируется), внутри пусто.</summary>
    public static Sprite Frame
    {
        get
        {
            if (_frame != null) return _frame;
            string[] rows =
            {
                "..wwwwwwww..",
                ".wwwwwwwwww.",
                "www......www",
                "ww........ww",
                "ww........ww",
                "ww........ww",
                "ww........ww",
                "ww........ww",
                "ww........ww",
                "www......www",
                ".wwwwwwwwww.",
                "..wwwwwwww..",
            };
            _frame = Make(rows, new Dictionary<char, Color> { { 'w', Color.white } }, new Vector4(4, 4, 4, 4));
            return _frame;
        }
    }

    /// <summary>Рука-указатель, палец смотрит влево-вверх.</summary>
    public static Sprite Hand
    {
        get
        {
            if (_hand != null) return _hand;
            string[] rows =
            {
                "##........",
                "#w#.......",
                "#ww#......",
                ".#ww#.....",
                "..#ww###..",
                "...#wwww#.",
                "...#wwwww#",
                "..#wwwwww#",
                "..#wwwwww#",
                "...#wwwww#",
                "....#wwww#",
                ".....####.",
            };
            _hand = Make(rows, new Dictionary<char, Color> { { '#', Ink }, { 'w', Color.white } }, default, new Vector2(0.05f, 0.95f));
            return _hand;
        }
    }

    /// <summary>Стрелка «дальше» ▼.</summary>
    public static Sprite ArrowDown
    {
        get
        {
            if (_arrowDown != null) return _arrowDown;
            string[] rows =
            {
                "#######",
                "#wwwww#",
                ".#www#.",
                "..#w#..",
                "...#...",
            };
            _arrowDown = Make(rows, new Dictionary<char, Color> { { '#', Ink }, { 'w', Color.white } });
            return _arrowDown;
        }
    }
}
