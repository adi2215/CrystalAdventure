using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пиксель-арт врагов и боссов, нарисованный кодом (без ассетов) — как лёд и снег в SpecialTiles.
/// Спрайты смотрят вправо; опора — середина низа (стоят «ногами» на клетке). 1 пиксель рисунка = 0.01 мира,
/// как у кубов CubesForGame. Сгенерировано скриптом из рисунков; заменить своими — PixelSprites.Make или ассеты.
/// </summary>
public static class EnemyArt
{
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    static readonly string[] ScoutRows =
    {
        "....##rrrr##....",
        "...#rrrrrrrr#...",
        "..#rrrrrrrrrr#..",
        ".#rrrrrrrrrrrr#.",
        ".#rrrrrrwwwrrr#.",
        ".#rrrrrrwkkrrr#.",
        "#rrrrrrrwwwrrrr#",
        "#rrrrrrrrrrrrrr#",
        ".#rrrrrrrrrrrr#.",
        ".#dddddddddddd#.",
        "..#dddddddddd#..",
        "..#dddddddddd#..",
        "...##dddddd##...",
        ".....######.....",
    };
    static readonly string[] DroneRows =
    {
        "...#pppppppp#...",
        "....###pp###....",
        ".......##.......",
        ".....######.....",
        "....#gggggg#....",
        "...#gggggggg#...",
        "..#gggggggggg#..",
        "..#gggggwwwgg#..",
        ".#ggggggwkkggg#.",
        ".#ggggggwwwggg#.",
        ".#gggggggggggg#.",
        "..#gggggggggg#..",
        "..#eeeeeeeeee#..",
        "...#eeeeeeee#...",
        "....#eeeeee#....",
        ".....######.....",
    };
    static readonly string[] SnowRows =
    {
        "....##ssss##....",
        "...#ssssssss#...",
        "..#ssssssssss#..",
        ".#ssssssssssss#.",
        ".#sssssswwwsss#.",
        ".#sssssswkksss#.",
        "#ssssssswwwssss#",
        "#ssssssssssssss#",
        ".#ssssssssssss#.",
        ".#bbbbbbbbbbbb#.",
        "..#bbbbbbbbbb#..",
        "..#bbbbbbbbbb#..",
        "...##bbbbbb##...",
        ".....######.....",
    };
    static readonly string[] CorruptRows =
    {
        "....##vvvv##....",
        "...#vvvvvvvv#...",
        "..#vvvvvvvvvv#..",
        ".#vvvvvvvvvvvv#.",
        ".#vvvvvvwwwvvv#.",
        ".#vvvvvvwkkvvv#.",
        "#vvvvvvvwwwvvvv#",
        "#vvvvvvvvvvvvvv#",
        ".#vvvvvvvvvvvv#.",
        ".#uuuuuuuuuuuu#.",
        "..#uuuuuuuuuu#..",
        "..#uuuuuuuuuu#..",
        "...##uuuuuu##...",
        ".....######.....",
    };
    static readonly string[] DummyRows =
    {
        "....####....",
        "...#yyyy#...",
        "..#yyyyyy#..",
        "..#ykyyky#..",
        "..#yyyyyy#..",
        "...#yyyy#...",
        "....#bb#....",
        "############",
        "#ssssbbssss#",
        "############",
        "....#bb#....",
        "...#ssss#...",
        "...#ssss#...",
        "....#bb#....",
        "....#bb#....",
        "....#bb#....",
        "...######...",
        "..########..",
    };
    static readonly string[] TurretRows =
    {
        "................",
        "................",
        "....####........",
        "...#iiii#.......",
        "..##iiii##......",
        ".#iiiiiiii#####.",
        ".#iIIkkIIcccccc#",
        ".#iIIIIIIcCCCCc#",
        ".#iiiiiiii#####.",
        "..#jjjjjj#......",
        "..#jjjjjj#......",
        "..#jjjjjj#......",
        "..#jjjjjj#......",
        "...######.......",
    };
    static readonly string[] WoodMechRows =
    {
        ".......#####gg#####.......",
        "......#wwwwwwwwwwww#......",
        "......#wWWWWWWWWWWw#......",
        "......#wWkkWWWWkkWw#......",
        "......#wWggWWWWggWw#......",
        "......#wWWWWWWWWWWw#......",
        "......#wwwwwwwwwwww#......",
        "....##################....",
        "####wwwwwwwwwwwwwwwwww####",
        "www#wWWWWWWWWWWWWWWWWw#www",
        "wWw#wWWWWWccccWWWWWWWw#wWw",
        "wWw#wWWWWcCCCCcWWWWWWw#wWw",
        "wWw#wWWWWcCCCCcWWWWWWw#wWw",
        "wWw#wWWWWWccccWWWWWWWw#wWw",
        "wWw#wWWWWWWWWWWWWWWWWw#wWw",
        "www#wWwWwWwWwWwWwWwWWw#www",
        "ggg#wwwwwwwwwwwwwwwwww#ggg",
        "###.##################.###",
        ".....#wwww#....#wwww#.....",
        ".....#wWWw#....#wWWw#.....",
        ".....#wWWw#....#wWWw#.....",
        ".....#wWWw#....#wWWw#.....",
        ".....#wWWw#....#wWWw#.....",
        ".....#wWWw#....#wWWw#.....",
        ".....#wwww#....#wwww#.....",
        ".....#bbbb#....#bbbb#.....",
        "......####......####......",
    };
    static readonly string[] FrostGeneralRows =
    {
        "...........#yy#...........",
        "........####yy####........",
        ".......#nnnnnnnnnn#.......",
        ".......#nNNNNNNNNn#.......",
        ".......#nNNNNNNNNn#.......",
        ".......#nnnnnnnnnn#.......",
        "......##ssssssssss##......",
        ".....#ssssssssssssss#.....",
        "....#ssskkk##kkksssss#....",
        "...#sssswkk##kkwssssss#...",
        "...#ssssssssssssssssss#...",
        "..#ssssssssssssssssssss#..",
        "..#ssssssssssssssssssss#..",
        "..#ysssssskkkkkkssssssy#..",
        ".#nnnssssssssssssssssnnn#.",
        ".#ssssssssssssssssssssss#.",
        "..#ssssssssssssssssssss#..",
        "..#ssssssssssssssssssss#..",
        "..#ssssssssssssssssssss#..",
        "..#bbbbbbbbbbbbbbbbbbbb#..",
        "...#bbbbbbbbbbbbbbbbbb#...",
        "...#bbbbbbbbbbbbbbbbbb#...",
        "....#bbbbbbbbbbbbbbbb#....",
        ".....#bbbbbbbbbbbbbb#.....",
        "......##bbbbbbbbbb##......",
        "........##bbbbbb##........",
        "..........######..........",
    };
    static readonly string[] HeartRows =
    {
        "..##.##..",
        ".#hh#hh#.",
        "#hhhhhhh#",
        "#hhhhhhh#",
        ".#hhhhh#.",
        "..#hhh#..",
        "...#h#...",
        "....#....",
    };
    static readonly string[] HeartEmptyRows =
    {
        "..##.##..",
        ".#ee#ee#.",
        "#eeeeeee#",
        "#eeeeeee#",
        ".#eeeee#.",
        "..#eee#..",
        "...#e#...",
        "....#....",
    };
    static Dictionary<char, Color> BallPal => new Dictionary<char, Color>
    {
        { '#', new Color(0.26f, 0.15f, 0.21f, 1f) },
        { 'w', new Color(1f, 1f, 1f, 1f) },
        { 'k', new Color(0.1f, 0.08f, 0.12f, 1f) },
        { 'p', new Color(0.6f, 0.6f, 0.66f, 1f) },
    };
    static Dictionary<char, Color> ScoutPal => new Dictionary<char, Color>
    {
        { 'r', new Color(0.9f, 0.33f, 0.3f, 1f) },
        { 'd', new Color(0.62f, 0.2f, 0.22f, 1f) },
    };
    static Dictionary<char, Color> DronePal => new Dictionary<char, Color>
    {
        { 'g', new Color(0.45f, 0.78f, 0.35f, 1f) },
        { 'e', new Color(0.28f, 0.55f, 0.25f, 1f) },
    };
    static Dictionary<char, Color> SnowPal => new Dictionary<char, Color>
    {
        { 's', new Color(0.95f, 0.97f, 1f, 1f) },
        { 'b', new Color(0.7f, 0.8f, 0.92f, 1f) },
    };
    static Dictionary<char, Color> CorruptPal => new Dictionary<char, Color>
    {
        { 'v', new Color(0.65f, 0.35f, 0.85f, 1f) },
        { 'u', new Color(0.42f, 0.2f, 0.6f, 1f) },
    };
    static Dictionary<char, Color> DummyPal => new Dictionary<char, Color>
    {
        { '#', new Color(0.26f, 0.15f, 0.21f, 1f) },
        { 'y', new Color(0.95f, 0.82f, 0.4f, 1f) },
        { 'k', new Color(0.1f, 0.08f, 0.12f, 1f) },
        { 'b', new Color(0.55f, 0.35f, 0.2f, 1f) },
        { 's', new Color(0.8f, 0.65f, 0.45f, 1f) },
    };
    static Dictionary<char, Color> TurretPal => new Dictionary<char, Color>
    {
        { '#', new Color(0.26f, 0.15f, 0.21f, 1f) },
        { 'i', new Color(0.75f, 0.9f, 1f, 1f) },
        { 'I', new Color(0.55f, 0.78f, 0.95f, 1f) },
        { 'j', new Color(0.45f, 0.55f, 0.7f, 1f) },
        { 'c', new Color(0.35f, 0.45f, 0.6f, 1f) },
        { 'C', new Color(0.25f, 0.32f, 0.45f, 1f) },
        { 'k', new Color(0.1f, 0.08f, 0.12f, 1f) },
    };
    static Dictionary<char, Color> WoodMechPal => new Dictionary<char, Color>
    {
        { '#', new Color(0.26f, 0.15f, 0.21f, 1f) },
        { 'w', new Color(0.55f, 0.36f, 0.2f, 1f) },
        { 'W', new Color(0.72f, 0.5f, 0.3f, 1f) },
        { 'g', new Color(0.5f, 1f, 0.5f, 1f) },
        { 'k', new Color(0.1f, 0.08f, 0.12f, 1f) },
        { 'c', new Color(0.4f, 0.95f, 1f, 1f) },
        { 'C', new Color(0.85f, 1f, 1f, 1f) },
        { 'b', new Color(0.35f, 0.22f, 0.12f, 1f) },
    };
    static Dictionary<char, Color> FrostGeneralPal => new Dictionary<char, Color>
    {
        { '#', new Color(0.26f, 0.15f, 0.21f, 1f) },
        { 's', new Color(0.95f, 0.97f, 1f, 1f) },
        { 'b', new Color(0.72f, 0.82f, 0.95f, 1f) },
        { 'n', new Color(0.2f, 0.28f, 0.55f, 1f) },
        { 'N', new Color(0.3f, 0.4f, 0.7f, 1f) },
        { 'y', new Color(1f, 0.85f, 0.3f, 1f) },
        { 'k', new Color(0.1f, 0.08f, 0.12f, 1f) },
        { 'w', new Color(1f, 1f, 1f, 1f) },
    };
    static Dictionary<char, Color> HeartPal => new Dictionary<char, Color>
    {
        { '#', new Color(0.26f, 0.15f, 0.21f, 1f) },
        { 'h', new Color(0.92f, 0.25f, 0.3f, 1f) },
        { 'e', new Color(0.32f, 0.26f, 0.36f, 1f) },
    };

