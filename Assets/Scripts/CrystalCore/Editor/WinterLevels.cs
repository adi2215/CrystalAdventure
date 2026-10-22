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
    /// Создаёт ассеты LevelWinter1…4 и LevelWinter10…12 (в Assets/Scenes/GameScenes) и дописывает их в конец списка
    /// уровней TilemapData; место в главе Winter им даёт кнопка «Расставить уровни по главам» (Editor/LevelOrder).
    /// Решение и оптимум для звёзд считает LevelSolver прямо при создании, затем уровень сверяется
    /// с ядром (LegacyParityCheck). Уже существующие ассеты не перезаписываются.
    ///
    /// Уровни:
    ///   1. «Каток» — лёд довозит до края: 2 карточки вместо 6 шагов.
    ///   2. «Хрупкий мост» — середина из плит — ловушка: по ней можно пройти только один раз.
    ///   3. «Ледяное поле» — каток 5×5: остановиться можно только на земле или у края.
    ///   4. «Ледяная лестница» — «въехал по льду, поднялся по плите» × 3: просится в цикл.
    ///  10. «Вторая функция» (туториал F2, WinterTutorials) — «долина»: два раза лесенкой вниз, два раза вверх.
    ///      В Main только F1 и F2: F1 = Назад Назад Вправо, F2 = Вправо Вперёд Вперёд, Main = F1 F1 F2 F2.
    ///  11. «Обрыв и лесенка» — шесть шагов вниз и два раза «Влево Влево Вперёд». Хитрость: F1 = Назад Назад
    ///      три раза (или Назад×3 два раза). С одной функцией — 12 карточек.
    ///  12. «Функция в функции» — F1 вызывает F2: F2 = Вперёд Вперёд Вправо (ступенька),
    ///      F1 = Вправо F2 F2 Вправо, Main = F1 F1. Без F2 — 10 карточек. Main не может вызвать F2 сама.
    /// Уровни 10–12 без While: эти уровни — про функции. Их оптимумы проходятся без упора в край (решатель с упором короче не находит).
    /// Оптимумы (2, 5, 7, 3, 10, 10, 9) проверены тестами CrystalCoreTests/WinterTests и FunctionTwoTests.
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
            /// <summary>Карточки F2 (null — панели F2 нет). Панели идут по номеру: Main, F1, F2 — поэтому с F2 всегда есть и панель F1.</summary>
            public ItemType[] F2;
            /// <summary>Туториал уровня: ключ для <see cref="TutorialProvider"/> (null — без туториала).</summary>
            public string Tutorial;
            public int ExpectedOptimal;
        }

        /// <summary>
        /// Создаёт ассет туториала по ключу Spec.Tutorial. Задаётся из Editor/WinterTutorials (Assets/Scripts/Editor):
        /// там нужен TutorialStep, а этот файл компилируется и в тестах ядра — на заглушках без туториалов.
        /// </summary>
        public static System.Func<string, TutorialScript> TutorialProvider;

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

            // ── Вторая функция (F2). Панели F1/F2 — только стрелки; Main в 10–11 — только вызовы F1 и F2. ──
            new Spec
            {
                Name = "LevelWinter10", Title = "Вторая функция", ExpectedOptimal = 10, Tutorial = "F2",
                Main = new[] { ItemType.FunctionOne, ItemType.FunctionTwo }, F1 = Moves, F2 = Moves,
                Rows = new[] { "#S#..C#", ".#...#.", "###.###", "..#.#..", ".#####.", "....#.." },
            },
            new Spec
            {
                Name = "LevelWinter11", Title = "Обрыв и лесенка", ExpectedOptimal = 10,
                Main = new[] { ItemType.FunctionOne, ItemType.FunctionTwo }, F1 = Moves, F2 = Moves,
                Rows = new[] { "...#S.", "....##", "...##.", "#...#.", "C..###", "###.#.", "#.####", "....#." },
            },
            new Spec
            {
                Name = "LevelWinter12", Title = "Функция в функции", ExpectedOptimal = 9,
                Main = MovesAnd(ItemType.FunctionOne), F1 = MovesAnd(ItemType.FunctionTwo), F2 = Moves,
                Rows = new[] { ".......#..", "......###C", ".......#..", ".....####.", "....#.#...", "..######..", "...#.#....", ".####.....", "..#.......", "#S#.......", ".#........" },
            },
        };

        static readonly Dictionary<ItemType, string> ItemGuids = new Dictionary<ItemType, string>
        {
            { ItemType.Forward, DraftLevels.ForwardGuid }, { ItemType.Left, DraftLevels.LeftGuid },
            { ItemType.Right, DraftLevels.RightGuid }, { ItemType.Bottom, DraftLevels.BottomGuid },
            { ItemType.FunctionOne, DraftLevels.FunctionOneGuid }, { ItemType.While, DraftLevels.WhileGuid },
            { ItemType.FunctionTwo, FunctionTwoGuid },
        };

        /// <summary>Карточка F2 (Assets/Scripts/Methods/FunctionTwo.asset).</summary>
        public const string FunctionTwoGuid = "350a688b5d41cd84b9c403fe5e7473d8";

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
            if (spec.F1 != null || spec.F2 != null)
                panels.Add(new Panels { panelName = "FunctionOne", commands = (spec.F1 ?? new ItemType[0]).Select(t => item(ItemGuids[t])).ToArray() });
            if (spec.F2 != null) panels.Add(new Panels { panelName = "FunctionTwo", commands = spec.F2.Select(t => item(ItemGuids[t])).ToArray() });
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
            p.F2Cards = spec.F2;
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
                if (spec.Tutorial != null)
                {
                    lvl.tutorial = TutorialProvider != null ? TutorialProvider(spec.Tutorial) : null;
                    if (lvl.tutorial == null) sb.AppendLine($"  !  {spec.Name}: туториал «{spec.Tutorial}» не создан");
                    EditorUtility.SetDirty(lvl);
                }
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
