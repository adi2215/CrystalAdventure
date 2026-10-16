using System;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;

static class T
{
    static int _pass, _fail;
    static readonly List<string> Failures = new List<string>();

    public static void Run(string name, Action test)
    {
        try { test(); _pass++; Console.WriteLine($"  ok   {name}"); }
        catch (Exception e) { _fail++; Failures.Add(name); Console.WriteLine($"  FAIL {name}\n       {e.Message}"); }
    }

    public static void True(bool cond, string msg) { if (!cond) throw new Exception(msg); }
    public static void Eq<TV>(TV expected, TV actual, string what) where TV : IEquatable<TV>
    {
        if (!expected.Equals(actual)) throw new Exception($"{what}: expected {expected}, got {actual}");
    }

    public static int Summary()
    {
        Console.WriteLine($"\n{_pass} passed, {_fail} failed");
        foreach (var f in Failures) Console.WriteLine("  - " + f);
        return _fail == 0 ? 0 : 1;
    }

    static string Dump(SimulationResult r) => r.Outcome + ": " + r.Message + "\n" +
        string.Join("\n", r.Turns.Select(t => $"   t{t.Turn} {t.Action}: " + string.Join("; ", t.Events.Select(e => e.ToString()))));

    public static void Won(SimulationResult r) { if (!r.Won) throw new Exception("Expected win, got " + Dump(r)); }
    public static void Outcome(Outcome o, SimulationResult r) { if (r.Outcome != o) throw new Exception($"Expected {o}, got " + Dump(r)); }

    public static Cell HeroPos(SimulationResult r) => r.Final.Hero.Pos;
}

static class Program
{
    // Полоска 0..n по X, герой в (0,0), кристалл в конце.
    static LevelSetup Strip(int n, bool auto = true)
    {
        var l = new LevelSetup();
        l.Rules.AutoPickupCrystals = auto;
        l.AddRect(0, 0, n, 0);
        l.Add(EntityKind.Hero, 0, 0);
        l.Add(EntityKind.Crystal, n, 0);
        return l;
    }

    static int Main()
    {
        Console.WriteLine("Crystal Adventure core tests\n");

        // ---------------- движение и кристаллы
        T.Run("walk to crystal (auto pickup)", () => T.Won(Simulation.Run(Strip(3), "R R R")));
        T.Run("crystal needs Interact when auto pickup is off", () =>
        {
            T.Outcome(Outcome.ProgramEnded, Simulation.Run(Strip(2, auto: false), "R R"));
            T.Won(Simulation.Run(Strip(2, auto: false), "R R I"));
            T.Won(Simulation.Run(Strip(2, auto: false), "R I")); // Interact берёт и кристалл впереди
        });
        T.Run("bump into the edge keeps position", () =>
        {
            var r = Simulation.Run(Strip(3), "L L R");
            T.Eq(new Cell(1, 0), T.HeroPos(r), "hero pos");
            T.True(r.AllEvents.Count(e => e.Type == GameEventType.Bumped) == 2, "two bumps");
        });
        T.Run("height step limit", () =>
        {
            var l = Strip(2); l.Set(1, 0, TileType.Ground, s => s.Height = 2);
            T.Eq(new Cell(0, 0), T.HeroPos(Simulation.Run(l, "R")), "cannot climb 2 blocks");
        });
        T.Run("Forward = +Y like the current game", () =>
        {
            var l = new LevelSetup(); l.AddRect(0, 0, 0, 2); l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 0, 2);
            T.Won(Simulation.Run(l, "F F"));
        });

