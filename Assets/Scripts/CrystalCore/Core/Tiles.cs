using System;
using System.Collections.Generic;

namespace CrystalAdventure.Core
{
    /// <summary>
    /// Типы клеток. Обычные + «супер-тайлы» биомов.
    /// Новый тип = значение здесь + класс ITileBehavior + регистрация в TileBehaviors.
    /// </summary>
    public enum TileType
    {
        // --- общие ---
        Ground = 0,
        Wall = 1,            // непроходимо
        Spikes = 2,          // ловушка: бьёт, когда активна (по таймеру)
        PressurePlate = 3,   // нажата, пока на ней кто-то стоит → открывает Gate с тем же Channel
        Lever = 4,           // переключается командой Interact
        Gate = 5,            // проходима, только когда канал включён

        // --- Forest / Лес ---
        Crumbling = 10,      // рассыпается после N шагов → пропасть
        BounceMushroom = 11, // подбрасывает на 2 клетки вперёд
        Vine = 12,           // лиана-мост: появляется, когда канал включён

        // --- Winter / Холод ---
        Ice = 20,            // скольжение до препятствия
        IceWall = 21,        // стена, ломается Break Ice / Super Attack
        Snow = 22,           // сугроб: нельзя пройти, ломается Attack

        // --- Sky / Небо ---
        Wind = 30,           // в конце хода сдвигает стоящего по направлению ветра
        VanishingCloud = 31, // исчезает после того, как с неё сошли, возвращается через N ходов
        Teleport = 32,       // переносит на парный телепорт (тот же Channel)
        MagicBarrier = 33,   // снимается командой Spell
        MagicPlatform = 34,  // неактивна, пока на неё не применили Spell
    }

    /// <summary>Состояние одной клетки уровня. Чистые данные.</summary>
    [Serializable]
    public sealed class TileState
    {
        public TileType Type;
        public int Height;          // высота блока (для лестниц из блоков)
        public Dir Direction;       // для Wind (если UseGlobalWind = false)
        public bool UseGlobalWind = true;
        public int Channel;         // связь кнопок/рычагов/ворот/телепортов
        public int Period = 2;      // для Spikes: раз в сколько ходов активна
        public int Offset;          // сдвиг фазы для Spikes
        public int Durability = 1;  // для Crumbling: сколько раз можно наступить
        public int RespawnTurns = 3;// для VanishingCloud
        public int Color;           // цвет клетки для условий карточек: 0 — нет, дальше как TileColor (1 Red, 2 Blue, 3 Yellow)
        public int Counter;         // внутренний счётчик (не задаётся в редакторе)
        public bool Gone;           // клетка исчезла (рассыпалась / облако пропало)

        public TileState Clone() => (TileState)MemberwiseClone();
    }

    /// <summary>Поведение типа клетки. Все методы детерминированные и без Unity.</summary>
    public interface ITileBehavior
    {
        /// <summary>Можно ли сейчас стоять на клетке.</summary>
        bool IsWalkable(World w, Cell cell, TileState t);

        /// <summary>Сущность вошла на клетку (обычный ход, скольжение, толчок).</summary>
        void OnEnter(TurnContext ctx, Entity e, Cell cell, TileState t, Dir moveDir);

        /// <summary>Сущность ушла с клетки.</summary>
        void OnExit(TurnContext ctx, Entity e, Cell cell, TileState t);

        /// <summary>Конец хода (ловушки, ветер, таймеры).</summary>
        void OnTurnEnd(TurnContext ctx, Cell cell, TileState t);

        /// <summary>Попытка «опасна» ли клетка (для условия If Trap Ahead).</summary>
        bool IsTrap(World w, Cell cell, TileState t);
    }

    public class GroundBehavior : ITileBehavior
    {
        public virtual bool IsWalkable(World w, Cell c, TileState t) => !t.Gone;
        public virtual void OnEnter(TurnContext ctx, Entity e, Cell c, TileState t, Dir d) { }
        public virtual void OnExit(TurnContext ctx, Entity e, Cell c, TileState t) { }
        public virtual void OnTurnEnd(TurnContext ctx, Cell c, TileState t) { }
        public virtual bool IsTrap(World w, Cell c, TileState t) => false;
    }

