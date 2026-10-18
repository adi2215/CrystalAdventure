using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пиксель-арт для титульного экрана и кат-сцен, нарисованный кодом (как EnemyArt / SpecialTiles),
/// чтобы ничего не настраивать в сцене. Все спрайты — с точечной фильтрацией; в UI их растягивают
/// в целое число раз (StoryStage.Pixel), так пиксели остаются квадратными.
/// </summary>
public static class StoryArt
{
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    static Sprite Cached(string key, System.Func<Sprite> make)
    {
        if (Cache.TryGetValue(key, out var s) && s != null) return s;
        return Cache[key] = make();
    }

    static Color C(int r, int g, int b, int a = 255) => new Color(r / 255f, g / 255f, b / 255f, a / 255f);

    static Sprite FromGrid(char[,] grid, Dictionary<char, Color> pal, Vector2 pivot)
    {
        int h = grid.GetLength(0), w = grid.GetLength(1);
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var line = new char[w];
            for (int x = 0; x < w; x++) line[x] = grid[y, x] == '\0' ? '.' : grid[y, x];
            rows[y] = new string(line);
        }
        return PixelSprites.Make(rows, pal, default, pivot);
    }

    /// <summary>Белый пиксель — для цветных плашек и полос.</summary>
    public static Sprite White => Cached("White", () => PixelSprites.Make(new[] { "w" }, new Dictionary<char, Color> { { 'w', Color.white } }));

    /// <summary>Небо полосами (16 ступенек сверху вниз) — как в пиксельных играх, без плавного градиента.</summary>
    public static Sprite Sky(Color top, Color bottom)
    {
        string key = "Sky" + top + bottom;
        return Cached(key, () =>
        {
            const int bands = 16;
            var rows = new string[bands];
            var pal = new Dictionary<char, Color>();
            for (int i = 0; i < bands; i++)
            {
                char ch = (char)('a' + i);
                pal[ch] = Color.Lerp(top, bottom, i / (bands - 1f));
                rows[i] = ch.ToString();
            }
            return PixelSprites.Make(rows, pal);
        });
    }

    /// <summary>Облако: несколько кругов вместе, светлый верх, тень снизу, тёмный контур.</summary>
    public static Sprite Cloud(int variant)
    {
        return Cached("Cloud" + variant, () =>
        {
            var rnd = new System.Random(5 + variant * 13);
            int w = 44 + variant % 3 * 6, h = 18;
            var inside = new bool[h, w];
            int blobs = 4 + variant % 2;
            for (int b = 0; b < blobs; b++)
            {
                float cx = 6 + (w - 12) * (b + 0.5f) / blobs + rnd.Next(-2, 3);
                float cy = h - 6 - rnd.Next(0, 5);
                float r = 4 + rnd.Next(2, 6);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) * 1.4f <= r * r && y <= h - 3) inside[y, x] = true;
            }
            for (int x = 3; x < w - 3; x++) for (int y = h - 7; y <= h - 3; y++) inside[y, x] = true; // ровное «дно»
            var grid = new char[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!inside[y, x]) continue;
                    bool edge = y == 0 || x == 0 || y == h - 1 || x == w - 1 ||
                                !inside[y - 1, x] || !inside[y + 1, x] || !inside[y, x - 1] || !inside[y, x + 1];
                    grid[y, x] = edge ? 'o' : y >= h - 6 ? 's' : 'w';
                }
            var pal = new Dictionary<char, Color> { { 'o', C(150, 150, 205) }, { 'w', Color.white }, { 's', C(214, 220, 245) } };
            return FromGrid(grid, pal, new Vector2(0.5f, 0.5f));
        });
    }

    /// <summary>
    /// Гора-остров: лес у подножия, выше — снег с камнями (главы Forest и Winter), у вершины —
    /// облака (их кладёт сцена) и Сердце: три главы снизу вверх, «The Ascent». 120×100, опора — низ.
    /// </summary>
    public static Sprite Mountain => Cached("Mountain", () =>
    {
        const int w = 120, h = 100;
        var rnd = new System.Random(42);
        var grid = new char[h, w];
        // Склоны чуть выпуклые, с мелкими зубцами.
        var top = new int[w];
        for (int x = 0; x < w; x++)
        {
            float k = 1f - Mathf.Abs(x + 0.5f - w / 2f) / (w / 2f);       // 0 у краёв, 1 в центре
            float shape = (float)System.Math.Pow(k, 0.8);
            top[x] = Mathf.Clamp(h - 1 - Mathf.RoundToInt(shape * (h - 14)) + rnd.Next(0, 2), 12, h - 1);
        }
        var snowLine = new int[w];
        for (int x = 0; x < w; x++) snowLine[x] = 30 + rnd.Next(-2, 3) + (x % 7 == 0 ? 2 : 0);
        for (int x = 0; x < w; x++)
            for (int y = top[x]; y < h; y++)
            {
                bool left = x < w / 2;
                int fromBottom = h - 1 - y;
                char c;
                if (fromBottom < snowLine[x]) c = left ? 'g' : 'G';                      // лес
                else c = rnd.Next(40) == 0 ? (left ? 'r' : 'R') : (left ? 'n' : 'N');   // снег с камнями
                grid[y, x] = y == top[x] ? 'o' : c;
            }
        // Ёлочки на лесном поясе.
        for (int i = 0; i < 18; i++)
        {
            int tx = 8 + rnd.Next(w - 16), ty = h - 2 - rnd.Next(22);
            if (grid[ty, tx] != 'g' && grid[ty, tx] != 'G') continue;
            for (int k = 0; k < 4; k++)
                for (int dx = -k; dx <= k; dx++)
                {
                    int yy = ty - 4 + k, xx = tx + dx;
                    if (xx >= 0 && xx < w && yy >= 0 && grid[yy, xx] != '\0' && grid[yy, xx] != 'o') grid[yy, xx] = 't';
                }
        }
        var pal = new Dictionary<char, Color>
        {
            { 'o', C(40, 30, 60) }, { 'g', C(90, 170, 90) }, { 'G', C(60, 130, 75) }, { 't', C(35, 95, 60) },
            { 'r', C(150, 140, 170) }, { 'R', C(115, 105, 140) }, { 'n', C(245, 250, 255) }, { 'N', C(195, 212, 240) },
        };
        return FromGrid(grid, pal, new Vector2(0.5f, 0f));
    });

    /// <summary>Мягкое круглое свечение 32×32 (три кольца прозрачности, по-пиксельному).</summary>
    public static Sprite Glow => Cached("Glow", () =>
    {
        const int n = 32;
        var rows = new string[n];
        for (int y = 0; y < n; y++)
        {
            var line = new char[n];
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - n / 2f) * (x + 0.5f - n / 2f) + (y + 0.5f - n / 2f) * (y + 0.5f - n / 2f)) / (n / 2f);
                line[x] = d > 1f ? '.' : d > 0.75f ? 'a' : d > 0.45f ? 'b' : 'c';
            }
            rows[y] = new string(line);
        }
        return PixelSprites.Make(rows, new Dictionary<char, Color>
        {
            { 'a', new Color(1f, 1f, 1f, 0.25f) }, { 'b', new Color(1f, 1f, 1f, 0.5f) }, { 'c', new Color(1f, 1f, 1f, 0.8f) },
        });
    });

    /// <summary>Кристальное Сердце (15×22): розово-золотой кристалл с бликом. Опора — центр.</summary>
    public static Sprite Heart => Cached("Heart", () => PixelSprites.Make(new[]
    {
        ".......o.......",
        "......owo......",
        ".....owwpo.....",
        "....owwppPo....",
        "...owwpppPPo...",
        "..owwppppPPPo..",
        ".owwpppppPPPPo.",
        "owwppppppPPPPPo",
        "owpppppppPPPPPo",
        "owppppphpPPPPPo",
        "owppppphhPPPPdo",
        "oppppppphPPPddo",
        "oppppppppPPPddo",
        ".opppppppPPddo.",
        ".opppppppPPddo.",
        "..oppppppPddo..",
        "..oppppppPddo..",
        "...opppppPdo...",
        "....oppppdo....",
        ".....opppo.....",
        "......opo......",
        ".......o.......",
    }, new Dictionary<char, Color>
    {
        { 'o', C(70, 30, 70) }, { 'w', C(255, 240, 250) }, { 'p', C(255, 120, 200) }, { 'P', C(225, 80, 170) },
        { 'd', C(170, 45, 130) }, { 'h', Color.white },
    }));

    /// <summary>Трещина поверх Сердца.</summary>
    public static Sprite HeartCrack => Cached("HeartCrack", () => PixelSprites.Make(new[]
    {
        "...............",
        "...............",
        ".......x.......",
        ".......x.......",
        "......x........",
        "......x........",
        ".......x.......",
        "........x......",
        "........xx.....",
        ".......x..x....",
        "......x....x...",
        ".....x.........",
        ".....x.........",
        "......x........",
        "......x........",
        ".......x.......",
        "...............",
        "...............",
        "...............",
        "...............",
        "...............",
        "...............",
    }, new Dictionary<char, Color> { { 'x', C(50, 20, 50) } }));

    /// <summary>Осколок Сердца (5×7).</summary>
    public static Sprite Shard => Cached("Shard", () => PixelSprites.Make(new[]
    {
        "..o..",
        ".owo.",
        "owppo",
        "oppPo",
        "oppPo",
        ".oPo.",
        "..o..",
    }, new Dictionary<char, Color>
    {
        { 'o', C(70, 30, 70) }, { 'w', Color.white }, { 'p', C(255, 120, 200) }, { 'P', C(205, 70, 160) },
    }));

    /// <summary>Звезда-искорка 3×3.</summary>
    public static Sprite Star => Cached("Star", () => PixelSprites.Make(new[] { ".w.", "www", ".w." },
        new Dictionary<char, Color> { { 'w', Color.white } }));

    /// <summary>Полоса лесной земли с травой (160×28), опора — верх травы по центру.</summary>
    public static Sprite ForestGround => Cached("ForestGround", () =>
    {
        const int w = 160, h = 28;
        var rnd = new System.Random(7);
        var grid = new char[h, w];
        for (int x = 0; x < w; x++)
        {
            int grass = 2 + rnd.Next(0, 2);
            for (int y = 0; y < h; y++)
                grid[y, x] = y < grass ? (y == 0 && rnd.Next(4) == 0 ? 'l' : 'g') : y < grass + 1 ? 'G' : (rnd.Next(9) == 0 ? 'D' : 'd');
        }
        var pal = new Dictionary<char, Color>
        {
            { 'l', C(150, 220, 120) }, { 'g', C(95, 175, 90) }, { 'G', C(60, 125, 70) }, { 'd', C(120, 80, 70) }, { 'D', C(95, 60, 60) },
        };
        return FromGrid(grid, pal, new Vector2(0.5f, 1f));
    });

    /// <summary>Круглое дерево (22×30), опора — низ ствола.</summary>
    public static Sprite Tree(int variant) => Cached("Tree" + variant, () =>
    {
        const int w = 22, h = 30;
        var rnd = new System.Random(3 + variant * 7);
        var grid = new char[h, w];
        float cx = w / 2f, cy = 10f, r = 9.5f + variant % 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) * 1.1f);
                if (d <= r) grid[y, x] = d > r - 1.2f ? 'o' : (x < cx - 2 && y < cy ? 'l' : rnd.Next(7) == 0 ? 'L' : 'g');
            }
        for (int y = 18; y < h; y++) for (int x = 9; x < 13; x++) if (grid[y, x] == '\0' || y > 19) grid[y, x] = x == 9 || x == 12 ? 'o' : 'b';
        var pal = new Dictionary<char, Color>
        {
            { 'o', C(30, 60, 45) }, { 'g', C(70, 150, 80) }, { 'l', C(120, 200, 110) }, { 'L', C(50, 120, 65) }, { 'b', C(120, 80, 60) },
        };
        return FromGrid(grid, pal, new Vector2(0.5f, 0f));
    });
}
