using System;
using System.Collections.Generic;

namespace CrystalAdventure.Core
{
    /// <summary>
    /// Лесной босс — змея из кубиков («BossForest» с маршрутом и телом, LevelEnemy.body).
    ///
    /// Голова ползёт по маршруту по кругу (как ScriptedBrain), сегменты тела (SnakeBodyBrain) едут
    /// следом — каждый в клетку того, кто перед ним; касание любого сегмента смертельно.
    /// Каждый period-й ход змея останавливается и бьёт, по очереди:
    ///   шипы — 3 клетки перед головой: в ход удара и ещё SpikeLinger ходов наступать нельзя (смерть);
    ///   хвост — клетки вокруг хвоста проваливаются насовсем (кроме клеток самого маршрута змеи —
    ///           иначе она застряла бы), так арена постепенно сужается.
    /// За ход до удара клетки подсвечиваются (IntentPlanned), как у ледяного босса.
    /// </summary>
    public sealed class SnakeBrain : IEnemyBrain
    {
        public const string Spikes = "spikes";
        public const string Quake = "quake";
        public const int SpikeReach = 3;
        public const int SpikeLinger = 2;

        readonly string _script;
        readonly int _period;
        readonly HashSet<Cell> _route = new HashSet<Cell>();
        int _index, _shots;

        /// <summary>Последний сегмент тела (для удара хвостом). Назначает LevelSetup.Build.</summary>
        public Entity Tail;

        public SnakeBrain(string script, Cell start, int period = 4)
        {
            _script = (script ?? "").Replace(" ", "").ToUpperInvariant();
            _period = Math.Max(2, period);
            // Клетки маршрута — их хвост не рушит.
            var c = start;
            _route.Add(c);
            foreach (char ch in _script)
                if (ScriptedBrain.TryDir(ch, out var d)) { c = c.Step(d); _route.Add(c); }
        }

        public Intent Plan(World w, Entity self)
        {
            if ((w.Turn + 1) % _period == 0)
            {
                bool quake = _shots++ % 2 == 1;
                var area = new List<Cell>();
                if (quake)
                {
                    var tail = Tail != null && Tail.Alive ? Tail.Pos : self.Pos;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var c = new Cell(tail.X + dx, tail.Y + dy);
                            var t = w.GetTile(c);
                            if (t == null || t.Gone || _route.Contains(c) || w.CrystalAt(c) != null) continue;
                            area.Add(c);
                        }
                    return new Intent { Kind = IntentKind.Attack, Area = area.ToArray(), Damage = 1, Target = tail, Style = Quake, Dir = self.Facing };
                }
                var cell = self.Pos;
                for (int i = 0; i < SpikeReach; i++)
                {
                    cell = cell.Step(self.Facing);
                    var t = w.GetTile(cell);
                    if (t == null || t.Gone) break;
                    area.Add(cell);
                }
                return new Intent { Kind = IntentKind.Attack, Area = area.ToArray(), Damage = 1, Target = self.Pos.Step(self.Facing), Style = Spikes, Dir = self.Facing };
            }

            if (_script.Length == 0) return Intent.Wait;
            char ch = _script[_index % _script.Length];
            _index++;
            if (!ScriptedBrain.TryDir(ch, out var dir)) return Intent.Wait;
            return w.CanStep(self.Pos, self.Pos.Step(dir)) ? Intent.Move(dir) : Intent.Wait;
        }

        /// <summary>
        /// Клетки, где стоит тело в начале: голова пришла в старт по маршруту, значит тело — на клетках,
        /// которые голова прошла перед этим (маршрут по кругу, идём от конца назад).
        /// </summary>
        public static List<Cell> BodyCells(string script, Cell head, int length)
        {
            var s = (script ?? "").Replace(" ", "").ToUpperInvariant();
            var cells = new List<Cell>();
            var c = head;
            for (int i = s.Length - 1, guard = 0; cells.Count < length && guard < s.Length * 4; i--, guard++)
            {
                if (i < 0) i += s.Length;
                if (!ScriptedBrain.TryDir(s[i], out var d)) continue;
                c = c.Step(d.Opposite());
                cells.Add(c);
            }
            return cells;
        }
    }

    /// <summary>Сегмент тела змеи: шагает в клетку ведущего, если тот в этот ход ползёт; иначе стоит.</summary>
    public sealed class SnakeBodyBrain : IEnemyBrain
    {
        readonly Entity _leader;
        public SnakeBodyBrain(Entity leader) { _leader = leader; }

        public Intent Plan(World w, Entity self)
        {
            // Ведущий спланировал раньше (меньший Id) — его намерение уже известно.
            if (_leader == null || !_leader.Alive || _leader.Planned.Kind != IntentKind.Move) return Intent.Wait;
            if (self.Pos.ManhattanTo(_leader.Pos) != 1) return Intent.Wait;
            return Intent.Move(DirExtensions.Towards(self.Pos, _leader.Pos));
        }
    }
}
