using System.Collections.Generic;
using CrystalAdventure.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>Перевод логической клетки в мировую позицию изометрического Tilemap.</summary>
    public class GridMapper : MonoBehaviour
    {
        public Tilemap tilemap;

        [Tooltip("Как в старом MapManager: overlay стоит на +0.12 по Y")]
        public float yOffset = 0.12f;
        [Tooltip("Как в старом CharacterCode: персонаж на +1.001 по Z относительно клетки")]
        public float zOffset = 1.001f;
        [Tooltip("Базовый sortingOrder для героя и врагов")]
        public int sortingOrderOnTop = 10;

        readonly Dictionary<Cell, int> _heights = new Dictionary<Cell, int>();

        public void SetHeights(Dictionary<Cell, int> heights)
        {
            _heights.Clear();
            foreach (var kv in heights) _heights[kv.Key] = kv.Value;
        }

        public int HeightAt(Cell c) => _heights.TryGetValue(c, out var h) ? h : 0;

        public Vector3Int ToTilemapCell(Cell c) => new Vector3Int(c.X, c.Y, HeightAt(c));

        public Vector3 CellToWorld(Cell c)
        {
            var p = tilemap.GetCellCenterWorld(ToTilemapCell(c));
            return new Vector3(p.x, p.y + yOffset, p.z + zOffset);
        }

        /// <summary>Чем ниже по экрану (больше X+Y в изометрии меньше), тем «ближе» — рисуем поверх.</summary>
        public int SortingOrderFor(Cell c) => sortingOrderOnTop - (c.X + c.Y) * 2 + HeightAt(c);

        public static Vector3 DirToWorld(Dir d)
        {
            // Изометрия 2:1: +X ячейки уходит вправо-вверх, +Y — влево-вверх.
            switch (d)
            {
                case Dir.North: return new Vector3(-0.5f, 0.25f, 0f);
                case Dir.East: return new Vector3(0.5f, 0.25f, 0f);
                case Dir.South: return new Vector3(0.5f, -0.25f, 0f);
                default: return new Vector3(-0.5f, -0.25f, 0f);
            }
        }
    }
}
