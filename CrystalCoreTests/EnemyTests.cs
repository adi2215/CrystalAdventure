using System;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.EditorTools;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>
/// Уровни с врагами: синие кубы по маршруту (касание — смерть), боссы-кубы, пушки.
/// Решения, оптимумы (перебором через ядро), «прямой путь — гибель», правила касания, мост к старому кубу.
/// </summary>
static class EnemyTests
{
    static LevelSt Build(EnemyLevels.Spec spec)
    {
        var lvl = new LevelSt { name = spec.Name };
        EnemyLevels.Build(lvl, spec, i => new UnityEngine.Tilemaps.TileBase(), t => { var it = new Item(); it.type = t; return it; });
        return lvl;
    }

    static Vector2Int Start(LevelSt l) { LegacyLevelImport.TryFindStartCell(l, null, out var s, out _); return s; }

    static SimulationResult Run(LevelSt lvl, string program) =>
        Simulation.Run(LegacyLevelImport.ToSetup(lvl, Start(lvl)), ProgramText.Parse(program));

    /// <summary>Маленькое поле с врагами: строки сверху вниз, как в WinterTests.Level.</summary>
    static LevelSt Field(string[] rows, params LevelEnemy[] enemies)
    {
        var lvl = WinterTests.Level("enemy-field", rows);
        lvl.enemies = enemies.ToList();
        return lvl;
    }

    static LevelEnemy Walker(int x, int y, string path) => new LevelEnemy { cell = new Vector2Int(x, y), archetype = "Walker", facing = ItemType.Left, path = path };

    /// <summary>
    /// Есть ли решение без упоров в край не длиннее maxLen карточек (только Main). While — с любым числом
    /// повторов 1–5 и любой скобкой: «всё до предыдущего While» или 1–3 карточки (как может выставить игрок).
    /// Упереться в край — тоже способ переждать ход, но за край снимается звезда, поэтому оптимум считается без них.
    /// Перебор идёт в 4 потока — по первой карточке.
    /// </summary>
    static string Shorter(LevelSt lvl, ItemType[] main, int maxLen)
    {
        var moves = main.Where(t => t != ItemType.While).ToArray();
        bool hasWhile = main.Contains(ItemType.While);
        string found = null;
        System.Threading.Tasks.Parallel.ForEach(moves, first =>
        {
            var setup = LegacyLevelImport.ToSetup(lvl, Start(lvl));
            var seq = new List<CardSpec> { new CardSpec { Type = first } };

            void Dfs(int sinceWhile, bool prevWhile)
            {
                if (found != null) return;
                var p = ItemProgramAdapter.FromCards(s => s == SlotType.Main ? seq : new List<CardSpec>());
                var r = Simulation.Run(setup, p);
                if (r.Won && r.EdgeHits == 0) { found = ProgramText.Print(p); return; }
                if (seq.Count == maxLen) return;
                foreach (var m in moves)
                {
                    seq.Add(new CardSpec { Type = m });
                    Dfs(sinceWhile + 1, false);
                    seq.RemoveAt(seq.Count - 1);
                }
                if (!hasWhile || prevWhile || sinceWhile == 0) return;
                for (int n = 1; n <= 5; n++)
                    for (int span = 0; span <= Math.Min(3, sinceWhile - 1); span++)
                    {
                        seq.Add(new CardSpec { Type = ItemType.While, Iterations = n, Span = span == 0 ? -1 : span });
                        Dfs(0, true);
                        seq.RemoveAt(seq.Count - 1);
                    }
            }

            if (maxLen > 0) Dfs(1, false);
        });
        return found;
    }

    /// <summary>«Очевидные» программы, которые должны погибнуть (иначе враг на уровне ни на что не влияет).</summary>
    static readonly Dictionary<string, string[]> Naive = new Dictionary<string, string[]>
    {
        { "LevelEnemy1", new[] { "R R R R", "repeat 4 { R }" } },
        { "LevelEnemy2", new[] { "R R R R" } },
        { "LevelEnemy3", new[] { "repeat 6 { R }" } },
        { "LevelBossForest", new[] { "repeat 4 { F } repeat 4 { R } repeat 4 { B }" } },
        { "LevelBossWinter", new[] { "repeat 7 { R } repeat 7 { F }", "repeat 7 { F } repeat 7 { R }", "repeat 3 { R R F } repeat 7 { F }" } },
    };

