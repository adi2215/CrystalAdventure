using System.Collections.Generic;
using System.Linq;

namespace CrystalAdventure.Core
{
    /// <summary>
    /// Один ход мира. Герой выполняет действие из программы, все враги — своё
    /// заранее показанное намерение (Planned). Всё происходит в одном ходе,
    /// фазами, в строго заданном порядке:
    ///
    ///   1. Prepare — повороты, щит, Interact/Spell/Break Ice/Change Wind, призывы босса
    ///   2. Move    — ВСЕ шаги одновременно, с разбором конфликтов клеток
    ///   3. Effects — эффекты клеток после шага (лёд, гриб, телепорт, рассыпание)
    ///   4. Combat  — сначала атака героя, потом атаки врагов (кто выжил)
    ///   5. Tiles   — ловушки, ветер, таймеры облаков
    ///   6. Resolve — падения в пропасть, автоподбор кристаллов
    ///   7. Plan    — враги решают, что сделают в СЛЕДУЮЩЕМ ходу (это видно игроку)
    /// </summary>
    public static class TurnResolver
    {
        public static Intent HeroIntent(Entity hero, ActionType a)
        {
            switch (a)
            {
                // Направления абсолютные, как сейчас в игре: Forward = вверх по сетке (+Y).
                case ActionType.MoveForward: return Intent.Move(Dir.North);
                case ActionType.MoveBack: return Intent.Move(Dir.South);
                case ActionType.MoveRight: return Intent.Move(Dir.East);
                case ActionType.MoveLeft: return Intent.Move(Dir.West);
                case ActionType.TurnLeft: return Intent.Turn(hero.Facing.RotateCCW());
                case ActionType.TurnRight: return Intent.Turn(hero.Facing.RotateCW());
                case ActionType.Attack: return Intent.Attack(hero.Facing, 1);
                case ActionType.SuperAttack: return new Intent { Kind = IntentKind.SuperAttack, Dir = hero.Facing, Damage = 3 };
                case ActionType.Shield: return new Intent { Kind = IntentKind.Shield };
                case ActionType.Interact: return new Intent { Kind = IntentKind.Interact, Dir = hero.Facing };
                case ActionType.BreakIce: return new Intent { Kind = IntentKind.BreakIce, Dir = hero.Facing };
                case ActionType.Spell: return new Intent { Kind = IntentKind.Spell, Dir = hero.Facing };
                case ActionType.ChangeWind: return new Intent { Kind = IntentKind.ChangeWind };
                default: return Intent.Wait;
            }
        }

        /// <summary>Сыграть один ход. Возвращает журнал событий хода.</summary>
        public static List<GameEvent> Resolve(World w, ActionType heroAction)
        {
            var ctx = new TurnContext(w);
            var hero = w.Hero;
            w.Turn++;

            // Все намерения фиксируются ДО каких-либо изменений мира.
            var intents = new List<KeyValuePair<Entity, Intent>>();
            intents.Add(new KeyValuePair<Entity, Intent>(hero, HeroIntent(hero, heroAction)));
            foreach (var e in w.Entities.Where(x => x.Alive && x.IsHostile).OrderBy(x => x.Id).ToList())
                intents.Add(new KeyValuePair<Entity, Intent>(e, e.Planned));

            var snap = SnapshotTiles(w);
            PreparePhase(ctx, intents);   EmitTileDiff(ctx, ref snap);
            MovePhase(ctx, intents);      EmitTileDiff(ctx, ref snap);
            CombatPhase(ctx, intents);    EmitTileDiff(ctx, ref snap);
            TilesPhase(ctx);              EmitTileDiff(ctx, ref snap);
            ResolvePhase(ctx);
            PlanPhase(ctx);
            return ctx.Events;
        }

        /// <summary>Только фаза планирования — вызывается перед первым ходом, чтобы игрок
        /// сразу видел, куда пойдут враги.</summary>
        public static List<GameEvent> PlanOnly(World w)
        {
            var ctx = new TurnContext(w);
            w.RecomputePlates();
            PlanPhase(ctx);
            return ctx.Events;
        }

