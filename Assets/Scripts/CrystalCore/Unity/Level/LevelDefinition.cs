using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Описание уровня для нового ядра. Можно собирать из старого LevelSt
    /// (клетки + грибы + кристаллы + цвета) и дополнять врагами, правилами и звёздами.
    /// Create → Crystal Adventure → Level, или сразу для всех старых уровней:
    /// Crystal Adventure → Ядро → Создать LevelDefinition из LevelSt.
    ///
    /// Исправлено: цвет клеток (условия покрашенных карточек) раньше терялся.
    /// </summary>
    [CreateAssetMenu(menuName = "Crystal Adventure/Level")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Карта")]
        public Biome biome = Biome.Forest;
        [Tooltip("Старый уровень: клетки, грибы и кристаллы берутся из него")]
        public LevelSt legacyLevel;
        [Tooltip("Доп. / переопределённые клетки (ловушки, ворота, лёд...)")]
        public List<TileSpec> tileOverrides = new List<TileSpec>();

        [Header("Герой")]
        public Vector2Int heroCell;
        public Dir heroFacing = Dir.East;

        [Header("Враги, боссы, кристаллы")]
        [Tooltip("Archetype: SmallBallScout, ForestDrone, TrainingDummy, FrostTurret, HeavyIceGuard, WoodMechGuardian, ...")]
        public List<EntitySpec> entities = new List<EntitySpec>();

        [Header("Правила")]
        public LevelRules rules = new LevelRules();

        [Header("Звёзды (по количеству блоков)")]
        [Min(0)] public int optimalBlocks;
        [Min(0)] public int goodBlocks;

        [Header("Проверка уровня")]
        [Tooltip("Эталонное решение, например: def A { F F A } repeat 3 { call A R }")]
        [TextArea(2, 6)] public string referenceSolution;

        public LevelSetup ToSetup()
        {
            var setup = new LevelSetup
            {
                Id = name, Rules = rules, OptimalBlocks = optimalBlocks, GoodBlocks = goodBlocks,
                ReferenceSolution = referenceSolution,
            };

            // Клетки, грибы, кристаллы и цвета старого уровня (тот же код, что у проверки совпадения).
            LegacyLevelImport.AddTiles(setup, legacyLevel);

            foreach (var o in tileOverrides)
            {
                setup.Tiles.RemoveAll(t => t.X == o.X && t.Y == o.Y);
                setup.Tiles.Add(o);
            }

            setup.Entities.Add(new EntitySpec { Kind = EntityKind.Hero, X = heroCell.x, Y = heroCell.y, Facing = heroFacing });
            setup.Entities.AddRange(entities);
            return setup;
        }

        /// <summary>Высоты клеток для перевода «клетка → мир».</summary>
        public Dictionary<Cell, int> Heights()
        {
            var d = new Dictionary<Cell, int>();
            foreach (var t in ToSetup().Tiles) d[new Cell(t.X, t.Y)] = t.Height;
            return d;
        }
    }
}