        // ---------------- программа
        T.Run("repeat N", () => T.Won(Simulation.Run(Strip(6), "repeat 3 { R R }")));
        T.Run("function call", () => T.Won(Simulation.Run(Strip(6), "def A { R R R } call A call A")));
        T.Run("function inside repeat inside function", () =>
            T.Won(Simulation.Run(Strip(8), "def Step { R } def Two { repeat 2 { call Step } } repeat 4 { call Two }")));
        T.Run("while path clear (Facing set by first step)", () =>
        {
            var r = Simulation.Run(Strip(9), "R while path { R }");
            T.Won(r);
            T.Eq(9, r.Turns.Count, "turns");
        });
        T.Run("if / else", () =>
        {
            var l = Strip(4);
            l.Add(EntityKind.Enemy, 2, 0, "TrainingDummy");
            T.Won(Simulation.Run(l, "R repeat 6 { if enemy { A } else { R } }"));
        });
        T.Run("empty while = error, not a freeze", () =>
            T.Outcome(Outcome.Error, Simulation.Run(Strip(3), "while path { }")));
        T.Run("missing function = error", () =>
            T.Outcome(Outcome.Error, Simulation.Run(Strip(3), "call Nope")));
        T.Run("infinite recursion = error", () =>
            T.Outcome(Outcome.Error, Simulation.Run(Strip(3), "def A { call A } call A")));
        T.Run("endless loop with actions stops at turn limit", () =>
        {
            var l = Strip(3); l.Rules.MaxTurns = 50;
            T.Outcome(Outcome.TurnLimit, Simulation.Run(l, "while !wind { L }"));
        });
        T.Run("block ids are reported for UI highlight", () =>
        {
            var p = ProgramText.Parse("repeat 2 { R }");
            var r = Simulation.Run(Strip(2), p);
            T.True(r.Turns.All(t => t.BlockId == 2), "both turns highlight block #2 (the R inside repeat)");
        });
        T.Run("text roundtrip", () =>
        {
            var src = "def A { F A } repeat 3 { call A } while !crystals { if enemy { SA } else { R } }";
            var printed = ProgramText.Print(ProgramText.Parse(src));
            T.Eq(ProgramText.Print(ProgramText.Parse(printed)), printed, "roundtrip");
        });
        T.Run("block count for stars", () =>
        {
            var p = ProgramText.Parse("def A { R R } call A call A");
            T.Eq(4, p.CountBlocks(), "2 in A + 2 calls (function panel is not a card)");
            var l = Strip(4); l.OptimalBlocks = 5; l.GoodBlocks = 7;
            T.Eq(3, Simulation.Run(l, p).Stars, "5 blocks -> 3 stars");
            T.Eq(2, Simulation.Run(l, "R R R R WAIT WAIT WAIT").Stars, "7 blocks -> 2 stars");
            T.Eq(1, Simulation.Run(l, "R R R R WAIT WAIT WAIT WAIT WAIT").Stars, "9 blocks -> 1 star");
        });

        T.Run("old UI cards -> program (While = repeat previous cards N more times, F1 call)", () =>
        {
            Item Card(ItemType t, int it = 0) { var i = new Item(); i.type = t; i.iteration = it; return i; }
            var cmds = new Dictionary<SlotType, List<Item>>
            {
                { SlotType.Main, new List<Item> { Card(ItemType.Right), null, Card(ItemType.While, 2), Card(ItemType.FunctionOne) } },
                { SlotType.FunctionOne, new List<Item> { Card(ItemType.Right), Card(ItemType.Right) } },
                { SlotType.FunctionTwo, new List<Item>() },
            };
            var p = CrystalAdventure.UnityLayer.ItemProgramAdapter.FromCommands(cmds);
            T.Eq("def F1 { R R } def F2 { } repeat 3 { R } call F1", ProgramText.Print(p), "program");
            var r = Simulation.Run(Strip(5), p);
            T.Won(r);
            var tag = p.Find(r.Turns[0].BlockId).Tag as CrystalAdventure.UnityLayer.SlotRef;
            T.True(tag != null && tag.Slot == SlotType.Main && tag.Index == 0, "turn 1 highlights card Main[0]");
        });