        // ------------------------------------------------------------------ 1. Prepare

        static void PreparePhase(TurnContext ctx, List<KeyValuePair<Entity, Intent>> intents)
        {
            ctx.Phase = EventPhase.Prepare;
            var w = ctx.World;
            foreach (var e in w.Entities) { e.ShieldUp = false; e.Invulnerable = false; }

            foreach (var kv in intents)
            {
                var e = kv.Key; var it = kv.Value;
                if (!e.Alive) continue;
                switch (it.Kind)
                {
                    case IntentKind.Turn:
                        e.Facing = it.Dir;
                        ctx.Emit(GameEventType.Turned, e);
                        break;
                    case IntentKind.Shield:
                        e.ShieldUp = true;
                        ctx.Emit(GameEventType.ShieldRaised, e);
                        break;
                    case IntentKind.Guard:
                        e.Invulnerable = true;
                        ctx.Emit(GameEventType.Guarded, e);
                        break;
                    case IntentKind.Interact:
                        DoInteract(ctx, e);
                        break;
                    case IntentKind.BreakIce:
                        DoBreakIce(ctx, e);
                        break;
                    case IntentKind.Spell:
                        DoSpell(ctx, e);
                        break;
                    case IntentKind.ChangeWind:
                        w.WindDir = w.WindDir.RotateCW();
                        w.WindActive = true;
                        ctx.Emit(GameEventType.WindChanged, e).Dir = w.WindDir;
                        break;
                    case IntentKind.Summon:
                        DoSummon(ctx, e, it);
                        break;
                    case IntentKind.Freeze:
                        DoFreeze(ctx, e, it);
                        break;
                }
            }
        }

        static void DoInteract(TurnContext ctx, Entity e)
        {
            var w = ctx.World;
            foreach (var c in new[] { e.Pos, e.Pos.Step(e.Facing) })
            {
                var crystal = w.CrystalAt(c);
                if (crystal != null)
                {
                    crystal.Alive = false;
                    ctx.Emit(new GameEvent(GameEventType.CrystalCollected, ctx.Phase) { EntityId = e.Id, TargetId = crystal.Id, Cell = c });
                    return;
                }
                var t = w.GetTile(c);
                if (t != null && t.Type == TileType.Lever)
                {
                    w.ToggleLever(t.Channel);
                    ctx.Emit(new GameEvent(GameEventType.ChannelChanged, ctx.Phase)
                    {
                        EntityId = e.Id, Cell = c, Amount = t.Channel, Info = w.IsChannelOn(t.Channel) ? "on" : "off",
                    });
                    return;
                }
            }
            ctx.Emit(GameEventType.Bumped, e).Info = "nothing to interact";
        }

        static void DoBreakIce(TurnContext ctx, Entity e)
        {
            var w = ctx.World;
            var ahead = e.Pos.Step(e.Facing);
            var t = w.GetTile(ahead);
            if (t != null && (t.Type == TileType.IceWall || t.Type == TileType.Ice))
            {
                t.Type = TileType.Ground;
                ctx.Emit(new GameEvent(GameEventType.TileChanged, ctx.Phase) { EntityId = e.Id, Cell = ahead, Info = "ice_broken" });
                return;
            }
            var target = w.SolidAt(ahead);
            if (target != null && target.Armored)
            {
                target.Armored = false;
                ctx.Emit(new GameEvent(GameEventType.ArmorBroken, ctx.Phase) { EntityId = target.Id, Cell = ahead });
                return;
            }
            ctx.Emit(GameEventType.Bumped, e).Info = "no ice";
        }