    public static void RunAll()
    {
        Console.WriteLine("\n  -- враги и боссы --");

        foreach (var spec in EnemyLevels.Specs)
        {
            T.Run($"enemy level {spec.Name} «{spec.Title}»: solution wins without edges, optimum {spec.ExpectedOptimal} is minimal, naive path dies", () =>
            {
                var lvl = Build(spec);
                var line = LegacyParityCheck.Check(lvl, null, out var verdict);
                T.True(verdict == LegacyParityCheck.Verdict.Ok, line);
                var r = Simulation.Run(LegacyLevelImport.ToSetup(lvl, Start(lvl)), ItemProgramAdapter.FromSolution(lvl));
                T.Won(r);
                T.Eq(0, r.EdgeHits, "reference solution touches no edge");
                T.Eq(spec.ExpectedOptimal, spec.Solution.Count, "solution length = optimum");
                T.True(spec.Main.All(t => t != ItemType.Attack && t != ItemType.Shield && t != ItemType.SuperAttack), "no attack cards for now");
                foreach (var naive in Naive[spec.Name])
                    T.Outcome(Outcome.Lost, Run(lvl, naive));
                // Арена 8×8 большая: в тестах перебор до 6 карточек (7 проверено разовым перебором при подборе уровня).
                int depth = spec.Name == "LevelBossWinter" ? Math.Min(6, spec.ExpectedOptimal - 1) : spec.ExpectedOptimal - 1;
                var shorter = Shorter(lvl, spec.Main, depth);
                T.True(shorter == null, $"{spec.Name}: shorter solution exists: {shorter}");
            });
        }

        T.Run("scripted walker: follows its path in a loop, waits on 'W' and where there is no tile", () =>
        {
            // Ряд из 5 клеток, враг на (4,0) с маршрутом «L L W R R R»: 3, 2, стоит, 3, 4, дальше клетки нет — стоит; и по кругу.
            var lvl = Field(new[] { "S####" }, Walker(4, 0, "LLWRRR"));
            var setup = LegacyLevelImport.ToSetup(lvl, Start(lvl));
            var w = setup.Build();
            var cells = new List<int>();
            var walker = w.Entities.First(e => e.Archetype == "Walker");
            T.True(walker.Deadly, "walker is deadly on touch");
            for (int i = 0; i < 8; i++)
            {
                var it = walker.Brain.Plan(w, walker);
                if (it.Kind == IntentKind.Move) walker.Pos = walker.Pos.Step(it.Dir);
                cells.Add(walker.Pos.X);
            }
            T.Eq("3,2,2,3,4,4,3,2", string.Join(",", cells), "walker x by turn (W = stand, R past the edge = stand)");
        });

        T.Run("touch rules: swap, same cell, step into a standing enemy, enemy steps into a standing hero -> death", () =>
        {
            // Обмен местами: герой (0,0)→(1,0), враг (1,0)→(0,0).
            T.Outcome(Outcome.Lost, Run(Field(new[] { "S###C" }, Walker(1, 0, "L")), "R R R R"));
            // Одна клетка на двоих: герой (0,0)→(1,0), враг (2,0)→(1,0).
            T.Outcome(Outcome.Lost, Run(Field(new[] { "S###C" }, Walker(2, 0, "L")), "R R R R"));
            // Шагнул в стоящего врага: враг ждёт.
            T.Outcome(Outcome.Lost, Run(Field(new[] { "S###C" }, Walker(1, 0, "W")), "R R R R"));
            // Враг шагнул в героя, который упёрся в край и стоит.
            var r = Run(Field(new[] { "S###C" }, Walker(1, 0, "WL")), "B B R R R R");
            T.Outcome(Outcome.Lost, r);
            T.Eq(2, r.Turns.Count, "dies on the 2nd turn");
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Died && e.Info == "touched"), "death reason = touched");
        });

        T.Run("touch rules: following a leaving enemy (and being followed) is allowed", () =>
        {
            // Враг уходит вправо, герой идёт следом в освобождённую клетку.
            T.Won(Run(Field(new[] { "S###C", ".#..." }, Walker(1, 1, "BW")), "R R R R"));
            // Герой уходит, враг идёт следом.
            var r = Run(Field(new[] { "#S##C" }, Walker(0, 0, "RRRR")), "R R R");
            T.Won(r);
        });

        T.Run("touching the enemy on the crystal cell -> defeat, not victory", () =>
        {
            T.Outcome(Outcome.Lost, Run(Field(new[] { "S#C" }, Walker(2, 0, "W")), "R R"));
        });

        T.Run("turret line kills a one-hp hero", () =>
        {
            var spec = EnemyLevels.Specs.First(s => s.Name == "LevelEnemy3");
            var lvl = Build(spec);
            var r = Run(lvl, "R R");
            T.Outcome(Outcome.Lost, r);
            T.True(r.AllEvents.Any(e => e.Type == GameEventType.Attacked), "turret fired");
        });

        T.Run("bridge: death and victory flags of the plan", () =>
        {
            var lvl = Build(EnemyLevels.Specs.First(s => s.Name == "LevelEnemy1"));
            var start = Start(lvl);
            var lost = Run(lvl, "R R R R");
            T.Outcome(Outcome.Lost, lost);
            var plan = CoreRunBridge.ToPlan(lost, start);
            T.True(plan[plan.Count - 1].HeroDied && plan.Take(plan.Count - 1).All(p => !p.HeroDied), "death only on the last step");
            T.True(!plan[plan.Count - 1].Moved && !plan[plan.Count - 1].Blocked, "touched: the hero did not move (view lunges, not an edge)");
            T.Eq(0, lost.EdgeHits, "touching an enemy is not an edge");

            var won = Simulation.Run(LegacyLevelImport.ToSetup(lvl, start), ItemProgramAdapter.FromSolution(lvl));
            var plan2 = CoreRunBridge.ToPlan(won, start);
            T.True(plan2[plan2.Count - 1].Won && plan2.Take(plan2.Count - 1).All(p => !p.Won), "won only on the last step");
            T.True(plan2.All(p => !p.HeroDied && p.IsMove), "every step is a move");
        });

        T.Run("frost boss: every 3rd turn strikes where the hero looks; lightning = cross, dodge by turning", () =>
        {
            // Поле 6×3, босс в дальнем углу, кристалл далеко — смотрим только, жив ли герой.
            var rows = new[] { "#####.", "######", "S####C" };
            LevelSt F() => Field(rows, new LevelEnemy { cell = new Vector2Int(5, 1), archetype = "BossWinter", facing = ItemType.Left });
            var straight = Run(F(), "R R R");
            T.Outcome(Outcome.Lost, straight);
            T.Eq(3, straight.Turns.Count, "hit on turn 3");
            var hit = straight.AllEvents.First(e => e.Type == GameEventType.Attacked);
            T.True(hit.Style == StormBossBrain.Lightning && hit.To == new Cell(3, 0), "lightning aimed at the cell in front of the hero");
            T.Eq(4, hit.Area.Length, "cross clipped by the field edge (no row below)");
            var plan = straight.AllEvents.First(e => e.Type == GameEventType.IntentPlanned && e.Style != null);
            T.True(plan.Area != null && plan.To == hit.To, "telegraphed one turn ahead with the same target");
            T.Outcome(Outcome.ProgramEnded, Run(F(), "R R F"));   // свернул — мимо
            T.Outcome(Outcome.ProgramEnded, Run(F(), "R R L"));   // шагнул назад — мимо
        });

        T.Run("frost boss: super ice = 3x3, only a step back saves; frozen cells turn to ice and you slide", () =>
        {
            var rows = new[] { "########", "########", "S######C" };
            LevelSt F() => Field(rows, new LevelEnemy { cell = new Vector2Int(7, 2), archetype = "BossWinter", facing = ItemType.Left });
            // Ход 3 — молния (свернули вверх), ход 6 — лёд 3×3 перед героем.
            T.Outcome(Outcome.Lost, Run(F(), "R R F R R R"));
            T.Outcome(Outcome.Lost, Run(F(), "R R F R R F"));      // свернуть от льда мало
            var back = Run(F(), "R R F R R L");
            T.Outcome(Outcome.ProgramEnded, back);
            var frozen = back.AllEvents.Where(e => e.Type == GameEventType.Frozen).ToList();
            T.Eq(9, frozen.Count, "3x3 frozen");
            T.True(frozen.All(e => back.Final.GetTile(e.Cell).Type == TileType.Ice), "cells are ice now");
            // Дальше по замёрзшим клеткам герой скользит.
            var slide = Run(F(), "R R F R R L R");
            T.True(slide.AllEvents.Any(e => e.Type == GameEventType.Slid && e.EntityId == slide.Final.Hero.Id), "hero slides on frozen cells");
        });

        T.Run("frost boss: a crystal cell does not freeze; the boss is deadly to touch", () =>
        {
            var rows = new[] { "#####C##", "########", "S#######" };
            var lvl = Field(rows, new LevelEnemy { cell = new Vector2Int(7, 2), archetype = "BossWinter", facing = ItemType.Left });
            var r = Run(lvl, "R R F R R L");
            var frozen = r.AllEvents.Where(e => e.Type == GameEventType.Frozen).ToList();
            T.Eq(8, frozen.Count, "3x3 minus the crystal cell");
            T.True(frozen.All(e => e.Cell != new Cell(5, 2)), "crystal cell stays ground");
            var touch = Run(Field(new[] { "S#C" }, new LevelEnemy { cell = new Vector2Int(1, 0), archetype = "BossWinter", facing = ItemType.Left }), "R");
            T.Outcome(Outcome.Lost, touch);
        });

        T.Run("cards Attack / Shield / SuperAttack still map to core actions (not used in levels yet)", () =>
        {
            var cards = new List<CardSpec> { new CardSpec { Type = ItemType.Attack }, new CardSpec { Type = ItemType.Shield }, new CardSpec { Type = ItemType.SuperAttack } };
            T.Eq("def F1 { } def F2 { } A S SA", ProgramText.Print(ItemProgramAdapter.FromCards(s => s == SlotType.Main ? cards : null)), "program");
        });
    }
}
