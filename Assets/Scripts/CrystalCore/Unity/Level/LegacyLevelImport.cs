using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Перевод старого уровня (LevelSt) в описание для нового ядра — по тем же правилам,
    /// что у старой системы (MapManager, WayTiles, LevelSolver):
    ///   • клетка = колонка (x, y) Tilemap, берётся самый высокий блок колонки;
    ///   • ходить можно на любую клетку с тайлом — высота не мешает;
    ///   • гриб: стоишь на нём — следующий шаг на 2 клетки (LevelRules.ClassicMushroom);
    ///   • лёд (Winter): встал — скользишь в ту же сторону, пока не встанешь на обычную клетку или не упрёшься в край;
    ///   • рассыпающаяся плита (Winter): пройти можно один раз, после ухода с неё — пустота;
    ///   • кристалл берётся, когда герой ПРИШЁЛ на клетку (LevelRules.PickupOnArrival);
    ///   • цвет клетки — у верхнего блока колонки (как ColorTiles.Load);
    ///   • старт героя — клетка, ближайшая к characterPos (как CharacterCode.FindNearestTile).
    /// </summary>
    public static class LegacyLevelImport
    {
        // Как в LevelEditorWindow: Tilemap в CollectorLevel сдвинут на (0, -1.62),
        // герой стоит на (0, 0.12, 1.001) над центром клетки.
        public static readonly Vector3 GameTilemapOffset = new Vector3(0f, -1.62f, 0f);
        public static readonly Vector3 CharacterOnTileOffset = new Vector3(0f, 0.12f, 1.001f);
        // Grid в CollectorLevel: Isometric Z as Y, клетка 0.32 × 0.16 × 0.32.
        public static readonly Vector3 GridCellSize = new Vector3(0.32f, 0.16f, 0.32f);
        // Дальше этого characterPos считается «не на клетке» (как StartMatchDistance в редакторе уровней).
        public const float StartMatchDistance = 0.05f;
        // Старая очередь команд обрезалась на 300 шагах (MovingManager.MaxQueueLength).
        public const int ClassicMaxTurns = 300;

        /// <summary>Правила старой игры: гриб «встал → следующий шаг ×2», высоты не мешают, лимит 300 шагов.</summary>
        public static void ApplyClassicRules(LevelRules rules)
        {
            rules.ClassicMushroom = true;
            rules.PickupOnArrival = true;
            rules.MaxStepHeight = 1000; // старая игра высоту не проверяла вовсе
            rules.MaxTurns = ClassicMaxTurns;
        }

        /// <summary>Клетки, грибы, кристаллы и цвета старого уровня → в setup.</summary>
        public static void AddTiles(LevelSetup setup, LevelSt level)
        {
            if (level == null || level.tiles == null) return;

            // Как в старом MapManager: на одну (x,y) берём самый высокий слой.
            foreach (var group in level.tiles.Where(t => t != null).GroupBy(t => new Vector2Int(t.position.x, t.position.y)))
            {
                var top = group.OrderByDescending(t => t.position.z).First();
                int x = top.position.x, y = top.position.y, h = top.position.z;

                var crystalTile = top.tile as CrystalTile;
                var spec = crystalTile != null ? crystalTile.ToSpec(x, y, h) : new TileSpec { X = x, Y = y, Height = h };
                spec.Color = (int)top.color;

                foreach (var info in group)
                {
                    if (info.superTileType == SuperBlocks.Mushroom) spec.Type = TileType.BounceMushroom;
                    if (info.superTileType == SuperBlocks.Ice) spec.Type = TileType.Ice;
                    if (info.superTileType == SuperBlocks.Crumbling) { spec.Type = TileType.Crumbling; spec.Durability = 1; }
                    if (info.superTileType == SuperBlocks.Crystal)
                        setup.Entities.Add(new EntitySpec { Kind = EntityKind.Crystal, X = x, Y = y, Archetype = "Crystal" });
                }
                setup.Tiles.Add(spec);
            }
        }

        /// <summary>Готовый уровень для ядра прямо из LevelSt (для проверки совпадения и тестов).</summary>
        public static LevelSetup ToSetup(LevelSt level, Vector2Int heroCell)
        {
            var setup = new LevelSetup { Id = level != null ? level.name : "level" };
            ApplyClassicRules(setup.Rules);
            AddTiles(setup, level);
            setup.Entities.Add(new EntitySpec { Kind = EntityKind.Hero, X = heroCell.x, Y = heroCell.y, Facing = Dir.South });
            AddEnemies(setup, level);
            if (level != null && level.optimalCards > 0)
            {
                setup.OptimalBlocks = level.optimalCards;
                setup.GoodBlocks = GoodBlocksFor(level.optimalCards);
            }
            return setup;
        }

        /// <summary>Враги и боссы уровня, здоровье героя и число супер-атак.</summary>
        public static void AddEnemies(LevelSetup setup, LevelSt level)
        {
            if (level == null) return;
            setup.Rules.HeroHp = Mathf.Max(1, level.heroHp);
            setup.Rules.SuperAttacks = Mathf.Max(0, level.superAttacks);
            if (level.enemies == null) return;
            foreach (var e in level.enemies)
            {
                if (e == null || string.IsNullOrEmpty(e.archetype)) continue;
                // Kind = Enemy; боссом его сделает EnemyBrains.ApplyStats по архетипу.
                setup.Entities.Add(new EntitySpec { Kind = EntityKind.Enemy, Archetype = e.archetype, X = e.cell.x, Y = e.cell.y, Facing = DirOf(e.facing) });
            }
        }

        /// <summary>Стрелка карточки → направление ядра (Forward = +Y = North, как TurnResolver.HeroIntent).</summary>
        public static Dir DirOf(ItemType arrow)
        {
            switch (arrow)
            {
                case ItemType.Forward: return Dir.North;
                case ItemType.Right: return Dir.East;
                case ItemType.Bottom: return Dir.South;
                default: return Dir.West;
            }
        }

        /// <summary>
        /// Порог 2 звёзд как в FinishingLevel.CalculateStars (minSlack = 2, slackPercent = 0.3).
        /// Если в сцене у FinishingLevel другие значения — поправь goodBlocks в LevelDefinition.
        /// </summary>
        public static int GoodBlocksFor(int optimal, int minSlack = 2, float slackPercent = 0.3f) =>
            optimal + Mathf.Max(minSlack, Mathf.CeilToInt(optimal * slackPercent));

        /// <summary>
        /// Центр клетки в локальных координатах Tilemap. Если Tilemap есть (открыта сцена) — точно через него,
        /// иначе — формулой изометрии Unity для Grid CollectorLevel. Формула сверена со всеми 18 уровнями
        /// (у всех z = 0); для уровней с высотой лучше открыть сцену.
        /// </summary>
        public static Vector3 CellCenterLocal(Vector3Int cell, Tilemap tilemap)
        {
            if (tilemap != null) return tilemap.GetCellCenterLocal(cell);
            float cx = cell.x + 0.5f, cy = cell.y + 0.5f;
            return new Vector3((cx - cy) * GridCellSize.x * 0.5f, (cx + cy) * GridCellSize.y * 0.5f + cell.z * GridCellSize.z * 0.5f, 0f);
        }

        /// <summary>Где стоял бы герой на этой клетке в игре — то, что хранится в LevelSt.characterPos.</summary>
        public static Vector3 CharacterPosFor(Vector3Int cell, Tilemap tilemap) =>
            GameTilemapOffset + CellCenterLocal(cell, tilemap) + CharacterOnTileOffset;

        /// <summary>
        /// Старт = клетка (верх колонки), ближайшая к characterPos по X/Y — так её выбирает CharacterCode.
        /// distance — насколько characterPos далёк от этой клетки (больше StartMatchDistance — подозрительно).
        /// </summary>
        public static bool TryFindStartCell(LevelSt level, Tilemap tilemap, out Vector2Int cell, out float distance)
        {
            cell = default;
            distance = float.MaxValue;
            if (level == null || level.tiles == null) return false;

            var top = new Dictionary<Vector2Int, Vector3Int>();
            foreach (var t in level.tiles)
            {
                if (t == null) continue;
                var key = new Vector2Int(t.position.x, t.position.y);
                if (!top.TryGetValue(key, out var cur) || t.position.z > cur.z) top[key] = t.position;
            }

            bool found = false;
            foreach (var kv in top)
            {
                var p = CharacterPosFor(kv.Value, tilemap);
                float dx = p.x - level.characterPos.x, dy = p.y - level.characterPos.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < distance) { distance = d; cell = kv.Key; found = true; }
            }
            return found;
        }

        /// <summary>
        /// Заполнить LevelDefinition по старому уровню. Враги, tileOverrides и биом не трогаются —
        /// их можно спокойно дописывать руками, повторный импорт их не сотрёт.
        /// </summary>
        public static void FillDefinition(LevelDefinition def, LevelSt level, Vector2Int heroCell)
        {
            def.legacyLevel = level;
            def.heroCell = heroCell;
            def.heroFacing = Dir.South;
            if (def.rules == null) def.rules = new LevelRules();
            ApplyClassicRules(def.rules);
            def.optimalBlocks = level != null ? Mathf.Max(0, level.optimalCards) : 0;
            def.goodBlocks = def.optimalBlocks > 0 ? GoodBlocksFor(def.optimalBlocks) : 0;
            def.referenceSolution = level != null && level.HasSolution
                ? ProgramText.Print(ItemProgramAdapter.FromSolution(level))
                : "";
        }
    }
}
