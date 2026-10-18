using System;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.EditorTools;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>Клетки неба (Sky): облако, ветер, телепорт — импорт, правила, план для куба и уровни.</summary>
static class SkyTests
{
    static Vector2Int Start(LevelSt l) { LegacyLevelImport.TryFindStartCell(l, null, out var s, out _); return s; }

    static SimulationResult Run(LevelSt l, string program) =>
        Simulation.Run(LegacyLevelImport.ToSetup(l, Start(l)), ProgramText.Parse(program));

    public static void RunAll()
    {
        Console.WriteLine("\n  -- небо --");

        T.Run("sky: import (cloud, wind with its own direction, teleport channel); wind turns on only with wind tiles", () =>
        {
            var lvl = WinterTests.Level("sky", "S#O>Tt", "...T.t");
            var setup = LegacyLevelImport.ToSetup(lvl, Start(lvl));
            T.True(setup.Tiles.Single(t => t.X == 2 && t.Y == 1).Type == TileType.VanishingCloud, "cloud");
            var wind = setup.Tiles.Single(t => t.X == 3 && t.Y == 1);
            T.True(wind.Type == TileType.Wind && !wind.UseGlobalWind && wind.Direction == Dir.East, "wind east");
            T.True(setup.Rules.WindActive, "wind on");
            T.True(setup.Tiles.Single(t => t.X == 5 && t.Y == 1).Channel == 1, "teleport channel 1");
            T.True(lvl.HasSkyTiles && lvl.NeedsCore, "sky level is core-only");
            T.True(!LegacyLevelImport.ToSetup(WinterTests.Level("plain", "S#C"), Vector2Int.zero).Rules.WindActive, "no wind tiles — wind off");
        });

        T.Run("sky: a cloud vanishes when you step off and comes back later", () =>
        {
            var r = Run(WinterTests.Level("cloud", "S#O#C"), "R R L R R R");
            var ev = r.AllEvents.Where(e => e.Type == GameEventType.TileChanged).Select(e => e.Info).ToList();
            T.True(ev.Contains("vanished") && ev.Contains("restored"), "vanished and restored");
            T.True(r.EdgeHits >= 1, "stepping onto a vanished cloud is an edge");
        });

        T.Run("sky: wind pushes once per turn whatever its direction (right-pointing rows used to chain)", () =>
        {
            var right = Run(WinterTests.Level("wind", "S>>>.C"), "R");
            T.Eq(1, right.AllEvents.Count(e => e.Type == GameEventType.Pushed), "one push to the right");
            T.True(right.Final.Hero.Pos == new Cell(2, 0), "landed one cell further");
            var left = Run(WinterTests.Level("wind", "C.<<<S"), "L");
            T.Eq(1, left.AllEvents.Count(e => e.Type == GameEventType.Pushed), "one push to the left");
        });

        T.Run("sky: bridge plan — walk onto the special cell first, then wind push / teleport", () =>
        {
            var lvl = WinterTests.Level("wind", "S#>##C");
            var r = Run(lvl, "R R");
            var plan = CoreRunBridge.ToPlan(r, Start(lvl));
            T.True(plan[1].Special == SpecialMove.Pushed && plan[1].SpecialFrom == new Vector2Int(2, 0) && plan[1].To == new Vector2Int(3, 0), "pushed after the step");
            var tp = WinterTests.Level("tp", "ST.TC");
            var r2 = Run(tp, "R R");
            var plan2 = CoreRunBridge.ToPlan(r2, Start(tp));
            T.True(plan2[0].Special == SpecialMove.Teleported && plan2[0].SpecialFrom == new Vector2Int(1, 0) && plan2[0].To == new Vector2Int(3, 0), "teleported after the step");
            T.True(!plan2[0].Blocked, "not blocked");
        });

        var naive = new Dictionary<string, string[]>
        {
            { "LevelSky1", new[] { "repeat 4 { R } repeat 4 { L } F" } },
            { "LevelSky2", new[] { "repeat 4 { R }" } },
            { "LevelSky3", new[] { "repeat 4 { R }" } },
        };
        foreach (var spec in SkyLevels.Specs)
        {
            T.Run($"sky level {spec.Name} «{spec.Title}»: solution wins without edges, optimum {spec.ExpectedOptimal} is minimal, naive path fails", () =>
            {
                var lvl = EnemyTests.Build(spec);
                var line = LegacyParityCheck.Check(lvl, null, out var verdict);
                T.True(verdict == LegacyParityCheck.Verdict.Ok, line);
                var r = Simulation.Run(LegacyLevelImport.ToSetup(lvl, Start(lvl)), ItemProgramAdapter.FromSolution(lvl));
                T.Won(r);
                T.Eq(0, r.EdgeHits, "no edges");
                T.Eq(spec.ExpectedOptimal, spec.Solution.Count, "solution length = optimum");
                foreach (var n in naive[spec.Name]) T.True(!Run(lvl, n).Won, $"naive «{n}» must not win");
                var shorter = EnemyTests.Shorter(lvl, spec.Main, spec.ExpectedOptimal - 1);
                T.True(shorter == null, $"{spec.Name}: shorter solution exists: {shorter}");
            });
        }
    }
}
