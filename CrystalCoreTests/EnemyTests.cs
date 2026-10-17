using System;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.EditorTools;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>Уровни с врагами и боссами: решения, оптимумы (перебором через ядро), мост к старому кубу.</summary>
static class EnemyTests
{
    static LevelSt Build(EnemyLevels.Spec spec)
    {
        var lvl = new LevelSt { name = spec.Name };
        EnemyLevels.Build(lvl, spec, i => new UnityEngine.Tilemaps.TileBase(), t => { var it = new Item(); it.type = t; return it; });
        return lvl;
    }

    static Vector2Int Start(LevelSt l) { LegacyLevelImport.TryFindStartCell(l, null, out var s, out _); return s; }

    /// <summary>Есть ли решение не длиннее maxLen карточек (только Main, While со скобкой «всё до предыдущего»).</summary>
    static string Shorter(LevelSt lvl, ItemType[] main, int maxLen)
    {
        var setup = LegacyLevelImport.ToSetup(lvl, Start(lvl));
        var alphabet = new List<CardSpec>();
        foreach (var t in main)
        {
            if (t == ItemType.While) for (int n = 1; n <= 5; n++) alphabet.Add(new CardSpec { Type = t, Iterations = n, Span = -1 });
            else alphabet.Add(new CardSpec { Type = t });
        }
        for (int len = 1; len <= maxLen; len++)
        {
            var idx = new int[len];
            while (true)
            {
                var seq = idx.Select(i => alphabet[i]).ToList();
                bool ok = true;
                for (int i = 0; i < len; i++) if (seq[i].Type == ItemType.While && (i == 0 || seq[i - 1].Type == ItemType.While)) { ok = false; break; }
                if (ok)
                {
                    var p = ItemProgramAdapter.FromCards(s => s == SlotType.Main ? seq : new List<CardSpec>());
                    if (Simulation.Run(setup, p).Won) return ProgramText.Print(p);
                }
                int pos = len - 1;
                while (pos >= 0 && ++idx[pos] == alphabet.Count) { idx[pos] = 0; pos--; }
                if (pos < 0) break;
            }
        }
        return null;
    }

    public static void RunAll()
    {
        Console.WriteLine("\n  -- враги и боссы --");

        foreach (var spec in EnemyLevels.Specs)
        {
            T.Run($"enemy level {spec.Name} «{spec.Title}»: solution wins, optimum {spec.ExpectedOptimal} is minimal", () =>
            {
                var lvl = Build(spec);
                var line = LegacyParityCheck.Check(lvl, null, out var verdict);
                T.True(verdict == LegacyParityCheck.Verdict.Ok, line);
                var shorter = Shorter(lvl, spec.Main, spec.ExpectedOptimal - 1);
                T.True(shorter == null, $"{spec.Name}: shorter solution exists: {shorter}");
            });
        }

        T.Run("Frost General: armor phase needs Super Attack, plain attacks are blocked", () =>
        {
            var lvl = Build(EnemyLevels.Specs.First(s => s.Name == "LevelBossWinter"));
            lvl.heroHp = 9;
            var setup = LegacyLevelImport.ToSetup(lvl, Start(lvl));
            var r = Simulation.Run(setup, ProgramText.Parse("repeat 20 { S A }"));
            T.True(!r.Won, "no Super Attack — boss survives");
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Blocked && e.Info == "armor"), "armor blocked hits");
            T.True(r.AllEvents.Count(e => e.Type == GameEventType.ArmorGained) == 1, "armor is announced once (ArmorGained) for the view");
        });

        T.Run("hero and boss fall in the same turn -> defeat, not victory", () =>
        {
            var spec = EnemyLevels.Specs.First(s => s.Name == "LevelBossForest");
            var lvl = Build(spec);
            lvl.heroHp = 4; // с 4 сердцами дрон добивает героя в тот же ход, когда падает босс
            var r = Simulation.Run(LegacyLevelImport.ToSetup(lvl, Start(lvl)), ProgramText.Parse("repeat 6 { S S A }"));
            T.Outcome(Outcome.Lost, r);
        });

        T.Run("bridge: bumping into an enemy is not an edge; attack, death and victory flags", () =>
        {
            var lvl = Build(EnemyLevels.Specs.First(s => s.Name == "LevelEnemy1"));
            var start = Start(lvl);
            var setup = LegacyLevelImport.ToSetup(lvl, start);
            var r = Simulation.Run(setup, ProgramText.Parse("R R A R R R"));
            T.Won(r);
            var plan = CoreRunBridge.ToPlan(r, start);
            T.True(plan[1].Blocked && !plan[1].Moved && plan[1].Facing == new Vector2Int(1, 0), "R into the dummy = blocked, facing east");
            T.Eq(0, r.EdgeHits, "no edge hits");
            T.True(plan[2].Action == ActionType.Attack && !plan[2].IsMove, "attack step");
            T.True(plan[plan.Count - 1].Won && plan.Take(plan.Count - 1).All(p => !p.Won), "won only on the last step");

            var lvl2 = Build(EnemyLevels.Specs.First(s => s.Name == "LevelEnemy3"));
            var s2 = Start(lvl2);
            var lost = Simulation.Run(LegacyLevelImport.ToSetup(lvl2, s2), ProgramText.Parse("R R R R"));
            T.Outcome(Outcome.Lost, lost);
            var plan2 = CoreRunBridge.ToPlan(lost, s2);
            T.True(plan2[plan2.Count - 1].HeroDied && plan2[plan2.Count - 1].HeroHp == 0, "death on the last step");
        });

        T.Run("new cards map to core actions (Attack / Shield / SuperAttack)", () =>
        {
            var cards = new List<CardSpec> { new CardSpec { Type = ItemType.Attack }, new CardSpec { Type = ItemType.Shield }, new CardSpec { Type = ItemType.SuperAttack } };
            T.Eq("def F1 { } def F2 { } A S SA", ProgramText.Print(ItemProgramAdapter.FromCards(s => s == SlotType.Main ? cards : null)), "program");
        });
    }
}
