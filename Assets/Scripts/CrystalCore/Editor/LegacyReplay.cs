using System.Collections.Generic;
using CrystalAdventure.UnityLayer;
using UnityEngine;

namespace CrystalAdventure.EditorTools
{
    /// <summary>
    /// Эталон СТАРЫХ правил для проверки нового ядра. Нарочно написан отдельно от ядра и повторяет
    /// старый код один в один, чтобы расхождение было видно, а не «совпало само с собой»:
    ///   • очередь шагов — как MovingManager.Expand (While со скобкой, F1/F2 до глубины 6, лимит 300);
    ///   • шаг — как CharacterCode.RunRoutine + WayTiles.Resolve (гриб под ногами = шаг на 2 клетки,
    ///     нет клетки — куб балансирует на краю и остаётся, покрашенная карточка не на своём цвете — пропуск);
    ///   • кристалл — когда куб встал на клетку; все собраны — победа, остальные шаги не выполняются;
    ///   • Winter: лёд — встал на лёд, скользишь в ту же сторону, пока следующая клетка есть (останавливаешься
    ///     на первой не-ледяной клетке или на последнем льду у края); рассыпающаяся плита — после ухода с неё пустота.
    /// Только для редактора и тестов, в сборку игры не попадает.
    /// </summary>
    public static class LegacyReplay
    {
        const int MaxQueueLength = 300; // MovingManager.MaxQueueLength
        const int MaxCallDepth = 6;     // MovingManager.MaxCallDepth

        public struct Result
        {
            public bool Won;
            public int Steps;         // сколько шагов очереди сыграно (вместе с пропусками и краями)
            public Vector2Int Final;
            public int EdgeHits;
            public int Skipped;
            public int Collected;
            public int CrystalsTotal;
            public int QueueLength;   // длина развёрнутой очереди (без учёта ранней победы)
        }

        struct Step
        {
            public ItemType Type;
            public int Color;
        }

        /// <param name="maxQueue">Лимит очереди. 300 — как в игре; больше — чтобы узнать «честную» длину программы
        /// (старый код на длинных программах молча выкидывал часть повторов цикла).</param>
        public static Result Run(LevelSt level, Vector2Int start, System.Func<SlotType, IList<CardSpec>> panel, int maxQueue = MaxQueueLength)
        {
            // Поле: колонки (x, y) — как ключи MapManager.map; гриб/кристалл/цвет — у верхнего блока колонки.
            var cells = new HashSet<Vector2Int>();
            var mushrooms = new HashSet<Vector2Int>();
            var crystals = new HashSet<Vector2Int>();
            var ice = new HashSet<Vector2Int>();
            var crumbling = new HashSet<Vector2Int>();
            var broken = new HashSet<Vector2Int>();
            var colors = new Dictionary<Vector2Int, int>();
            var topZ = new Dictionary<Vector2Int, int>();
            if (level != null && level.tiles != null)
            {
                foreach (var t in level.tiles)
                {
                    if (t == null) continue;
                    var key = new Vector2Int(t.position.x, t.position.y);
                    cells.Add(key);
                    if (t.superTileType == SuperBlocks.Mushroom) mushrooms.Add(key);
                    if (t.superTileType == SuperBlocks.Crystal) crystals.Add(key);
                    if (t.superTileType == SuperBlocks.Ice) ice.Add(key);
                    if (t.superTileType == SuperBlocks.Crumbling) crumbling.Add(key);
                    if (!topZ.TryGetValue(key, out var z) || t.position.z > z) { topZ[key] = t.position.z; colors[key] = (int)t.color; }
                }
            }

            var queue = new List<Step>();
            Expand(panel, SlotType.Main, 0, queue, maxQueue);

            var r = new Result { Final = start, CrystalsTotal = crystals.Count, QueueLength = queue.Count };
            var pos = start;
            var collected = new HashSet<Vector2Int>();
            foreach (var step in queue)
            {
                r.Steps++;
                if (step.Color != 0 && (!colors.TryGetValue(pos, out var c) || c != step.Color)) { r.Skipped++; continue; }

                var dir = Direction(step.Type);
                if (dir == Vector2Int.zero) { r.EdgeHits++; continue; } // WayTiles: шага нет → «край»
                int dist = mushrooms.Contains(pos) ? 2 : 1;
                var target = new Vector2Int(pos.x + dir.x * dist, pos.y + dir.y * dist);
                if (!cells.Contains(target) || broken.Contains(target)) { r.EdgeHits++; continue; }

                if (crumbling.Contains(pos)) broken.Add(pos); // ушёл с плиты — она падает
                pos = target;
                while (ice.Contains(pos))
                {
                    var next = new Vector2Int(pos.x + dir.x, pos.y + dir.y);
                    if (!cells.Contains(next) || broken.Contains(next)) break;
                    pos = next;
                }
                if (crystals.Contains(pos) && collected.Add(pos) && collected.Count == crystals.Count && crystals.Count > 0)
                {
                    r.Won = true;
                    break;
                }
            }
            r.Final = pos;
            r.Collected = collected.Count;
            return r;
        }

        static Vector2Int Direction(ItemType t)
        {
            switch (t)
            {
                case ItemType.Forward: return new Vector2Int(0, 1);
                case ItemType.Right: return new Vector2Int(1, 0);
                case ItemType.Left: return new Vector2Int(-1, 0);
                case ItemType.Bottom: return new Vector2Int(0, -1);
                default: return Vector2Int.zero;
            }
        }

        static void Expand(System.Func<SlotType, IList<CardSpec>> panel, SlotType slot, int depth, List<Step> output, int maxQueue)
        {
            var list = panel(slot) ?? new List<CardSpec>();
            var local = new List<Step>();
            var startOf = new int[list.Count + 1];
            int lastWhile = -1;

            for (int i = 0; i < list.Count; i++)
            {
                startOf[i] = local.Count;
                var card = list[i];
                if (card.Empty) continue;

                switch (card.Type)
                {
                    case ItemType.While:
                    {
                        int maxSpan = i - (lastWhile + 1);
                        lastWhile = i;
                        int span = card.Span < 0 ? maxSpan : Mathf.Clamp(card.Span, 0, maxSpan);
                        int from = startOf[i - span];
                        var body = local.GetRange(from, local.Count - from);
                        if (body.Count == 0) break;
                        for (int r = 0; r < card.Iterations && local.Count + body.Count <= maxQueue; r++)
                            local.AddRange(body);
                        break;
                    }
                    case ItemType.FunctionOne:
                        if (depth < MaxCallDepth) Expand(panel, SlotType.FunctionOne, depth + 1, local, maxQueue);
                        break;
                    case ItemType.FunctionTwo:
                        if (depth < MaxCallDepth) Expand(panel, SlotType.FunctionTwo, depth + 1, local, maxQueue);
                        break;
                    case ItemType.Ifandelse:
                    case ItemType.Null:
                        break;
                    default:
                        local.Add(new Step { Type = card.Type, Color = card.Color });
                        break;
                }
                if (local.Count > maxQueue) local.RemoveRange(maxQueue, local.Count - maxQueue);
            }

            output.AddRange(local);
            if (output.Count > maxQueue) output.RemoveRange(maxQueue, output.Count - maxQueue);
        }
    }
}
