using System;
using System.Collections.Generic;

namespace CrystalAdventure.Core
{
    public enum EntityKind { Hero, Enemy, Boss, Crystal }

    /// <summary>Любой объект на поле: герой, враг, босс, кристалл.</summary>
    public sealed class Entity
    {
        public int Id;
        public EntityKind Kind;
        public string Archetype;   // "SmallBallScout", "FrostTurret"... — по нему View выбирает префаб
        public Cell Pos;
        public Dir Facing;
        public int Hp = 1;
        public int MaxHp = 1;
        public bool Alive = true;
        public bool Armored;       // ледяная броня: обычная атака не пробивает, нужна Super Attack
        public bool Invulnerable;  // босс закрыл ядро на этот ход
        /// <summary>Касание смертельно: шагнул в него, он шагнул в тебя, поменялись местами, пришли в одну клетку.</summary>
        public bool Deadly;

        public IEnemyBrain Brain;  // null у героя и кристаллов
        public Intent Planned = Intent.Wait; // намерение врага на СЛЕДУЮЩИЙ ход (показывается игроку)

        // Состояние героя
        public int SuperAttacksLeft;
        public bool ShieldUp;      // только в течение текущего хода

        public bool IsSolid => Alive && Kind != EntityKind.Crystal && !Flying;
        /// <summary>Летает (небесный кит): клетку не занимает, под ним можно ходить, не мешает и не толкается.</summary>
        public bool Flying;
        public bool IsHostile => Kind == EntityKind.Enemy || Kind == EntityKind.Boss;

        public override string ToString() => $"{Kind}#{Id} {Archetype} {Pos} hp={Hp}";
    }

    public enum IntentKind { Wait, Move, Turn, Attack, SuperAttack, Shield, Interact, BreakIce, Spell, ChangeWind, Summon, Guard, Freeze }

    /// <summary>
    /// Что сущность собирается сделать в этом ходу. Намерения всех сущностей
    /// собираются ДО применения — так получается честное одновременное движение.
    /// </summary>
    public struct Intent
    {
        public IntentKind Kind;
        public Dir Dir;           // направление движения / поворота / атаки
        public Cell[] Area;       // для атак по площади (босс, турель). null = клетка перед собой
        public int Damage;
        public string SummonArchetype; // для Summon
        /// <summary>Прицельный удар босса: клетка, куда он целится (центр волны во View).</summary>
        public Cell Target;
        /// <summary>Вид удара для View: "lightning" (молния), "ice" (супер-лёд). null — обычная атака.</summary>
        public string Style;
        /// <summary>После удара обычные клетки области становятся льдом (супер-лёд).</summary>
        public bool FreezeArea;

        public static Intent Wait => new Intent { Kind = IntentKind.Wait };
        public static Intent Move(Dir d) => new Intent { Kind = IntentKind.Move, Dir = d };
        public static Intent Turn(Dir d) => new Intent { Kind = IntentKind.Turn, Dir = d };
        public static Intent Attack(Dir d, int dmg = 1) => new Intent { Kind = IntentKind.Attack, Dir = d, Damage = dmg };
        public static Intent AreaAttack(Cell[] area, int dmg = 1) => new Intent { Kind = IntentKind.Attack, Area = area, Damage = dmg };

        public override string ToString() => Area != null ? $"{Kind}[{Area.Length} cells]" : $"{Kind} {Dir}";
    }
}
