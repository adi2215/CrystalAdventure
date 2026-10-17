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