        static void DoSpell(TurnContext ctx, Entity e)
        {
            var w = ctx.World;
            ctx.Emit(GameEventType.SpellCast, e);
            foreach (var c in new[] { e.Pos.Step(e.Facing), e.Pos })
            {
                var t = w.GetTile(c);
                if (t == null) continue;
                if (t.Type == TileType.MagicBarrier)
                {
                    t.Type = TileType.Ground;
                    ctx.Emit(new GameEvent(GameEventType.TileChanged, ctx.Phase) { Cell = c, Info = "barrier_removed" });
                    return;
                }
                if (t.Type == TileType.MagicPlatform && t.Gone)
                {
                    t.Gone = false;
                    ctx.Emit(new GameEvent(GameEventType.TileChanged, ctx.Phase) { Cell = c, Info = "platform_on" });
                    return;
                }
            }
        }

        static void DoSummon(TurnContext ctx, Entity boss, Intent it)
        {
            var w = ctx.World;
            foreach (var c in it.Area ?? new Cell[0])
            {
                if (!w.IsWalkable(c) || w.SolidAt(c) != null) continue;
                ctx.Spawn(it.SummonArchetype ?? "Drone", c, Dir.South, 1, EnemyBrains.Create(it.SummonArchetype), ctx.Phase);
            }
        }

        static void DoFreeze(TurnContext ctx, Entity boss, Intent it)
        {
            var w = ctx.World;
            foreach (var c in it.Area ?? new Cell[0])
            {
                var t = w.GetTile(c);
                if (t == null || t.Type != TileType.Ground) continue;
                t.Type = TileType.Ice;
                ctx.Emit(new GameEvent(GameEventType.Frozen, ctx.Phase) { EntityId = boss.Id, Cell = c });
            }
        }

        // ------------------------------------------------------------------ 2. Move (одновременно)

        sealed class Mover { public Entity E; public Cell From; public Cell To; public Dir Dir; public bool Failed; public string Why; }

        static void MovePhase(TurnContext ctx, List<KeyValuePair<Entity, Intent>> intents)
        {
            ctx.Phase = EventPhase.Move;
            var w = ctx.World;

            var movers = new List<Mover>();
            foreach (var kv in intents)
            {
                var e = kv.Key;
                if (!e.Alive || kv.Value.Kind != IntentKind.Move) continue;
                e.Facing = kv.Value.Dir; // смотрит туда, куда пытался идти — даже если упёрся
                var m = new Mover { E = e, From = e.Pos, Dir = kv.Value.Dir, To = e.Pos.Step(kv.Value.Dir) };
                if (!w.CanStep(m.From, m.To)) { m.Failed = true; m.Why = "blocked"; }
                movers.Add(m);
            }

            // Разбор конфликтов: повторяем, пока что-то меняется (цепочки «паровозиком» разрешены).
            bool changed = true;
            while (changed)
            {
                changed = false;
                var live = movers.Where(m => !m.Failed).ToList();

                // Двое в одну клетку — оба стоят.
                foreach (var g in live.GroupBy(m => m.To).Where(g => g.Count() > 1))
                    foreach (var m in g) { m.Failed = true; m.Why = "same cell"; changed = true; }

                // Обмен местами — столкновение, оба стоят.
                foreach (var a in live)
                    foreach (var b in live)
                        if (a != b && !a.Failed && a.To == b.From && b.To == a.From)
                        { a.Failed = b.Failed = true; a.Why = b.Why = "swap"; changed = true; }

                // В клетке стоит тот, кто не уходит.
                foreach (var m in movers.Where(x => !x.Failed))
                {
                    var occ = w.SolidAt(m.To);
                    if (occ == null || occ == m.E) continue;
                    var occMove = movers.FirstOrDefault(x => x.E == occ);
                    if (occMove == null || occMove.Failed) { m.Failed = true; m.Why = "occupied"; changed = true; }
                }
            }

            // Применяем все успешные шаги ОДНОВРЕМЕННО.
            foreach (var m in movers)
            {
                if (m.Failed)
                {
                    ctx.Emit(new GameEvent(GameEventType.Bumped, EventPhase.Move) { EntityId = m.E.Id, Cell = m.From, To = m.To, Dir = m.Dir, Info = m.Why });
                    continue;
                }
                m.E.Pos = m.To;
                ctx.Emit(new GameEvent(GameEventType.Moved, EventPhase.Move) { EntityId = m.E.Id, Cell = m.From, To = m.To, Dir = m.Dir });
            }

            // Эффекты клеток: сначала «ушёл», потом «пришёл».
            ctx.Phase = EventPhase.Effects;
            var ok = movers.Where(m => !m.Failed).OrderBy(m => m.E.Id).ToList();
            foreach (var m in ok)
            {
                var t = w.GetTile(m.From);
                if (t != null) TileBehaviors.Get(t.Type).OnExit(ctx, m.E, m.From, t);
            }
            foreach (var m in ok)
            {
                if (!m.E.Alive || m.E.Pos != m.To) continue;
                var t = w.GetTile(m.To);
                if (t != null) TileBehaviors.Get(t.Type).OnEnter(ctx, m.E, m.To, t, m.Dir);
            }

            if (w.RecomputePlates())
                ctx.Emit(new GameEvent(GameEventType.ChannelChanged, ctx.Phase) { Info = "plates" });
        }

