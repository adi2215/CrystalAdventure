using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CrystalAdventure.Core;
using CrystalAdventure.EditorTools;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>
/// Перенос на новое ядро: правила старой игры (гриб, цвета, край, подбор кристалла, While со скобкой)
/// и сверка с настоящими уровнями Assets/Scenes/GameScenes/Level*.asset.
/// </summary>
static class LegacyTests
{
    // Полоска 0..n по X, герой в (0,0), кристалл в конце.
    static LevelSetup Strip(int n)
    {
        var l = new LevelSetup();
        l.AddRect(0, 0, n, 0);
        l.Add(EntityKind.Hero, 0, 0);
        l.Add(EntityKind.Crystal, n, 0);
        return l;
    }

    public static void RunAll()
    {
        Console.WriteLine("\n  -- старые правила --");

        T.Run("classic mushroom: stepping ON it is a plain step, the NEXT step goes 2 cells", () =>
        {
            var l = Strip(5); l.Set(1, 0, TileType.BounceMushroom);
            var r = Simulation.Run(l, "R");
            T.Eq(new Cell(1, 0), T.HeroPos(r), "stands on the mushroom");
            r = Simulation.Run(l, "R R");
            T.Eq(new Cell(3, 0), T.HeroPos(r), "jumped from the mushroom");
            T.True(r.Turns[1].Events.Any(e => e.Type == GameEventType.Moved && e.Info == "jump"), "jump is marked for the view");
        });
        T.Run("classic mushroom: direction comes from the card, middle cell not needed", () =>
        {
            var l = new LevelSetup();
            l.AddTile(0, 0); l.AddTile(0, 2); l.Set(0, 0, TileType.BounceMushroom); // (0,1) — пустота
            l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 0, 2);
            T.Won(Simulation.Run(l, "F"));
        });
        T.Run("classic mushroom: nothing 2 cells away -> edge, hero stays", () =>
        {
            var l = Strip(5); l.Set(4, 0, TileType.BounceMushroom);
            var r = Simulation.Run(l, "R R R R R");
            T.Eq(new Cell(4, 0), T.HeroPos(r), "stays on the mushroom");
            T.Eq(1, r.EdgeHits, "edge hits");
        });
        T.Run("old bounce-on-enter still available (ClassicMushroom = false)", () =>
        {
            var l = Strip(4); l.Rules.ClassicMushroom = false; l.Set(1, 0, TileType.BounceMushroom);
            T.Eq(new Cell(3, 0), T.HeroPos(Simulation.Run(l, "R")), "bounced");
        });

        T.Run("edge vs wall: Bumped.Info = edge only for void", () =>
        {
            var l = Strip(3); l.AddTile(0, 1, TileType.Wall);
            var r = Simulation.Run(l, "L F R R R");
            var bumps = r.AllEvents.Where(e => e.Type == GameEventType.Bumped).ToList();
            T.Eq(2, bumps.Count, "two bumps");
            T.Eq(TurnResolver.EdgeReason, bumps[0].Info, "left = void");
            T.Eq("blocked", bumps[1].Info, "up = wall");
            T.Eq(1, r.EdgeHits, "edge hits");
        });

