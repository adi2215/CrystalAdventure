using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.UnityLayer;
using UnityEngine;

/// <summary>
/// Враги на поле старой системы (уровни, где LevelSt.enemies не пуст).
///
/// Решает всё ядро CrystalCore — здесь только показ того же прогона (CoreRunBridge.Last):
///   • Setup — при загрузке уровня и после Rewind: враги на стартовых клетках и красные клетки
///     «сюда враг шагнёт / ударит в следующий ход» (видны ещё до Play);
///   • BeginTurn(i) — в начале шага куба: враги шагают одновременно с ним;
///   • FinishTurn(i) — после шага куба: выстрелы пушек, новые красные клетки.
/// Касание врага — смерть героя (решает ядро, Entity.Deadly); куб при этом телепортируется на старт
/// (CharacterCode → MovingManager.OnHeroDefeated). Победу на таких уровнях объявляет ядро:
/// ActiveBlockManager.victoryByCore + ForceVictory из CharacterCode.
///
/// Вид: синий враг и боссы — копия куба героя (все его спрайты, как у призрака-подсказки), перекрашенная;
/// пушка — пиксель-арт World/EnemyArt. Порядок отрисовки — World/SortDepth.
/// </summary>
public static class EnemyLayer
{
    sealed class View
    {
        public int Id;
        public string Archetype;
        public GameObject Go;
        public SpriteRenderer[] Parts;
        public int[] Offsets;
        public SpriteRenderer Main;
        public Sprite Front, Back;
        public Vector2Int Cell;
        /// <summary>Ледяной ромб висит над клеткой и покачивается — его не двигают шаги и волна.</summary>
        public bool Hovers;
    }

    static readonly Dictionary<int, View> Views = new Dictionary<int, View>();
    static readonly List<GameObject> Markers = new List<GameObject>();
    static readonly List<Coroutine> Running = new List<Coroutine>();
    static LevelSt _level;
    static int _busy;

    const int HeroId = 1; // ядро всегда создаёт героя первым (LevelSetup.Build)

    // Цвета перекраски копии куба. Меняй здесь.
    static readonly Color WalkerTint = new Color(0.45f, 0.62f, 1f, 1f);
    static readonly Color BossForestTint = new Color(0.55f, 1f, 0.45f, 1f);
    static readonly Color BossWinterTint = new Color(0.8f, 0.55f, 1f, 1f);
    static readonly Color SnakeBodyTint = new Color(0.36f, 0.78f, 0.36f, 1f);
    const float SnakeHeadScale = 1.15f, SnakeBodyScale = 0.85f;
    const float BossScale = 1.35f;
    const float DiamondScale = 1.5f;
    const float DiamondHover = 0.06f;

    /// <summary>На уровне есть враги — им управляет этот слой.</summary>
    public static bool Active { get; private set; }

    static MonoBehaviour Host => ActiveBlockManager.Instance;
    static CharacterCode Hero => ActiveBlockManager.Instance != null && ActiveBlockManager.Instance.heroPlayer != null
        ? ActiveBlockManager.Instance.heroPlayer : Object.FindObjectOfType<CharacterCode>();

    // ================================================================ уровень

    /// <summary>Загрузка уровня (MapManager, когда остров собран).</summary>
    public static void Setup(LevelSt level)
    {
        _level = level;
        Rebuild();
    }

    /// <summary>Rewind и смерть героя: враги — на старт.</summary>
    public static void ResetAll()
    {
        if (_level != null) Rebuild();
    }

