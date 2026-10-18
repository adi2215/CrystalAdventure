using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Иконки новых карточек (Атака, Щит, Супер-атака) — 13×13 пикселей в палитре карточек Sprites/Arrows.png,
/// нарисованы кодом. Ensure(item) ставит иконку ассету карточки, если своей картинки у него нет —
/// поэтому ассеты карточек можно создавать без PNG (Crystal Adventure → Уровни → Создать уровни с врагами).
/// </summary>
public static class CardIcons
{
    static readonly Dictionary<ItemType, Sprite> Cache = new Dictionary<ItemType, Sprite>();

    static readonly string[] AttackRows =
    {
        "..aaaaaaaaa..",
        ".abbbbbbbbba.",
        "abbbbbbbccbba",
        "abbbbbbcccbba",
        "abbbbbcccbbba",
        "abcbcccbbbbba",
        "abbcccbbbbbba",
        "abbccbbbbbbba",
        "abcbcbbbbbbba",
        "adbbbbbbbbbda",
        "addddddddddda",
        ".addddddddda.",
        "..aaaaaaaaa..",
    };
    static readonly string[] ShieldRows =
    {
        "..aaaaaaaaa..",
        ".abbbbbbbbba.",
        "abbcccccccbba",
        "abbcbbcbbcbba",
        "abbcbbcbbcbba",
        "abbcccccccbba",
        "abbbcbcbcbbba",
        "abbbbcbcbbbba",
        "abbbbbcbbbbba",
        "adbbbbbbbbbda",
        "addddddddddda",
        ".addddddddda.",
        "..aaaaaaaaa..",
    };
    static readonly string[] SuperAttackRows =
    {
        "..aaaaaaaaa..",
        ".abbbbbbbbba.",
        "abybbbbbccbba",
        "abbbbbcccybba",
        "abbbbcccbbbba",
        "abcbcccbbybba",
        "abbcccbbbbbba",
        "abbccbbbybbba",
        "abcbcbbbbbbba",
        "adbbbbbbbbbda",
        "addddddddddda",
        ".addddddddda.",
        "..aaaaaaaaa..",
    };

    static Dictionary<char, Color> Pal => new Dictionary<char, Color>
    {
        { 'a', new Color(24f / 255f, 20f / 255f, 37f / 255f, 1f) },
        { 'b', new Color(58f / 255f, 68f / 255f, 102f / 255f, 1f) },
        { 'c', new Color(192f / 255f, 203f / 255f, 220f / 255f, 1f) },
        { 'd', new Color(38f / 255f, 43f / 255f, 68f / 255f, 1f) },
        { 'y', new Color(1f, 0.85f, 0.3f, 1f) },
    };

    /// <summary>Иконка для типа карточки; null — у типа своя картинка в ассете.</summary>
    public static Sprite For(ItemType type)
    {
        if (Cache.TryGetValue(type, out var s) && s != null) return s;
        string[] rows = type == ItemType.Attack ? AttackRows : type == ItemType.Shield ? ShieldRows : type == ItemType.SuperAttack ? SuperAttackRows : null;
        if (rows == null) return null;
        // Опора — левый нижний угол, как у Arrows_*.
        s = PixelSprites.Make(rows, Pal, default, Vector2.zero);
        Cache[type] = s;
        return s;
    }

    /// <summary>Поставить карточке иконку, если у неё нет своей картинки.</summary>
    public static void Ensure(Item item)
    {
        if (item == null || item.image != null) return;
        var s = For(item.type);
        if (s != null) item.image = s;
    }
}
