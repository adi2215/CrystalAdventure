using System;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>
/// Шипы (Forest): SuperBlocks.Spikes + TileInfo.phase → TileType.Spikes с периодом 3.
/// Поднимаются в ход N, если (N + phase) % 3 == 0; кто стоит на клетке в конце такого хода — погибает.
/// </summary>
static class SpikeTests
{
    static Vector2Int Start(LevelSt l) { LegacyLevelImport.TryFindStartCell(l, null, out var s, out _); return s; }

    static SimulationResult Run(LevelSt l, string program) =>
        Simulation.Run(LegacyLevelImport.ToSetup(l, Start(l)), ProgramText.Parse(program));

    public static void RunAll()
    {
        Console.WriteLine("\n  -- шипы --");

        T.Run("spikes: import gives TileType.Spikes with period 3 and the phase as offset; level needs the core", () =>
        {
            var lvl = WinterTests.Level("spikes", "S#1#C");
            var setup = LegacyLevelImport.ToSetup(lvl, Start(lvl));
            var spec = setup.Tiles.Single(t => t.X == 2 && t.Y == 0);
            T.True(spec.Type == TileType.Spikes && spec.Period == LegacyLevelImport.SpikePeriod && spec.Offset == 1, "spike spec");
            T.True(lvl.HasSpikes && lvl.NeedsCore && !lvl.HasEnemies, "spike level is core-only");
        });

        T.Run("spikes: standing on them in their turn kills, passing at another time is safe", () =>
        {
            // Шип (2,0) с фазой 1 поднимается на ходах 2, 5, 8… Напрямую — в него на ходу 2.
            var lvl = WinterTests.Level("spikes", "S#1#C");
            var straight = Run(lvl, "R R R R");
            T.Outcome(Outcome.Lost, straight);
            T.Eq(2, straight.Turns.Count, "dies on turn 2");
            T.True(straight.AllEvents.Any(e => e.Type == GameEventType.TrapFired), "trap fired");
            // Переждать два хода (R L) — на шип приходим на ходу 4.
            T.Won(Run(lvl, "R L R R R R"));
        });

        T.Run("spikes: waiting on a spike (edge bump) until it rises kills", () =>
        {
            // Шип (1,0) с фазой 0 — ходы 3, 6… Встали на него и упираемся в край.
            var r = Run(WinterTests.Level("spikes", "S0#C"), "R F F R R");
            T.Outcome(Outcome.Lost, r);
            T.Eq(3, r.Turns.Count, "dies on turn 3");
        });

        var naive = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "LevelSpikes1", new[] { "repeat 4 { R }" } },
            { "LevelSpikes2", new[] { "repeat 4 { R } repeat 4 { F }", "F B repeat 4 { R } repeat 4 { F }" } },
        };
        foreach (var spec in CrystalAdventure.EditorTools.SpikeLevels.Specs)
        {
            T.Run($"spike level {spec.Name} «{spec.Title}»: solution wins without edges, optimum {spec.ExpectedOptimal} is minimal, naive path dies", () =>
            {
                var lvl = EnemyTests.Build(spec);
                var line = CrystalAdventure.EditorTools.LegacyParityCheck.Check(lvl, null, out var verdict);
                T.True(verdict == CrystalAdventure.EditorTools.LegacyParityCheck.Verdict.Ok, line);
                var r = Simulation.Run(LegacyLevelImport.ToSetup(lvl, Start(lvl)), ItemProgramAdapter.FromSolution(lvl));
                T.Won(r);
                T.Eq(0, r.EdgeHits, "no edges");
                T.Eq(spec.ExpectedOptimal, spec.Solution.Count, "solution length = optimum");
                foreach (var n in naive[spec.Name]) T.Outcome(Outcome.Lost, Run(lvl, n));
                var shorter = EnemyTests.Shorter(lvl, spec.Main, spec.ExpectedOptimal - 1);
                T.True(shorter == null, $"{spec.Name}: shorter solution exists: {shorter}");
            });
        }

        T.Run("spikes: walkers die on spikes too", () =>
        {
            var lvl = WinterTests.Level("spikes", "S##0#C");
            lvl.enemies.Add(new LevelEnemy { cell = new Vector2Int(4, 0), archetype = "Walker", facing = ItemType.Left, path = "LWW" });
            var r = Run(lvl, "L L L");
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Died && e.EntityId != r.Final.Hero.Id), "walker died on the spike (turn 3)");
        });
    }
}
