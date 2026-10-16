using System;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.EditorTools;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>
/// Глава Winter: лёд и рассыпающаяся плита. Правила проверяются трижды независимо:
/// ядро (Simulation), эталон (LegacyReplay) и решатель для звёзд/подсказок (LevelSolver).
/// </summary>
static class WinterTests
{
    /// <summary>Уровень из рисунка: '#' клетка, 'I' лёд, 'P' плита, 'M' гриб, 'C' кристалл, 'S' старт. Первая строка — верх (y = rows-1).</summary>
    public static LevelSt Level(string name, params string[] rows)
    {
        var lvl = new LevelSt { name = name, tiles = new List<TileInfo>() };
        Vector2Int start = default;
        for (int r = 0; r < rows.Length; r++)
            for (int x = 0; x < rows[r].Length; x++)
            {
                char ch = rows[r][x];
                if (ch == '.') continue;
                int y = rows.Length - 1 - r;
                var super = ch == 'I' ? SuperBlocks.Ice : ch == 'P' ? SuperBlocks.Crumbling : ch == 'M' ? SuperBlocks.Mushroom : ch == 'C' ? SuperBlocks.Crystal : SuperBlocks.None;
                lvl.tiles.Add(new TileInfo { position = new Vector3Int(x, y, 0), superTileType = super });
                if (ch == 'S') start = new Vector2Int(x, y);
            }
        lvl.characterPos = LegacyLevelImport.CharacterPosFor(new Vector3Int(start.x, start.y, 0), null);
        return lvl;
    }

    static Vector2Int Start(LevelSt l) { LegacyLevelImport.TryFindStartCell(l, null, out var s, out _); return s; }

    static SimulationResult Run(LevelSt l, string program) =>
        Simulation.Run(LegacyLevelImport.ToSetup(l, Start(l)), ProgramText.Parse(program));

