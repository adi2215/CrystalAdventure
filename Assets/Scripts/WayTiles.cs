using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Куда пойдёт куб по одной команде. Чистая функция: ничего не меняет,
/// не трогает списки команд (раньше удаляла команду из MovingManager — это ломало Rewind).
/// </summary>
public class WayTiles
{
    public struct Step
    {
        public OverlayTile target;   // null = идти некуда (край карты) — куб стоит
        public bool fromMushroom;    // прыжок с гриба на 2 клетки
        public bool onMushroom;      // запрыгиваем НА гриб
        public OverlayTile via;      // лёд: клетка, куда куб сначала встал (дальше скользит до target)
        public bool slid;            // был лёд — от via до target куб скользит
    }

    public static Step Resolve(OverlayTile start, Item cmd, Dictionary<Vector3Int, OverlayTile> map) =>
        cmd == null ? new Step() : Resolve(start, cmd.type, map);

    public static Step Resolve(OverlayTile start, ItemType type, Dictionary<Vector3Int, OverlayTile> map)
    {
        var step = new Step();
        if (start == null || map == null) return step;

        Vector3Int dir = Direction(type);
        if (dir == Vector3Int.zero) return step;

        bool standingOnMushroom = IsMushroom(start);
        int distance = standingOnMushroom ? 2 : 1;

        // Ключи в MapManager.map — (x, y, 0). gridLocation хранит ещё и слой z,
        // поэтому его нельзя подставлять напрямую (раньше на высоких клетках куб не мог ходить).
        var key = new Vector3Int(start.gridLocation.x + dir.x * distance, start.gridLocation.y + dir.y * distance, 0);

        if (map.TryGetValue(key, out var tile) && tile != null && !SpecialTiles.IsBroken(tile))
        {
            step.target = tile;
            step.fromMushroom = standingOnMushroom;
            step.onMushroom = IsMushroom(tile);
            ApplyIce(ref step, dir, map);
        }
        return step;
    }

    /// <summary>
    /// Лёд (Winter): встал на лёд — скользишь в ту же сторону, пока следующая клетка есть
    /// (стоп на первой не-ледяной клетке или на последнем льду у края). Те же правила, что в ядре и LevelSolver.
    /// </summary>
    public static void ApplyIce(ref Step step, Vector3Int dir, Dictionary<Vector3Int, OverlayTile> map)
    {
        if (step.target == null || !SpecialTiles.IsIce(step.target)) return;
        var first = step.target;
        var cur = first;
        for (int guard = 0; guard < 64 && SpecialTiles.IsIce(cur); guard++)
        {
            var key = new Vector3Int(cur.gridLocation.x + dir.x, cur.gridLocation.y + dir.y, 0);
            if (!map.TryGetValue(key, out var next) || next == null || SpecialTiles.IsBroken(next)) break;
            cur = next;
        }
        if (cur == first) return;
        step.via = first;
        step.slid = true;
        step.target = cur;
        step.onMushroom = IsMushroom(cur);
    }

    public static bool IsMushroom(OverlayTile tile)
    {
        if (tile == null) return false;
        if (tile.block == SuperBlocks.Mushroom) return true;
        return tile.attachedObject != null && tile.attachedObject.TryGetComponent<MushroomObj>(out _);
    }

    public static Vector3Int Direction(ItemType type)
    {
        switch (type)
        {
            case ItemType.Forward: return new Vector3Int(0, +1, 0);
            case ItemType.Right: return new Vector3Int(+1, 0, 0);
            case ItemType.Left: return new Vector3Int(-1, 0, 0);
            case ItemType.Bottom: return new Vector3Int(0, -1, 0);
            default: return Vector3Int.zero;
        }
    }
}