    static void Rebuild()
    {
        StopAll();
        Clear();
        Active = _level != null && _level.HasEnemies;
        if (ActiveBlockManager.Instance != null) ActiveBlockManager.Instance.victoryByCore = Active;
        if (Hero != null) Hero.RefreshSortOrder(); // глубина куба — по клетке (SortDepth) только на уровнях с врагами
        if (!Active) return;

        var mm = MapManager.Instance;
        if (!LegacyLevelImport.TryFindStartCell(_level, mm != null ? mm.tilemap : null, out var start, out _)) return;
        World world;
        try { world = LegacyLevelImport.ToSetup(_level, start).Build(); }
        catch (System.Exception e) { Debug.LogWarning("[Crystal] Враги: не удалось собрать уровень — " + e.Message); Active = false; return; }

        foreach (var e in world.Entities.Where(x => x.Alive && x.IsHostile))
            Spawn(e.Id, e.Archetype, new Vector2Int(e.Pos.X, e.Pos.Y), e.Facing);

        ShowIntents(TurnResolver.PlanOnly(world));
    }

    static void Clear()
    {
        foreach (var v in Views.Values) if (v.Go != null) Object.Destroy(v.Go);
        Views.Clear();
        foreach (var sp in SpikeViews.Values) if (sp != null) Object.Destroy(sp);
        SpikeViews.Clear();
        ClearMarkers();
    }

    static void StopAll()
    {
        if (Host != null) foreach (var c in Running) if (c != null) Host.StopCoroutine(c);
        Running.Clear();
        _busy = 0;
        BoardWave.ResetAll(); // волну могли прервать на середине
    }

    // ================================================================ ход

    static TurnRecord Turn(int i)
    {
        var r = CoreRunBridge.Last;
        return r != null && i >= 0 && i < r.Turns.Count ? r.Turns[i] : null;
    }

    /// <summary>Начало шага куба: враги шагают одновременно с ним.</summary>
    public static void BeginTurn(int i)
    {
        if (!Active || Host == null) return;
        var turn = Turn(i);
        if (turn == null) return;
        ClearMarkers(); // намерения этого хода исполняются — подсветку снимаем

        foreach (var e in turn.Events.Where(e => e.Type == GameEventType.Spawned))
            Spawn(e.EntityId, e.Info, new Vector2Int(e.Cell.X, e.Cell.Y), e.Dir);

        // Перемещения одного врага — по очереди (шаг, потом скольжение по льду), разных — одновременно.
        foreach (var group in turn.Events.Where(e => e.EntityId != HeroId && IsMove(e.Type)).GroupBy(e => e.EntityId))
            if (Views.TryGetValue(group.Key, out var v)) Track(MoveChain(v, group.ToList()));
    }

    /// <summary>Конец шага куба: выстрелы пушек, исчезнувшие враги, новые красные клетки.</summary>
    public static IEnumerator FinishTurn(int i)
    {
        if (!Active) yield break;
        var turn = Turn(i);
        if (turn == null) yield break;
        while (_busy > 0) yield return null;

        foreach (var e in turn.Events)
        {
            if (e.EntityId == HeroId && e.Type != GameEventType.TileChanged) continue;
            if (e.Type == GameEventType.Attacked && e.Style != null && Views.TryGetValue(e.EntityId, out var boss))
            {
                // Молния / супер-лёд ледяного босса: удар, частицы, волна по полю, замёрзшие клетки.
                if (e.Style == SnakeBrain.Spikes || e.Style == SnakeBrain.Quake)
                {
                    // Лесная змея: шипы перед головой / удар хвостом — клетки берём из событий этого хода.
                    string info = e.Style == SnakeBrain.Spikes ? "spikes_up" : "collapsed";
                    var cells = turn.Events.Where(f => f.Type == GameEventType.TileChanged && f.Info == info && f.EntityId == e.EntityId)
                                           .Select(f => new Vector2Int(f.Cell.X, f.Cell.Y)).ToList();
                    Track(SnakeStrike(boss, e, cells));
                }
                else
                {
                    var frozen = turn.Events.Where(f => f.Type == GameEventType.Frozen && f.EntityId == e.EntityId)
                                            .Select(f => new Vector2Int(f.Cell.X, f.Cell.Y)).ToList();
                    Track(Strike(boss, e, frozen));
                }
            }
            else if (e.Type == GameEventType.Attacked && Views.TryGetValue(e.EntityId, out var a))
            {
                Face(a, e.Dir);
                Track(Recoil(a));
                FlashArea(e.Area);
                PixelSfx.Play(Sfx.Remove, 0.6f, 1.1f);
            }
            else if (e.Type == GameEventType.TileChanged && e.Info == "spikes_down")
                Track(LowerSpikes(new Vector2Int(e.Cell.X, e.Cell.Y)));
            else if (e.Type == GameEventType.TileChanged && e.Info == "crumbled")
            {
                // Плита рассыпалась под врагом (под героем её роняет CharacterCode; повторный вызов безопасен).
                var tile = TileAt(new Vector2Int(e.Cell.X, e.Cell.Y));
                if (tile != null) SpecialTiles.Crumble(tile);
            }
            else if ((e.Type == GameEventType.Died || e.Type == GameEventType.Fell) && Views.TryGetValue(e.EntityId, out var dead))
            {
                Views.Remove(e.EntityId);
                Track(Vanish(dead));
            }
        }
        while (_busy > 0) yield return null;

        ShowIntents(turn.Events.Where(e => e.Type == GameEventType.IntentPlanned));
    }

