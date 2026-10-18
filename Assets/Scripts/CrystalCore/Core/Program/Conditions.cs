using System.Linq;

namespace CrystalAdventure.Core
{
    public enum ConditionType
    {
        PathClear,          // впереди можно пройти
        EnemyAhead,         // враг на клетке впереди
        TrapAhead,          // впереди ловушка / рассыпающаяся / исчезающая клетка
        DoorClosed,         // впереди закрытые ворота, барьер или ледяная стена
        IceAhead,
        WindActive,
        CrystalNear,        // кристалл на своей клетке или впереди
        CrystalNotCollected,// остались кристаллы
        EnemyAlive,         // остались враги
        HpLow,              // HP героя ≤ 1
        TileColor,          // герой стоит на клетке цвета Value (цветные клетки старой игры)
    }

    /// <summary>Условие для If / While. Проверяется по ТЕКУЩЕМУ состоянию мира.</summary>
    public sealed class Condition
    {
        public ConditionType Type;
        public bool Negate; // «Not ...»
        public int Value;   // параметр условия: для TileColor — номер цвета (1 Red, 2 Blue, 3 Yellow)

        public Condition(ConditionType type, bool negate = false) { Type = type; Negate = negate; }

        public static Condition Of(ConditionType t) => new Condition(t);
        public static Condition Not(ConditionType t) => new Condition(t, true);
        /// <summary>«Герой стоит на клетке цвета color» — условие покрашенной карточки.</summary>
        public static Condition OnColor(int color) => new Condition(ConditionType.TileColor) { Value = color };

        public bool Evaluate(World w)
        {
            bool r = Raw(w);
            return Negate ? !r : r;
        }

        bool Raw(World w)
        {
            var hero = w.Hero;
            var ahead = hero.Pos.Step(hero.Facing);
            var tile = w.GetTile(ahead);
            switch (Type)
            {
                case ConditionType.PathClear:
                    return w.CanEnter(hero, hero.Pos, ahead);
                case ConditionType.EnemyAhead:
                    var e = w.SolidAt(ahead);
                    return e != null && e.IsHostile;
                case ConditionType.TrapAhead:
                    return tile != null && TileBehaviors.Get(tile.Type).IsTrap(w, ahead, tile);
                case ConditionType.DoorClosed:
                    return tile != null && !w.IsWalkable(ahead) &&
                           (tile.Type == TileType.Gate || tile.Type == TileType.MagicBarrier ||
                            tile.Type == TileType.IceWall || tile.Type == TileType.Snow);
                case ConditionType.IceAhead:
                    return tile != null && tile.Type == TileType.Ice;
                case ConditionType.WindActive:
                    return w.WindActive;
                case ConditionType.CrystalNear:
                    return w.CrystalAt(hero.Pos) != null || w.CrystalAt(ahead) != null;
                case ConditionType.CrystalNotCollected:
                    return w.Crystals.Any();
                case ConditionType.EnemyAlive:
                    return w.Hostiles.Any();
                case ConditionType.HpLow:
                    return hero.Hp <= 1;
                case ConditionType.TileColor:
                    var here = w.GetTile(hero.Pos);
                    return here != null && Value != 0 && here.Color == Value;
                default:
                    return false;
            }
        }

        public override string ToString() => (Negate ? "Not " : "") + Type + (Type == ConditionType.TileColor ? " " + Value : "");
    }
}
