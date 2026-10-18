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
    /// Создаёт уровни LevelEnemy1–3, LevelBossForest, LevelBossWinter и дописывает их в конец списка
    /// TilemapData (№23–27). Если уровни уже есть (первая версия врагов) — после подтверждения
    /// перезаписывает их содержимое на месте: ассет и его место в списке те же, звёзды по номеру уровня
    /// не съезжают.
    ///
    /// Враги — синие копии куба героя, идут по своему маршруту (LevelEnemy.path, по кругу); коснулся —
    /// смерть и телепорт на старт. Лесной босс — змея из кубиков (SnakeBrain); зимний — ледяной ромб
    /// с прицельными ударами (StormBossBrain). Пушка стреляет по линии через ход. Атака / Щит / Супер-атака в этих уровнях не используются (решим позже).
    ///
    /// Решения заданы вручную; оптимумы проверены перебором программ через ядро
    /// (CrystalCoreTests/EnemyTests): короче не бывает, прямой путь — гибель, в край решения не упираются.
    ///   1. «Синий патруль» — обойти патрульного сверху: (R F B) × 4 (4).
    ///   2. «Перекрёсток» — переждать: R L, потом R × 4 (цикл только на последнюю R) (4).
    ///   3. «Ледяные пушки» — по нижнему ряду пушки попадают, по верхнему нет: F, R × 6 (цикл на одну R), B (4).
    ///   4. «Корневой змей» — лесной босс: змея из кубиков (голова и 3 сегмента) ползёт по кольцу вокруг ямы,
    ///      каждый 4-й ход — шипы перед головой (держатся 2 хода) или удар хвостом (земля проваливается).
    ///      Короткий путь к кристаллу на полосе змеи упирается в шипы; надо обойти по краю и зайти
    ///      на полосу прямо за хвостом: R × 4, F × 3, L × 3 (6).
    ///   5. «Ледяной ромб» — арена 8×8, босс в углу каждый 3-й ход бьёт в клетку перед героем: молния (крест)
    ///      и супер-лёд (3×3, клетки замерзают). «Два вперёд — шаг назад»: шаг назад спасает от обоих ударов,
    ///      а по замёрзшим клеткам потом проезжаешь: (R R L) × 3, (F F B) × 3 (8).
    /// </summary>
    public static class EnemyLevels
    {
        public sealed class Spec
        {
            public string Name, Title;
            /// <summary>Сверху вниз: '#' клетка, 'S' старт, 'C' кристалл, 'I' лёд, 'P' плита, '0'–'2' шипы с фазой, '.' пусто.</summary>
            public string[] Rows;
            public LevelEnemy[] Enemies;
            public int HeroHp = 1, SuperAttacks = 0;
            public ItemType[] Main;
            public List<SolutionCard> Solution;
            public int ExpectedOptimal;
        }

        public static readonly ItemType[] MovesAndWhile = { ItemType.Forward, ItemType.Left, ItemType.Bottom, ItemType.Right, ItemType.While };
        public static SolutionCard C(ItemType t, int iterations = 1, int span = 0) => new SolutionCard { type = t, iterations = iterations, span = span };
        public static LevelEnemy E(int x, int y, string archetype, ItemType facing, string path = "", int body = 0) =>
            new LevelEnemy { cell = new Vector2Int(x, y), archetype = archetype, facing = facing, path = path, body = body };

        const ItemType F = ItemType.Forward, B = ItemType.Bottom, L = ItemType.Left, R = ItemType.Right, W = ItemType.While;

        public static Spec[] Specs => new[]
        {
            new Spec
            {
                Name = "LevelEnemy1", Title = "Синий патруль", ExpectedOptimal = 4,
                Rows = new[] { ".###.", "S###C" },
                Enemies = new[] { E(3, 0, "Walker", L, "LLRR") },
                Main = MovesAndWhile,
                Solution = new List<SolutionCard> { C(R), C(F), C(B), C(W, 3) },
            },
            new Spec
            {
                Name = "LevelEnemy2", Title = "Перекрёсток", ExpectedOptimal = 4,
                Rows = new[] { "..#..", "..#..", "S###C", "..#..", "..#.." },
                Enemies = new[] { E(2, 4, "Walker", B, "BBBBFFFF") },
                Main = MovesAndWhile,
                Solution = new List<SolutionCard> { C(R), C(L), C(R), C(W, 3, span: 1) },
            },
            new Spec
            {
                Name = "LevelEnemy3", Title = "Ледяные пушки", ExpectedOptimal = 4,
                Rows = new[] { "..#.#..", "#######", "S#####C" },
                Enemies = new[] { E(2, 2, "FrostTurret", B), E(4, 2, "FrostTurret", B) },
                Main = MovesAndWhile,
                Solution = new List<SolutionCard> { C(F), C(R), C(W, 5, span: 1), C(B) },
            },
            new Spec
            {
                Name = "LevelBossForest", Title = "Корневой змей", ExpectedOptimal = 6,
                Rows = new[] { "#####", "#C###", "##.##", "#####", "S###C" },
                Enemies = new[] { E(3, 1, "BossForest", F, "FFLLBBRR", body: 3) },
                Main = MovesAndWhile,
                Solution = new List<SolutionCard> { C(R), C(W, 3), C(F), C(W, 2), C(L), C(W, 2) },
            },
            new Spec
            {
                Name = "LevelBossWinter", Title = "Ледяной ромб", ExpectedOptimal = 8,
                Rows = new[]
                {
                    "######C#",
                    "########",
                    "########",
                    "########",
                    "########",
                    "########",
                    "########",
                    "S######C",
                },
                Enemies = new[] { E(7, 7, "BossWinter", L) },
                Main = MovesAndWhile,
                Solution = new List<SolutionCard> { C(R), C(R), C(L), C(W, 2), C(F), C(F), C(B), C(W, 2) },
            },
        };

        /// <summary>
        /// Клетка из рисунка уровня: '#' / 'S' обычная, 'C' кристалл, 'I' лёд, 'P' плита, 'M' гриб, '0'–'2' шипы с фазой,
        /// 'O' облако, '>' '<' '^' 'v' ветер (вправо-вверх / влево-вниз / влево-вверх / вправо-вниз — как карточки
        /// Right / Left / Forward / Bottom), 'T' / 't' телепорты пары 0 / 1.
        /// </summary>
        public static TileInfo TileFor(char ch)
        {
            var info = new TileInfo();
            switch (ch)
            {
                case 'C': info.superTileType = SuperBlocks.Crystal; break;
                case 'I': info.superTileType = SuperBlocks.Ice; break;
                case 'P': info.superTileType = SuperBlocks.Crumbling; break;
                case 'M': info.superTileType = SuperBlocks.Mushroom; break;
                case 'O': info.superTileType = SuperBlocks.Cloud; break;
                case '>': info.superTileType = SuperBlocks.Wind; info.windDir = ItemType.Right; break;
                case '<': info.superTileType = SuperBlocks.Wind; info.windDir = ItemType.Left; break;
                case '^': info.superTileType = SuperBlocks.Wind; info.windDir = ItemType.Forward; break;
                case 'v': info.superTileType = SuperBlocks.Wind; info.windDir = ItemType.Bottom; break;
                case 'T': info.superTileType = SuperBlocks.Teleport; info.channel = 0; break;
                case 't': info.superTileType = SuperBlocks.Teleport; info.channel = 1; break;
                default:
                    if (ch >= '0' && ch <= '2') { info.superTileType = SuperBlocks.Spikes; info.phase = ch - '0'; }
                    break;
            }
            return info;
        }

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
                    var info = TileFor(ch);
                    info.position = new Vector3Int(x, y, 0);
                    info.tile = art(info.superTileType == SuperBlocks.Crystal || info.superTileType == SuperBlocks.Mushroom ? DraftLevels.SpecialGrass : ((x * 7 + y * 3) % 3 + 3) % 3);
                    tiles.Add(info);
                    if (ch == 'S') start = new Vector2Int(x, y);
                }
            lvl.tiles = tiles;
            lvl.characterPos = LegacyLevelImport.CharacterPosFor(new Vector3Int(start.x, start.y, 0), null);
            lvl.enemies = (spec.Enemies ?? new LevelEnemy[0]).Select(e => new LevelEnemy { cell = e.cell, archetype = e.archetype, facing = e.facing, path = e.path, body = e.body }).ToList();
            lvl.heroHp = spec.HeroHp;
            lvl.superAttacks = spec.SuperAttacks;
            lvl.panels = new[] { new Panels { panelName = "Main", commands = spec.Main.Select(item).ToArray() } };
            lvl.optimalCards = spec.ExpectedOptimal;
            lvl.solutionMain = spec.Solution.Select(c => new SolutionCard { type = c.type, iterations = c.iterations, span = c.span, color = c.color }).ToList();
            lvl.solutionF1 = new List<SolutionCard>();
            lvl.solutionF2 = new List<SolutionCard>();
            lvl.tutorial = null;
        }

        const string LevelsFolder = "Assets/Scenes/GameScenes";
        const string CardsFolder = "Assets/Scripts/Methods";

        static readonly Dictionary<ItemType, string> ExistingCards = new Dictionary<ItemType, string>
        {
            { ItemType.Forward, DraftLevels.ForwardGuid }, { ItemType.Left, DraftLevels.LeftGuid },
            { ItemType.Right, DraftLevels.RightGuid }, { ItemType.Bottom, DraftLevels.BottomGuid },
            { ItemType.FunctionOne, DraftLevels.FunctionOneGuid }, { ItemType.While, DraftLevels.WhileGuid },
        };

        /// <summary>Ассет карточки: старые — по GUID, новые (Атака/Щит/Супер-атака) — создаются при первом вызове
        /// (сейчас в уровнях не используются).</summary>
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
        static void Create() => CreateLevels(Specs, "Уровни с врагами",
            "Перезаписать их новыми (синие враги-кубы по маршруту, змея, ледяной ромб)? Ассеты и номера уровней останутся те же.");

        /// <summary>
        /// Создать уровни по спецификациям и дописать их в конец списка TilemapData (как «Уровни с врагами»).
        /// Уже существующие — после вопроса перезаписываются на месте (ассет и номер те же). Им же пользуется
        /// SpikeLevels.
        /// </summary>
        public static void CreateLevels(Spec[] specs, string title, string overwriteQuestion)
        {
            var art = DraftLevels.GrassGuids.Select(g => AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            if (art.Any(a => a == null)) { Debug.LogError("[Crystal] Не нашёл тайлы травы по GUID — уровни не созданы"); return; }

            var data = Object.FindObjectOfType<TilemapData>();
            if (data == null && !EditorUtility.DisplayDialog(title,
                    "Сцена CollectorLevel не открыта — уровни создам, но в список игры не добавлю. Продолжить?", "Да", "Отмена"))
                return;

            var existing = specs.Where(sp => AssetDatabase.LoadAssetAtPath<LevelSt>($"{LevelsFolder}/{sp.Name}.asset") != null).ToList();
            bool overwrite = existing.Count > 0 && EditorUtility.DisplayDialog(title,
                $"Уже есть: {string.Join(", ", existing.Select(sp => sp.Name))}.\n\n" + overwriteQuestion,
                "Перезаписать", "Не трогать");

            var sb = new StringBuilder();
            var created = new List<LevelSt>();
            foreach (var spec in specs)
            {
                string path = $"{LevelsFolder}/{spec.Name}.asset";
                var lvl = AssetDatabase.LoadAssetAtPath<LevelSt>(path);
                bool isNew = lvl == null;
                if (!isNew && !overwrite)
                {
                    sb.AppendLine($"  =  {spec.Name} «{spec.Title}»: уже есть — не трогаю");
                    created.Add(lvl);
                    continue;
                }
                if (isNew) lvl = ScriptableObject.CreateInstance<LevelSt>();
                else Undo.RecordObject(lvl, "Rebuild enemy level");
                Build(lvl, spec, i => art[i], CardAsset);
                if (isNew) AssetDatabase.CreateAsset(lvl, path);
                else EditorUtility.SetDirty(lvl);
                created.Add(lvl);

                var line = LegacyParityCheck.Check(lvl, data != null ? data.tilemap : null, out var verdict);
                sb.AppendLine($"  {(verdict == LegacyParityCheck.Verdict.Ok ? (isNew ? "+" : "~") : "✗")}  {spec.Name} «{spec.Title}»: {line}");
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
            Debug.Log($"[Crystal] {title}\n" + sb);
        }
    }
}