        T.Run("crystal on the start cell is taken only after coming back", () =>
        {
            var l = new LevelSetup(); l.AddRect(0, 0, 1, 0);
            l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 0, 0);
            T.Outcome(Outcome.ProgramEnded, Simulation.Run(l, "L"));      // упёрся, остался на старте
            var r = Simulation.Run(l, "R L");
            T.Won(r); T.Eq(2, r.Turns.Count, "turns");
            l.Rules.PickupOnArrival = false;
            T.Eq(1, Simulation.Run(l, "L").Turns.Count, "new rule: picked up right after the first turn");
        });

        T.Run("colored card: skipped off-color, no world turn, enemies wait", () =>
        {
            var l = Strip(3); l.Rules.HeroHp = 99;
            l.Set(1, 0, TileType.Ground, s => s.Color = 1);
            l.AddRect(0, 2, 4, 2);
            l.Add(EntityKind.Enemy, 0, 2, "SmallBallScout", Dir.East);
            var p = ProgramText.Parse("R@1 R R@1 R@2 R");
            var r = Simulation.Run(l, p);
            T.Won(r);
            T.Eq(5, r.Turns.Count, "records incl. skips");
            T.True(r.Turns[0].Skipped && !r.Turns[1].Skipped && !r.Turns[2].Skipped && r.Turns[3].Skipped, "skips: #1 and #4");
            T.Eq(3, r.Final.Turn, "world advanced only on real moves");
            T.Eq(new Cell(3, 0), T.HeroPos(r), "hero");
            T.True(r.Turns[0].Events.Single().Type == GameEventType.Skipped, "skip event");
        });
        T.Run("color guard text roundtrip", () =>
        {
            var printed = ProgramText.Print(ProgramText.Parse("def F1 { R@1 } F@3 call F1"));
            T.Eq("def F1 { R@1 } F@3 call F1", printed, "print");
        });
        T.Run("endless recursion of skipped cards still stops", () =>
        {
            var l = Strip(3); l.Rules.MaxTurns = 40;
            T.Outcome(Outcome.Error, Simulation.Run(l, "def A { R@2 call A } call A")); // глубина вызовов
            T.Outcome(Outcome.TurnLimit, Simulation.Run(l, "repeat 1000 { R@2 }"));
        });

        T.Run("block count: function panels are not cards, empty functions free", () =>
        {
            T.Eq(4, ProgramText.Parse("def A { R R } def B { } call A call A").CountBlocks(), "R R + 2 calls");
            var p = ProgramText.Parse("def A { R R } call A");
            p.CountFunctionDefs = true;
            T.Eq(4, p.CountBlocks(), "with defs counted");
        });

        T.Run("cards from MovingManager: per-card repeats, narrowed bracket, colors", () =>
        {
            Item Card(ItemType t) { var i = new Item(); i.type = t; i.iteration = 9; return i; } // 9 в ассете не должен использоваться
            var mm = new MovingManager();
            mm.Commands[SlotType.Main] = new List<Item> { Card(ItemType.Forward), Card(ItemType.Right), null, Card(ItemType.Left), Card(ItemType.While), Card(ItemType.FunctionOne) };
            mm.Iterations[SlotType.Main] = new List<int> { 1, 1, 1, 1, 2, 1 };
            mm.Spans[SlotType.Main] = new List<int> { 0, 0, 0, 0, 3, 0 };     // скобка: слоты 1..3 (R, пусто, L)
            mm.Colors[SlotType.Main] = new List<TileColor> { TileColor.None, TileColor.Red, TileColor.None, TileColor.None, TileColor.None, TileColor.None };
            mm.Commands[SlotType.FunctionOne] = new List<Item> { Card(ItemType.Bottom) };
            mm.Commands[SlotType.FunctionTwo] = new List<Item>();
            var p = ItemProgramAdapter.FromMovingManager(mm);
            T.Eq("def F1 { B } def F2 { } F repeat 3 { R@1 L } call F1", ProgramText.Print(p), "program");
            T.Eq(6, p.CountBlocks(), "= CountCards: 5 in Main + 1 in F1");
            var tag = p.Main[1].Tag as SlotRef;
            T.True(tag != null && tag.Index == 4, "repeat highlights the While card");
        });
        T.Run("While bracket of width 0 does nothing (like MovingManager)", () =>
        {
            var mm = new MovingManager();
            var r = new Item(); r.type = ItemType.Right;
            var w = new Item(); w.type = ItemType.While;
            mm.Commands[SlotType.Main] = new List<Item> { r, w };
            mm.Iterations[SlotType.Main] = new List<int> { 1, 3 };
            mm.Spans[SlotType.Main] = new List<int> { 0, 0 };
            T.Eq("def F1 { } def F2 { } R", ProgramText.Print(ItemProgramAdapter.FromMovingManager(mm)), "program");
        });
        T.Run("second While cannot reach over the first", () =>
        {
            var cards = new List<CardSpec>
            {
                new CardSpec { Type = ItemType.Right }, new CardSpec { Type = ItemType.While, Iterations = 1, Span = -1 },
                new CardSpec { Type = ItemType.Forward }, new CardSpec { Type = ItemType.While, Iterations = 2, Span = 5 },
            };
            var p = ItemProgramAdapter.FromCards(s => s == SlotType.Main ? cards : null);
            T.Eq("def F1 { } def F2 { } repeat 2 { R } repeat 3 { F }", ProgramText.Print(p), "program");
        });

        Console.WriteLine("\n  -- настоящие уровни (Assets/Scenes/GameScenes/Level*.asset) --");
        var levels = LoadLevels();
        T.Run($"found level assets ({levels.Count})", () => T.True(levels.Count >= 18, "expected 18+ levels"));

        foreach (var lvl in levels)
        {
            T.Run($"{lvl.name}: reference solution wins and matches old rules", () =>
            {
                var line = LegacyParityCheck.Check(lvl, null, out var verdict);
                T.True(verdict == LegacyParityCheck.Verdict.Ok, line);
            });
        }

        T.Run("new level «Snake» (replaces the LevelEleven copy): solution wins, matches old rules", () =>
        {
            var types = new Dictionary<string, ItemType>
            {
                { DraftLevels.ForwardGuid, ItemType.Forward }, { DraftLevels.LeftGuid, ItemType.Left }, { DraftLevels.RightGuid, ItemType.Right },
                { DraftLevels.BottomGuid, ItemType.Bottom }, { DraftLevels.FunctionOneGuid, ItemType.FunctionOne }, { DraftLevels.WhileGuid, ItemType.While },
            };
            var lvl = new LevelSt { name = "Snake" };
            DraftLevels.FillSnake(lvl, i => new UnityEngine.Tilemaps.TileBase(), g => { var it = new Item(); it.type = types[g]; return it; });
            var line = LegacyParityCheck.Check(lvl, null, out var verdict);
            T.True(verdict == LegacyParityCheck.Verdict.Ok, line);
            T.True(LegacyLevelImport.TryFindStartCell(lvl, null, out var start, out var d) && start == DraftLevels.SnakeStart && d < 0.001f, "start cell");
            T.Eq(2, lvl.tiles.Count(t => t.superTileType == SuperBlocks.Crystal), "crystals");
            T.Eq(4, lvl.tiles.Count(t => t.superTileType == SuperBlocks.Mushroom), "mushrooms");
            T.True(lvl.panels[0].commands.All(c => c != null) && lvl.panels[1].commands.Any(c => c.type == ItemType.While), "cards on panels");
            // Без цветов — на карточку больше (2★): Main [F1 F1 F1], F1 [F L While×5 R While×3] (найдено LevelSolver).
            var plain = new LevelSt { name = "SnakePlain", tiles = lvl.tiles, characterPos = lvl.characterPos, optimalCards = 8,
                solutionMain = new List<SolutionCard> { new SolutionCard { type = ItemType.FunctionOne }, new SolutionCard { type = ItemType.FunctionOne }, new SolutionCard { type = ItemType.FunctionOne } },
                solutionF1 = new List<SolutionCard> { new SolutionCard { type = ItemType.Forward }, new SolutionCard { type = ItemType.Left }, new SolutionCard { type = ItemType.While, iterations = 4 },
                                                      new SolutionCard { type = ItemType.Right }, new SolutionCard { type = ItemType.While, iterations = 2 } } };
            line = LegacyParityCheck.Check(plain, null, out verdict);
            T.True(verdict == LegacyParityCheck.Verdict.Ok, "plain: " + line);
        });

        T.Run("Phase 3 bridge: cards in MovingManager -> core plan for the old cube (all real levels)", () =>
        {
            foreach (var lvl in levels)
            {
                T.True(LegacyLevelImport.TryFindStartCell(lvl, null, out var start, out _), lvl.name + ": start");
                var mm = new MovingManager();
                foreach (var slot in new[] { SlotType.Main, SlotType.FunctionOne, SlotType.FunctionTwo })
                {
                    var sol = lvl.GetSolution(slot);
                    mm.Commands[slot] = sol.Select(c => { var it = new Item(); it.type = c.type; return it; }).ToList();
                    mm.Iterations[slot] = sol.Select(c => c.iterations).ToList();
                    mm.Spans[slot] = sol.Select((c, i) => c.type == ItemType.While ? SolutionCard.ResolveSpan(sol, i) : 0).ToList();
                    mm.Colors[slot] = sol.Select(c => c.color).ToList();
                }
                T.True(CoreRunBridge.TryPlan(mm, lvl, start, out var plan, out var result), lvl.name + ": plan");
                var old = LegacyReplay.Run(lvl, start, s => ItemProgramAdapter.SolutionCards(lvl, s));
                T.True(result.Won && old.Won, lvl.name + ": won");
                T.Eq(old.Steps, plan.Count, lvl.name + ": steps");
                T.Eq(old.Skipped, plan.Count(p => p.Skipped), lvl.name + ": skipped");
                T.Eq(old.Steps - old.Skipped - old.EdgeHits, plan.Count(p => p.Moved), lvl.name + ": moves");
                T.True(plan[plan.Count - 1].To == old.Final, $"{lvl.name}: final {plan[plan.Count - 1].To} vs {old.Final}");
            }
        });
        T.Run("Phase 3 bridge: edge and skip become plan steps", () =>
        {
            var l = new LevelSetup(); l.AddRect(0, 0, 2, 0); l.Set(1, 0, TileType.Ground, t => t.Color = 1);
            l.Add(EntityKind.Hero, 0, 0); l.Add(EntityKind.Crystal, 2, 0);
            var plan = CoreRunBridge.ToPlan(Simulation.Run(l, "L R@1 R R@1"), new Vector2Int(0, 0));
            T.Eq(4, plan.Count, "steps");
            T.True(!plan[0].Moved && !plan[0].Skipped && plan[0].To == new Vector2Int(0, 0), "edge");
            T.True(plan[1].Skipped, "skip");
            T.True(plan[2].Moved && plan[2].To == new Vector2Int(1, 0), "move");
            T.True(plan[3].Moved && plan[3].To == new Vector2Int(2, 0), "colored move");
        });

        T.Run("random programs on real levels: core == old rules (step by step)", () =>
        {
            var rnd = new System.Random(12345);
            int compared = 0;
            foreach (var lvl in levels)
            {
                T.True(LegacyLevelImport.TryFindStartCell(lvl, null, out var start, out var dist) && dist < LegacyLevelImport.StartMatchDistance, lvl.name + ": start");
                var setup = LegacyLevelImport.ToSetup(lvl, start);
                bool hasColors = lvl.tiles.Any(t => t.color != TileColor.None);
                for (int k = 0; k < 400; k++)
                {
                    var panels = new Dictionary<SlotType, List<CardSpec>>
                    {
                        { SlotType.Main, RandomPanel(rnd, true, hasColors) },
                        { SlotType.FunctionOne, RandomPanel(rnd, false, hasColors, allowF2: true) },
                        { SlotType.FunctionTwo, RandomPanel(rnd, false, hasColors) },
                    };
                    // Программы длиннее 300 шагов не сравниваем: старый MovingManager молча выкидывал
                    // часть повторов цикла, а ядро останавливается на 300-м ходу (известное отличие).
                    if (LegacyReplay.Run(lvl, start, s => panels[s], 1_000_000).QueueLength >= LegacyLevelImport.ClassicMaxTurns) continue;
                    var old = LegacyReplay.Run(lvl, start, s => panels[s]);
                    var core = Simulation.Run(setup, ItemProgramAdapter.FromCards(s => panels[s]));
                    var hero = new Vector2Int(core.Final.Hero.Pos.X, core.Final.Hero.Pos.Y);
                    string ctx = $"{lvl.name} #{k}: {ProgramText.Print(ItemProgramAdapter.FromCards(s => panels[s]))}";
                    T.Eq(old.Won, core.Won, ctx + " won");
                    T.Eq(old.Steps, core.Turns.Count, ctx + " steps");
                    T.True(old.Final == hero, $"{ctx} final: old {old.Final}, core {hero}");
                    T.Eq(old.EdgeHits, core.EdgeHits, ctx + " edge hits");
                    T.Eq(old.Skipped, core.Turns.Count(t => t.Skipped), ctx + " skipped");
                    compared++;
                }
            }
            Console.WriteLine($"       compared {compared} random programs");
            T.True(compared > levels.Count * 100, "enough programs compared");
        });
    }

    // ------------------------------------------------------------------ случайная программа

    static readonly ItemType[] Moves = { ItemType.Forward, ItemType.Right, ItemType.Left, ItemType.Bottom };

    /// <summary>Панель как у игрока: до 8 слотов, пустые слоты, While со своей скобкой, цвета. F2 не вызывает функций (без рекурсии).</summary>
    static List<CardSpec> RandomPanel(System.Random rnd, bool main, bool colors, bool allowF2 = false)
    {
        var list = new List<CardSpec>();
        int n = rnd.Next(main ? 1 : 0, 9);
        int lastWhile = -1;
        for (int i = 0; i < n; i++)
        {
            int roll = rnd.Next(100);
            if (roll < 8) { list.Add(CardSpec.None); continue; }
            if (roll < 22 && i > 0)
            {
                int max = i - (lastWhile + 1);
                list.Add(new CardSpec { Type = ItemType.While, Iterations = rnd.Next(1, 6), Span = rnd.Next(0, max + 1) });
                lastWhile = i;
                continue;
            }
            if (roll < 30 && main) { list.Add(new CardSpec { Type = ItemType.FunctionOne }); continue; }
            if (roll < 36 && (main || allowF2)) { list.Add(new CardSpec { Type = ItemType.FunctionTwo }); continue; }
            list.Add(new CardSpec { Type = Moves[rnd.Next(4)], Color = colors && rnd.Next(3) == 0 ? rnd.Next(1, 4) : 0 });
        }
        return list;
    }

    // ------------------------------------------------------------------ чтение ассетов уровней

    static string LevelsDir([CallerFilePath] string self = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(self), "..", "Assets", "Scenes", "GameScenes"));

    static List<LevelSt> LoadLevels()
    {
        var dir = LevelsDir();
        if (!Directory.Exists(dir)) return new List<LevelSt>();
        return Directory.GetFiles(dir, "Level*.asset").OrderBy(f => f).Select(ParseLevel).Where(l => l != null).ToList();
    }

    static int Int(string s) => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    static float Float(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Минимальный разбор YAML ассета LevelSt: тайлы, старт, оптимум и решение.</summary>
    static LevelSt ParseLevel(string path)
    {
        var lines = File.ReadAllLines(path);
        if (!lines.Any(l => l.Contains("tiles:"))) return null;
        var lvl = new LevelSt { name = Path.GetFileNameWithoutExtension(path), tiles = new List<TileInfo>() };
        List<SolutionCard> section = null;
        TileInfo tile = null;
        SolutionCard card = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            Match m;
            if ((m = Regex.Match(line, @"^  - position: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}")).Success)
            {
                tile = new TileInfo { position = new Vector3Int(Int(m.Groups[1].Value), Int(m.Groups[2].Value), Int(m.Groups[3].Value)) };
                lvl.tiles.Add(tile); card = null; section = null;
            }
            else if (tile != null && (m = Regex.Match(line, @"^    superTileType: (\d+)")).Success) tile.superTileType = (SuperBlocks)Int(m.Groups[1].Value);
            else if (tile != null && (m = Regex.Match(line, @"^    color: (\d+)")).Success) tile.color = (TileColor)Int(m.Groups[1].Value);
            else if ((m = Regex.Match(line, @"^  characterPos: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}")).Success)
            { lvl.characterPos = new Vector3(Float(m.Groups[1].Value), Float(m.Groups[2].Value), Float(m.Groups[3].Value)); tile = null; }
            else if ((m = Regex.Match(line, @"^  optimalCards: (\d+)")).Success) { lvl.optimalCards = Int(m.Groups[1].Value); tile = null; }
            else if ((m = Regex.Match(line, @"^  solution(Main|F1|F2):")).Success)
            {
                tile = null; card = null;
                section = m.Groups[1].Value == "Main" ? lvl.solutionMain : m.Groups[1].Value == "F1" ? lvl.solutionF1 : lvl.solutionF2;
            }
            else if (section != null && (m = Regex.Match(line, @"^  - type: (\d+)")).Success) { card = new SolutionCard { type = (ItemType)Int(m.Groups[1].Value) }; section.Add(card); }
            else if (card != null && (m = Regex.Match(line, @"^    iterations: (-?\d+)")).Success) card.iterations = Int(m.Groups[1].Value);
            else if (card != null && (m = Regex.Match(line, @"^    span: (-?\d+)")).Success) card.span = Int(m.Groups[1].Value);
            else if (card != null && (m = Regex.Match(line, @"^    color: (\d+)")).Success) card.color = (TileColor)Int(m.Groups[1].Value);
            else if (Regex.IsMatch(line, @"^  \w")) { tile = null; card = null; if (!line.StartsWith("  - ")) section = null; }
        }
        return lvl;
    }
}
