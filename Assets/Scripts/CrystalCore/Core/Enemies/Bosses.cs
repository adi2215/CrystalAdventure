using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalAdventure.Core
{
    /// <summary>Приём босса. Каждый ход босс берёт следующий приём из цикла текущей фазы.</summary>
    public enum BossMove
    {
        Wait,     // «окно» — ядро открыто, самое время бить
        Chase,    // шаг к герою (или удар, если рядом)
        Slam,     // удар по 8 клеткам вокруг себя
        Beam,     // луч по линии в сторону героя
        Summon,   // призвать миньонов рядом
        Guard,    // закрыть ядро: неуязвим этот ход
        Freeze,   // превратить клетки вокруг героя в лёд
        Wind,     // повернуть ветер
    }

    [Serializable]
    public sealed class BossPhase
    {
        public int MinHp;               // фаза активна, пока HP > MinHp
        public BossMove[] Cycle;        // повторяющийся рисунок приёмов
        public bool ArmorOnEnter;       // в начале фазы надевает ледяную броню (нужна Super Attack / Break Ice)
        public string SummonArchetype = "ForestDrone";
        public string Line;             // реплика при входе в фазу (для диалогов Ela)
    }

    /// <summary>
    /// Босс как набор фаз. Фазы переключаются по HP, приёмы идут по кругу —
    /// игрок может «прочитать» рисунок и запрограммировать ответ (Function / While / If).
    /// </summary>
    public sealed class PatternBossBrain : IEnemyBrain
    {
        readonly BossPhase[] _phases;
        int _phaseIndex = -1;
        int _step;

        public PatternBossBrain(params BossPhase[] phases) { _phases = phases; }

        public int PhaseIndex => _phaseIndex;

        public Intent Plan(World w, Entity self)
        {
            int idx = Array.FindIndex(_phases, p => self.Hp > p.MinHp);
            if (idx < 0) idx = _phases.Length - 1;
            if (idx != _phaseIndex)
            {
                _phaseIndex = idx;
                _step = 0;
                if (_phases[idx].ArmorOnEnter) self.Armored = true;
            }

            var phase = _phases[_phaseIndex];
            var move = phase.Cycle[_step % phase.Cycle.Length];
            _step++;
            return Build(w, self, move, phase);
        }

        static Intent Build(World w, Entity self, BossMove move, BossPhase phase)
        {
            var hero = w.Hero;
            var toHero = DirExtensions.Towards(self.Pos, hero.Pos);
            switch (move)
            {
                case BossMove.Chase:
                    if (hero.Pos.ManhattanTo(self.Pos) == 1) return Intent.Attack(toHero, 1);
                    return BrainUtil.CanWalk(w, self, toHero) ? Intent.Move(toHero) : Intent.Wait;
                case BossMove.Slam:
                    return Intent.AreaAttack(BrainUtil.Ring(self.Pos), 1);
                case BossMove.Beam:
                    return new Intent { Kind = IntentKind.Attack, Dir = toHero, Area = BrainUtil.Line(w, self.Pos, toHero, 10), Damage = 1 };
                case BossMove.Summon:
                    var spots = new[] { Dir.North, Dir.East, Dir.South, Dir.West }
                        .Select(d => self.Pos.Step(d))
                        .Where(c => w.IsWalkable(c) && w.SolidAt(c) == null && c != hero.Pos)
                        .Take(2).ToArray();
                    return new Intent { Kind = IntentKind.Summon, Area = spots, SummonArchetype = phase.SummonArchetype };
                case BossMove.Guard:
                    return new Intent { Kind = IntentKind.Guard };
                case BossMove.Freeze:
                    var area = BrainUtil.Ring(hero.Pos).Concat(new[] { hero.Pos }).ToArray();
                    return new Intent { Kind = IntentKind.Freeze, Area = area };
                case BossMove.Wind:
                    return new Intent { Kind = IntentKind.ChangeWind };
                default:
                    return Intent.Wait;
            }
        }
    }

    /// <summary>
    /// Ледяной босс арены 8×8 («BossWinter» без маршрута). Стоит на месте, касание смертельно.
    /// Каждый period-й ход бьёт туда, КУДА СМОТРИТ герой (клетка перед ним) — за ход до удара
    /// клетки подсвечиваются. Удары чередуются:
    ///   молния — клетка и крест вокруг: увернуться можно, свернув в сторону или шагнув назад;
    ///   супер-лёд — квадрат 3×3: спасает только шаг назад; потом обычные клетки там — лёд.
    /// Рисунок предсказуемый, его можно запрограммировать: «на каждом третьем шаге — поворот».
    /// </summary>
    public sealed class StormBossBrain : IEnemyBrain
    {
        public const string Lightning = "lightning";
        public const string Ice = "ice";

        readonly int _period;
        int _shots;

        public StormBossBrain(int period = 3) { _period = Math.Max(2, period); }

        public Intent Plan(World w, Entity self)
        {
            // Plan вызывается в конце хода N — намерение на ход N + 1.
            if ((w.Turn + 1) % _period != 0) return Intent.Wait;
            var hero = w.Hero;
            if (hero == null || !hero.Alive) return Intent.Wait;

            var target = hero.Pos.Step(hero.Facing);
            bool ice = _shots++ % 2 == 1;
            var area = new List<Cell>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!ice && dx != 0 && dy != 0) continue; // молния — крест
                    var c = new Cell(target.X + dx, target.Y + dy);
                    if (w.GetTile(c) != null && c != self.Pos) area.Add(c);
                }
            return new Intent
            {
                Kind = IntentKind.Attack, Dir = hero.Facing, Area = area.ToArray(), Damage = 1,
                Target = target, Style = ice ? Ice : Lightning, FreezeArea = ice,
            };
        }
    }

    /// <summary>
    /// Боссы из дизайн-документа. Цифры — стартовые, их удобно крутить в одном месте.
    /// </summary>
    public static class BossLibrary
    {
        public static int HpOf(string archetype)
        {
            switch (archetype)
            {
                case "WoodMechGuardian": return 6;
                case "FrostBallGeneral": return 6;
                case "SkyEngineer": return 6;
                case "BallKing": return 8;
                case "CorruptedCrystalFusion": return 10;
                default: return 0;
            }
        }

        public static IEnemyBrain Create(string archetype)
        {
            switch (archetype)
            {
                // Зима (арена 8×8): молния и супер-лёд туда, куда смотрит герой.
                case "BossWinter":
                    return new StormBossBrain(3);

                // Лес: медленный, потом дроны, потом открывает ядро только на «окно».
                case "WoodMechGuardian":
                    return new PatternBossBrain(
                        new BossPhase { MinHp = 4, Cycle = new[] { BossMove.Chase, BossMove.Slam, BossMove.Wait }, Line = "Crystal fragment detected." },
                        new BossPhase { MinHp = 2, Cycle = new[] { BossMove.Summon, BossMove.Slam, BossMove.Wait }, SummonArchetype = "ForestDrone" },
                        new BossPhase { MinHp = 0, Cycle = new[] { BossMove.Guard, BossMove.Guard, BossMove.Wait }, Line = "Core exposed!" });

                // Холод: снаряды → заморозка поля → ледяная броня.
                case "FrostBallGeneral":
                    return new PatternBossBrain(
                        new BossPhase { MinHp = 4, Cycle = new[] { BossMove.Beam, BossMove.Wait } },
                        new BossPhase { MinHp = 2, Cycle = new[] { BossMove.Freeze, BossMove.Beam, BossMove.Wait } },
                        new BossPhase { MinHp = 0, Cycle = new[] { BossMove.Slam, BossMove.Wait }, ArmorOnEnter = true });

                // Небо: дроны → ветер → луч.
                case "SkyEngineer":
                    return new PatternBossBrain(
                        new BossPhase { MinHp = 4, Cycle = new[] { BossMove.Summon, BossMove.Wait, BossMove.Wait }, SummonArchetype = "FlyingBallDrone" },
                        new BossPhase { MinHp = 2, Cycle = new[] { BossMove.Wind, BossMove.Beam, BossMove.Wait } },
                        new BossPhase { MinHp = 0, Cycle = new[] { BossMove.Beam, BossMove.Guard, BossMove.Wait } });

                case "BallKing":
                    return new PatternBossBrain(
                        new BossPhase { MinHp = 6, Cycle = new[] { BossMove.Chase, BossMove.Beam, BossMove.Wait } },
                        new BossPhase { MinHp = 3, Cycle = new[] { BossMove.Summon, BossMove.Slam, BossMove.Guard, BossMove.Wait }, SummonArchetype = "CloudTurret" },
                        new BossPhase { MinHp = 0, Cycle = new[] { BossMove.Beam, BossMove.Slam, BossMove.Wait }, ArmorOnEnter = true });

                // Финал — экзамен по всем механикам.
                case "CorruptedCrystalFusion":
                    return new PatternBossBrain(
                        new BossPhase { MinHp = 7, Cycle = new[] { BossMove.Beam, BossMove.Slam, BossMove.Wait } },
                        new BossPhase { MinHp = 4, Cycle = new[] { BossMove.Freeze, BossMove.Wind, BossMove.Beam, BossMove.Wait } },
                        new BossPhase { MinHp = 0, Cycle = new[] { BossMove.Summon, BossMove.Guard, BossMove.Slam, BossMove.Wait }, ArmorOnEnter = true, SummonArchetype = "CrystalCorruptedBall" });

                default: return null;
            }
        }
    }
}