    static Dictionary<char, Color> Merge(Dictionary<char, Color> a, Dictionary<char, Color> b)
    {
        foreach (var kv in b) a[kv.Key] = kv.Value;
        return a;
    }

    static Sprite Get(string key, string[] rows, Dictionary<char, Color> pal, Vector2 pivot)
    {
        if (Cache.TryGetValue(key, out var s) && s != null) return s;
        s = PixelSprites.Make(rows, pal, default, pivot);
        Cache[key] = s;
        return s;
    }

    static readonly Vector2 Feet = new Vector2(0.5f, 0f);

    /// <summary>Спрайт архетипа ядра (EnemyBrains / BossLibrary). Неизвестный — фиолетовый шар.</summary>
    public static Sprite For(string archetype)
    {
        switch (archetype)
        {
            case "TrainingDummy": return Get("Dummy", DummyRows, DummyPal, Feet);
            case "SmallBallScout":
            case "WindBot": return Get("Scout", ScoutRows, Merge(BallPal, ScoutPal), Feet);
            case "ForestDrone":
            case "IceDrone":
            case "FlyingBallDrone": return Get("Drone", DroneRows, Merge(BallPal, DronePal), Feet);
            case "SnowBallSoldier":
            case "HeavyIceGuard": return Get("Snow", SnowRows, Merge(BallPal, SnowPal), Feet);
            case "FrostTurret":
            case "CloudTurret": return Get("Turret", TurretRows, TurretPal, Feet);
            case "WoodMechGuardian": return Get("WoodMech", WoodMechRows, WoodMechPal, Feet);
            case "FrostBallGeneral": return Get("FrostGeneral", FrostGeneralRows, FrostGeneralPal, Feet);
            default: return Get("Corrupt", CorruptRows, Merge(BallPal, CorruptPal), Feet);
        }
    }