        // ------------------------------------------------------------------ 3. Combat

        static void CombatPhase(TurnContext ctx, List<KeyValuePair<Entity, Intent>> intents)
        {
            ctx.Phase = EventPhase.Combat;
            var w = ctx.World;
            var hero = w.Hero;

            // Герой бьёт первым: кого он убил, тот уже не атакует.
            var heroIntent = intents[0].Value;
            if (hero.Alive && (heroIntent.Kind == IntentKind.Attack || heroIntent.Kind == IntentKind.SuperAttack))
            {
                bool super = heroIntent.Kind == IntentKind.SuperAttack;
                var ahead = hero.Pos.Step(hero.Facing);
                if (super && hero.SuperAttacksLeft <= 0)
                {
                    ctx.Emit(GameEventType.Bumped, hero).Info = "no super attacks left";
                }
                else
                {
                    if (super) hero.SuperAttacksLeft--;
                    var ev = ctx.Emit(super ? GameEventType.SuperAttacked : GameEventType.Attacked, hero);
                    ev.Area = new[] { ahead };

                    var tile = w.GetTile(ahead);
                    if (tile != null && (tile.Type == TileType.Snow || (super && tile.Type == TileType.IceWall)))
                    {
                        tile.Type = TileType.Ground;
                        ctx.Emit(new GameEvent(GameEventType.TileChanged, ctx.Phase) { Cell = ahead, Info = "smashed" });
                    }

                    var target = w.SolidAt(ahead);
                    if (target != null && target.IsHostile)
                        ctx.Damage(target, heroIntent.Damage, ctx.Phase, ahead, hero, pierceArmor: super);
                }
            }

            // Атаки врагов — по клеткам, которые они заранее показали.
            foreach (var kv in intents.Skip(1))
            {
                var e = kv.Key; var it = kv.Value;
                if (!e.Alive || it.Kind != IntentKind.Attack) continue;
                var area = it.Area ?? new[] { e.Pos.Step(it.Dir) };
                var ev = ctx.Emit(GameEventType.Attacked, e);
                ev.Area = area;
                ev.Dir = it.Dir;
                if (area.Contains(hero.Pos))
                    ctx.Damage(hero, it.Damage <= 0 ? 1 : it.Damage, ctx.Phase, hero.Pos, e);
            }
        }

        // ------------------------------------------------------------------ 4. Tiles

        static void TilesPhase(TurnContext ctx)
        {
            ctx.Phase = EventPhase.Tiles;
            var w = ctx.World;
            // Фиксированный порядок обхода — результат всегда одинаковый.
            foreach (var kv in w.Tiles.OrderBy(k => k.Key.Y).ThenBy(k => k.Key.X).ToList())
                TileBehaviors.Get(kv.Value.Type).OnTurnEnd(ctx, kv.Key, kv.Value);

            if (w.RecomputePlates())
                ctx.Emit(new GameEvent(GameEventType.ChannelChanged, ctx.Phase) { Info = "plates" });
        }

