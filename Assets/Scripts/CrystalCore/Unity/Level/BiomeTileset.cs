using System;
using System.Collections.Generic;
using CrystalAdventure.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalAdventure.UnityLayer
{
    public enum Biome { Forest, Winter, Sky }

    /// <summary>
    /// Как выглядят типы клеток в конкретном биоме. Одна механика (например Gate) —
    /// три разных спрайта: деревянные ворота в Лесу, ледяные в Холоде, облачные в Небе.
    /// Create → Crystal Adventure → Biome Tileset.
    /// </summary>
    [CreateAssetMenu(menuName = "Crystal Adventure/Biome Tileset")]
    public class BiomeTileset : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public TileType type;
            [Tooltip("Обычный вид (проходимая / активная клетка)")] public TileBase normal;
            [Tooltip("Закрыто / выключено (ворота закрыты, лиана не выросла, платформа не активна). Пусто = как normal")]
            public TileBase closed;
        }

        public Biome biome;
        public TileBase fallbackGround;
        public List<Entry> entries = new List<Entry>();

        /// <summary>Тайл для отрисовки. null = клетку не рисуем (провалилась / исчезла).</summary>
        public TileBase Resolve(TileType type, bool gone, bool walkable)
        {
            if (gone) return null;
            foreach (var e in entries)
            {
                if (e.type != type) continue;
                if (!walkable && e.closed != null) return e.closed;
                return e.normal != null ? e.normal : fallbackGround;
            }
            return fallbackGround;
        }
    }
}
