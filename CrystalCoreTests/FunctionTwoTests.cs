using System;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.EditorTools;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>
/// Вторая функция (F2) и «функция в функции» (карточка F2 на панели F1): решатель LevelSolver,
/// ядро и эталон старых правил (LegacyReplay) на уровнях Winter 10–12.
/// </summary>
static class FunctionTwoTests
{
    static readonly ItemType[] Moves = { ItemType.Forward, ItemType.Left, ItemType.Bottom, ItemType.Right };

    static WinterLevels.Spec SpecOf(string name) => WinterLevels.Specs.First(s => s.Name == name);

    static LevelSt Build(WinterLevels.Spec spec)
    {
        var types = new Dictionary<string, ItemType>
        {
            { DraftLevels.ForwardGuid, ItemType.Forward }, { DraftLevels.LeftGuid, ItemType.Left }, { DraftLevels.RightGuid, ItemType.Right },
            { DraftLevels.BottomGuid, ItemType.Bottom }, { DraftLevels.FunctionOneGuid, ItemType.FunctionOne }, { DraftLevels.WhileGuid, ItemType.While },
            { WinterLevels.FunctionTwoGuid, ItemType.FunctionTwo },
        };
        var lvl = new LevelSt { name = spec.Name };
        WinterLevels.Build(lvl, spec, i => new UnityEngine.Tilemaps.TileBase(), g => { var it = new Item(); it.type = types[g]; return it; });
        return lvl;
    }

    /// <summary>Карточка решателя → карточка UI (у решателя «вся скобка» — Span 0, у CardSpec — -1).</summary>
    static CardSpec Spec(LevelSolver.Card c) => new CardSpec { Type = c.Type, Iterations = c.Iterations, Span = c.Span == 0 ? -1 : c.Span };

    static List<CardSpec> Cards(params ItemType[] t) => t.Select(x => new CardSpec { Type = x }).ToList();

    /// <summary>Прогон программы в ядре и в эталоне; оба должны победить без упора в край.</summary>
    static void WinsCleanly(LevelSt lvl, Dictionary<SlotType, List<CardSpec>> panels, string what)
    {
        LegacyLevelImport.TryFindStartCell(lvl, null, out var start, out _);
        Func<SlotType, IList<CardSpec>> get = s => panels.TryGetValue(s, out var l) ? l : new List<CardSpec>();
        var core = Simulation.Run(LegacyLevelImport.ToSetup(lvl, start), ItemProgramAdapter.FromCards(get));
        var old = LegacyReplay.Run(lvl, start, get);
        T.Won(core);
        T.True(old.Won, what + ": reference rules win too");
        T.Eq(0, core.EdgeHits, what + ": no edge hits");
        T.Eq(old.Steps, core.Turns.Count, what + ": same steps");
    }

    public static void RunAll()
    {
        Console.WriteLine("\n  -- F2: вторая функция и функция в функции --");

        T.Run("panels: a level with F2 gets Main, F1 and F2 panels (F1 even if empty)", () =>
        {
            var spec = new WinterLevels.Spec { Name = "f2panels", Rows = new[] { "SC" }, Main = new[] { ItemType.FunctionTwo }, F2 = Moves };
            var lvl = Build(spec);
            T.Eq(3, lvl.panels.Length, "three panels");
            T.True(lvl.panels[1].panelName == "FunctionOne" && lvl.panels[1].commands.Length == 0, "empty F1 panel");
            T.True(lvl.panels[2].panelName == "FunctionTwo" && lvl.panels[2].commands.Length == 4, "F2 panel");
        });

        T.Run("LevelSolver: F1 calls F2 — nested solution is shorter than F1 alone", () =>
        {
            var spec = SpecOf("LevelWinter12");
            var lvl = Build(spec);
            var nested = LevelSolver.Solve(WinterLevels.Problem(lvl, spec));
            T.True(nested.Solvable && nested.ProvenOptimal, nested.ToString());
            T.Eq(9, nested.Cards, "nested optimum");
            T.True(nested.F1.Any(c => c.Type == ItemType.FunctionTwo), "F1 calls F2: " + nested);
            T.True(nested.F2.Count > 0, "F2 is filled");

            var flat = new WinterLevels.Spec { Name = spec.Name, Rows = spec.Rows, Main = spec.Main, F1 = Moves };
            var single = LevelSolver.Solve(WinterLevels.Problem(Build(flat), flat));
            T.True(single.Solvable && single.ProvenOptimal, single.ToString());
            T.Eq(10, single.Cards, "without F2 in F1");
        });

        T.Run("LevelSolver: F2 is never filled when nobody can call it", () =>
        {
            // Main не вызывает F2, на F1 нет карточки F2 — значит, F2 пустая.
            var spec = new WinterLevels.Spec { Name = "f2unused", Rows = new[] { "S###C" }, Main = new[] { ItemType.Right, ItemType.FunctionOne }, F1 = Moves, F2 = Moves };
            var res = LevelSolver.Solve(WinterLevels.Problem(Build(spec), spec));
            T.True(res.Solvable, res.ToString());
            T.Eq(0, res.F2.Count, "empty F2: " + res);
        });

        T.Run("Winter10 tutorial program (F1 = B B R, F2 = R F F, Main = F1 F1 F2 F2) wins with the optimum", () =>
        {
            var lvl = Build(SpecOf("LevelWinter10"));
            var panels = new Dictionary<SlotType, List<CardSpec>>
            {
                { SlotType.Main, Cards(ItemType.FunctionOne, ItemType.FunctionOne, ItemType.FunctionTwo, ItemType.FunctionTwo) },
                { SlotType.FunctionOne, Cards(ItemType.Bottom, ItemType.Bottom, ItemType.Right) },
                { SlotType.FunctionTwo, Cards(ItemType.Right, ItemType.Forward, ItemType.Forward) },
            };
            WinsCleanly(lvl, panels, "tutorial");
            T.Eq(SpecOf("LevelWinter10").ExpectedOptimal, panels.Values.Sum(l => l.Count), "tutorial uses the optimum");
        });

        T.Run("Winter12: nested program F1 = R F2 F2 R wins in core and reference", () =>
        {
            var lvl = Build(SpecOf("LevelWinter12"));
            var panels = new Dictionary<SlotType, List<CardSpec>>
            {
                { SlotType.Main, Cards(ItemType.FunctionOne, ItemType.FunctionOne) },
                { SlotType.FunctionOne, Cards(ItemType.Right, ItemType.FunctionTwo, ItemType.FunctionTwo, ItemType.Right) },
                { SlotType.FunctionTwo, Cards(ItemType.Forward, ItemType.Forward, ItemType.Right) },
            };
            WinsCleanly(lvl, panels, "nested");
        });

        T.Run("Winter10–12: the solver optimum needs no edge hits", () =>
        {
            foreach (var name in new[] { "LevelWinter10", "LevelWinter11", "LevelWinter12" })
            {
                var spec = SpecOf(name);
                var lvl = Build(spec);
                var res = WinterLevels.Solve(lvl, spec);
                T.True(res.Solvable && res.ProvenOptimal, name + ": " + res);
                var panels = new Dictionary<SlotType, List<CardSpec>>
                {
                    { SlotType.Main, res.Main.Select(c => Spec(c)).ToList() },
                    { SlotType.FunctionOne, res.F1.Select(c => Spec(c)).ToList() },
                    { SlotType.FunctionTwo, res.F2.Select(c => Spec(c)).ToList() },
                };
                WinsCleanly(lvl, panels, name);
            }
        });
    }
}