    static bool IsMove(GameEventType t) =>
        t == GameEventType.Moved || t == GameEventType.Slid || t == GameEventType.Pushed ||
        t == GameEventType.Bounced || t == GameEventType.Teleported;

    // ================================================================ вид

    static Transform Root
    {
        get
        {
            var hero = Hero;
            if (hero != null && hero.transform.parent != null) return hero.transform.parent;
            return ActiveBlockManager.Instance != null ? ActiveBlockManager.Instance.transform : null;
        }
    }

    static OverlayTile TileAt(Vector2Int c)
    {
        var mm = MapManager.Instance;
        return mm != null && mm.map != null && mm.map.TryGetValue(new Vector3Int(c.x, c.y, 0), out var t) ? t : null;
    }

    static Vector3 WorldOf(Vector2Int c)
    {
        var t = TileAt(c);
        if (t != null) return CharacterCode.TopOf(t);
        var mm = MapManager.Instance;
        return mm != null && mm.tilemap != null ? mm.tilemap.GetCellCenterWorld(new Vector3Int(c.x, c.y, 0)) + new Vector3(0f, 0.12f, 1f) : Vector3.zero;
    }

    static int DepthAt(Vector2Int c)
    {
        var t = TileAt(c);
        return t != null ? SortDepth.Character(t) : SortDepth.Character(0, c.x, c.y);
    }

    static bool IsCube(string archetype) => archetype == "Walker" || archetype == "BossForest" || archetype == EnemyBrains.SnakeBodyArchetype;

    /// <summary>Ледяной босс арены 8×8 (StormBossBrain) — ромб, а не копия куба.</summary>
    static bool IsDiamond(string archetype) => archetype == "BossWinter";

