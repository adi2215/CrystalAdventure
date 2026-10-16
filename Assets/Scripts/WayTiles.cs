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

        if (map.TryGetValue(key, out var tile) && tile != null)
        {
            step.target = tile;
            step.fromMushroom = standingOnMushroom;
            step.onMushroom = IsMushroom(tile);
        }
        return step;
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