    // ───────────── ледяной босс арены 8×8 («BossWinter») ─────────────

    /// <summary>
    /// Ледяной ромб: верх — та же грань, что у куба героя (Sprites/Cube.png), а бока не стоят
    /// стенками, а сходятся вниз в острый угол. Глаза — как у героя, на правой грани.
    /// </summary>
    static readonly string[] FrostDiamondRows =
    {
        "..........aaaa..........",
        "........aabbccaa........",
        "......aabbddeeccaa......",
        "....aabbddffffeeccaa....",
        "..aabbddffffffffeeccaa..",
        ".abbddffffffffffffeecca.",
        "abggffffffffffffffffhhia",
        "abjkggffffffffffffhhlmia",
        "abjkkkggffffffffhhlllmia",
        "abjkkkkkggffffhhlllllmia",
        "abjkkkkkkkgghhlllllllmia",
        "abjkkkkkkkkklllllllllmia",
        ".anjkkkkkkkkllllllllmia.",
        ".anjkkkkkkkkllllllllmia.",
        "..anjkkkkkkklloollooia..",
        "..anjkkkkkkklloollooia..",
        "...anjkkkkkkllllllmia...",
        "...anjkkkkkkllllllmia...",
        "....anjkkkkkllllaaia....",
        "....anjkkkkklllllmia....",
        ".....anjkkkkllllmia.....",
        ".....anjkkkkllllmia.....",
        "......anjkkklllmia......",
        "......anjkkklllmia......",
        ".......anjkkllmia.......",
        ".......anjkkllmia.......",
        "........anjklmia........",
        "........anjklmia........",
        ".........anjmia.........",
        ".........anjmia.........",
        "..........ania..........",
        "..........ania..........",
        "...........aa...........",
    };

