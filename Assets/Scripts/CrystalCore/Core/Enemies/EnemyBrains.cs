using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalAdventure.Core
{
    /// <summary>
    /// «Мозг» врага решает, что он сделает в СЛЕДУЮЩЕМ ходу. Решение сразу
    /// показывается игроку (стрелка / красные клетки), поэтому враги
    /// честные и предсказуемые — как в Into the Breach. Никакого Random.
    /// </summary>
    public interface IEnemyBrain
    {
        Intent Plan(World w, Entity self);
    }

    static class BrainUtil
    {
        public static bool Adjacent(Cell a, Cell b) => a.ManhattanTo(b) == 1;

        public static bool CanWalk(World w, Entity self, Dir d)
        {
            var to = self.Pos.Step(d);
            if (!w.CanStep(self.Pos, to)) return false;
            var occ = w.SolidAt(to);
            return occ == null || occ == self;
        }

        public static Cell[] Line(World w, Cell from, Dir d, int maxLen)
        {
            var cells = new List<Cell>();
            var c = from;
            for (int i = 0; i < maxLen; i++)
            {
                c = c.Step(d);
                var t = w.GetTile(c);
                if (t == null) break;
                cells.Add(c);
                if (t.Type == TileType.Wall || t.Type == TileType.IceWall) break;
            }
            return cells.ToArray();
        }

        public static Cell[] Ring(Cell center)
        {
            var list = new List<Cell>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) list.Add(new Cell(center.X + dx, center.Y + dy));
            return list.ToArray();
        }
    }

    /// <summary>Training Dummy: стоит и ничего не делает.</summary>
    public sealed class IdleBrain : IEnemyBrain
    {
        public Intent Plan(World w, Entity self) => Intent.Wait;
    }

    /// <summary>
    /// Small Ball Scout: ходит туда-обратно. Если герой прямо перед ним — бьёт.
    /// Учит: «подожди, пока враг отвернётся» и «бей первым».
    /// </summary>
    public sealed class PatrolBrain : IEnemyBrain
    {
        public Intent Plan(World w, Entity self)
        {
            var hero = w.Hero;
            if (hero.Alive && BrainUtil.Adjacent(self.Pos, hero.Pos))
                return Intent.Attack(DirExtensions.Towards(self.Pos, hero.Pos));

            if (BrainUtil.CanWalk(w, self, self.Facing)) return Intent.Move(self.Facing);
            var back = self.Facing.Opposite();
            if (BrainUtil.CanWalk(w, self, back)) return Intent.Move(back);
            return Intent.Wait;
        }
    }

    /// <summary>
    /// Forest Drone / Snow Ball Soldier: идёт к герою, рядом — атакует.
    /// stepEvery = 2 делает врага медленным (ходит через ход).
    /// </summary>
    public sealed class ChaserBrain : IEnemyBrain
    {
        readonly int _stepEvery;
        public ChaserBrain(int stepEvery = 1) { _stepEvery = Math.Max(1, stepEvery); }

        public Intent Plan(World w, Entity self)
        {
            var hero = w.Hero;
            if (!hero.Alive) return Intent.Wait;
            if (BrainUtil.Adjacent(self.Pos, hero.Pos))
                return Intent.Attack(DirExtensions.Towards(self.Pos, hero.Pos));
            if (w.Turn % _stepEvery != 0) return Intent.Wait;

            // Сначала по большей оси, потом по меньшей. Детерминированно.
            int dx = hero.Pos.X - self.Pos.X, dy = hero.Pos.Y - self.Pos.Y;
            var primary = DirExtensions.Towards(self.Pos, hero.Pos);
            Dir? secondary = null;
            if (Math.Abs(dx) >= Math.Abs(dy) && dy != 0) secondary = dy > 0 ? Dir.North : Dir.South;
            else if (Math.Abs(dy) > Math.Abs(dx) && dx != 0) secondary = dx > 0 ? Dir.East : Dir.West;

            if (BrainUtil.CanWalk(w, self, primary)) return Intent.Move(primary);
            if (secondary.HasValue && BrainUtil.CanWalk(w, self, secondary.Value)) return Intent.Move(secondary.Value);
            return Intent.Wait;
        }
    }

    /// <summary>
    /// Frost Turret / Cloud Turret: стоит, раз в period ходов стреляет по линии.
    /// За ход до выстрела линия подсвечивается — успей уйти или поставь Shield.
    /// </summary>
    public sealed class TurretBrain : IEnemyBrain
    {
        readonly int _period, _offset, _range;
        public TurretBrain(int period = 2, int offset = 0, int range = 8) { _period = Math.Max(1, period); _offset = offset; _range = range; }

        public Intent Plan(World w, Entity self)
        {
            // Plan вызывается в конце хода N, атака случится в ходе N+1.
            if ((w.Turn + 1 + _offset) % _period != 0) return Intent.Wait;
            return Intent.AreaAttack(BrainUtil.Line(w, self.Pos, self.Facing, _range));
        }
    }

    public static class EnemyBrains
    {
        /// <summary>Мозг по имени архетипа (имя = префаб во View).</summary>
        public static IEnemyBrain Create(string archetype)
        {
            switch (archetype)
            {
                case "TrainingDummy": return new IdleBrain();
                case "SmallBallScout": return new PatrolBrain();
                case "ForestDrone": return new ChaserBrain();
                case "SnowBallSoldier": return new ChaserBrain();
                case "IceDrone": return new ChaserBrain();
                case "HeavyIceGuard": return new ChaserBrain(stepEvery: 2);
                case "FrostTurret": return new TurretBrain(period: 2);
                case "CloudTurret": return new TurretBrain(period: 3);
                case "FlyingBallDrone": return new ChaserBrain();
                case "WindBot": return new PatrolBrain();
                case "CrystalCorruptedBall": return new ChaserBrain();
                default:
                    var boss = BossLibrary.Create(archetype);
                    return boss ?? (IEnemyBrain)new ChaserBrain();
            }
        }

        /// <summary>Стандартные HP / броня архетипа.</summary>
        public static void ApplyStats(Entity e)
        {
            switch (e.Archetype)
            {
                case "TrainingDummy": e.Hp = e.MaxHp = 1; break;
                case "HeavyIceGuard": e.Hp = e.MaxHp = 2; e.Armored = true; break;
                case "SnowBallSoldier": e.Hp = e.MaxHp = 2; break;
                case "CrystalCorruptedBall": e.Hp = e.MaxHp = 2; break;
                default:
                    var hp = BossLibrary.HpOf(e.Archetype);
                    if (hp > 0) { e.Hp = e.MaxHp = hp; e.Kind = EntityKind.Boss; }
                    break;
            }
        }
    }
}