    public static void RunAll()
    {
        Console.WriteLine("\n  -- Winter: лёд и рассыпающаяся плита --");

        T.Run("ice: slide until the first normal cell", () =>
        {
            var l = Level("ice1", "SIIIC#");
            var r = Run(l, "R");
            T.Won(r);
            T.Eq(1, r.Turns.Count, "one card, one turn");
            T.Eq(3, r.Turns[0].Events.Count(e => e.Type == GameEventType.Slid), "3 slide steps");
        });
        T.Run("ice: stop on the last ice before the edge", () =>
        {
            var l = Level("ice2", "...C", "SII.");
            var r = Run(l, "R F R");
            T.Eq(new Cell(2, 0), T.HeroPos(Run(l, "R")), "stopped at the last ice");
            T.Outcome(Outcome.ProgramEnded, r); // F с (2,0) — пусто сверху: край
        });
        T.Run("ice: jump from a mushroom onto ice keeps sliding", () =>
        {
            var l = Level("ice3", "SM.IIC");
            var r = Run(l, "R R");
            T.Won(r);
            T.True(r.Turns[1].Events.Any(e => e.Type == GameEventType.Moved && e.Info == "jump"), "jump");
        });
        T.Run("crumbling: you can cross it once, coming back is an edge", () =>
        {
            var l = Level("cr1", "SP#C");
            var r = Run(l, "R R L L R R R");
            T.Eq(2, r.EdgeHits, "both L hit the void where the plate was");
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.TileChanged && e.Info == "crumbled"), "crumbled");
            T.Won(r);
        });
        T.Run("crumbling under ice: slide stops before a fallen plate", () =>
        {
            var l = Level("cr2", "C...", "#PII", "S...");
            // S(0,0) → F на (0,1) → R на плиту (1,1) → R на лёд (2,1): скользит к (3,1) ... затем назад влево по льду до плиты? Плита упала → стоп на льду (2,1).
            var r = Run(l, "F R R L");
            T.Eq(new Cell(2, 1), T.HeroPos(r), "slide back stops at the ice next to the fallen plate");
        });
        T.Run("bridge plan: slide = step onto the first ice, then glide", () =>
        {
            var l = Level("ice4", "SIIC");
            var r = Run(l, "R");
            var plan = CoreRunBridge.ToPlan(r, Start(l));
            T.True(plan[0].Moved && plan[0].Slid && plan[0].Via == new Vector2Int(1, 0) && plan[0].To == new Vector2Int(3, 0), "plan");
        });

        foreach (var spec in WinterLevels.Specs)
        {
            T.Run($"Winter level {spec.Name} «{spec.Title}»: optimum {spec.ExpectedOptimal}, solution wins, core == reference", () =>
            {
                var types = new Dictionary<string, ItemType>
                {
                    { DraftLevels.ForwardGuid, ItemType.Forward }, { DraftLevels.LeftGuid, ItemType.Left }, { DraftLevels.RightGuid, ItemType.Right },
                    { DraftLevels.BottomGuid, ItemType.Bottom }, { DraftLevels.FunctionOneGuid, ItemType.FunctionOne }, { DraftLevels.WhileGuid, ItemType.While },
                };
                var lvl = new LevelSt { name = spec.Name };
                WinterLevels.Build(lvl, spec, i => new UnityEngine.Tilemaps.TileBase(), g => { var it = new Item(); it.type = types[g]; return it; });
                var res = WinterLevels.Solve(lvl, spec);
                T.True(res.Solvable && res.ProvenOptimal, spec.Name + ": " + res);
                T.Eq(spec.ExpectedOptimal, lvl.optimalCards, spec.Name + " optimum");
                var line = LegacyParityCheck.Check(lvl, null, out var verdict);
                T.True(verdict == LegacyParityCheck.Verdict.Ok, line);
                T.True(lvl.panels[0].commands.All(c => c != null), "cards on the Main panel");
            });
        }

        T.Run("random winter maps: core == reference rules (step by step)", () =>
        {
            var rnd = new System.Random(777);
            int compared = 0;
            for (int m = 0; m < 300; m++)
            {
                var lvl = RandomMap(rnd, m);
                var start = Start(lvl);
                var setup = LegacyLevelImport.ToSetup(lvl, start);
                for (int k = 0; k < 40; k++)
                {
                    var panels = new Dictionary<SlotType, List<CardSpec>>
                    {
                        { SlotType.Main, LegacyTests.RandomPanel(rnd, true, false) },
                        { SlotType.FunctionOne, LegacyTests.RandomPanel(rnd, false, false, allowF2: true) },
                        { SlotType.FunctionTwo, LegacyTests.RandomPanel(rnd, false, false) },
                    };
                    if (LegacyReplay.Run(lvl, start, s => panels[s], 1_000_000).QueueLength >= LegacyLevelImport.ClassicMaxTurns) continue;
                    var old = LegacyReplay.Run(lvl, start, s => panels[s]);
                    var core = Simulation.Run(setup, ItemProgramAdapter.FromCards(s => panels[s]));
                    var hero = new Vector2Int(core.Final.Hero.Pos.X, core.Final.Hero.Pos.Y);
                    string ctx = $"{lvl.name} #{k}: {ProgramText.Print(ItemProgramAdapter.FromCards(s => panels[s]))}";
                    T.Eq(old.Won, core.Won, ctx + " won");
                    T.Eq(old.Steps, core.Turns.Count, ctx + " steps");
                    T.True(old.Final == hero, $"{ctx} final: ref {old.Final}, core {hero}");
                    T.Eq(old.EdgeHits, core.EdgeHits, ctx + " edges");
                    compared++;
                }
            }
            Console.WriteLine($"       compared {compared} programs on 300 random winter maps");
        });

        T.Run("LevelSolver on winter maps: its solution wins in the core with the same card count", () =>
        {
            var rnd = new System.Random(4242);
            int solved = 0;
            for (int m = 0; m < 120; m++)
            {
                var lvl = RandomMap(rnd, m);
                var res = LevelSolver.Solve(Problem(lvl, 80_000));
                if (!res.Solvable) continue;
                lvl.optimalCards = res.Cards;
                lvl.solutionMain = ToSolution(res.Main); lvl.solutionF1 = ToSolution(res.F1); lvl.solutionF2 = ToSolution(res.F2);
                var line = LegacyParityCheck.Check(lvl, null, out var verdict);
                T.True(verdict == LegacyParityCheck.Verdict.Ok, lvl.name + ": " + line);
                solved++;
            }
            Console.WriteLine($"       solved and verified {solved} maps");
            T.True(solved > 40, "enough solvable maps");
        });
    }

    public static List<SolutionCard> ToSolution(List<LevelSolver.Card> cards) =>
        cards.Select(c => new SolutionCard { type = c.Type, iterations = c.Iterations, span = c.Span, color = c.Color }).ToList();

    static readonly ItemType[] AllMoves = { ItemType.Forward, ItemType.Left, ItemType.Right, ItemType.Bottom };

    /// <summary>Задача для решателя — так же, как её собирает LevelEditorWindow.BuildProblem.</summary>
    public static LevelSolver.Problem Problem(LevelSt lvl, long budget)
    {
        var p = new LevelSolver.Problem { NodeBudget = budget };
        foreach (var t in lvl.tiles)
        {
            var c = (t.position.x, t.position.y);
            p.Tiles.Add(c);
            if (t.superTileType == SuperBlocks.Mushroom) p.Mushrooms.Add(c);
            if (t.superTileType == SuperBlocks.Crystal) p.Crystals.Add(c);
            if (t.superTileType == SuperBlocks.Ice) p.Ice.Add(c);
            if (t.superTileType == SuperBlocks.Crumbling) p.Crumbling.Add(c);
            if (t.color != TileColor.None) p.Colors[c] = t.color;
        }
        var s = Start(lvl);
        p.Start = (s.x, s.y);
        p.MainCards = AllMoves.Concat(new[] { ItemType.FunctionOne, ItemType.While }).ToArray();
        p.F1Cards = AllMoves.Concat(new[] { ItemType.While }).ToArray();
        return p;
    }

    static LevelSt RandomMap(System.Random rnd, int n)
    {
        var rows = new string[5];
        bool startSet = false;
        int crystals = 0;
        for (int r = 0; r < 5; r++)
        {
            var row = new char[6];
            for (int x = 0; x < 6; x++)
            {
                int roll = rnd.Next(100);
                char ch = roll < 25 ? '.' : roll < 45 ? 'I' : roll < 58 ? 'P' : roll < 64 ? 'M' : '#';
                if (ch == '#' && !startSet && rnd.Next(4) == 0) { ch = 'S'; startSet = true; }
                else if (ch == '#' && crystals < 3 && rnd.Next(5) == 0) { ch = 'C'; crystals++; }
                row[x] = ch;
            }
            rows[r] = new string(row);
        }
        if (!startSet) rows[4] = "S" + rows[4].Substring(1);
        // Кристалл обязателен (старт мог его затереть) — уровень без кристаллов редактор не сохранит.
        if (!rows.Any(r => r.Contains('C'))) rows[0] = rows[0].Substring(0, 5) + "C";
        return Level("winter" + n, rows);
    }
}
