using System.Collections.Generic;
using CrystalAdventure.Core;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>Один шаг программы, заранее посчитанный ядром, — его проигрывает старый куб (CharacterCode).</summary>
    public struct CorePlannedStep
    {
        public bool Skipped;     // покрашенная карточка не на своём цвете — пропуск
        public bool Moved;       // герой перешёл на другую клетку (иначе — упёрся или стоял на месте)
        public Vector2Int To;    // где герой оказался после шага (колонка x, y как ключ MapManager.map)
        public bool Slid;        // лёд: сначала встал на Via, потом скользил до To
        public Vector2Int Via;

        public ActionType Action;   // что сделал герой (ход, атака, щит…)
        /// <summary>Хотел шагнуть, но упёрся НЕ в край: во врага, стену, другого. Это не «чуть не упал».</summary>
        public bool Blocked;
        public Vector2Int Facing;   // куда смотрит герой после хода (+X / +Y сетки) — туда бьёт атака
        public int HeroHp;          // здоровье героя после хода
        public bool HeroDied;       // герой пал в этом ходу
        public bool Won;            // после этого хода уровень пройден (решает ядро — на уровнях с врагами)

        public bool IsMove => Action == ActionType.MoveForward || Action == ActionType.MoveBack ||
                              Action == ActionType.MoveLeft || Action == ActionType.MoveRight;
    }

    /// <summary>
    /// Фаза 3 переноса: РЕШАЕТ новое ядро, ПОКАЗЫВАЕТ старая система.
    ///
    /// MovingManager (флаг useCrystalCore) просит отсюда план: программа из карточек игрока
    /// (ItemProgramAdapter.FromMovingManager) прогоняется через Simulation на уровне из LevelSt
    /// (LegacyLevelImport), и каждый ход превращается в CorePlannedStep. CharacterCode проигрывает
    /// его теми же анимациями, что раньше (шаг, прыжок с гриба, балансирование на краю, пропуск),
    /// а кристаллы, победу, подсветку карточек, звёзды и туториал по-прежнему ведёт старый код.
    /// Враги и боссы (World/EnemyLayer) проигрываются по Last — тому же прогону.
    ///
    /// Ход i ядра = шаг i старой очереди MovingManager.BuildQueue (проверено тестами на всех уровнях),
    /// поэтому подсветка карточек и скобок циклов по номеру шага остаётся верной.
    /// </summary>
    public static class CoreRunBridge
    {
        /// <summary>Последний посчитанный прогон (для EnemyLayer: враги, урон, призывы, заморозка).</summary>
        public static SimulationResult Last { get; private set; }

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
            Last = result;
            plan = ToPlan(result, start);
            return true;
        }

        /// <summary>Ходы ядра → шаги для куба. Позиция после хода — последнее перемещение героя в этом ходу.</summary>
        public static List<CorePlannedStep> ToPlan(SimulationResult result, Vector2Int start)
        {
            var plan = new List<CorePlannedStep>(result.Turns.Count);
            int heroId = result.Final != null && result.Final.Hero != null ? result.Final.Hero.Id : 1;
            int hp = result.Final != null && result.Final.Rules != null ? result.Final.Rules.HeroHp : 3;
            var pos = start;
            var facing = new Vector2Int(0, -1); // герой начинает лицом вниз (Dir.South), как куб FaceDefault

            for (int i = 0; i < result.Turns.Count; i++)
            {
                var turn = result.Turns[i];
                bool last = i == result.Turns.Count - 1;
                if (turn.Skipped)
                {
                    plan.Add(new CorePlannedStep { Skipped = true, To = pos, Action = turn.Action, Facing = facing, HeroHp = hp, Won = last && result.Won });
                    continue;
                }

                var before = pos;
                Vector2Int? via = null;
                bool slid = false, blocked = false, died = false;
                foreach (var e in turn.Events)
                {
                    if (e.EntityId != heroId) continue;
                    switch (e.Type)
                    {
                        case GameEventType.Moved:
                            pos = new Vector2Int(e.To.X, e.To.Y);
                            if (via == null) via = pos;
                            facing = Offset(e.Dir);
                            break;
                        case GameEventType.Slid:
                            slid = true;
                            pos = new Vector2Int(e.To.X, e.To.Y);
                            break;
                        case GameEventType.Pushed:
                        case GameEventType.Bounced:
                        case GameEventType.Teleported:
                            pos = new Vector2Int(e.To.X, e.To.Y);
                            break;
                        case GameEventType.Bumped:
                            // Упёрся: повернулся туда, куда хотел (так ядро решает, куда потом бить).
                            if (e.Phase == EventPhase.Move)
                            {
                                facing = Offset(e.Dir);
                                blocked = e.Info != TurnResolver.EdgeReason;
                            }
                            break;
                        case GameEventType.Damaged:
                            hp = System.Math.Max(0, hp - e.Amount);
                            break;
                        case GameEventType.Died:
                        case GameEventType.Fell:
                            died = true;
                            hp = 0;
                            break;
                    }
                }
                plan.Add(new CorePlannedStep
                {
                    Moved = pos != before, To = pos, Slid = slid && via != null, Via = via ?? pos,
                    Action = turn.Action, Blocked = blocked && pos == before, Facing = facing,
                    HeroHp = hp, HeroDied = died, Won = last && result.Won,
                });
            }
            return plan;
        }

        static Vector2Int Offset(Dir d)
        {
            var o = d.Offset();
            return new Vector2Int(o.X, o.Y);
        }
    }
}
