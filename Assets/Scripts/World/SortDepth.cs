using UnityEngine;

/// <summary>
/// Порядок отрисовки персонажей (герой-куб, враги, боссы) по клетке — одна формула для всех.
///
/// Исправлено: персонажи перекрывали друг друга как попало. В проекте спрайты с одинаковым sortingOrder
/// сортируются по оси Y (Graphics → Transparency Sort Axis), а точка сортировки у куба — Pivot, у
/// созданных кодом спрайтов — Center; плюс у всех был один и тот же sortingOrder. Теперь глубина явная:
/// чем ближе клетка к камере (меньше x + y в изометрии), тем больше sortingOrder.
///
/// Слои над клеткой (base — sortingOrder клетки):
///   base + 1..2  — снег, лёд, трещины (World/SpecialTiles);
///   base + 3     — красные клетки «сюда шагнёт / ударит враг» (World/EnemyLayer) — всегда ПОД персонажами;
///   base + 10 + глубина — персонажи.
/// </summary>
public static class SortDepth
{
    public const int MarkerOffset = 3;
    const int CharacterOffset = 10;

    /// <summary>sortingOrder персонажа на клетке (x, y).</summary>
    public static int Character(int baseOrder, int x, int y) =>
        baseOrder + CharacterOffset + Mathf.Max(0, (100 - (x + y)) * 2);

    public static int Character(OverlayTile tile)
    {
        if (tile == null) return CharacterOffset;
        var sr = tile.GetComponent<SpriteRenderer>();
        return Character(sr != null ? sr.sortingOrder : 0, tile.gridLocation.x, tile.gridLocation.y);
    }

    /// <summary>sortingOrder красной клетки (над снегом и льдом, под всеми персонажами).</summary>
    public static int Marker(OverlayTile tile)
    {
        var sr = tile != null ? tile.GetComponent<SpriteRenderer>() : null;
        return (sr != null ? sr.sortingOrder : 0) + MarkerOffset;
    }
}
