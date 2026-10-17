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
    /// Crystal Adventure → Уровни → Создать уровни с врагами.
    ///
    /// 1) Создаёт ассеты карточек Attack / Shield / SuperAttack в Assets/Scripts/Methods (если их нет).
    ///    Картинки у них пустые — иконки рисует UI/CardIcons, кнопки на панели делает SpawnScript.
    /// 2) Создаёт уровни LevelEnemy1–3, LevelBossForest, LevelBossWinter и дописывает их в конец списка
    ///    TilemapData (№23–27). Уже существующие ассеты не перезаписываются.
    ///
    /// Решения здесь заданы вручную: старый LevelSolver боёв не знает. Оптимумы проверены перебором
    /// программ через ядро (CrystalCoreTests/EnemyTests): короче не бывает.
    ///   1. «Чучело» — атака: R A R R R (5).
    ///   2. «Патрульный» — 1 сердце, бей первым, в цикле: (R A) × 4 (3).
    ///   3. «Ледяные пушки» — 3 турели, без щита пройти нельзя: (R S) × 4 (3).
    ///   4. Wood Mech Guardian — читай рисунок босса: (S S A) × 6 (4). 5 сердец — дроны второй фазы бьют.
    ///   5. Frost General — 1 сердце: (S A) × 2, A, SA — супер-атака добивает до ледяной брони (5).
    /// </summary>
    public static class EnemyLevels
    {
        public sealed class Spec
        {
            public string Name, Title;
            /// <summary>Сверху вниз: '#' клетка, 'S' старт, 'C' кристалл, '.' пусто.</summary>
            public string[] Rows;
            public LevelEnemy[] Enemies;
            public int HeroHp = 3, SuperAttacks = 1;
            public ItemType[] Main;
            public List<SolutionCard> Solution;
            public int ExpectedOptimal;
        }

        static readonly ItemType[] Moves = { ItemType.Forward, ItemType.Left, ItemType.Bottom, ItemType.Right };
        static ItemType[] MovesAnd(params ItemType[] extra) => Moves.Concat(extra).ToArray();
        static SolutionCard C(ItemType t, int iterations = 1) => new SolutionCard { type = t, iterations = iterations };
        static LevelEnemy E(int x, int y, string archetype, ItemType facing) => new LevelEnemy { cell = new Vector2Int(x, y), archetype = archetype, facing = facing };

        public static Spec[] Specs => new[]
        {
            new Spec
            {
                Name = "LevelEnemy1", Title = "Чучело", ExpectedOptimal = 5, HeroHp = 3,
                Rows = new[] { "S###C" }, Enemies = new[] { E(2, 0, "TrainingDummy", ItemType.Left) },
                Main = MovesAnd(ItemType.Attack),
                Solution = new List<SolutionCard> { C(ItemType.Right), C(ItemType.Attack), C(ItemType.Right), C(ItemType.Right), C(ItemType.Right) },
            },
            new Spec
            {
                Name = "LevelEnemy2", Title = "Патрульный", ExpectedOptimal = 3, HeroHp = 1,
                Rows = new[] { "#####", "S###C" }, Enemies = new[] { E(3, 0, "SmallBallScout", ItemType.Left) },
                Main = MovesAnd(ItemType.Attack, ItemType.While),
                Solution = new List<SolutionCard> { C(ItemType.Right), C(ItemType.Attack), C(ItemType.While, 3) },
            },
            new Spec
            {
                Name = "LevelEnemy3", Title = "Ледяные пушки", ExpectedOptimal = 3, HeroHp = 1,
                Rows = new[] { ".###.", "S###C" },
                Enemies = new[] { E(1, 1, "FrostTurret", ItemType.Bottom), E(2, 1, "FrostTurret", ItemType.Bottom), E(3, 1, "FrostTurret", ItemType.Bottom) },
                Main = MovesAnd(ItemType.Shield, ItemType.While),
                Solution = new List<SolutionCard> { C(ItemType.Right), C(ItemType.Shield), C(ItemType.While, 3) },
            },
            new Spec
            {
                Name = "LevelBossForest", Title = "Wood Mech Guardian", ExpectedOptimal = 4, HeroHp = 5,
                Rows = new[] { ".#S..", ".##.." }, Enemies = new[] { E(2, 0, "WoodMechGuardian", ItemType.Forward) },
                Main = MovesAnd(ItemType.Attack, ItemType.Shield, ItemType.While),
                Solution = new List<SolutionCard> { C(ItemType.Shield), C(ItemType.Shield), C(ItemType.Attack), C(ItemType.While, 5) },
            },
            new Spec
            {
                Name = "LevelBossWinter", Title = "Frost General", ExpectedOptimal = 5, HeroHp = 1, SuperAttacks = 1,
                Rows = new[] { ".###.", ".#S#.", ".###." }, Enemies = new[] { E(2, 0, "FrostBallGeneral", ItemType.Forward) },
                Main = MovesAnd(ItemType.Attack, ItemType.Shield, ItemType.SuperAttack, ItemType.While),
                Solution = new List<SolutionCard> { C(ItemType.Shield), C(ItemType.Attack), C(ItemType.While, 1), C(ItemType.Attack), C(ItemType.SuperAttack) },
            },
        };

        /// <summary>Клетки, старт, враги, карточки на панели и решение.</summary>
        public static void Build(LevelSt lvl, Spec spec, System.Func<int, TileBase> art, System.Func<ItemType, Item> item)
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
                    var super = ch == 'C' ? SuperBlocks.Crystal : SuperBlocks.None;
                    int grass = super == SuperBlocks.Crystal ? DraftLevels.SpecialGrass : ((x * 7 + y * 3) % 3 + 3) % 3;
                    tiles.Add(new TileInfo { position = new Vector3Int(x, y, 0), tile = art(grass), superTileType = super });
                    if (ch == 'S') start = new Vector2Int(x, y);
                }
            lvl.tiles = tiles;
            lvl.characterPos = LegacyLevelImport.CharacterPosFor(new Vector3Int(start.x, start.y, 0), null);
            lvl.enemies = spec.Enemies.Select(e => new LevelEnemy { cell = e.cell, archetype = e.archetype, facing = e.facing }).ToList();
            lvl.heroHp = spec.HeroHp;
            lvl.superAttacks = spec.SuperAttacks;
            lvl.panels = new[] { new Panels { panelName = "Main", commands = spec.Main.Select(item).ToArray() } };
            lvl.optimalCards = spec.ExpectedOptimal;
            lvl.solutionMain = spec.Solution.Select(c => new SolutionCard { type = c.type, iterations = c.iterations, span = c.span, color = c.color }).ToList();
            lvl.solutionF1 = new List<SolutionCard>();
            lvl.solutionF2 = new List<SolutionCard>();
        }

        const string LevelsFolder = "Assets/Scenes/GameScenes";
        const string CardsFolder = "Assets/Scripts/Methods";

        static readonly Dictionary<ItemType, string> ExistingCards = new Dictionary<ItemType, string>
        {
            { ItemType.Forward, DraftLevels.ForwardGuid }, { ItemType.Left, DraftLevels.LeftGuid },
            { ItemType.Right, DraftLevels.RightGuid }, { ItemType.Bottom, DraftLevels.BottomGuid },
            { ItemType.FunctionOne, DraftLevels.FunctionOneGuid }, { ItemType.While, DraftLevels.WhileGuid },
        };

        /// <summary>Ассет карточки: старые — по GUID, новые (Атака/Щит/Супер-атака) — создаются при первом вызове.</summary>
        static Item CardAsset(ItemType type)
        {
            if (ExistingCards.TryGetValue(type, out var guid))
                return AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(guid));
            string path = $"{CardsFolder}/{type}.asset";
            var item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item != null) return item;
            item = ScriptableObject.CreateInstance<Item>();
            item.type = type;
            item.iteration = 1;
            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"[Crystal] Создана карточка {path} (иконку рисует UI/CardIcons)");
            return item;
        }

        [MenuItem("Crystal Adventure/Уровни/Создать уровни с врагами")]
        static void Create()
        {
            var art = DraftLevels.GrassGuids.Select(g => AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            if (art.Any(a => a == null)) { Debug.LogError("[Crystal] Не нашёл тайлы травы по GUID — уровни не созданы"); return; }

            var data = Object.FindObjectOfType<TilemapData>();
            if (data == null && !EditorUtility.DisplayDialog("Уровни с врагами",
                    "Сцена CollectorLevel не открыта — уровни создам, но в список игры не добавлю. Продолжить?", "Да", "Отмена"))
                return;

            var sb = new StringBuilder();
            var created = new List<LevelSt>();
            foreach (var spec in Specs)
            {
                string path = $"{LevelsFolder}/{spec.Name}.asset";
                var lvl = AssetDatabase.LoadAssetAtPath<LevelSt>(path);
                if (lvl != null)
                {
                    sb.AppendLine($"  =  {spec.Name} «{spec.Title}»: уже есть — не трогаю");
                    created.Add(lvl);
                    continue;
                }
                lvl = ScriptableObject.CreateInstance<LevelSt>();
                Build(lvl, spec, i => art[i], CardAsset);
                AssetDatabase.CreateAsset(lvl, path);
                created.Add(lvl);

                var line = LegacyParityCheck.Check(lvl, data != null ? data.tilemap : null, out var verdict);
                sb.AppendLine($"  {(verdict == LegacyParityCheck.Verdict.Ok ? "+" : "✗")}  {spec.Name} «{spec.Title}»: {line}");
            }
            AssetDatabase.SaveAssets();

            if (data != null)
            {
                var list = (data.tilemapData ?? new LevelSt[0]).ToList();
                var toAdd = created.Where(l => !list.Contains(l)).ToList();
                if (toAdd.Count > 0)
                {
                    Undo.RecordObject(data, "Add enemy levels");
                    list.AddRange(toAdd);
                    data.tilemapData = list.ToArray();
                    EditorUtility.SetDirty(data);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                }
                foreach (var l in created)
                    sb.AppendLine($"  №{list.IndexOf(l) + 1}: {l.name}" + (toAdd.Contains(l) ? " (добавлен)" : ""));
                if (toAdd.Count > 0) sb.AppendLine("  !  Сохрани сцену (Ctrl+S).");
            }
            Debug.Log("[Crystal] Уровни с врагами\n" + sb);
        }
    }
}
