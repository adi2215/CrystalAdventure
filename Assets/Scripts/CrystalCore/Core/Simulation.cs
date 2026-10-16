using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalAdventure.Core
{
    // ============================================================ Описание уровня (чистые данные)

    [Serializable]
    public sealed class TileSpec
    {
        public int X, Y;
        public TileType Type = TileType.Ground;
        public int Height;
        public int Channel;
        public Dir Direction;
        public bool UseGlobalWind = true;
        public int Period = 2, Offset, Durability = 1, RespawnTurns = 3;
    }

    [Serializable]
    public sealed class EntitySpec
    {
        public EntityKind Kind = EntityKind.Enemy;
        public string Archetype;
        public int X, Y;
        public Dir Facing = Dir.East;
        public int HpOverride;        // 0 = стандартное HP архетипа
    }

    [Serializable]
    public sealed class LevelSetup
    {
        public string Id = "level";
        public LevelRules Rules = new LevelRules();
        public List<TileSpec> Tiles = new List<TileSpec>();
        public List<EntitySpec> Entities = new List<EntitySpec>();
        public int OptimalBlocks;     // ≤ этого — 3 звезды
        public int GoodBlocks;        // ≤ этого — 2 звезды
        public string ReferenceSolution; // эталонное решение (ProgramText) — для автопроверки уровней

        public World Build()
        {
            var w = new World();
            w.ApplyRules(Rules);
            foreach (var t in Tiles)
            {
                var tile = w.SetTile(new Cell(t.X, t.Y), t.Type, t.Height);
                tile.Channel = t.Channel; tile.Direction = t.Direction; tile.UseGlobalWind = t.UseGlobalWind;
                tile.Period = t.Period; tile.Offset = t.Offset; tile.Durability = t.Durability; tile.RespawnTurns = t.RespawnTurns;
            }
            // Герой первым — тогда у него Id = 1 и он всегда ходит первым в журнале.
            foreach (var e in Entities.OrderBy(x => x.Kind == EntityKind.Hero ? 0 : 1))
            {
                var pos = new Cell(e.X, e.Y);
                if (e.Kind == EntityKind.Hero) { w.Spawn(EntityKind.Hero, pos, e.Facing); continue; }
                if (e.Kind == EntityKind.Crystal) { w.Spawn(EntityKind.Crystal, pos, e.Facing, 1, e.Archetype ?? "Crystal"); continue; }

                var ent = w.Spawn(e.Kind, pos, e.Facing, 1, e.Archetype, EnemyBrains.Create(e.Archetype));
                EnemyBrains.ApplyStats(ent);
                if (e.HpOverride > 0) ent.Hp = ent.MaxHp = e.HpOverride;
            }
            if (w.Hero == null) throw new InvalidOperationException($"Level {Id}: no hero");
            return w;
        }

        public LevelSetup AddTile(int x, int y, TileType type = TileType.Ground, int height = 0)
        {
            Tiles.Add(new TileSpec { X = x, Y = y, Type = type, Height = height });
            return this;
        }

        public LevelSetup AddRect(int x0, int y0, int x1, int y1, TileType type = TileType.Ground)
        {
            for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) AddTile(x, y, type);
            return this;
        }

        public LevelSetup Set(int x, int y, TileType type, Action<TileSpec> cfg = null)
        {
            Tiles.RemoveAll(t => t.X == x && t.Y == y);
            var spec = new TileSpec { X = x, Y = y, Type = type };
            cfg?.Invoke(spec);
            Tiles.Add(spec);
            return this;
        }

        public LevelSetup Add(EntityKind kind, int x, int y, string archetype = null, Dir facing = Dir.East)
        {
            Entities.Add(new EntitySpec { Kind = kind, X = x, Y = y, Archetype = archetype, Facing = facing });
            return this;
        }
    }

    // ============================================================ Результат прогона

    public enum Outcome { Running, Won, Lost, ProgramEnded, Error, TurnLimit }

    public sealed class TurnRecord
    {
        public int Turn;
        public ActionType Action;
        public int BlockId;              // какой блок программы подсветить
        public List<GameEvent> Events;
    }

    public sealed class SimulationResult
    {
        public Outcome Outcome;
        public string Message;
        public List<GameEvent> InitialEvents = new List<GameEvent>(); // намерения врагов до старта
        public List<TurnRecord> Turns = new List<TurnRecord>();
        public int BlocksUsed;
        public int Stars;
        public World Final;

        public bool Won => Outcome == Outcome.Won;
        public IEnumerable<GameEvent> AllEvents => InitialEvents.Concat(Turns.SelectMany(t => t.Events));
    }

    /// <summary>
    /// Главная идея новой архитектуры: при нажатии «Старт» игра МГНОВЕННО считает
    /// весь прогон программы целиком (это микросекунды), а потом просто
    /// проигрывает записанные события с нужной скоростью.
    ///
    /// Логика больше не зависит от анимаций, кадров и корутин — значит:
    ///  • скорость x1 / x2 / x4 / «мгновенно» меняет только проигрывание;
    ///  • перезапуск = заново построить World из LevelSetup (никаких «откатов» списков);
    ///  • любой уровень можно проверить тестом без Unity.
    /// </summary>
    public static class Simulation
    {
        public static SimulationResult Run(LevelSetup level, ProgramDef program)
        {
            var world = level.Build();
            var runner = new ProgramRunner(program);
            var result = new SimulationResult { BlocksUsed = program.CountBlocks(), Final = world };

            result.InitialEvents.AddRange(TurnResolver.PlanOnly(world));

            while (true)
            {
                if (IsWon(world)) { Finish(result, Outcome.Won, "Level complete", level); break; }
                if (!world.Hero.Alive) { Finish(result, Outcome.Lost, "Hero defeated", level); break; }
                if (world.Turn >= world.Rules.MaxTurns) { Finish(result, Outcome.TurnLimit, "Too many turns", level); break; }

                if (!runner.Next(world, out var action))
                {
                    if (runner.Status == RunnerStatus.Error) Finish(result, Outcome.Error, runner.Error, level);
                    else Finish(result, Outcome.ProgramEnded, "Program ended before the goal", level);
                    break;
                }

                var events = TurnResolver.Resolve(world, action);
                result.Turns.Add(new TurnRecord { Turn = world.Turn, Action = action, BlockId = runner.CurrentBlockId, Events = events });
            }
            return result;
        }

        public static SimulationResult Run(LevelSetup level, string programText) =>
            Run(level, ProgramText.Parse(programText));

        public static bool IsWon(World w)
        {
            if (w.Crystals.Any()) return false;
            if (w.Entities.Any(e => e.Alive && e.Kind == EntityKind.Boss)) return false; // уровень с боссом — победить босса
            if (w.Rules.RequireAllEnemiesDefeated && w.Hostiles.Any()) return false;
            return true;
        }

        static void Finish(SimulationResult r, Outcome o, string msg, LevelSetup level)
        {
            r.Outcome = o;
            r.Message = msg;
            r.Stars = o == Outcome.Won ? StarsFor(level, r.BlocksUsed) : 0;
            var last = r.Turns.Count > 0 ? r.Turns[r.Turns.Count - 1].Events : r.InitialEvents;
            last.Add(new GameEvent(o == Outcome.Won ? GameEventType.Won : GameEventType.Lost, EventPhase.Resolve) { Info = msg });
        }

        public static int StarsFor(LevelSetup level, int blocks)
        {
            if (level.OptimalBlocks <= 0) return 3;
            if (blocks <= level.OptimalBlocks) return 3;
            int good = level.GoodBlocks > 0 ? level.GoodBlocks : level.OptimalBlocks + 3;
            return blocks <= good ? 2 : 1;
        }
    }
}