    static void Spawn(int id, string archetype, Vector2Int cell, Dir facing)
    {
        var root = Root;
        var hero = Hero;
        if (root == null) return;
        var go = new GameObject($"Enemy_{archetype}_{id}");
        go.transform.SetParent(root, false);
        go.transform.position = WorldOf(cell);
        var v = new View { Id = id, Archetype = archetype, Go = go, Cell = cell };

        if (IsCube(archetype) && hero != null && hero.MainRenderer != null)
        {
            // Копия куба героя: все его спрайты (тело, глаза…), кроме тени — как у призрака-подсказки.
            // Масштаб — по z: x и y у куба «дышат» (HeroJuice), z — нет.
            float scale = archetype == "Walker" ? 1f
                        : archetype == EnemyBrains.SnakeBodyArchetype ? SnakeBodyScale
                        : archetype == "BossForest" && HasSnakeBody() ? SnakeHeadScale : BossScale;
            go.transform.localScale = Vector3.one * Mathf.Abs(hero.transform.localScale.z) * scale;
            var src = hero.GetComponentsInChildren<SpriteRenderer>(true);
            var tint = archetype == "BossForest" ? BossForestTint : archetype == EnemyBrains.SnakeBodyArchetype ? SnakeBodyTint : WalkerTint;
            v.Parts = new SpriteRenderer[src.Length];
            v.Offsets = new int[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var part = new GameObject("Part").AddComponent<SpriteRenderer>();
                part.transform.SetParent(go.transform, false);
                part.transform.localPosition = hero.transform.InverseTransformPoint(src[i].transform.position);
                part.transform.localRotation = Quaternion.identity;
                part.transform.localScale = src[i].transform == hero.transform ? Vector3.one : src[i].transform.localScale;
                part.sprite = src[i] == hero.MainRenderer ? hero.FrontSprite : src[i].sprite;
                part.sharedMaterial = src[i].sharedMaterial;
                part.sortingLayerID = src[i].sortingLayerID;
                part.color = tint;
                v.Offsets[i] = src[i].sortingOrder - hero.MainRenderer.sortingOrder;
                v.Parts[i] = part;
                if (src[i] == hero.MainRenderer) v.Main = part;
            }
            v.Front = hero.FrontSprite;
            v.Back = hero.backSprite;
        }
        else
        {
            // Пушка, ледяной ромб и прочие — пиксель-арт.
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = IsDiamond(archetype) ? EnemyArt.FrostDiamond : EnemyArt.For(archetype);
            if (IsDiamond(archetype) && hero != null)
            {
                go.transform.localScale = Vector3.one * Mathf.Abs(hero.transform.localScale.z) * DiamondScale;
                go.transform.position = WorldOf(cell) + Vector3.up * DiamondHover;
                v.Hovers = true;
            }
            if (hero != null && hero.MainRenderer != null) { sr.sortingLayerID = hero.MainRenderer.sortingLayerID; sr.sharedMaterial = hero.MainRenderer.sharedMaterial; }
            v.Parts = new[] { sr };
            v.Offsets = new[] { 0 };
            v.Main = sr;
        }

        Views[id] = v;
        SetOrder(v, DepthAt(cell));
        Face(v, facing);
        Track(PopIn(v));
        if (v.Hovers && Host != null) Running.Add(Host.StartCoroutine(Hover(v)));
    }

    static void SetOrder(View v, int order)
    {
        for (int i = 0; i < v.Parts.Length; i++)
            if (v.Parts[i] != null) v.Parts[i].sortingOrder = order + v.Offsets[i];
    }

    static ItemType Arrow(Dir d) =>
        d == Dir.North ? ItemType.Forward : d == Dir.East ? ItemType.Right : d == Dir.South ? ItemType.Bottom : ItemType.Left;

    static void Face(View v, Dir d)
    {
        if (v.Front != null) CharacterCode.ApplyFacing(v.Parts, v.Main, v.Front, v.Back, Arrow(d)); // как куб
        else if (v.Hovers) return;                                                              // ромб всегда смотрит на нас
        else if (v.Main != null) v.Main.flipX = d == Dir.West || d == Dir.North;               // пушка
    }

    // ───────────── красные клетки ─────────────

    static void ShowIntents(IEnumerable<GameEvent> planned)
    {
        ClearMarkers();
        var root = Root;
        if (root == null) return;
        foreach (var e in planned)
        {
            if (e.Type != GameEventType.IntentPlanned) continue;
            if (e.Area != null)
            {
                foreach (var c in e.Area) AddMarker(root, new Vector2Int(c.X, c.Y));         // выстрел пушки / удар босса
                if (e.Style != null) AddReticle(root, new Vector2Int(e.To.X, e.To.Y), e.Style, e.EntityId);
            }
            else if (e.Info == IntentKind.Move.ToString() && Views.TryGetValue(e.EntityId, out var v) && v.Archetype != EnemyBrains.SnakeBodyArchetype)
            {
                var o = e.Dir.Offset();
                AddMarker(root, new Vector2Int(v.Cell.x + o.X, v.Cell.y + o.Y));             // следующий шаг врага
            }
        }
    }

