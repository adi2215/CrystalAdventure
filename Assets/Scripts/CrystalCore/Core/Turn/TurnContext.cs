using System.Collections.Generic;

namespace CrystalAdventure.Core
{
    /// <summary>
    /// Всё, что нужно правилам во время одного хода: мир, текущая фаза и журнал событий.
    /// Тайлы, враги и боссы меняют мир ТОЛЬКО через этот объект, поэтому каждое изменение
    /// попадает в журнал и потом проигрывается анимацией.
    /// </summary>
    public sealed class TurnContext
    {
        public readonly World World;
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public readonly HashSet<int> TeleportedThisTurn = new HashSet<int>();
        public EventPhase Phase;

        const int MaxRelocateDepth = 64; // защита от бесконечного льда / телепортов
        int _relocateDepth;

        public TurnContext(World world) { World = world; }

        public void Emit(GameEvent e) => Events.Add(e);

        public GameEvent Emit(GameEventType type, Entity e = null)
        {
            var ev = new GameEvent(type, Phase);
            if (e != null) { ev.EntityId = e.Id; ev.Cell = e.Pos; ev.Dir = e.Facing; }
            Events.Add(ev);
            return ev;
        }

        /// <summary>Нанести урон с учётом щита, брони и неуязвимости.</summary>
        public void Damage(Entity target, int amount, EventPhase phase, Cell at, Entity source = null, bool pierceArmor = false)
        {
            if (target == null || !target.Alive || amount <= 0) return;

            if (target.ShieldUp || target.Invulnerable || (target.Armored && !pierceArmor))
            {
                Emit(new GameEvent(GameEventType.Blocked, phase)
                {
                    EntityId = target.Id, TargetId = source?.Id ?? -1, Cell = at,
                    Info = target.ShieldUp ? "shield" : target.Invulnerable ? "guard" : "armor",
                });
                return;
            }

            if (target.Armored && pierceArmor)
            {
                target.Armored = false;
                Emit(new GameEvent(GameEventType.ArmorBroken, phase) { EntityId = target.Id, Cell = at });
            }

            target.Hp -= amount;
            Emit(new GameEvent(GameEventType.Damaged, phase)
            {
                EntityId = target.Id, TargetId = source?.Id ?? -1, Cell = at, Amount = amount,
            });

            if (target.Hp <= 0) Kill(target, phase, "hp");
        }

        public void Kill(Entity e, EventPhase phase, string reason)
        {
            if (!e.Alive) return;
            e.Alive = false;
            e.Hp = 0;
            Emit(new GameEvent(GameEventType.Died, phase) { EntityId = e.Id, Cell = e.Pos, Info = reason });
        }

        /// <summary>
        /// Переместить сущность (скольжение, толчок ветром, батут, телепорт)
        /// и запустить эффекты клеток. Обычные шаги идут через TurnResolver, а не сюда.
        /// </summary>
        public void Relocate(Entity e, Cell to, Dir dir, GameEventType reason, EventPhase phase)
        {
            if (_relocateDepth >= MaxRelocateDepth) return;
            _relocateDepth++;
            try
            {
                var from = e.Pos;
                var fromTile = World.GetTile(from);
                if (fromTile != null) TileBehaviors.Get(fromTile.Type).OnExit(this, e, from, fromTile);

                e.Pos = to;
                Emit(new GameEvent(reason, phase) { EntityId = e.Id, Cell = from, To = to, Dir = dir });

                var toTile = World.GetTile(to);
                if (toTile != null) TileBehaviors.Get(toTile.Type).OnEnter(this, e, to, toTile, dir);
            }
            finally { _relocateDepth--; }
        }

        public Entity Spawn(string archetype, Cell at, Dir facing, int hp, IEnemyBrain brain, EventPhase phase)
        {
            var e = World.Spawn(EntityKind.Enemy, at, facing, hp, archetype, brain);
            Emit(new GameEvent(GameEventType.Spawned, phase) { EntityId = e.Id, Cell = at, Dir = facing, Info = archetype });
            return e;
        }
    }
}