    public sealed class WallBehavior : GroundBehavior
    {
        public override bool IsWalkable(World w, Cell c, TileState t) => false;
    }

    public sealed class SpikesBehavior : GroundBehavior
    {
        public static bool ActiveOn(TileState t, int turn) => t.Period > 0 && (turn + t.Offset) % t.Period == 0;

        public override bool IsTrap(World w, Cell c, TileState t) => true;

        public override void OnTurnEnd(TurnContext ctx, Cell c, TileState t)
        {
            if (!ActiveOn(t, ctx.World.Turn)) return;
            ctx.Emit(new GameEvent(GameEventType.TrapFired, ctx.Phase) { Cell = c });
            var victim = ctx.World.SolidAt(c);
            if (victim != null) ctx.Damage(victim, 1, ctx.Phase, c);
        }
    }

    public sealed class PressurePlateBehavior : GroundBehavior
    {
        // Канал пересчитывается в World.RecomputeChannels() после движения.
    }

    public sealed class LeverBehavior : GroundBehavior
    {
        // Переключается в TurnResolver при Interact.
    }

    public sealed class GateBehavior : GroundBehavior
    {
        public override bool IsWalkable(World w, Cell c, TileState t) => !t.Gone && w.IsChannelOn(t.Channel);
    }

    public sealed class CrumblingBehavior : GroundBehavior
    {
        public override bool IsTrap(World w, Cell c, TileState t) => true;

        public override void OnExit(TurnContext ctx, Entity e, Cell c, TileState t)
        {
            t.Counter++;
            if (t.Counter >= t.Durability)
            {
                t.Gone = true;
                ctx.Emit(new GameEvent(GameEventType.TileChanged, ctx.Phase) { Cell = c, Info = "crumbled" });
            }
        }
    }

    /// <summary>
    /// Гриб. При LevelRules.ClassicMushroom (по умолчанию) сам ничего не делает при входе —
    /// прыжок на 2 клетки считает TurnResolver.MovePhase для того, кто СТОИТ на грибе.
    /// Иначе — отскок на 2 клетки вперёд сразу при входе.
    /// </summary>
    public sealed class BounceMushroomBehavior : GroundBehavior
    {
        public override void OnEnter(TurnContext ctx, Entity e, Cell c, TileState t, Dir d)
        {
            if (ctx.World.Rules.ClassicMushroom) return;
            var landing = c.Step(d, 2);
            if (ctx.World.CanEnter(e, c, landing, ignoreHeight: true))
                ctx.Relocate(e, landing, d, GameEventType.Bounced, ctx.Phase);
        }
    }

    public sealed class VineBehavior : GroundBehavior
    {
        public override bool IsWalkable(World w, Cell c, TileState t) => !t.Gone && w.IsChannelOn(t.Channel);
    }

    public sealed class IceBehavior : GroundBehavior
    {
        public override void OnEnter(TurnContext ctx, Entity e, Cell c, TileState t, Dir d)
        {
            // Скользим дальше, пока следующая клетка проходима. Каждая новая клетка
            // снова вызывает OnEnter, поэтому лёд → лёд → лёд работает цепочкой.
            var next = c.Step(d);
            if (ctx.World.CanEnter(e, c, next))
                ctx.Relocate(e, next, d, GameEventType.Slid, ctx.Phase);
        }
    }

    public sealed class IceWallBehavior : GroundBehavior
    {
        public override bool IsWalkable(World w, Cell c, TileState t) => false;
    }

    public sealed class SnowBehavior : GroundBehavior
    {
        public override bool IsWalkable(World w, Cell c, TileState t) => false;
    }

    public sealed class WindBehavior : GroundBehavior
    {
        public static Dir DirOf(World w, TileState t) => t.UseGlobalWind ? w.WindDir : t.Direction;

