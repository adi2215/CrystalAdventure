using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Цветные клетки — условия «если».
/// Клетка уровня может быть красной / синей / жёлтой (TileInfo.color, ставится в Редакторе уровней).
/// Карточку хода можно «покрасить» (метка в углу карточки): тогда она срабатывает,
/// только если куб стоит на клетке того же цвета, иначе пропускается.
/// Две соседние карточки разного цвета — это и есть if / else.
/// </summary>
public static class ColorTiles
{
    static readonly Dictionary<Vector2Int, TileColor> Current = new Dictionary<Vector2Int, TileColor>();
    static readonly List<TileColor> Used = new List<TileColor>();

    /// <summary>На уровне есть цветные клетки (иначе метки на карточках не показываются).</summary>
    public static bool Any => Used.Count > 0;

    /// <summary>Цвета, которые есть на уровне (по порядку перечисления).</summary>
    public static IReadOnlyList<TileColor> UsedColors => Used;

    public static TileColor At(int x, int y) => Current.TryGetValue(new Vector2Int(x, y), out var c) ? c : TileColor.None;

    public static TileColor At(OverlayTile tile) => tile == null ? TileColor.None : At(tile.gridLocation.x, tile.gridLocation.y);

    /// <summary>Может ли карточка с условием color сработать на этой клетке.</summary>
    public static bool Passes(TileColor condition, OverlayTile tile) => condition == TileColor.None || At(tile) == condition;

    /// <summary>Цвет для интерфейса (метки на карточках, подсказки).</summary>
    public static Color Ui(TileColor c)
    {
        switch (c)
        {
            case TileColor.Red: return new Color(1f, 0.36f, 0.36f, 1f);
            case TileColor.Blue: return new Color(0.32f, 0.58f, 1f, 1f);
            case TileColor.Yellow: return new Color(1f, 0.84f, 0.25f, 1f);
            default: return Color.white;
        }
    }

    /// <summary>Оттенок блока на карте (умножается на спрайт тайла).</summary>
    public static Color Tint(TileColor c)
    {
        switch (c)
        {
            case TileColor.Red: return new Color(1f, 0.38f, 0.36f, 1f);
            case TileColor.Blue: return new Color(0.4f, 0.6f, 1f, 1f);
            case TileColor.Yellow: return new Color(1f, 0.92f, 0.3f, 1f);
            default: return Color.white;
        }
    }

    /// <summary>Следующий цвет метки: нет → цвета уровня по кругу → нет.</summary>
    public static TileColor Next(TileColor c)
    {
        if (Used.Count == 0) return TileColor.None;
        int i = c == TileColor.None ? -1 : Used.IndexOf(c);
        return i + 1 < Used.Count ? Used[i + 1] : TileColor.None;
    }

    /// <summary>Вызывается при загрузке уровня: запоминает цвета и красит блоки Tilemap.</summary>
    public static void Load(LevelSt level, Tilemap tilemap)
    {
        Current.Clear();
        Used.Clear();
        if (level == null || level.tiles == null) return;

        // Цвет берётся у верхнего блока колонки
        var top = new Dictionary<Vector2Int, TileInfo>();
        foreach (var info in level.tiles)
        {
            if (info == null) continue;
            var key = new Vector2Int(info.position.x, info.position.y);
            if (!top.TryGetValue(key, out var cur) || info.position.z > cur.position.z) top[key] = info;
        }
        foreach (var kv in top)
        {
            var c = kv.Value.color;
            if (c == TileColor.None) continue;
            Current[kv.Key] = c;
            if (!Used.Contains(c)) Used.Add(c);
            if (tilemap != null)
            {
                tilemap.SetTileFlags(kv.Value.position, TileFlags.None);
                tilemap.SetColor(kv.Value.position, Tint(c));
            }
        }
        Used.Sort();
    }
}