namespace CrystalAdventure.Core
{
    /// <summary>
    /// Небесный босс («BossSky», летает — Entity.Flying): кит над полем ползёт по маршруту по кругу.
    /// Клетку не занимает — под ним можно ходить, касание не смертельно; на поле видна его тень.
    /// Каждый period-й ход зависает и сбрасывает бомбу в клетку под собой: удар крестом (клетка
    /// и 4 соседние). За ход до удара клетки подсвечиваются — «тень = прицел».
    /// </summary>
    public sealed class SkyBrain : IEnemyBrain
    {
        public const string Bomb = "bomb";

        readonly string _script;
        readonly int _period;
        int _index;

        public SkyBrain(string script, int period = 3)
        {
            _script = (script ?? "").Replace(" ", "").ToUpperInvariant();
            _period = Math.Max(2, period);
        }

        public Intent Plan(World w, Entity self)
        {
            if ((w.Turn + 1) % _period == 0)
            {
                var area = new List<Cell>();
                foreach (var c in new[] { self.Pos, self.Pos.Step(Dir.North), self.Pos.Step(Dir.East), self.Pos.Step(Dir.South), self.Pos.Step(Dir.West) })
                    if (w.GetTile(c) != null) area.Add(c);
                return new Intent { Kind = IntentKind.Attack, Area = area.ToArray(), Damage = 1, Target = self.Pos, Style = Bomb, Dir = self.Facing };
            }
            if (_script.Length == 0) return Intent.Wait;
            char ch = _script[_index % _script.Length];
            _index++;
            // Летит над полем: клетки под маршрутом могут быть и пустыми — проверки шага нет.
            return ScriptedBrain.TryDir(ch, out var d) ? Intent.Move(d) : Intent.Wait;
        }
    }
}