        // ---------------- одновременные ходы
        T.Run("enemy moves at the same time as hero (follow train)", () =>
        {
            var l = Strip(6);
            l.Add(EntityKind.Enemy, 1, 0, "SmallBallScout", Dir.East);
            var w = l.Build();
            var scout = w.Entities.First(e => e.IsHostile);
            scout.Planned = Intent.Move(Dir.East);          // уходит из (1,0)
            var ev = TurnResolver.Resolve(w, ActionType.MoveRight); // герой заходит в (1,0) в тот же ход
            var moved = ev.Where(e => e.Type == GameEventType.Moved).ToList();
            T.Eq(2, moved.Count, "hero and scout both moved in turn 1");
            T.Eq(new Cell(1, 0), w.Hero.Pos, "hero took the freed cell");
            T.True(moved.All(e => e.Phase == EventPhase.Move), "same phase -> animated together");
        });
        T.Run("two into one cell -> both bump", () =>
        {
            var l = Strip(4);
            l.Add(EntityKind.Enemy, 2, 0, "SmallBallScout", Dir.West);
            var r = Simulation.Run(l, "R");
            T.Eq(new Cell(0, 0), T.HeroPos(r), "hero stayed");
            T.True(r.Turns[0].Events.Count(e => e.Type == GameEventType.Bumped) == 2, "both bumped");
        });
        T.Run("swap places -> both bump", () =>
        {
            var l = Strip(4);
            l.Add(EntityKind.Enemy, 1, 0, "SmallBallScout", Dir.West);
            // скаут рядом -> он запланирует атаку, а не шаг. Проверим обмен на ChaserBrain с расстоянием 2 после хода.
            var w = l.Build();
            var scout = w.Entities.First(e => e.Archetype == "SmallBallScout");
            scout.Planned = Intent.Move(Dir.West);
            var ev = TurnResolver.Resolve(w, ActionType.MoveRight);
            T.Eq(new Cell(0, 0), w.Hero.Pos, "hero");
            T.Eq(new Cell(1, 0), scout.Pos, "scout");
            T.True(ev.Any(e => e.Info == "swap"), "swap detected");
        });