    static void AddMarker(Transform root, Vector2Int cell)
    {
        var tile = TileAt(cell);
        if (tile == null) return;
        var m = new GameObject("Danger").AddComponent<SpriteRenderer>();
        m.transform.SetParent(root, false);
        m.transform.position = WorldOf(cell) + new Vector3(0f, 0.04f, 0f);
        m.sprite = EnemyArt.Danger;
        var heroSr = Hero != null ? Hero.MainRenderer : null;
        if (heroSr != null) m.sortingLayerID = heroSr.sortingLayerID;
        m.sortingOrder = SortDepth.Marker(tile); // всегда под персонажами (SortDepth)
        Markers.Add(m.gameObject);
    }

    static void ClearMarkers()
    {
        foreach (var m in Markers) if (m != null) Object.Destroy(m);
        Markers.Clear();
    }

    static void FlashArea(Cell[] area)
    {
        var sr = Hero != null ? Hero.MainRenderer : null;
        if (area == null || sr == null) return;
        foreach (var c in area)
        {
            var cell = new Vector2Int(c.X, c.Y);
            if (TileAt(cell) == null) continue;
            PixelFX.Burst(WorldOf(cell) + Vector3.up * 0.04f, new[] { new Color(1f, 0.35f, 0.25f, 1f), new Color(1f, 0.8f, 0.4f, 1f) },
                          4, 0.25f, 0.08f, 0.8f, sr.sortingLayerID, DepthAt(cell) + 3, PixelFX.Dot, 0.8f, 0.35f);
        }
    }

    // ───────────── ледяной босс: прицел и удар ─────────────

    /// <summary>
    /// Прицел босса на клетке удара: мигающие уголки и значок над ними (молния / снежинка),
    /// а сам ромб «заряжается» — вспыхивает. Видно ещё до хода, в который он ударит.
    /// </summary>
    static void AddReticle(Transform root, Vector2Int cell, string style, int bossId)
    {
        var heroSr = Hero != null ? Hero.MainRenderer : null;
        int layer = heroSr != null ? heroSr.sortingLayerID : 0;
        var tile = TileAt(cell);
        Vector3 at = WorldOf(cell); // клетка может быть за краем поля — прицел всё равно рисуем
        int order = tile != null ? SortDepth.Marker(tile) + 1 : SortDepth.Character(0, cell.x, cell.y);

        var ret = new GameObject("Reticle").AddComponent<SpriteRenderer>();
        ret.transform.SetParent(root, false);
        ret.transform.position = at + new Vector3(0f, 0.04f, 0f);
        ret.sprite = EnemyArt.Reticle;
        ret.sortingLayerID = layer;
        ret.sortingOrder = order;
        Markers.Add(ret.gameObject);

        var icon = new GameObject("StrikeIcon").AddComponent<SpriteRenderer>();
        icon.transform.SetParent(root, false);
        icon.sprite = style == StormBossBrain.Ice ? EnemyArt.IconFlake
                    : style == SnakeBrain.Spikes ? EnemyArt.IconSpikes
                    : style == SnakeBrain.Quake ? EnemyArt.IconCrack : EnemyArt.IconBolt;
        icon.sortingLayerID = layer;
        icon.sortingOrder = (tile != null ? SortDepth.Character(tile) : SortDepth.Character(0, cell.x, cell.y)) + 400; // над персонажами — значок «что будет»
        icon.transform.localScale = Vector3.one * 1.5f;
        Markers.Add(icon.gameObject);

        if (Host != null) Running.Add(Host.StartCoroutine(ReticlePulse(ret, icon, at + new Vector3(0f, 0.3f, 0f))));
        if (Host != null && Views.TryGetValue(bossId, out var boss) && boss.Main != null) Running.Add(Host.StartCoroutine(Charge(boss, style)));
    }

    static IEnumerator ReticlePulse(SpriteRenderer ret, SpriteRenderer icon, Vector3 iconHome)
    {
        for (float t = 0f; ret != null && icon != null; t += Time.deltaTime)
        {
            ret.color = new Color(1f, 1f, 1f, Mathf.Sin(t * 10f) > 0f ? 1f : 0.45f);
            icon.transform.position = iconHome + new Vector3(0f, Mathf.Round(Mathf.Sin(t * 5f) * 2f) / 100f, 0f);
            yield return null;
        }
    }

