using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrystalAdventure.UnityLayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalAdventure.EditorTools
{
    /// <summary>
    /// Crystal Adventure → Уровни → Создать уровни Winter (лёд и плиты).
    ///
    /// Создаёт ассеты LevelWinter1…4 (в Assets/Scenes/GameScenes) и дописывает их в конец списка уровней
    /// TilemapData — это уровни 19–22, глава Winter (снег на блоках включается сам, SpecialTiles).
    /// Решение и оптимум для звёзд считает LevelSolver прямо при создании, затем уровень сверяется
    /// с ядром (LegacyParityCheck). Уже существующие ассеты не перезаписываются.
    ///
    /// Уровни:
    ///   1. «Каток» — лёд довозит до края: 2 карточки вместо 6 шагов.
    ///   2. «Хрупкий мост» — середина из плит — ловушка: по ней можно пройти только один раз.
    ///   3. «Ледяное поле» — каток 5×5: остановиться можно только на земле или у края.
    ///   4. «Ледяная лестница» — «въехал по льду, поднялся по плите» × 3: просится в цикл.
    /// Оптимумы (2, 5, 7, 3) проверены тестами CrystalCoreTests/WinterTests.
    /// </summary>
    public static class WinterLevels
    {
        public sealed class Spec
        {
            public string Name;
            public string Title;
            /// <summary>Сверху вниз. '#' земля, 'S' старт, 'C' кристалл, 'M' гриб, 'I' лёд, 'P' плита, '.' пусто.</summary>
            public string[] Rows;
            public ItemType[] Main;
            public ItemType[] F1;   // null — панели функции нет
            public int ExpectedOptimal;
        }

        static readonly ItemType[] Moves = { ItemType.Forward, ItemType.Left, ItemType.Bottom, ItemType.Right };
        static ItemType[] MovesAnd(params ItemType[] extra) => Moves.Concat(extra).ToArray();

        public static readonly Spec[] Specs =
        {
            new Spec
            {
                Name = "LevelWinter1", Title = "Каток", ExpectedOptimal = 2, Main = Moves,
                Rows = new[] { "....C", "....I", "SIII#" },
            },
            new Spec
            {
                Name = "LevelWinter2", Title = "Хрупкий мост", ExpectedOptimal = 5, Main = MovesAnd(ItemType.While),
                Rows = new[] { "C#P#C", "#.P.#", "##S##" },
            },
            new Spec
            {
                Name = "LevelWinter3", Title = "Ледяное поле", ExpectedOptimal = 7, Main = Moves,
                Rows = new[] { "IISII", "ICIII", "II#II", "I#ICI", "I#III" },
            },
            new Spec
            {
                Name = "LevelWinter4", Title = "Ледяная лестница", ExpectedOptimal = 3, Main = MovesAnd(ItemType.While),
                Rows = new[] { ".........C", ".........P", "......#II#", "......P...", "...CII#...", "...P......", "SII#......" },
            },
        };

        static readonly Dictionary<ItemType, string> ItemGuids = new Dictionary<ItemType, string>
        {
            { ItemType.Forward, DraftLevels.ForwardGuid }, { ItemType.Left, DraftLevels.LeftGuid },
            { ItemType.Right, DraftLevels.RightGuid }, { ItemType.Bottom, DraftLevels.BottomGuid },
            { ItemType.FunctionOne, DraftLevels.FunctionOneGuid }, { ItemType.While, DraftLevels.WhileGuid },
        };

        /// <summary>Клетки, старт и карточки на панелях (без решения).</summary>
        public static void Build(LevelSt lvl, Spec spec, System.Func<int, TileBase> art, System.Func<string, Item> item)
        {
            var tiles = new List<TileInfo>();
            var start = Vector2Int.zero;
            int top = spec.Rows.Length - 1;
            for (int r = 0; r < spec.Rows.Length; r++)
                for (int x = 0; x < spec.Rows[r].Length; x++)
                {
                    char ch = spec.Rows[r][x];
                    if (ch == '.') continue;
                    int y = top - r;
                    var super = ch == 'C' ? SuperBlocks.Crystal : ch == 'M' ? SuperBlocks.Mushroom
                              : ch == 'I' ? SuperBlocks.Ice : ch == 'P' ? SuperBlocks.Crumbling : SuperBlocks.None;
                    int grass = super == SuperBlocks.Crystal || super == SuperBlocks.Mushroom
                        ? DraftLevels.SpecialGrass : ((x * 7 + y * 3) % 3 + 3) % 3;
                    tiles.Add(new TileInfo { position = new Vector3Int(x, y, 0), tile = art(grass), superTileType = super });
                    if (ch == 'S') start = new Vector2Int(x, y);
                }
            lvl.tiles = tiles;
            lvl.characterPos = LegacyLevelImport.CharacterPosFor(new Vector3Int(start.x, start.y, 0), null);

            var panels = new List<Panels> { new Panels { panelName = "Main", commands = spec.Main.Select(t => item(ItemGuids[t])).ToArray() } };
            if (spec.F1 != null) panels.Add(new Panels { panelName = "FunctionOne", commands = spec.F1.Select(t => item(ItemGuids[t])).ToArray() });
            lvl.panels = panels.ToArray();
        }

        /// <summary>Задача для LevelSolver по клеткам и панелям уровня (как LevelEditorWindow.BuildProblem).</summary>
        public static LevelSolver.Problem Problem(LevelSt lvl, Spec spec)
        {
            var p = new LevelSolver.Problem();
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
            LegacyLevelImport.TryFindStartCell(lvl, null, out var start, out _);
            p.Start = (start.x, start.y);
            p.MainCards = spec.Main;
            p.F1Cards = spec.F1;
            return p;
        }

        /// <summary>Посчитать оптимум и эталонное решение (для звёзд и подсказок).</summary>
        public static LevelSolver.Result Solve(LevelSt lvl, Spec spec)
        {
            var res = LevelSolver.Solve(Problem(lvl, spec));
            lvl.optimalCards = res.Solvable ? res.Cards : 0;
            lvl.solutionMain = ToSolution(res.Main);
            lvl.solutionF1 = ToSolution(res.F1);
            lvl.solutionF2 = ToSolution(res.F2);
            return res;
        }

        static List<SolutionCard> ToSolution(List<LevelSolver.Card> cards) =>
            cards.Select(c => new SolutionCard { type = c.Type, iterations = c.Iterations, span = c.Span, color = c.Color }).ToList();

        const string Folder = "Assets/Scenes/GameScenes";

        [MenuItem("Crystal Adventure/Уровни/Создать уровни Winter (лёд и плиты)")]
        static void Create()
        {
            var art = DraftLevels.GrassGuids.Select(Load<TileBase>).ToArray();
            if (art.Any(a => a == null)) { Debug.LogError("[Crystal] Не нашёл тайлы травы по GUID — уровни не созданы"); return; }

            var data = Object.FindObjectOfType<TilemapData>();
            if (data == null && !EditorUtility.DisplayDialog("Уровни Winter",
                    "Сцена CollectorLevel не открыта — уровни создам, но в список игры не добавлю. Продолжить?", "Да", "Отмена"))
                return;

            var sb = new StringBuilder();
            var created = new List<LevelSt>();
            foreach (var spec in Specs)
            {
                string path = $"{Folder}/{spec.Name}.asset";
                var lvl = AssetDatabase.LoadAssetAtPath<LevelSt>(path);
                if (lvl != null)
                {
                    sb.AppendLine($"  =  {spec.Name} «{spec.Title}»: уже есть — не трогаю");
                    created.Add(lvl);
                    continue;
                }

                lvl = ScriptableObject.CreateInstance<LevelSt>();
                Build(lvl, spec, i => art[i], guid => Load<Item>(guid));
                var res = Solve(lvl, spec);
                AssetDatabase.CreateAsset(lvl, path);
                created.Add(lvl);

                var line = LegacyParityCheck.Check(lvl, data != null ? data.tilemap : null, out var verdict);
                string mark = verdict == LegacyParityCheck.Verdict.Ok && lvl.optimalCards == spec.ExpectedOptimal ? "+" : "✗";
                sb.AppendLine($"  {mark}  {spec.Name} «{spec.Title}»: оптимум {lvl.optimalCards} (ждали {spec.ExpectedOptimal}) — {res}");
                if (mark != "+") sb.AppendLine($"       проверка: {line}");
            }
            AssetDatabase.SaveAssets();

            if (data != null)
            {
                var list = (data.tilemapData ?? new LevelSt[0]).ToList();
                var toAdd = created.Where(l => !list.Contains(l)).ToList();
                if (toAdd.Count > 0)
                {
                    Undo.RecordObject(data, "Add Winter levels");
                    list.AddRange(toAdd);
                    data.tilemapData = list.ToArray();
                    EditorUtility.SetDirty(data);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                }
                foreach (var l in created)
                    sb.AppendLine($"  №{list.IndexOf(l) + 1}: {l.name}" + (toAdd.Contains(l) ? " (добавлен)" : ""));
                if (toAdd.Count > 0) sb.AppendLine("  !  Сохрани сцену (Ctrl+S).");
            }

            Debug.Log("[Crystal] Уровни Winter\n" + sb);
        }

        static T Load<T>(string guid) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    }
}
