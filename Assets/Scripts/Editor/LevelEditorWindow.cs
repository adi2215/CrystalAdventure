using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Crystal Adventure → Редактор уровней.
///
/// Как пользоваться:
///  1. Открыть сцену с Tilemap (CreateLevel или CollectorLevel), окно найдёт Tilemap само.
///  2. Выбрать уровень (LevelSt) → «Загрузить в Tilemap» — тайлы уровня появятся в сцене.
///  3. Рисовать тайлы обычной Tile Palette.
///  4. Инструментом в окне кликать в Scene view: Кристалл / Гриб / Старт / Стереть.
///  5. «Сохранить» — тайлы + кристаллы/грибы + старт пишутся в ассет и на диск.
///
/// Кристаллы, грибы и старт хранятся в окне отдельно от тайлов, поэтому пересохранение
/// тайлов их больше не стирает. Старт хранится как клетка, а characterPos считается
/// так же, как в игре: центр клетки + сдвиг Tilemap игровой сцены + (0, 0.12, 1.001).
/// </summary>
public class LevelEditorWindow : EditorWindow
{
    enum Tool { None, Crystal, Mushroom, Start, Erase, Paint, Ice, Crumbling }

    // Позиция Tilemap в CollectorLevel (Grid в нуле). Из неё считается characterPos.
    static readonly Vector3 DefaultGameTilemapOffset = new Vector3(0f, -1.62f, 0f);
    static readonly Vector3 CharacterOnTileOffset = new Vector3(0f, 0.12f, 1.001f);
    const float StartMatchDistance = 0.05f;

    LevelSt level;
    Tilemap tilemap;
    Vector3 gameTilemapOffset = DefaultGameTilemapOffset;
    Tool tool = Tool.None;

    // Рабочая копия супер-тайлов и старта (ключ — колонка x, y).
    readonly Dictionary<Vector2Int, SuperBlocks> supers = new Dictionary<Vector2Int, SuperBlocks>();
    // Цвета клеток (условия для покрашенных карточек)
    readonly Dictionary<Vector2Int, TileColor> colors = new Dictionary<Vector2Int, TileColor>();
    TileColor paintColor = TileColor.Red;
    Vector2Int? start;
    bool unsaved;
    Vector2 scroll;
    string report = "";

    [MenuItem("Crystal Adventure/Редактор уровней")]
    static void Open() => GetWindow<LevelEditorWindow>("Редактор уровней");

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        FindTilemap();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnHierarchyChange()
    {
        if (tilemap == null) FindTilemap();
        Repaint();
    }

    void FindTilemap()
    {
        var data = FindObjectOfType<TilemapData>();
        if (data != null && data.tilemap != null) tilemap = data.tilemap;
        else if (tilemap == null) tilemap = FindObjectOfType<Tilemap>();
    }