    static IEnumerator Charge(View boss, string style)
    {
        var glow = style == StormBossBrain.Ice ? new Color(0.75f, 0.95f, 1f, 1f) : new Color(1f, 0.95f, 0.6f, 1f);
        if (boss.Main != null)
            PixelFX.Burst(boss.Go.transform.position + Vector3.up * 0.2f, new[] { glow, Color.white }, 6, 0.3f, 0.25f, 0.2f,
                          boss.Main.sortingLayerID, boss.Main.sortingOrder + 1, PixelFX.Dot, 0.8f, 0.4f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            if (boss.Main == null) yield break;
            boss.Main.color = Color.Lerp(glow, Color.white, t);
            yield return null;
        }
        if (boss.Main != null) boss.Main.color = Color.white;
    }

    static readonly Color[] BoltColors = { Color.white, new Color(1f, 0.95f, 0.55f, 1f), new Color(0.6f, 0.85f, 1f, 1f) };
    static readonly Color[] IceColors = { Color.white, new Color(0.8f, 0.95f, 1f, 1f), new Color(0.5f, 0.8f, 1f, 1f) };

    /// <summary>
    /// Удар ледяного босса: молния с неба (мерцает) или сосулька падает в клетку — вспышка частиц,
    /// клетки области замерзают (супер-лёд), по полю расходится волна (World/BoardWave).
    /// </summary>
    static IEnumerator Strike(View boss, GameEvent e, List<Vector2Int> frozen)
    {
        var heroSr = Hero != null ? Hero.MainRenderer : null;
        int layer = heroSr != null ? heroSr.sortingLayerID : 0;
        var target = new Vector2Int(e.To.X, e.To.Y);
        Vector3 at = WorldOf(target);
        bool ice = e.Style == StormBossBrain.Ice;
        const int SkyOrder = 1000; // молния и сосулька — поверх всего поля

        var fx = new GameObject(ice ? "IceShard" : "Lightning").AddComponent<SpriteRenderer>();
        fx.transform.SetParent(Root, false);
        fx.sortingLayerID = layer;
        fx.sortingOrder = SkyOrder;
        if (ice)
        {
            // Сосулька падает сверху в центр клетки.
            fx.sprite = EnemyArt.IceShard;
            fx.transform.localScale = Vector3.one * 2f;
            PixelSfx.Play(Sfx.Whoosh, 0.6f, 1.4f);
            Vector3 from = at + Vector3.up * 1.4f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                fx.transform.position = Snap(Vector3.Lerp(from, at, t * t));
                yield return null;
            }
            Object.Destroy(fx.gameObject);
            PixelSfx.Play(Sfx.Land, 1f, 0.6f);
        }
        else
        {
            // Молния: три кадра разного излома.
            PixelSfx.Play(Sfx.Edge, 1f, 0.5f);
            fx.transform.position = at;
            fx.transform.localScale = Vector3.one * 1.5f;
            for (int i = 0; i < 4; i++)
            {
                fx.sprite = EnemyArt.Bolt(i % 3);
                fx.color = i % 2 == 0 ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                yield return new WaitForSeconds(0.06f);
            }
            Object.Destroy(fx.gameObject);
        }

        // Вспышка частиц по всей области удара, в центре — сильнее.
        var colors = ice ? IceColors : BoltColors;
        PixelFX.Burst(at + Vector3.up * 0.05f, colors, 16, 0.9f, 0.5f, 1.4f, layer, SkyOrder - 1, PixelFX.Dot, 1.2f, 0.6f);
        foreach (var c in e.Area ?? new Cell[0])
        {
            var cell = new Vector2Int(c.X, c.Y);
            if (cell == target || TileAt(cell) == null) continue;
            PixelFX.Burst(WorldOf(cell) + Vector3.up * 0.04f, colors, 4, 0.35f, 0.2f, 1f, layer, DepthAt(cell) + 3, PixelFX.Dot, 0.8f, 0.4f);
        }
        foreach (var cell in frozen) SpecialTiles.Freeze(cell);