        public override void OnTurnEnd(TurnContext ctx, Cell c, TileState t)
        {
            if (!ctx.World.WindActive) return;
            var e = ctx.World.SolidAt(c);
            if (e == null || !e.Alive) return;
            var d = DirOf(ctx.World, t);
            var target = c.Step(d);
            if (ctx.World.CanEnter(e, c, target))
                ctx.Relocate(e, target, d, GameEventType.Pushed, ctx.Phase);
        }
    }

    public sealed class VanishingCloudBehavior : GroundBehavior
    {
        public override bool IsTrap(World w, Cell c, TileState t) => true;

        public override void OnExit(TurnContext ctx, Entity e, Cell c, TileState t)
        {
            t.Gone = true;
            t.Counter = t.RespawnTurns;
            ctx.Emit(new GameEvent(GameEventType.TileChanged, ctx.Phase) { Cell = c, Info = "vanished" });
        }

        public override void OnTurnEnd(TurnContext ctx, Cell c, TileState t)
        {
            if (!t.Gone) return;
            if (--t.Counter > 0) return;
            if (ctx.World.EntityAt(c) != null) { t.Counter = 1; return; }
            t.Gone = false;
            ctx.Emit(new GameEvent(GameEventType.TileChanged, ctx.Phase) { Cell = c, Info = "restored" });
        }
    }

    public sealed class TeleportBehavior : GroundBehavior
    {
        public override void OnEnter(TurnContext ctx, Entity e, Cell c, TileState t, Dir d)
        {
            if (ctx.TeleportedThisTurn.Contains(e.Id)) return; // защита от пинг-понга
            foreach (var kv in ctx.World.Tiles)
            {
                if (kv.Key == c || kv.Value.Type != TileType.Teleport || kv.Value.Channel != t.Channel) continue;
                if (ctx.World.SolidAt(kv.Key) != null) return;
                ctx.TeleportedThisTurn.Add(e.Id);
                ctx.Relocate(e, kv.Key, d, GameEventType.Teleported, ctx.Phase);
                return;
            }
        }
    }

    public sealed class MagicBarrierBehavior : GroundBehavior
    {
        public override bool IsWalkable(World w, Cell c, TileState t) => false;
    }

    public sealed class MagicPlatformBehavior : GroundBehavior
    {
        // Gone = true значит «ещё не активирована». Spell делает Gone = false.
    }

    /// <summary>Реестр поведений. Чтобы добавить супер-тайл — зарегистрируй его здесь.</summary>
    public static class TileBehaviors
    {
        static readonly Dictionary<TileType, ITileBehavior> Map = new Dictionary<TileType, ITileBehavior>
        {
            { TileType.Ground, new GroundBehavior() },
            { TileType.Wall, new WallBehavior() },
            { TileType.Spikes, new SpikesBehavior() },
            { TileType.PressurePlate, new PressurePlateBehavior() },
            { TileType.Lever, new LeverBehavior() },
            { TileType.Gate, new GateBehavior() },
            { TileType.Crumbling, new CrumblingBehavior() },
            { TileType.BounceMushroom, new BounceMushroomBehavior() },
            { TileType.Vine, new VineBehavior() },
            { TileType.Ice, new IceBehavior() },
            { TileType.IceWall, new IceWallBehavior() },
            { TileType.Snow, new SnowBehavior() },
            { TileType.Wind, new WindBehavior() },
            { TileType.VanishingCloud, new VanishingCloudBehavior() },
            { TileType.Teleport, new TeleportBehavior() },
            { TileType.MagicBarrier, new MagicBarrierBehavior() },
            { TileType.MagicPlatform, new MagicPlatformBehavior() },
        };

        public static ITileBehavior Get(TileType type) =>
            Map.TryGetValue(type, out var b) ? b : Map[TileType.Ground];

        public static void Register(TileType type, ITileBehavior behavior) => Map[type] = behavior;
    }
}
