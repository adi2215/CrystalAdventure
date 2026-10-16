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
        public string Line;             // реплика при входе в фазу (для диалогов Luma)
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
