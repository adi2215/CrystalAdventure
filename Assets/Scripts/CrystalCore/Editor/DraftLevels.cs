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
    /// Crystal Adventure → Уровни → Заполнить черновики и добавить в игру.
    ///
    /// Черновики LevelFive, LevelSeven, LevelEleven лежали в проекте, но не были в списке уровней игры.
    /// LevelEleven оказался точной копией LevelTen — кнопка заменяет его новым уровнем «Змейка»,
    /// а все три уровня дописывает В КОНЕЦ списка TilemapData (№16–18): сохранения звёзд привязаны
    /// к номеру уровня, вставка в середину сдвинула бы звёзды уже пройденных уровней.
    ///
    /// «Змейка»: ряды разной длины, между ними пустые строки — перебраться можно только грибом.
    /// Клетки покрашены по направлению следующего шага (синяя — вправо, жёлтая — влево,
    /// красная — вверх). В лоб — 8 карточек (2★), а если заметить подсказку цветом —
    /// 7 карточек (3★): Main [F1 F1 F1], F1 [F, L@жёлтый, R@синий, While×5].
    /// Оптимум доказан LevelSolver, решение сверено с ядром и со старыми правилами (тесты).
    ///
    /// Всё через Undo; сцену после нажатия надо сохранить (Ctrl+S).
    /// </summary>
    public static class DraftLevels
    {
        // Ассеты проекта (Scripts/Methods/*.asset и тайлы травы из уровней Forest).
        public const string ForwardGuid = "7bca09acd6e123149a05568b4e84d335";
        public const string LeftGuid = "c4b68b0a1ca666d4398220a5925c3e91";
        public const string RightGuid = "3761fbde5ebc4dc4fa22aada50002b92";
        public const string BottomGuid = "a36b4ddd499e17b45a04f767ed85509c";
        public const string FunctionOneGuid = "7118d544b4f2a61419da87960ce21f7b";
        public const string WhileGuid = "ad319c635084ceb488f2d45cc16e3bdd";
        static readonly string[] GrassGuids =
        {
            "31d5ccdc0f354844b897d0c6302e4a7f", "9893c68a16d7b8141a89f44c806a7a61", "fe3841476b4d00144af3423a02823130",
        };
        const int SpecialGrass = 1; // этим тайлом в старых уровнях нарисованы грибы и кристаллы

        static readonly string[] Drafts = { "LevelFive", "LevelSeven", "LevelEleven" };

        /// <summary>
        /// Карта «Змейки», сверху вниз (y = 8 … 0), слева направо (x = -2 …).
        /// b — синяя (дальше вправо), y — жёлтая (влево), n — красный гриб (вверх через пустую строку),
        /// t — старт (синяя), C — кристалл, Y — кристалл на жёлтой клетке, «.» — пусто.
        /// </summary>
        public static readonly string[] SnakeRows =
        {
            "bbbC..",
            "......",
            "nyyy..",
            "......",
            ".bbn..",
            "......",
            ".nYyyy",
            "......",
            "..tbbn",
        };
        public const int SnakeX0 = -2, SnakeTopY = 8;
        public static readonly Vector2Int SnakeStart = new Vector2Int(0, 0);
        public const int SnakeOptimalCards = 7;

        /// <summary>Клетки «Змейки». art(i) — тайл травы i из GrassGuids.</summary>
        public static List<TileInfo> SnakeTiles(System.Func<int, TileBase> art)
        {
            var tiles = new List<TileInfo>();
            for (int r = SnakeRows.Length - 1; r >= 0; r--)
            {
                int y = SnakeTopY - r;
                var row = SnakeRows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    char ch = row[c];
                    if (ch == '.') continue;
                    int x = SnakeX0 + c;
                    var super = ch == 'n' ? SuperBlocks.Mushroom : ch == 'C' || ch == 'Y' ? SuperBlocks.Crystal : SuperBlocks.None;
                    var color = ch == 'b' || ch == 't' ? TileColor.Blue
                              : ch == 'y' || ch == 'Y' ? TileColor.Yellow
                              : ch == 'n' ? TileColor.Red
                              : TileColor.None;
                    int grass = super != SuperBlocks.None ? SpecialGrass : ((x * 7 + y * 3) % 3 + 3) % 3;
                    tiles.Add(new TileInfo { position = new Vector3Int(x, y, 0), tile = art(grass), superTileType = super, color = color });
                }
            }
            return tiles;
        }

        /// <summary>Заполнить уровень «Змейкой»: клетки, старт, карточки на панелях и эталонное решение.</summary>
        public static void FillSnake(LevelSt lvl, System.Func<int, TileBase> art, System.Func<string, Item> item)
        {
            lvl.tiles = SnakeTiles(art);
            lvl.characterPos = LegacyLevelImport.CharacterPosFor(new Vector3Int(SnakeStart.x, SnakeStart.y, 0), null);
            lvl.panels = new[]
            {
                new Panels { panelName = "Main", commands = new[] { item(ForwardGuid), item(LeftGuid), item(BottomGuid), item(RightGuid), item(FunctionOneGuid) } },
                new Panels { panelName = "FunctionOne", commands = new[] { item(ForwardGuid), item(LeftGuid), item(BottomGuid), item(RightGuid), item(WhileGuid) } },
            };
            lvl.optimalCards = SnakeOptimalCards;
            lvl.solutionMain = new List<SolutionCard>
            {
                new SolutionCard { type = ItemType.FunctionOne }, new SolutionCard { type = ItemType.FunctionOne }, new SolutionCard { type = ItemType.FunctionOne },
            };
            lvl.solutionF1 = new List<SolutionCard>
            {
                new SolutionCard { type = ItemType.Forward },
                new SolutionCard { type = ItemType.Left, color = TileColor.Yellow },
                new SolutionCard { type = ItemType.Right, color = TileColor.Blue },
                new SolutionCard { type = ItemType.While, iterations = 4 }, // ещё 4 раза → 5 проходов
            };
            lvl.solutionF2 = new List<SolutionCard>();
        }

        [MenuItem("Crystal Adventure/Уровни/Заполнить черновики и добавить в игру")]
        static void Run()
        {
            var levels = Drafts.ToDictionary(n => n, FindLevel);
            var missing = levels.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
            if (missing.Count > 0) { Debug.LogError("[Crystal] Не нашёл уровни: " + string.Join(", ", missing)); return; }

            var art = GrassGuids.Select(Load<TileBase>).ToArray();
            if (art.Any(a => a == null)) { Debug.LogError("[Crystal] Не нашёл тайлы травы по GUID — уровень не заполнен"); return; }

            var data = Object.FindObjectOfType<TilemapData>();
            var sb = new StringBuilder();

            var eleven = levels["LevelEleven"];
            var ten = FindLevel("LevelTen");
            bool isCopy = ten != null && Signature(eleven) == Signature(ten);
            string question = isCopy
                ? "LevelEleven — точная копия LevelTen. Заменить его новым уровнем «Змейка» (грибы + цветные клетки)?"
                : "LevelEleven уже не копия LevelTen. Всё равно перезаписать его уровнем «Змейка»?";
            if (EditorUtility.DisplayDialog("Черновики уровней", question, "Заменить", "Не трогать"))
            {
                Undo.RecordObject(eleven, "Fill LevelEleven");
                FillSnake(eleven, i => art[i], guid => Load<Item>(guid));
                EditorUtility.SetDirty(eleven);
                AssetDatabase.SaveAssets();
                var line = LegacyParityCheck.Check(eleven, data != null ? data.tilemap : null, out var verdict);
                sb.AppendLine($"  {(verdict == LegacyParityCheck.Verdict.Ok ? "✓" : "✗")}  LevelEleven → «Змейка»: {line}");
            }

            if (data == null)
            {
                sb.AppendLine("  ?  Сцена с TilemapData не открыта — открой CollectorLevel и нажми ещё раз, чтобы добавить уровни в игру.");
            }
            else
            {
                var list = (data.tilemapData ?? new LevelSt[0]).ToList();
                var toAdd = Drafts.Select(n => levels[n]).Where(l => !list.Contains(l)).ToList();
                if (toAdd.Count > 0)
                {
                    Undo.RecordObject(data, "Add draft levels");
                    list.AddRange(toAdd);
                    data.tilemapData = list.ToArray();
                    EditorUtility.SetDirty(data);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                }
                foreach (var l in Drafts.Select(n => levels[n]))
                    sb.AppendLine($"  +  {l.name}: уровень №{list.IndexOf(l) + 1}" + (toAdd.Contains(l) ? " (добавлен)" : " (уже был в игре)"));
                if (toAdd.Count > 0) sb.AppendLine("  !  Сохрани сцену (Ctrl+S).");
            }

            Debug.Log("[Crystal] Черновики уровней\n" + sb);
        }

        static LevelSt FindLevel(string name) =>
            AssetDatabase.FindAssets("t:LevelSt " + name)
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelSt>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(l => l != null && l.name == name);

        static T Load<T>(string guid) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

        static string Signature(LevelSt l) => l == null || l.tiles == null ? "" :
            string.Join(";", l.tiles.Where(t => t != null).OrderBy(t => t.position.x).ThenBy(t => t.position.y).ThenBy(t => t.position.z)
                .Select(t => $"{t.position.x},{t.position.y},{t.position.z}:{(int)t.superTileType}"));
    }
}