    // ───────────────────────────── окно ─────────────────────────────

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUI.BeginChangeCheck();
        var newLevel = (LevelSt)EditorGUILayout.ObjectField("Уровень", level, typeof(LevelSt), false);
        if (EditorGUI.EndChangeCheck() && newLevel != level)
        {
            if (!unsaved || EditorUtility.DisplayDialog("Несохранённые изменения",
                    "Кристаллы/грибы/старт текущего уровня не сохранены. Переключиться?", "Да", "Нет"))
            {
                level = newLevel;
                ReadMarkersFromLevel();
            }
        }

        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap в сцене", tilemap, typeof(Tilemap), true);
        gameTilemapOffset = EditorGUILayout.Vector3Field(
            new GUIContent("Сдвиг Tilemap в игре", "Позиция объекта Tilemap в CollectorLevel. Нужна, чтобы правильно посчитать позицию героя."),
            gameTilemapOffset);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Новый уровень")) CreateLevel();
            GUI.enabled = level != null;
            if (GUILayout.Button("Выделить ассет")) { Selection.activeObject = level; EditorGUIUtility.PingObject(level); }
            GUI.enabled = true;
        }

        if (tilemap == null)
        {
            EditorGUILayout.HelpBox("В открытой сцене не найден Tilemap. Открой сцену с уровнем или перетащи Tilemap сюда.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = level != null;
            if (GUILayout.Button("Загрузить в Tilemap", GUILayout.Height(28))) LoadIntoTilemap();
            if (GUILayout.Button(unsaved ? "Сохранить *" : "Сохранить", GUILayout.Height(28))) Save();
            GUI.enabled = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Инструмент (клик в Scene view)", EditorStyles.boldLabel);
        tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "Нет", "Кристалл", "Гриб", "Старт", "Стереть", "Цвет", "Лёд", "Плита" });
        if (tool == Tool.Ice)
            EditorGUILayout.HelpBox("Лёд (Winter): встал — скользишь в ту же сторону до обычной клетки или края.", MessageType.None);
        if (tool == Tool.Crumbling)
            EditorGUILayout.HelpBox("Рассыпающаяся плита (Winter): пройти можно один раз — после ухода с неё пустота.", MessageType.None);
        if (tool == Tool.Paint)
        {
            paintColor = (TileColor)EditorGUILayout.EnumPopup(new GUIContent("Цвет клетки", "None — убрать цвет. Покрашенная карточка хода срабатывает только на клетке своего цвета."), paintColor);
        }
        if (tool != Tool.None)
            EditorGUILayout.HelpBox("Кликни по клетке в Scene view. Alt + мышь — вращать/двигать камеру как обычно.", MessageType.None);

        EditorGUILayout.Space();
        int crystals = supers.Count(kv => kv.Value == SuperBlocks.Crystal);
        int mushrooms = supers.Count(kv => kv.Value == SuperBlocks.Mushroom);
        EditorGUILayout.LabelField($"Кристаллов: {crystals}   Грибов: {mushrooms}   Цветных клеток: {colors.Count}   Старт: {(start.HasValue ? $"({start.Value.x}, {start.Value.y})" : "не задан")}");
        int ice = supers.Count(kv => kv.Value == SuperBlocks.Ice);
        int crumbling = supers.Count(kv => kv.Value == SuperBlocks.Crumbling);
        if (ice + crumbling > 0) EditorGUILayout.LabelField($"Льда: {ice}   Плит: {crumbling}");

        foreach (var problem in Validate(level, CollectColumns(), supers, start))
            EditorGUILayout.HelpBox(problem, MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Решение и звёзды", EditorStyles.boldLabel);
        if (level != null)
        {
            if (level.HasSolution)
                EditorGUILayout.HelpBox($"Оптимум: {level.optimalCards} карт. → 3★ при ≤ {level.optimalCards}.\n{DescribeSolution(level)}", MessageType.None);
            else
                EditorGUILayout.HelpBox("Решение не посчитано: звёзды по общим порогам, подсказка не работает.", MessageType.Warning);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = level != null;
            if (GUILayout.Button("Найти решение")) { var r = SolveAndStore(level); report = $"{level.name}: {r}"; }
            GUI.enabled = true;
            if (GUILayout.Button("Решить все уровни")) report = SolveAll();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Проверить все уровни")) report = CheckAllLevels();
            GUI.enabled = level != null;
            if (GUILayout.Button("Добавить в список уровней игры")) AddToGameList();
            GUI.enabled = true;
        }
        if (!string.IsNullOrEmpty(report))
            EditorGUILayout.HelpBox(report, MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // ───────────────────────────── геометрия ─────────────────────────────

    /// <summary>Верхний тайл каждой колонки (x, y) — как MapManager в игре.</summary>
    Dictionary<Vector2Int, Vector3Int> CollectColumns()
    {
        var top = new Dictionary<Vector2Int, Vector3Int>();
        if (tilemap == null) return top;
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos)) continue;
            var key = new Vector2Int(pos.x, pos.y);
            if (!top.TryGetValue(key, out var cur) || pos.z > cur.z) top[key] = pos;
        }
        return top;
    }

    Vector3 CharacterPosFor(Vector3Int cell) =>
        gameTilemapOffset + tilemap.GetCellCenterLocal(cell) + CharacterOnTileOffset;

    Vector3 MarkerWorldPos(Vector3Int cell) => tilemap.GetCellCenterWorld(cell) + new Vector3(0f, 0.12f, 0f);

    // ───────────────────────────── данные уровня ─────────────────────────────

    void ReadMarkersFromLevel()
    {
        supers.Clear();
        colors.Clear();
        start = null;
        unsaved = false;
        if (level == null || level.tiles == null) return;

        foreach (var info in level.tiles)
        {
            if (info != null && info.superTileType != SuperBlocks.None)
                supers[new Vector2Int(info.position.x, info.position.y)] = info.superTileType;
            if (info != null && info.color != TileColor.None)
                colors[new Vector2Int(info.position.x, info.position.y)] = info.color;
        }

        if (tilemap == null) return;
        // Старт: клетка уровня, для которой characterPos совпадает с сохранённым.
        float best = float.MaxValue;
        foreach (var info in level.tiles)
        {
            if (info == null) continue;
            float d = Vector3.Distance(CharacterPosFor(info.position), level.characterPos);
            if (d < best) { best = d; start = new Vector2Int(info.position.x, info.position.y); }
        }
        if (best > StartMatchDistance) start = null;
    }

    void LoadIntoTilemap()
    {
        if (level == null) return;
        if (unsaved && !EditorUtility.DisplayDialog("Загрузить уровень",
                "Несохранённые кристаллы/грибы/старт будут потеряны. Продолжить?", "Да", "Нет"))
            return;

        Undo.RegisterCompleteObjectUndo(tilemap, "Load level into Tilemap");
        tilemap.ClearAllTiles();
        if (level.tiles != null)
            foreach (var info in level.tiles)
                if (info != null) tilemap.SetTile(info.position, info.tile);

        ReadMarkersFromLevel();
        TintColumns(CollectColumns());
        if (!start.HasValue && level.tiles != null && level.tiles.Count > 0)
            Debug.LogWarning($"[Редактор уровней] {level.name}: старт героя (characterPos {level.characterPos}) не совпадает ни с одной клеткой. Проверь «Сдвиг Tilemap в игре» или поставь старт заново.");
        SceneView.RepaintAll();
    }

    void Save()
    {
        if (level == null) return;
        tilemap.CompressBounds();
        var columns = CollectColumns();

        if (columns.Count == 0 &&
            !EditorUtility.DisplayDialog("Пустой Tilemap", $"В Tilemap нет тайлов. Сохранить в {level.name} пустой уровень?", "Сохранить", "Отмена"))
            return;

        var problems = Validate(level, columns, supers, start);
        if (problems.Count > 0 &&
            !EditorUtility.DisplayDialog("Есть проблемы", string.Join("\n", problems) + "\n\nВсё равно сохранить?", "Сохранить", "Отмена"))
            return;

        var tiles = new List<TileInfo>();
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos)) continue;
            var key = new Vector2Int(pos.x, pos.y);
            var type = SuperBlocks.None;
            // Супер-тайл — на верхний блок колонки (как кладёт кристалл ActiveBlockManager).
            if (columns.TryGetValue(key, out var topCell) && topCell.z == pos.z && supers.TryGetValue(key, out var t)) type = t;
            var color = columns.TryGetValue(key, out var top2) && top2.z == pos.z && colors.TryGetValue(key, out var c) ? c : TileColor.None;
            tiles.Add(new TileInfo { position = pos, tile = tilemap.GetTile(pos), superTileType = type, color = color });
        }

        Undo.RecordObject(level, "Save level");
        level.tiles = tiles;
        if (start.HasValue && columns.TryGetValue(start.Value, out var startCell))
            level.characterPos = CharacterPosFor(startCell);
        if (level.panels == null || level.panels.Length == 0)
            level.panels = new[] { new Panels { panelName = "Main", commands = new Item[0] } };

        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();
        unsaved = false;
        Debug.Log($"[Редактор уровней] {level.name} сохранён: тайлов {tiles.Count}, кристаллов {tiles.Count(i => i.superTileType == SuperBlocks.Crystal)}, грибов {tiles.Count(i => i.superTileType == SuperBlocks.Mushroom)}.");

        // Уровень поменялся — пересчитываем оптимум и решение для подсказок.
        report = $"{level.name}: {SolveAndStore(level)}";
    }

    // ───────────────────────────── решатель ─────────────────────────────

    LevelSolver.Problem BuildProblem(LevelSt lvl)
    {
        var p = new LevelSolver.Problem();
        if (lvl.tiles == null) return p;

        var top = new Dictionary<Vector2Int, TileInfo>();
        foreach (var t in lvl.tiles)
        {
            if (t == null) continue;
            var key = new Vector2Int(t.position.x, t.position.y);
            if (!top.TryGetValue(key, out var cur) || t.position.z > cur.position.z) top[key] = t;
            if (t.superTileType == SuperBlocks.Mushroom) p.Mushrooms.Add((key.x, key.y));
            if (t.superTileType == SuperBlocks.Crystal) p.Crystals.Add((key.x, key.y));
            if (t.superTileType == SuperBlocks.Ice) p.Ice.Add((key.x, key.y));
            if (t.superTileType == SuperBlocks.Crumbling) p.Crumbling.Add((key.x, key.y));
        }
        foreach (var kv in top)
            if (kv.Value.color != TileColor.None) p.Colors[(kv.Key.x, kv.Key.y)] = kv.Value.color;
        foreach (var key in top.Keys) p.Tiles.Add((key.x, key.y));

        // Старт — клетка, ближайшая к characterPos (так же выбирает её CharacterCode в игре).
        float best = float.MaxValue;
        foreach (var t in top.Values)
        {
            float d = Vector3.Distance(CharacterPosFor(t.position), lvl.characterPos);
            if (d < best) { best = d; p.Start = (t.position.x, t.position.y); }
        }

        if (lvl.panels != null)
            foreach (var panel in lvl.panels)
            {
                if (panel == null || panel.commands == null) continue;
                var cards = panel.commands.Where(c => c != null).Select(c => c.type).Distinct().ToArray();
                string n = (panel.panelName ?? "").Trim().ToLowerInvariant();
                if (n == "main") p.MainCards = cards;
                else if (n == "functionone" || n == "f1") p.F1Cards = cards;
                else if (n == "functiontwo" || n == "f2") p.F2Cards = cards;
            }
        return p;
    }

    LevelSolver.Result SolveAndStore(LevelSt lvl)
    {
        if (lvl.HasEnemies)
        {
            // Решатель не знает о боях (враги, урон, щит) — у таких уровней решение задано вручную
            // и проверено ядром (Crystal Adventure → Уровни → Создать уровни с врагами). Не перезаписываем.
            var keep = new LevelSolver.Result { Solvable = lvl.HasSolution, Cards = lvl.optimalCards, ProvenOptimal = false,
                                                Message = "уровень с врагами — решение не пересчитывается" };
            Debug.Log($"[Решатель] {lvl.name}: уровень с врагами — оставляю решение ({lvl.optimalCards} карт.)");
            return keep;
        }
        var result = LevelSolver.Solve(BuildProblem(lvl));

        Undo.RecordObject(lvl, "Solve level");
        lvl.optimalCards = result.Solvable ? result.Cards : 0;
        lvl.solutionMain = ToSolution(result.Main);
        lvl.solutionF1 = ToSolution(result.F1);
        lvl.solutionF2 = ToSolution(result.F2);
        EditorUtility.SetDirty(lvl);
        AssetDatabase.SaveAssets();

        if (result.Solvable) Debug.Log($"[Решатель] {lvl.name}: {result}");
        else Debug.LogWarning($"[Решатель] {lvl.name}: {result.Message}");
        return result;
    }

    static List<SolutionCard> ToSolution(List<LevelSolver.Card> cards) =>
        cards.Select(c => new SolutionCard { type = c.Type, iterations = c.Iterations, span = c.Span, color = c.Color }).ToList();

    string SolveAll()
    {
        var sb = new StringBuilder();
        var guids = AssetDatabase.FindAssets("t:LevelSt");
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                var lvl = AssetDatabase.LoadAssetAtPath<LevelSt>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (lvl == null) continue;
                EditorUtility.DisplayProgressBar("Решатель", lvl.name, (float)i / guids.Length);
                var r = SolveAndStore(lvl);
                sb.AppendLine(r.Solvable ? $"✓ {lvl.name}: {r.Cards} карт. (путь {r.ShortestMoves} ходов){(r.ProvenOptimal ? "" : " — может быть короче")}"
                                         : $"✗ {lvl.name}: {r.Message}");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Repaint();
        return sb.ToString().TrimEnd();
    }

    static string DescribeSolution(LevelSt lvl)
    {
        string Line(List<SolutionCard> l) => string.Join(" ", l.Select(c => c.type == ItemType.While ? (c.span > 0 ? $"[While ×{c.iterations + 1} на {c.span}]" : $"[While ×{c.iterations + 1}]") : Short(c.type)));
        string s = "Main: " + Line(lvl.solutionMain);
        if (lvl.solutionF1 != null && lvl.solutionF1.Count > 0) s += "   F1: " + Line(lvl.solutionF1);
        if (lvl.solutionF2 != null && lvl.solutionF2.Count > 0) s += "   F2: " + Line(lvl.solutionF2);
        return s;
    }

    static string Short(ItemType t)
    {
        switch (t)
        {
            case ItemType.Forward: return "↑";
            case ItemType.Bottom: return "↓";
            case ItemType.Left: return "←";
            case ItemType.Right: return "→";
            case ItemType.FunctionOne: return "F1";
            case ItemType.FunctionTwo: return "F2";
            default: return t.ToString();
        }
    }

    void CreateLevel()
    {
        string path = EditorUtility.SaveFilePanelInProject("Новый уровень", "NewLevel", "asset",
            "Куда сохранить уровень", "Assets/Scenes/GameScenes");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<LevelSt>();
        asset.tiles = new List<TileInfo>();
        asset.panels = new[] { new Panels { panelName = "Main", commands = new Item[0] } };
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        level = asset;
        supers.Clear();
        start = null;
        unsaved = true; // текущий Tilemap ещё не записан в новый уровень
        Selection.activeObject = asset;
    }

    void AddToGameList()
    {
        var data = FindObjectOfType<TilemapData>();
        if (data == null)
        {
            EditorUtility.DisplayDialog("Список уровней", "В открытой сцене нет TilemapData. Открой CollectorLevel.", "Ок");
            return;
        }
        var list = (data.tilemapData ?? new LevelSt[0]).ToList();
        int index = list.IndexOf(level);
        if (index >= 0)
        {
            EditorUtility.DisplayDialog("Список уровней", $"{level.name} уже в списке под номером {index}.", "Ок");
            return;
        }
        Undo.RecordObject(data, "Add level to game list");
        list.Add(level);
        data.tilemapData = list.ToArray();
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(data.gameObject.scene);
        Debug.Log($"[Редактор уровней] {level.name} добавлен в TilemapData под номером {list.Count - 1}. Сохрани сцену.");
    }

    // ───────────────────────────── проверки ─────────────────────────────

    static List<string> Validate(LevelSt lvl, Dictionary<Vector2Int, Vector3Int> columns,
        Dictionary<Vector2Int, SuperBlocks> marks, Vector2Int? startCell)
    {
        var problems = new List<string>();
        if (lvl == null) return problems;

        if (!marks.Any(kv => kv.Value == SuperBlocks.Crystal))
            problems.Add("Нет ни одного кристалла — уровень нельзя пройти.");
        foreach (var kv in marks)
            if (!columns.ContainsKey(kv.Key))
                problems.Add($"{Name(kv.Value)} в ({kv.Key.x}, {kv.Key.y}) стоит на пустой клетке — при сохранении пропадёт.");

        if (!startCell.HasValue)
            problems.Add("Старт героя не задан (инструмент «Старт»). При сохранении останется старый characterPos.");
        else
        {
            if (!columns.ContainsKey(startCell.Value)) problems.Add("Старт героя стоит на пустой клетке.");
            if (marks.TryGetValue(startCell.Value, out var onStart)) problems.Add($"На стартовой клетке стоит {Name(onStart).ToLower()}.");
        }

        if (lvl.panels == null || lvl.panels.Length == 0)
            problems.Add("Нет панелей с командами (panels) — игроку нечего будет ставить.");
        else if (lvl.panels.All(p => p == null || p.commands == null || p.commands.Length == 0))
            problems.Add("В панелях нет ни одной команды.");
        else if (lvl.panels.Any(p => p != null && p.commands != null && p.commands.Any(c => c == null)))
            problems.Add("В панелях есть пустые команды (None).");

        return problems;
    }

    static string Name(SuperBlocks b) => b == SuperBlocks.Crystal ? "Кристалл" : b == SuperBlocks.Mushroom ? "Гриб"
        : b == SuperBlocks.Ice ? "Лёд" : b == SuperBlocks.Crumbling ? "Плита" : "Блок";

    static string Letter(SuperBlocks b) => b == SuperBlocks.Crystal ? "К" : b == SuperBlocks.Mushroom ? "Г"
        : b == SuperBlocks.Ice ? "Л" : b == SuperBlocks.Crumbling ? "П" : "?";

    static Color MarkerColor(SuperBlocks b) => b == SuperBlocks.Crystal ? new Color(0.95f, 0.4f, 1f, 0.8f)
        : b == SuperBlocks.Ice ? new Color(0.55f, 0.9f, 1f, 0.9f)
        : b == SuperBlocks.Crumbling ? new Color(0.75f, 0.55f, 0.35f, 0.9f)
        : new Color(1f, 0.6f, 0.2f, 0.8f);

    string CheckAllLevels()
    {
        var game = FindObjectOfType<TilemapData>();
        var inGame = game != null && game.tilemapData != null ? game.tilemapData.ToList() : null;

        var sb = new StringBuilder();
        int bad = 0;
        var signatures = new Dictionary<string, string>();
        var guids = AssetDatabase.FindAssets("t:LevelSt");
        foreach (var guid in guids)
        {
            var lvl = AssetDatabase.LoadAssetAtPath<LevelSt>(AssetDatabase.GUIDToAssetPath(guid));
            if (lvl == null) continue;
            var issues = new List<string>();
            var tiles = lvl.tiles ?? new List<TileInfo>();

            if (tiles.Count == 0) issues.Add("нет тайлов");
            if (tiles.Any(t => t == null || t.tile == null)) issues.Add("есть тайлы без TileBase");
            if (tiles.Where(t => t != null).GroupBy(t => t.position).Any(g => g.Count() > 1)) issues.Add("повторяются позиции");
            int crystals = tiles.Count(t => t != null && t.superTileType == SuperBlocks.Crystal);
            if (crystals == 0) issues.Add("нет кристаллов");
            if (tiles.Count > 0 && !tiles.Any(t => t != null && Vector3.Distance(CharacterPosFor(t.position), lvl.characterPos) <= StartMatchDistance))
                issues.Add($"старт {lvl.characterPos} не на клетке");
            if (lvl.panels == null || lvl.panels.Length == 0 || lvl.panels.All(p => p == null || p.commands == null || p.commands.Length == 0))
                issues.Add("нет команд в панелях");
            if (inGame != null && !inGame.Contains(lvl)) issues.Add("не в списке уровней игры");
            if (!lvl.HasSolution) issues.Add("решение не посчитано («Решить все уровни»)");

            // Одинаковые уровни (например, скопировали ассет и забыли поменять).
            string sig = string.Join(";", tiles.Where(t => t != null).OrderBy(t => t.position.x).ThenBy(t => t.position.y).ThenBy(t => t.position.z)
                .Select(t => $"{t.position}{(t.tile != null ? t.tile.name : "-")}{(int)t.superTileType}")) + lvl.characterPos;
            if (tiles.Count > 0 && signatures.TryGetValue(sig, out var twin)) issues.Add($"точная копия {twin}");
            else signatures[sig] = lvl.name;

            if (issues.Count == 0) sb.AppendLine($"✓ {lvl.name}: тайлов {tiles.Count}, кристаллов {crystals}");
            else { bad++; sb.AppendLine($"✗ {lvl.name}: {string.Join(", ", issues)}"); }
        }

        if (inGame != null)
            for (int i = 0; i < inGame.Count; i++)
                if (inGame[i] == null) { bad++; sb.AppendLine($"✗ Список уровней игры: слот {i} пустой"); }
        if (inGame == null) sb.AppendLine("(TilemapData в сцене нет — порядок уровней игры не проверен)");

        sb.Insert(0, $"Уровней: {guids.Length}, с проблемами: {bad}\n");
        Debug.Log("[Редактор уровней] " + sb);
        return sb.ToString().TrimEnd();
    }

    // ───────────────────────────── Scene view ─────────────────────────────

    void OnSceneGUI(SceneView view)
    {
        if (tilemap == null) return;
        var columns = CollectColumns();
        DrawMarkers(columns);

        if (tool == Tool.None) return;
        var e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        var hover = PickColumn(columns, HandleUtility.GUIPointToWorldRay(e.mousePosition).origin);
        if (hover.HasValue)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.6f);
            Handles.DrawWireDisc(MarkerWorldPos(columns[hover.Value]), Vector3.forward, 0.1f);
            if (e.type == EventType.MouseMove) view.Repaint();
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hover.HasValue)
        {
            Apply(hover.Value);
            e.Use();
            Repaint();
            view.Repaint();
        }
    }

    Vector2Int? PickColumn(Dictionary<Vector2Int, Vector3Int> columns, Vector3 world)
    {
        float maxDist = Mathf.Max(0.05f, tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize.x * 0.5f : 0.16f);
        Vector2Int? best = null;
        float bestD = maxDist;
        foreach (var kv in columns)
        {
            var p = MarkerWorldPos(kv.Value);
            float d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(world.x, world.y));
            // При равенстве выигрывает более высокий блок — он визуально сверху.
            if (d < bestD || (best.HasValue && Mathf.Approximately(d, bestD) && kv.Value.z > columns[best.Value].z))
            {
                bestD = d;
                best = kv.Key;
            }
        }
        return best;
    }

    void Apply(Vector2Int column)
    {
        switch (tool)
        {
            case Tool.Crystal: supers[column] = SuperBlocks.Crystal; break;
            case Tool.Mushroom: supers[column] = SuperBlocks.Mushroom; break;
            case Tool.Ice: supers[column] = SuperBlocks.Ice; break;
            case Tool.Crumbling: supers[column] = SuperBlocks.Crumbling; break;
            case Tool.Start: start = column; break;
            case Tool.Erase:
                supers.Remove(column);
                colors.Remove(column);
                if (start == column) start = null;
                break;
            case Tool.Paint:
                if (paintColor == TileColor.None) colors.Remove(column);
                else colors[column] = paintColor;
                break;
        }
        unsaved = true;
        TintColumns(CollectColumns());
    }

    /// <summary>Показать цвета клеток прямо в Tilemap (как в игре).</summary>
    void TintColumns(Dictionary<Vector2Int, Vector3Int> columns)
    {
        if (tilemap == null) return;
        foreach (var kv in columns)
        {
            tilemap.SetTileFlags(kv.Value, TileFlags.None);
            tilemap.SetColor(kv.Value, colors.TryGetValue(kv.Key, out var c) ? ColorTiles.Tint(c) : Color.white);
        }
    }

    void DrawMarkers(Dictionary<Vector2Int, Vector3Int> columns)
    {
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        foreach (var kv in supers)
        {
            if (!columns.TryGetValue(kv.Key, out var cell)) continue;
            var p = MarkerWorldPos(cell);
            Handles.color = MarkerColor(kv.Value);
            Handles.DrawSolidDisc(p, Vector3.forward, 0.06f);
            style.normal.textColor = Handles.color;
            Handles.Label(p + new Vector3(0f, 0.12f, 0f), Letter(kv.Value), style);
        }
        foreach (var kv in colors)
        {
            if (!columns.TryGetValue(kv.Key, out var cell)) continue;
            var p = MarkerWorldPos(cell) + new Vector3(0.07f, -0.03f, 0f);
            Handles.color = ColorTiles.Ui(kv.Value);
            Handles.DrawSolidDisc(p, Vector3.forward, 0.035f);
        }
        if (start.HasValue && columns.TryGetValue(start.Value, out var s))
        {
            var p = MarkerWorldPos(s);
            Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Handles.DrawWireDisc(p, Vector3.forward, 0.08f);
            style.normal.textColor = Handles.color;
            Handles.Label(p + new Vector3(0f, 0.12f, 0f), "Старт", style);
        }
    }
}