        // ------------------------------------------------------------------ 5. Resolve

        static void ResolvePhase(TurnContext ctx)
        {
            ctx.Phase = EventPhase.Resolve;
            var w = ctx.World;

            foreach (var e in w.Entities.Where(x => x.IsSolid).ToList())
            {
                if (w.IsWalkable(e.Pos)) continue;
                ctx.Emit(new GameEvent(GameEventType.Fell, ctx.Phase) { EntityId = e.Id, Cell = e.Pos });
                ctx.Kill(e, ctx.Phase, "fell");
            }

            // Босс повержен — его миньоны рассыпаются вместе с ним.
            if (ctx.Events.Any(ev => ev.Type == GameEventType.Died && w.EntityById(ev.EntityId)?.Kind == EntityKind.Boss)
                && !w.Entities.Any(x => x.Alive && x.Kind == EntityKind.Boss))
            {
                foreach (var m in w.Hostiles.ToList()) ctx.Kill(m, ctx.Phase, "boss_down");
            }

            if (w.Rules.AutoPickupCrystals && w.Hero.Alive)
            {
                var c = w.CrystalAt(w.Hero.Pos);
                if (c != null)
                {
                    c.Alive = false;
                    ctx.Emit(new GameEvent(GameEventType.CrystalCollected, ctx.Phase) { EntityId = w.Hero.Id, TargetId = c.Id, Cell = c.Pos });
                }
            }
        }

        // ------------------------------------------------------------------ 6. Plan

        static void PlanPhase(TurnContext ctx)
        {
            ctx.Phase = EventPhase.Plan;
            var w = ctx.World;
            foreach (var e in w.Entities.Where(x => x.Alive && x.IsHostile).OrderBy(x => x.Id))
            {
                e.Planned = e.Brain != null ? e.Brain.Plan(w, e) : Intent.Wait;
                var ev = ctx.Emit(GameEventType.IntentPlanned, e);
                ev.Dir = e.Planned.Dir;
                ev.Info = e.Planned.Kind.ToString();
                ev.Area = DangerCells(e, e.Planned);
            }
        }

        // ------------------------------------------------------------------ Визуал клеток

        /// <summary>
        /// Снимок «как выглядит» каждая клетка. После каждой фазы сравниваем и шлём
        /// TileVisual только для изменившихся — View не нужно знать правила тайлов
        /// (ворота, лианы, облака, лёд обновятся сами).
        /// </summary>
        public static Dictionary<Cell, int> SnapshotTiles(World w)
        {
            var d = new Dictionary<Cell, int>(w.Tiles.Count);
            foreach (var kv in w.Tiles) d[kv.Key] = Signature(w, kv.Key, kv.Value);
            return d;
        }

        static int Signature(World w, Cell c, TileState t) =>
            ((int)t.Type << 2) | (t.Gone ? 1 : 0) | (w.IsWalkable(c) ? 2 : 0);

        static void EmitTileDiff(TurnContext ctx, ref Dictionary<Cell, int> snap)
        {
            var w = ctx.World;
            var now = SnapshotTiles(w);
            foreach (var kv in now.OrderBy(k => k.Key.Y).ThenBy(k => k.Key.X))
            {
                if (snap.TryGetValue(kv.Key, out var old) && old == kv.Value) continue;
                var t = w.GetTile(kv.Key);
                ctx.Emit(new GameEvent(GameEventType.TileVisual, ctx.Phase)
                {
                    Cell = kv.Key, Info = t.Type.ToString(), Amount = (t.Gone ? 0 : 1), Dir = t.Direction,
                    TargetId = w.IsWalkable(kv.Key) ? 1 : 0,
                });
            }
            snap = now;
        }

        /// <summary>Клетки, которые надо подсветить красным (куда ударит враг).</summary>
        public static Cell[] DangerCells(Entity e, Intent it)
        {
            if (it.Kind != IntentKind.Attack) return null;
            return it.Area ?? new[] { e.Pos.Step(it.Dir) };
        }
    }
}