    static Color C255(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

    static readonly Dictionary<char, Color> FrostDiamondPal = new Dictionary<char, Color>
    {
        { 'a', C255(26, 30, 58) }, { 'b', C255(232, 248, 255) }, { 'c', C255(120, 190, 240) }, { 'd', C255(150, 205, 245) },
        { 'e', C255(95, 160, 225) }, { 'f', C255(170, 225, 255) }, { 'g', C255(220, 245, 255) }, { 'h', C255(140, 215, 255) },
        { 'i', C255(40, 90, 170) }, { 'j', C255(70, 130, 200) }, { 'k', C255(100, 170, 235) }, { 'l', C255(70, 140, 215) },
        { 'm', C255(45, 100, 180) }, { 'n', C255(85, 150, 220) }, { 'o', C255(240, 255, 255) },
    };

    /// <summary>Ледяной ромб-босс. Опора — нижний угол (он висит над клеткой, см. EnemyLayer).</summary>
    public static Sprite FrostDiamond => Get("FrostDiamond", FrostDiamondRows, FrostDiamondPal, new Vector2(0.5f, 0f));

    /// <summary>
    /// Молния с неба: ломаная белая линия с жёлто-голубой каймой, 15 px шириной, 180 px высотой.
    /// Опора — нижний конец (бьёт в центр клетки). variant — другой излом (для мерцания).
    /// </summary>
    public static Sprite Bolt(int variant)
    {
        string key = "Bolt" + variant;
        if (Cache.TryGetValue(key, out var s) && s != null) return s;
        const int w = 15, h = 180;
        var grid = new char[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid[y, x] = '.';
        var rnd = new System.Random(17 + variant * 31);
        int cx = w / 2;
        for (int y = 0; y < h; y++)   // y = 0 — верх рисунка
        {
            if (y % 9 == 0 && y < h - 12) cx = Mathf.Clamp(cx + rnd.Next(-3, 4), 3, w - 4);
            if (y >= h - 12) cx += cx < w / 2 ? 1 : cx > w / 2 ? -1 : 0; // к низу сходится в центр
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = cx + dx;
                if (x < 0 || x >= w) continue;
                grid[y, x] = Mathf.Abs(dx) <= 0 ? 'w' : Mathf.Abs(dx) == 1 ? 'y' : 'b';
            }
            // ответвления
            if (y % 23 == 11 && y < h - 30)
                for (int k = 1; k <= 5; k++) { int x = cx + (variant % 2 == 0 ? k : -k); if (x >= 0 && x < w && y + k < h) grid[y + k, x] = 'y'; }
        }
        var rows = new string[h];
        for (int y = 0; y < h; y++) { var line = new char[w]; for (int x = 0; x < w; x++) line[x] = grid[y, x]; rows[y] = new string(line); }
        var pal = new Dictionary<char, Color>
        {
            { 'w', Color.white }, { 'y', new Color(1f, 0.95f, 0.55f, 1f) }, { 'b', new Color(0.55f, 0.85f, 1f, 0.7f) },
        };
        return Cache[key] = PixelSprites.Make(rows, pal, default, new Vector2(0.5f, 0f));
    }

    /// <summary>Сосулька супер-льда (летит сверху вниз), опора — острие.</summary>
    public static Sprite IceShard => Get("IceShard", new[]
    {
        "..aaaaa..",
        ".abbbcca.",
        "abbbbccca",
        "abbbbccca",
        ".abbbcca.",
        ".abbbcca.",
        ".abbbcca.",
        "..abbca..",
        "..abbca..",
        "..abbca..",
        "...abca..",
        "...abca..",
        "...aca...",
        "...aca...",
        "....a....",
    }, new Dictionary<char, Color>
    {
        { 'a', C255(30, 70, 140) }, { 'b', C255(225, 248, 255) }, { 'c', C255(130, 200, 245) },
    }, new Vector2(0.5f, 0f));

    /// <summary>Прицел босса — уголки вокруг верхней грани клетки (32×16).</summary>
    public static Sprite Reticle
    {
        get
        {
            if (Cache.TryGetValue("Reticle", out var s) && s != null) return s;
            var rows = new string[16];
            for (int y = 0; y < 16; y++)
            {
                var chars = new char[32];
                float dy = Mathf.Abs(y + 0.5f - 8f) / 8f;
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - 16f) / 16f;
                    float d = dx + dy;
                    bool edge = d <= 1f && d > 0.84f;
                    bool corner = dx > 0.55f || dy > 0.55f; // только «уголки» у вершин ромба
                    chars[x] = edge && corner ? 'w' : '.';
                }
                rows[y] = new string(chars);
            }
            var pal = new Dictionary<char, Color> { { 'w', new Color(1f, 0.95f, 0.6f, 1f) } };
            return Cache["Reticle"] = PixelSprites.Make(rows, pal, default, new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>Значок «будет молния» над прицелом.</summary>
    public static Sprite IconBolt => Get("IconBolt", new[]
    {
        "....aaa",
        "...ayya",
        "..ayya.",
        ".ayyaaa",
        "ayyyyya",
        "aaayya.",
        "..ayya.",
        ".ayya..",
        ".aya...",
        ".aa....",
    }, new Dictionary<char, Color> { { 'a', C255(60, 40, 20) }, { 'y', C255(255, 230, 90) } }, new Vector2(0.5f, 0f));

    /// <summary>Значок «будет супер-лёд» над прицелом.</summary>
    public static Sprite IconFlake => Get("IconFlake", new[]
    {
        "....a....",
        ".a..w..a.",
        "..w.w.w..",
        "...www...",
        "awwwcwwwa",
        "...www...",
        "..w.w.w..",
        ".a..w..a.",
        "....a....",
    }, new Dictionary<char, Color> { { 'a', C255(30, 70, 140) }, { 'w', C255(220, 245, 255) }, { 'c', Color.white } }, new Vector2(0.5f, 0f));

    // ───────────── лесная змея ─────────────

    /// <summary>Шипы-корни из земли (24×16): три деревянных шипа с зелёными листиками. Опора — низ.</summary>
    public static Sprite Spikes => Get("Spikes", new[]
    {
        "...........a............",
        "..........aba...........",
        "....a.....aba......a....",
        "...aba....abba....aba...",
        "...aba...abbba....aba...",
        "...abba..abbbba..abba...",
        "..abbba..abbbba..abbba..",
        "..abbbba.abbbbba.abbbba.",
        ".abbbbba.abbbbba.abbbbba",
        ".abbbbbbaabbbbbbaabbbbba",
        "gaabbbbbaabbbbbbaabbbbag",
        "ggaabbbbaabbbbbaaabbbagg",
        ".ggaaabbaabbbbaaabbaagg.",
        "..gggaaaaaaaaaaaaaaggg..",
        "...gggggggggggggggggg...",
        "....gggggggggggggggg....",
    }, new Dictionary<char, Color>
    {
        { 'a', C255(60, 35, 30) }, { 'b', C255(170, 120, 80) }, { 'g', C255(70, 150, 70) },
    }, new Vector2(0.5f, 0f));

    /// <summary>Значок «будут шипы» над прицелом.</summary>
    public static Sprite IconSpikes => Get("IconSpikes", new[]
    {
        "...a.....",
        "..aba....",
        "..aba.a..",
        ".abba.ba.",
        ".abbaabba",
        "aabbbabba",
        "aaaaaaaaa",
    }, new Dictionary<char, Color> { { 'a', C255(60, 35, 30) }, { 'b', C255(220, 170, 110) } }, new Vector2(0.5f, 0f));

    /// <summary>Значок «провалится земля» над прицелом.</summary>
    public static Sprite IconCrack => Get("IconCrack", new[]
    {
        "aaaaaaaaa",
        "abbbcbbba",
        "abbcbbbba",
        "abbbccbba",
        "abbbbbcba",
        "abbbbcbba",
        "aaaaaaaaa",
    }, new Dictionary<char, Color> { { 'a', C255(60, 35, 30) }, { 'b', C255(150, 110, 80) }, { 'c', C255(20, 10, 10) } }, new Vector2(0.5f, 0f));

    // ───────────── небесный кит («BossSky») ─────────────

    static readonly string[] SkyWhaleRows =
    {
        "..o...............oooooooooooooo..........",
        ".oboo..........ooobbbbbbbbbbbbbbooo.......",
        ".obbbo........obbbllllllllllllllbbbo......",
        ".obbbbo.....oobbblbbbbbbbbbbbbbblbbboo....",
        ".obbbbboo..obbbbbbbbbbbbbbbbbbbbbbbbbbo...",
        ".obbbbbbboobbbbbbbbbbbbbbbbbbbbbbbbbbbbo..",
        ".obbbbbbbbobbbbbbbbbbbbbbbbbbbbbbhbbbbbo..",
        ".obbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbkkbbbbbo.",
        "..oooobbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbo.",
        ".....obbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbo.",
        "..oooobbbbbbbbbbbbbbbbbbbbbbbbbkkkkkkkkko.",
        ".obbbbbbbbbwwwwwwwwwwwwwwwwwwwwwwwwwwwwo..",
        ".obbbbbbbbowwwwswwswwswwswwswwswwswwswwo..",
        ".obbbbbbbo.owwwswwswwsddswwswwswwswwswo...",
        ".obbbbboo...oowswwswwswddddswwswwswwoo....",
        ".obbbbo.......oswwswwswwddwswwswwswo......",
        ".obbbo.........oooswwswwswwswwswooo.......",
        ".oboo.............oooooooooooooo..........",
        "..o.......................................",
    };

    /// <summary>Небесный кит: смотрит вправо (на экране), опора — центр (висит над тенью).</summary>
    public static Sprite SkyWhale => Get("SkyWhale", SkyWhaleRows, new Dictionary<char, Color>
    {
        { 'o', C255(30, 40, 80) }, { 'b', C255(110, 170, 235) }, { 'w', C255(235, 245, 255) }, { 's', C255(200, 220, 245) },
        { 'k', C255(20, 25, 50) }, { 'h', Color.white }, { 'd', C255(80, 130, 200) }, { 'l', C255(170, 215, 255) },
    }, new Vector2(0.5f, 0.5f));

    /// <summary>Тень летящего босса на поле — тёмный полупрозрачный овал по размеру грани (28×12).</summary>
    public static Sprite FlyShadow
    {
        get
        {
            if (Cache.TryGetValue("FlyShadow", out var s) && s != null) return s;
            var rows = new string[12];
            for (int y = 0; y < 12; y++)
            {
                var chars = new char[28];
                for (int x = 0; x < 28; x++)
                {
                    float dx = (x + 0.5f - 14f) / 14f, dy = (y + 0.5f - 6f) / 6f;
                    float d = dx * dx + dy * dy;
                    chars[x] = d > 1f ? '.' : d > 0.55f ? 'a' : 'b';
                }
                rows[y] = new string(chars);
            }
            var pal = new Dictionary<char, Color> { { 'a', new Color(0.05f, 0.05f, 0.15f, 0.25f) }, { 'b', new Color(0.05f, 0.05f, 0.15f, 0.45f) } };
            return Cache["FlyShadow"] = PixelSprites.Make(rows, pal, default, new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>Бомба кита (9×10), опора — низ.</summary>
    public static Sprite Bomb => Get("Bomb", new[]
    {
        ".....y...",
        "....yr...",
        "....a....",
        "..aaaaa..",
        ".abbbbba.",
        "abhbbbbba",
        "abbbbbbba",
        "abbbbbbba",
        ".abbbbba.",
        "..aaaaa..",
    }, new Dictionary<char, Color>
    {
        { 'a', C255(20, 20, 35) }, { 'b', C255(55, 55, 80) }, { 'h', C255(140, 140, 170) }, { 'y', C255(255, 230, 90) }, { 'r', C255(255, 120, 60) },
    }, new Vector2(0.5f, 0f));

    /// <summary>Значок «будет бомба» над прицелом.</summary>
    public static Sprite IconBomb => Get("IconBomb", new[]
    {
        "....y.y..",
        ".....r...",
        "....a....",
        "..aaaaa..",
        ".abhbbba.",
        ".abbbbba.",
        ".abbbbba.",
        "..aaaaa..",
    }, new Dictionary<char, Color>
    {
        { 'a', C255(20, 20, 35) }, { 'b', C255(55, 55, 80) }, { 'h', C255(140, 140, 170) }, { 'y', C255(255, 230, 90) }, { 'r', C255(255, 120, 60) },
    }, new Vector2(0.5f, 0f));

    public static bool IsBoss(string archetype) => archetype == "WoodMechGuardian" || archetype == "FrostBallGeneral";

    public static Sprite Heart => Get("Heart", HeartRows, HeartPal, new Vector2(0.5f, 0.5f));
    public static Sprite HeartEmpty => Get("HeartEmpty", HeartEmptyRows, HeartPal, new Vector2(0.5f, 0.5f));

    /// <summary>Красный ромб «сюда ударят» — по размеру верхней грани куба (32×16).</summary>
    public static Sprite Danger
    {
        get
        {
            if (Cache.TryGetValue("Danger", out var s) && s != null) return s;
            var rows = new string[16];
            for (int y = 0; y < 16; y++)
            {
                var chars = new char[32];
                float dy = Mathf.Abs(y + 0.5f - 8f) / 8f;
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - 16f) / 16f;
                    float d = dx + dy;
                    chars[x] = d > 1f ? '.' : d > 0.8f ? '#' : 'r';
                }
                rows[y] = new string(chars);
            }
            var pal = new Dictionary<char, Color> { { '#', new Color(1f, 0.25f, 0.2f, 0.95f) }, { 'r', new Color(1f, 0.2f, 0.15f, 0.35f) } };
            return Cache["Danger"] = PixelSprites.Make(rows, pal, default, new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>Пузырь щита (24×24, контур).</summary>
    public static Sprite Bubble
    {
        get
        {
            if (Cache.TryGetValue("Bubble", out var s) && s != null) return s;
            var rows = new string[24];
            for (int y = 0; y < 24; y++)
            {
                var chars = new char[24];
                for (int x = 0; x < 24; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - 12f) * (x + 0.5f - 12f) + (y + 0.5f - 12f) * (y + 0.5f - 12f));
                    chars[x] = d > 11.5f ? '.' : d > 10.2f ? '#' : (x == 7 && y == 6) || (x == 8 && y == 5) ? 'h' : 'f';
                }
                rows[y] = new string(chars);
            }
            var pal = new Dictionary<char, Color>
            {
                { '#', new Color(0.55f, 0.9f, 1f, 0.95f) }, { 'f', new Color(0.55f, 0.9f, 1f, 0.18f) }, { 'h', Color.white },
            };
            return Cache["Bubble"] = PixelSprites.Make(rows, pal, default, new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>Квадратик здоровья босса (3×3).</summary>
    public static Sprite Pip
    {
        get
        {
            if (Cache.TryGetValue("Pip", out var s) && s != null) return s;
            var pal = new Dictionary<char, Color> { { 'x', Color.white } };
            return Cache["Pip"] = PixelSprites.Make(new[] { "xxx", "xxx", "xxx" }, pal, default, new Vector2(0.5f, 0.5f));
        }
    }
}
