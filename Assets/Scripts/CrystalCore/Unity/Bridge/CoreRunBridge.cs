using System.Collections.Generic;
using CrystalAdventure.Core;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>Один шаг программы, заранее посчитанный ядром, — его проигрывает старый куб (CharacterCode).</summary>
    public struct CorePlannedStep
    {
        public bool Skipped;     // покрашенная карточка не на своём цвете — пропуск
        public bool Moved;       // герой перешёл на другую клетку (иначе — упёрся: край или стена)
        public Vector2Int To;    // где герой оказался после шага (колонка x, y как ключ MapManager.map)
    }

    /// <summary>
    /// Фаза 3 переноса: РЕШАЕТ новое ядро, ПОКАЗЫВАЕТ старая система.
    ///
    /// MovingManager (флаг useCrystalCore) просит отсюда план: программа из карточек игрока
    /// (ItemProgramAdapter.FromMovingManager) прогоняется через Simulation на уровне из LevelSt
    /// (LegacyLevelImport), и каждый ход превращается в CorePlannedStep. CharacterCode проигрывает
    /// его теми же анимациями, что раньше (шаг, прыжок с гриба, балансирование на краю, пропуск),
    /// а кристаллы, победу, подсветку карточек, звёзды и туториал по-прежнему ведёт старый код.
    ///
    /// Ход i ядра = шаг i старой очереди MovingManager.BuildQueue (проверено тестами на всех уровнях),
    /// поэтому подсветка карточек и скобок циклов по номеру шага остаётся верной.
    /// </summary>
    public static class CoreRunBridge
    {
        /// <param name="start">Клетка старта героя — та же, что у CharacterCode.StartTile.</param>
        public static bool TryPlan(MovingManager moving, LevelSt level, Vector2Int start,
                                   out List<CorePlannedStep> plan, out SimulationResult result)
        {
            plan = null;
            result = null;
            if (moving == null || level == null) return false;

            var setup = LegacyLevelImport.ToSetup(level, start);
            var program = ItemProgramAdapter.FromMovingManager(moving);
            result = Simulation.Run(setup, program);
            plan = ToPlan(result, start);
            return true;
        }

        /// <summary>Ходы ядра → шаги для куба. Позиция после хода — последнее перемещение героя в этом ходу.</summary>
        public static List<CorePlannedStep> ToPlan(SimulationResult result, Vector2Int start)
        {
            var plan = new List<CorePlannedStep>(result.Turns.Count);
            int heroId = result.Final != null && result.Final.Hero != null ? result.Final.Hero.Id : 1;
            var pos = start;

            foreach (var turn in result.Turns)
            {
                if (turn.Skipped)
                {
                    plan.Add(new CorePlannedStep { Skipped = true, To = pos });
                    continue;
                }

                var before = pos;
                foreach (var e in turn.Events)
                {
                    if (e.EntityId != heroId) continue;
                    switch (e.Type)
                    {
                        case GameEventType.Moved:
                        case GameEventType.Slid:
                        case GameEventType.Pushed:
                        case GameEventType.Bounced:
                        case GameEventType.Teleported:
                            pos = new Vector2Int(e.To.X, e.To.Y);
                            break;
                    }
                }
                plan.Add(new CorePlannedStep { Moved = pos != before, To = pos });
            }
            return plan;
        }
    }
}