        // ---------------- бой
        T.Run("hero strikes first: killed enemy does not hit back", () =>
        {
            var l = Strip(3); l.Rules.HeroHp = 1;
            l.Add(EntityKind.Enemy, 1, 0, "SmallBallScout", Dir.West);
            var r = Simulation.Run(l, "A R R R");
            T.Won(r);
            T.Eq(1, r.Final.Hero.Hp, "hero untouched");
        });
        T.Run("telegraphed attack misses if hero steps away", () =>
        {
            var l = new LevelSetup(); l.Rules.AutoPickupCrystals = true; l.Rules.HeroHp = 1;
            l.AddRect(0, 0, 3, 1);
            l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 3, 1);
            l.Add(EntityKind.Enemy, 1, 0, "TrainingDummy");
            var w = l.Build();
            var dummy = w.Entities.First(e => e.IsHostile);
            dummy.Planned = Intent.Attack(Dir.West); // бьёт в (0,0)
            TurnResolver.Resolve(w, ActionType.MoveForward); // герой уходит в (0,1)
            T.True(w.Hero.Alive, "dodged");
        });
        T.Run("shield blocks a turret beam", () =>
        {
            var l = Strip(5); l.Rules.HeroHp = 1;
            l.AddRect(0, 1, 5, 1);
            l.Add(EntityKind.Enemy, 0, 1, "FrostTurret", Dir.East); // стреляет вдоль y=1? нет — вдоль своей линии
            // Турель на (0,1) смотрит на юг и бьёт по (0,0)
            l.Entities.Last().Facing = Dir.South;
            var noShield = Simulation.Run(l, "WAIT WAIT R R R R R");
            T.Outcome(Outcome.Lost, noShield);
            var shield = Simulation.Run(l, "WAIT S R R R R R");
            T.Won(shield);
        });
        T.Run("super attack breaks armor, normal attack bounces", () =>
        {
            var l = Strip(3); l.Rules.SuperAttacks = 1; l.Rules.HeroHp = 10;
            l.Add(EntityKind.Enemy, 1, 0, "HeavyIceGuard", Dir.West);
            var weak = Simulation.Run(l, "A A A");
            T.True(weak.AllEvents.Count(e => e.Type == GameEventType.Blocked && e.Info == "armor") == 3, "armor blocks normal hits");
            var strong = Simulation.Run(l, "SA R R R");
            T.Won(strong);
            T.True(strong.AllEvents.Any(e => e.Type == GameEventType.ArmorBroken), "armor broken");
        });
        T.Run("super attack is limited per level", () =>
        {
            var l = Strip(3); l.Rules.SuperAttacks = 1;
            var r = Simulation.Run(l, "SA SA R R R");
            T.True(r.AllEvents.Count(e => e.Type == GameEventType.SuperAttacked) == 1, "only one super attack");
        });
        T.Run("chaser walks toward hero and attacks when adjacent", () =>
        {
            var l = new LevelSetup(); l.AddRect(0, 0, 4, 0);
            l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 4, 0);
            l.Add(EntityKind.Enemy, 3, 0, "ForestDrone", Dir.West);
            l.Rules.HeroHp = 3;
            var r = Simulation.Run(l, "WAIT WAIT WAIT");
            T.True(r.Final.Hero.Hp < 3, "drone reached and hit the hero");
        });

        // ---------------- супер-тайлы
        T.Run("ice: slide until obstacle", () =>
        {
            var l = Strip(5);
            l.Set(1, 0, TileType.Ice); l.Set(2, 0, TileType.Ice); l.Set(3, 0, TileType.Ice);
            l.Set(5, 0, TileType.Wall);
            l.Entities.RemoveAll(e => e.Kind == EntityKind.Crystal);
            l.Add(EntityKind.Crystal, 0, 1); l.AddTile(0, 1);
            var r = Simulation.Run(l, "R");
            T.Eq(new Cell(4, 0), T.HeroPos(r), "slid to the last cell before the wall");
            T.True(r.Turns[0].Events.Count(e => e.Type == GameEventType.Slid) == 3, "3 slide steps");
        });
        T.Run("mushroom bounces 2 cells (new rule, ClassicMushroom = false)", () =>
        {
            var l = Strip(4); l.Rules.ClassicMushroom = false; l.Set(1, 0, TileType.BounceMushroom);
            var r = Simulation.Run(l, "R");
            T.Eq(new Cell(3, 0), T.HeroPos(r), "bounced");
        });
        T.Run("crumbling tile breaks after leaving", () =>
        {
            var l = Strip(3); l.Set(1, 0, TileType.Crumbling);
            var r = Simulation.Run(l, "R R L L");
            T.Eq(new Cell(2, 0), T.HeroPos(r), "cannot walk back over the hole");
        });
        T.Run("lever opens gate", () =>
        {
            var l = Strip(3, auto: true); l.Set(0, 0, TileType.Lever, s => s.Channel = 1); l.Set(2, 0, TileType.Gate, s => s.Channel = 1);
            T.Outcome(Outcome.ProgramEnded, Simulation.Run(l, "R R R"));
            T.Won(Simulation.Run(l, "I R R R"));
        });
        T.Run("pressure plate holds gate only while pressed", () =>
        {
            var l = new LevelSetup(); l.Rules.AutoPickupCrystals = true;
            l.AddRect(0, 0, 3, 1);
            l.Set(1, 1, TileType.PressurePlate, s => s.Channel = 2);
            l.Set(2, 0, TileType.Gate, s => s.Channel = 2);
            l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 3, 0);
            l.Add(EntityKind.Enemy, 1, 1, "TrainingDummy"); // манекен стоит на кнопке
            T.Won(Simulation.Run(l, "R R R"));
        });
        T.Run("wind pushes at end of turn", () =>
        {
            var l = Strip(4); l.Rules.WindActive = true; l.Rules.WindDir = Dir.East;
            l.Set(1, 0, TileType.Wind);
            var r = Simulation.Run(l, "R");
            T.Eq(new Cell(2, 0), T.HeroPos(r), "pushed one cell");
        });
        T.Run("change wind rotates direction", () =>
        {
            var l = Strip(4); l.Rules.WindActive = true; l.Rules.WindDir = Dir.North;
            var r = Simulation.Run(l, "W");
            T.True(r.Final.WindDir == Dir.East, "N -> E");
        });
        T.Run("teleport pair", () =>
        {
            var l = new LevelSetup(); l.Rules.AutoPickupCrystals = true;
            l.AddRect(0, 0, 1, 0); l.AddRect(5, 5, 6, 5);
            l.Set(1, 0, TileType.Teleport, s => s.Channel = 7); l.Set(5, 5, TileType.Teleport, s => s.Channel = 7);
            l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 6, 5);
            T.Won(Simulation.Run(l, "R R"));
        });
        T.Run("spell removes barrier, break ice opens ice wall", () =>
        {
            var l = Strip(3); l.Set(1, 0, TileType.MagicBarrier);
            T.Won(Simulation.Run(l, "R SP R R R"));
            var l2 = Strip(3); l2.Set(1, 0, TileType.IceWall);
            T.Won(Simulation.Run(l2, "R BI R R R"));
        });
        T.Run("vanishing cloud returns after N turns", () =>
        {
            var l = Strip(3); l.Set(1, 0, TileType.VanishingCloud, s => s.RespawnTurns = 2);
            l.Entities.RemoveAll(e => e.Kind == EntityKind.Crystal); l.Add(EntityKind.Crystal, 0, 5); l.AddTile(0, 5);
            var w = l.Build();
            TurnResolver.Resolve(w, ActionType.MoveRight);
            TurnResolver.Resolve(w, ActionType.MoveRight);   // ушёл с облака -> оно пропало
            T.True(w.GetTile(new Cell(1, 0)).Gone, "gone");
            TurnResolver.Resolve(w, ActionType.Wait);
            TurnResolver.Resolve(w, ActionType.Wait);
            T.True(!w.GetTile(new Cell(1, 0)).Gone, "back");
        });
        T.Run("spikes hurt only on active turns", () =>
        {
            var l = Strip(3); l.Rules.HeroHp = 3;
            l.Set(1, 0, TileType.Spikes, s => { s.Period = 2; s.Offset = 0; });
            var r = Simulation.Run(l, "R R R");     // на шипах в конце хода 1 (нечётный) — безопасно
            T.Eq(3, r.Final.Hero.Hp, "safe timing");
            var r2 = Simulation.Run(l, "WAIT R R R"); // на шипах в конце хода 2 — удар
            T.Eq(2, r2.Final.Hero.Hp, "hit");
        });

        T.Run("tile visuals are reported when a gate opens", () =>
        {
            var l = Strip(3); l.Set(0, 0, TileType.Lever, s => s.Channel = 1); l.Set(2, 0, TileType.Gate, s => s.Channel = 1);
            var r = Simulation.Run(l, "I R R R");
            var vis = r.Turns[0].Events.Where(e => e.Type == GameEventType.TileVisual).ToList();
            T.True(vis.Any(e => e.Cell == new Cell(2, 0) && e.TargetId == 1), "gate became walkable in turn 1");
        });

        // ---------------- боссы
        T.Run("Wood Mech Guardian: phases, guard window, minions vanish", () =>
        {
            var l = new LevelSetup(); l.Rules.HeroHp = 99;
            l.AddRect(0, 0, 4, 4);
            l.Add(EntityKind.Hero, 1, 2, null, Dir.East);
            l.Add(EntityKind.Enemy, 2, 2, "WoodMechGuardian", Dir.West);
            var r = Simulation.Run(l, "repeat 60 { A }");
            T.Won(r);
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Spawned), "summoned drones in phase 2");
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Blocked && e.Info == "guard"), "guarded in phase 3");
            T.True(!r.Final.Hostiles.Any(), "minions vanished with the boss");
        });
        T.Run("Frost General freezes the floor and gets armor", () =>
        {
            var l = new LevelSetup(); l.Rules.HeroHp = 99; l.Rules.SuperAttacks = 5;
            l.AddRect(0, 0, 4, 4);
            l.Add(EntityKind.Hero, 1, 2, null, Dir.East);
            l.Add(EntityKind.Enemy, 2, 2, "FrostBallGeneral", Dir.West);
            var r = Simulation.Run(l, "repeat 40 { if hplow { S } else { A } } repeat 10 { SA A }");
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Frozen), "froze tiles");
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Blocked && e.Info == "armor"), "armor phase");
        });

        // ---------------- детерминизм
        T.Run("same input -> same events (replays, tests, best-score)", () =>
        {
            var l = new LevelSetup(); l.Rules.HeroHp = 99;
            l.AddRect(0, 0, 5, 5);
            l.Add(EntityKind.Hero, 0, 0);
            l.Add(EntityKind.Enemy, 5, 5, "ForestDrone"); l.Add(EntityKind.Enemy, 0, 5, "SmallBallScout", Dir.East);
            l.Add(EntityKind.Crystal, 5, 0);
            string Sig(SimulationResult x) => string.Join("|", x.AllEvents.Select(e => e.ToString()));
            var a = Simulation.Run(l, "repeat 5 { R A }");
            var b = Simulation.Run(l, "repeat 5 { R A }");
            T.Eq(Sig(a), Sig(b), "identical");
        });

        LegacyTests.RunAll();
        WinterTests.RunAll();

        return T.Summary();
    }
}