        // Волна по полю: герой и враги на клетках качаются вместе с ними.
        yield return BoardWave.Play(target, ice ? 0.05f : 0.07f, WaveRiders);
    }

    // ───────────── лесная змея: шипы и удар хвостом ─────────────

    static readonly Dictionary<Vector2Int, GameObject> SpikeViews = new Dictionary<Vector2Int, GameObject>();

    static bool HasSnakeBody()
    {
        if (_level == null || _level.enemies == null) return false;
        foreach (var e in _level.enemies) if (e != null && e.body > 0) return true;
        return false;
    }

    /// <summary>
    /// Удар змеи: кто бьёт (голова — шипы, хвост — провал) подпрыгивает и бьёт о землю; шипы
    /// вырастают из клеток / клетки проваливаются; по полю — волна от места удара.
    /// </summary>
    static IEnumerator SnakeStrike(View head, GameEvent e, List<Vector2Int> cells)
    {
        bool spikes = e.Style == SnakeBrain.Spikes;
        var origin = new Vector2Int(e.To.X, e.To.Y);
        // Хвостом бьёт последний сегмент — он стоит в клетке-цели удара.
        var striker = spikes ? head : Views.Values.FirstOrDefault(v => v.Cell == origin) ?? head;
        yield return SlamHop(striker);
        PixelSfx.Play(spikes ? Sfx.Land : Sfx.Remove, 1f, spikes ? 0.6f : 0.45f);
        var heroSr = Hero != null ? Hero.MainRenderer : null;
        int layer = heroSr != null ? heroSr.sortingLayerID : 0;
        foreach (var cell in cells)
        {
            var tile = TileAt(cell);
            if (tile == null) continue;
            PixelFX.Burst(WorldOf(cell) + Vector3.up * 0.04f, PixelFX.DustColors, 6, 0.4f, 0.2f, 1.2f, layer, DepthAt(cell) + 3, PixelFX.Dot, 1f, 0.5f);
            if (spikes) Track(RaiseSpikes(cell, tile, layer));
            else SpecialTiles.Collapse(tile);
        }
        yield return BoardWave.Play(spikes ? origin : striker.Cell, spikes ? 0.04f : 0.06f, WaveRiders);
    }

    static IEnumerator SlamHop(View v)
    {
        if (v == null || v.Go == null) yield break;
        var home = v.Go.transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.28f)
        {
            if (v.Go == null) yield break;
            float h = t < 0.6f ? Mathf.Sin(t / 0.6f * Mathf.PI * 0.5f) * 0.09f : (1f - (t - 0.6f) / 0.4f) * 0.09f;
            v.Go.transform.position = Snap(home + Vector3.up * h);
            yield return null;
        }
        if (v.Go != null) v.Go.transform.position = home;
    }

    static IEnumerator RaiseSpikes(Vector2Int cell, OverlayTile tile, int layer)
    {
        if (SpikeViews.TryGetValue(cell, out var old) && old != null) Object.Destroy(old);
        var sr = new GameObject("Spikes").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(Root, false);
        sr.sprite = EnemyArt.Spikes;
        sr.sortingLayerID = layer;
        sr.sortingOrder = SortDepth.Marker(tile) + 1; // над клеткой, под персонажами
        SpikeViews[cell] = sr.gameObject;
        var top = WorldOf(cell) - new Vector3(0f, 0.03f, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
        {
            if (sr == null) yield break;
            sr.transform.position = Snap(top + Vector3.down * 0.12f * (1f - t));
            sr.transform.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, t), 1f);
            yield return null;
        }
        if (sr != null) { sr.transform.position = top; sr.transform.localScale = Vector3.one; }
    }

    static IEnumerator LowerSpikes(Vector2Int cell)
    {
        if (!SpikeViews.TryGetValue(cell, out var go) || go == null) yield break;
        SpikeViews.Remove(cell);
        var start = go.transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
        {
            if (go == null) yield break;
            go.transform.position = Snap(start + Vector3.down * 0.12f * t);
            go.transform.localScale = new Vector3(1f, 1f - 0.8f * t, 1f);
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    /// <summary>Кто стоит на клетке (для волны): герой и враги, кроме висящего ромба.</summary>
    static IEnumerable<Transform> WaveRiders(Vector2Int cell)
    {
        var hero = Hero;
        if (hero != null && hero.activeTile != null &&
            hero.activeTile.gridLocation.x == cell.x && hero.activeTile.gridLocation.y == cell.y)
            yield return hero.transform;
        foreach (var v in Views.Values)
            if (!v.Hovers && v.Go != null && v.Cell == cell) yield return v.Go.transform;
        if (SpikeViews.TryGetValue(cell, out var spikes) && spikes != null) yield return spikes.transform;
    }

    /// <summary>Ромб висит над клеткой и медленно покачивается (на целые пиксели).</summary>
    static IEnumerator Hover(View v)
    {
        if (v.Go == null) yield break;
        var home = v.Go.transform.position;
        for (float t = 0f; v.Go != null; t += Time.deltaTime)
        {
            v.Go.transform.position = home + new Vector3(0f, Mathf.Round(Mathf.Sin(t * 2.2f) * 3f) / 100f, 0f);
            yield return null;
        }
    }

    // ───────────── анимации (на целые пиксели) ─────────────

    static void Track(IEnumerator routine)
    {
        if (Host == null) return;
        _busy++;
        Running.Add(Host.StartCoroutine(Tracked(routine)));
    }

    static IEnumerator Tracked(IEnumerator routine)
    {
        yield return routine;
        _busy = Mathf.Max(0, _busy - 1);
    }

    static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f, p.z);

    static IEnumerator MoveChain(View v, List<GameEvent> moves)
    {
        foreach (var e in moves)
        {
            if (v.Go == null) yield break;
            var to = new Vector2Int(e.To.X, e.To.Y);
            Face(v, e.Dir);
            Vector3 from = v.Go.transform.position, end = WorldOf(to);
            bool slide = e.Type == GameEventType.Slid;
            float time = slide ? 0.11f : 0.24f;
            // Шаг к камере — сразу поверх; шаг от камеры — новый порядок только по прибытии (как у куба).
            int fromOrder = DepthAt(v.Cell), toOrder = DepthAt(to);
            SetOrder(v, Mathf.Max(fromOrder, toOrder));
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                if (v.Go == null) yield break;
                float k = slide ? t : Mathf.SmoothStep(0f, 1f, t);
                float hop = slide ? 0f : 4f * 0.045f * t * (1f - t);
                v.Go.transform.position = Snap(Vector3.Lerp(from, end, k) + Vector3.up * hop);
                yield return null;
            }
            if (v.Go == null) yield break;
            v.Go.transform.position = end;
            v.Cell = to;
            SetOrder(v, toOrder);
        }
    }

    static IEnumerator Recoil(View v)
    {
        if (v.Go == null) yield break;
        var start = v.Go.transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
        {
            if (v.Go == null) yield break;
            v.Go.transform.position = Snap(start + Vector3.down * (0.02f * Mathf.Sin(t * Mathf.PI)));
            yield return null;
        }
        if (v.Go != null) v.Go.transform.position = start;
    }

    static IEnumerator Vanish(View v)
    {
        if (v.Go == null) yield break;
        var scale = v.Go.transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            if (v.Go == null) yield break;
            v.Go.transform.localScale = scale * (1f - t);
            yield return null;
        }
        if (v.Go != null) Object.Destroy(v.Go);
    }

    static IEnumerator PopIn(View v)
    {
        if (v.Go == null) yield break;
        var scale = v.Go.transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            if (v.Go == null) yield break;
            v.Go.transform.localScale = scale * Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        if (v.Go != null) v.Go.transform.localScale = scale;
    }
}
