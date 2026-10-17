using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using CrystalAdventure.UnityLayer;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Враги и боссы на поле старой системы (уровни, где LevelSt.enemies не пуст).
///
/// Решает всё ядро CrystalCore — здесь только показ того же прогона (CoreRunBridge.Last):
///   • Setup — при загрузке уровня и после Rewind: враги на стартовых клетках, красные клетки
///     «сюда ударят в следующий ход», сердечки героя, полоска здоровья босса;
///   • BeginTurn(i) — в начале шага куба: шаги врагов (одновременно с кубом), призыв дронов, заморозка поля;
///   • FinishTurn(i) — после шага куба: удары врагов, урон, щиты, смерти, новые красные клетки.
/// Победу на таких уровнях объявляет ядро (кристаллы собраны И босс повержен), а не счётчик кристаллов:
/// ActiveBlockManager.victoryByCore + ForceVictory из CharacterCode.
///
/// Всё создаётся кодом и лежит рядом с кубом (тот же родитель — покачивается вместе с островом).
/// Картинки — World/EnemyArt.
/// </summary>
public static class EnemyLayer
{
    sealed class View
    {
        public int Id;
        public string Archetype;
        public GameObject Go;
        public SpriteRenderer Sr;
        public Vector2Int Cell;
        public int Hp, MaxHp;
        public bool Boss, Armored;
        public SpriteRenderer[] Pips;
        public GameObject Bubble;
        public Color Base = Color.white;
    }

    static readonly Dictionary<int, View> Views = new Dictionary<int, View>();
    static readonly List<GameObject> Markers = new List<GameObject>();
    static readonly List<Coroutine> Running = new List<Coroutine>();
    static LevelSt _level;
    static int _busy;
    static GameObject _heartsRoot;
    static readonly List<Image> Hearts = new List<Image>();
    static int _heroHp;

    const int HeroId = 1; // ядро всегда создаёт героя первым (LevelSetup.Build)

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

    /// <summary>Rewind: враги и сердечки — как в начале уровня.</summary>
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
        if (!Active) { ShowHearts(0, 0); return; }

        var mm = MapManager.Instance;
        if (!LegacyLevelImport.TryFindStartCell(_level, mm != null ? mm.tilemap : null, out var start, out _)) return;
        World world;
        try { world = LegacyLevelImport.ToSetup(_level, start).Build(); }
        catch (System.Exception e) { Debug.LogWarning("[Crystal] Враги: не удалось собрать уровень — " + e.Message); Active = false; return; }

        foreach (var e in world.Entities.Where(x => x.Alive && x.IsHostile))
            Spawn(e.Id, e.Archetype, new Vector2Int(e.Pos.X, e.Pos.Y), e.Facing, e.Hp, e.MaxHp, e.Kind == EntityKind.Boss, e.Armored, false);

        ShowIntents(TurnResolver.PlanOnly(world));
        _heroHp = world.Hero.Hp;
        ShowHearts(_heroHp, world.Hero.MaxHp);
    }

    static void Clear()
    {
        foreach (var v in Views.Values) if (v.Go != null) Object.Destroy(v.Go);
        Views.Clear();
        ClearMarkers();
    }

    static void StopAll()
    {
        if (Host != null) foreach (var c in Running) if (c != null) Host.StopCoroutine(c);
        Running.Clear();
        _busy = 0;
    }

    // ================================================================ ход

    static TurnRecord Turn(int i)
    {
        var r = CoreRunBridge.Last;
        return r != null && i >= 0 && i < r.Turns.Count ? r.Turns[i] : null;
    }

    /// <summary>Начало шага куба: враги шагают одновременно с ним, босс призывает и замораживает.</summary>
    public static void BeginTurn(int i)
    {
        if (!Active || Host == null) return;
        var turn = Turn(i);
        if (turn == null) return;
        ClearMarkers(); // намерения этого хода исполняются — подсветку снимаем

        foreach (var e in turn.Events)
        {
            switch (e.Type)
            {
                case GameEventType.Spawned:
                    Spawn(e.EntityId, e.Info, new Vector2Int(e.Cell.X, e.Cell.Y), e.Dir, 1, 1, false, false, true);
                    break;
                case GameEventType.Frozen:
                    SpecialTiles.Freeze(new Vector2Int(e.Cell.X, e.Cell.Y));
                    break;
                case GameEventType.Guarded:
                    if (Views.TryGetValue(e.EntityId, out var g)) SetBubble(g, true);
                    break;
            }
        }

        // Перемещения одного врага — по очереди (шаг, потом скольжение), разных — одновременно.
        foreach (var group in turn.Events.Where(e => e.EntityId != HeroId && IsMove(e.Type)).GroupBy(e => e.EntityId))
            if (Views.TryGetValue(group.Key, out var v)) Track(MoveChain(v, group.ToList()));
        foreach (var e in turn.Events.Where(e => e.EntityId != HeroId && e.Type == GameEventType.Bumped && e.Phase == EventPhase.Move))
            if (Views.TryGetValue(e.EntityId, out var v)) { Face(v, e.Dir); Track(Nudge(v, e.Dir, 0.06f)); }
    }

    /// <summary>Конец шага куба: удары, урон, смерти, новые намерения.</summary>
    public static IEnumerator FinishTurn(int i)
    {
        if (!Active) yield break;
        var turn = Turn(i);
        if (turn == null) yield break;
        while (_busy > 0) yield return null;

        bool any = false;
        foreach (var e in turn.Events.Where(e => e.Phase == EventPhase.Combat || e.Phase == EventPhase.Tiles || e.Phase == EventPhase.Resolve))
        {
            switch (e.Type)
            {
                case GameEventType.Attacked when e.EntityId != HeroId:
                    if (Views.TryGetValue(e.EntityId, out var a))
                    {
                        Face(a, e.Dir);
                        Track(Nudge(a, e.Dir, 0.12f));
                        FlashArea(e.Area);
                        PixelSfx.Play(Sfx.Remove, 0.6f, a.Boss ? 0.7f : 1.1f);
                        any = true;
                    }
                    break;
                case GameEventType.Damaged:
                    if (e.EntityId == HeroId)
                    {
                        _heroHp = Mathf.Max(0, _heroHp - e.Amount);
                        ShowHearts(_heroHp, Hearts.Count);
                        var hero = Hero;
                        if (hero != null) hero.PlayHurt();
                    }
                    else if (Views.TryGetValue(e.EntityId, out var d))
                    {
                        d.Hp = Mathf.Max(0, d.Hp - e.Amount);
                        UpdatePips(d);
                        Track(Flash(d, new Color(1f, 0.45f, 0.45f, 1f)));
                    }
                    any = true;
                    break;
                case GameEventType.Blocked:
                    if (e.EntityId != HeroId && Views.TryGetValue(e.EntityId, out var b))
                        Track(Flash(b, new Color(0.6f, 0.85f, 1f, 1f)));
                    PixelSfx.Play(Sfx.Click, 0.6f, 1.4f);
                    break;
                case GameEventType.ArmorBroken:
                    if (Views.TryGetValue(e.EntityId, out var ab)) { ab.Armored = false; ab.Sr.color = ab.Base; }
                    break;
                case GameEventType.Died:
                case GameEventType.Fell:
                    if (e.EntityId != HeroId && Views.TryGetValue(e.EntityId, out var dead))
                    {
                        Views.Remove(e.EntityId);
                        Track(Die(dead, e.Type == GameEventType.Fell));
                        any = true;
                    }
                    break;
            }
        }
        while (_busy > 0) yield return null;
        if (any) yield return new WaitForSeconds(0.08f);

        // Щит босса (Guard) действует один ход.
        foreach (var v in Views.Values) if (v.Bubble != null && v.Bubble.activeSelf) SetBubble(v, false);
        // Новая фаза босса: ледяная броня (обычные удары не пробивают — нужна Супер-атака).
        foreach (var e in turn.Events.Where(x => x.Type == GameEventType.ArmorGained))
            if (Views.TryGetValue(e.EntityId, out var armored))
            {
                armored.Armored = true;
                armored.Sr.color = new Color(0.7f, 0.9f, 1f, 1f);
                PixelSfx.Play(Sfx.Pop, 0.7f, 0.6f);
            }
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

    static int OrderAt(Vector2Int c)
    {
        var t = TileAt(c);
        var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
        return (sr != null ? sr.sortingOrder : 0) + 2; // как куб (CharacterCode.OrderAboveTile)
    }

    static void Spawn(int id, string archetype, Vector2Int cell, Dir facing, int hp, int maxHp, bool boss, bool armored, bool popIn)
    {
        var root = Root;
        if (root == null) return;
        var go = new GameObject($"Enemy_{archetype}_{id}");
        go.transform.SetParent(root, false);
        go.transform.position = WorldOf(cell);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = EnemyArt.For(archetype);
        var heroSr = Hero != null ? Hero.MainRenderer : null;
        if (heroSr != null) { sr.sortingLayerID = heroSr.sortingLayerID; sr.sharedMaterial = heroSr.sharedMaterial; }
        sr.sortingOrder = OrderAt(cell);

        var v = new View { Id = id, Archetype = archetype, Go = go, Sr = sr, Cell = cell, Hp = hp, MaxHp = maxHp, Boss = boss || EnemyArt.IsBoss(archetype), Armored = armored };
        Face(v, facing);
        if (armored) sr.color = new Color(0.7f, 0.9f, 1f, 1f);
        v.Base = Color.white;
        if (v.Boss) BuildPips(v);
        Views[id] = v;
        if (popIn) Track(PopIn(v));
    }

    static void Face(View v, Dir d)
    {
        // Спрайты смотрят вправо по экрану; West и North в изометрии — влево (как у куба).
        if (v.Sr != null) v.Sr.flipX = d == Dir.West || d == Dir.North;
    }

    static void BuildPips(View v)
    {
        float top = v.Sr.sprite != null ? v.Sr.sprite.rect.height * 0.01f + 0.04f : 0.3f;
        v.Pips = new SpriteRenderer[v.MaxHp];
        for (int i = 0; i < v.MaxHp; i++)
        {
            var p = new GameObject("Hp" + i).AddComponent<SpriteRenderer>();
            p.transform.SetParent(v.Go.transform, false);
            p.transform.localPosition = new Vector3((i - (v.MaxHp - 1) * 0.5f) * 0.04f, top, 0f);
            p.sprite = EnemyArt.Pip;
            p.sortingLayerID = v.Sr.sortingLayerID;
            p.sortingOrder = v.Sr.sortingOrder + 1;
            v.Pips[i] = p;
        }
        UpdatePips(v);
    }

    static void UpdatePips(View v)
    {
        if (v.Pips == null) return;
        for (int i = 0; i < v.Pips.Length; i++)
            if (v.Pips[i] != null) v.Pips[i].color = i < v.Hp ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.25f, 0.2f, 0.3f, 0.8f);
    }

    static void SetBubble(View v, bool on)
    {
        if (on && v.Bubble == null)
        {
            var b = new GameObject("Guard").AddComponent<SpriteRenderer>();
            b.transform.SetParent(v.Go.transform, false);
            float h = v.Sr.sprite != null ? v.Sr.sprite.rect.height * 0.01f : 0.2f;
            b.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            b.transform.localScale = Vector3.one * Mathf.Max(1f, h / 0.2f);
            b.sprite = EnemyArt.Bubble;
            b.sortingLayerID = v.Sr.sortingLayerID;
            b.sortingOrder = v.Sr.sortingOrder + 1;
            v.Bubble = b.gameObject;
        }
        if (v.Bubble != null) v.Bubble.SetActive(on);
    }

    // ───────────── красные клетки ─────────────

    static void ShowIntents(IEnumerable<GameEvent> planned)
    {
        ClearMarkers();
        var root = Root;
        if (root == null) return;
        foreach (var e in planned)
        {
            if (e.Type != GameEventType.IntentPlanned || e.Area == null) continue;
            foreach (var c in e.Area)
            {
                var cell = new Vector2Int(c.X, c.Y);
                if (TileAt(cell) == null) continue;
                var m = new GameObject("Danger").AddComponent<SpriteRenderer>();
                m.transform.SetParent(root, false);
                m.transform.position = WorldOf(cell) + new Vector3(0f, 0.04f, 0f);
                m.sprite = EnemyArt.Danger;
                var heroSr = Hero != null ? Hero.MainRenderer : null;
                if (heroSr != null) m.sortingLayerID = heroSr.sortingLayerID;
                // Тот же порядок, что у героя, но ромб чуть выше по Y (сортировка по оси Y) — значит,
                // рисуется ЗА героем и врагом на этой клетке и поверх снега / льда (у них +1).
                m.sortingOrder = OrderAt(cell);
                Markers.Add(m.gameObject);
            }
        }
    }

    static void ClearMarkers()
    {
        foreach (var m in Markers) if (m != null) Object.Destroy(m);
        Markers.Clear();
    }

    static void FlashArea(Cell[] area)
    {
        if (area == null || Root == null) return;
        foreach (var c in area)
        {
            var cell = new Vector2Int(c.X, c.Y);
            if (TileAt(cell) == null) continue;
            var sr = Hero != null ? Hero.MainRenderer : null;
            if (sr != null)
                PixelFX.Burst(WorldOf(cell) + Vector3.up * 0.04f, new[] { new Color(1f, 0.35f, 0.25f, 1f), new Color(1f, 0.8f, 0.4f, 1f) },
                              4, 0.25f, 0.08f, 0.8f, sr.sortingLayerID, OrderAt(cell) + 3, PixelFX.Dot, 0.8f, 0.35f);
        }
    }

    // ───────────── сердечки героя ─────────────

    static void ShowHearts(int hp, int max)
    {
        if (max <= 0)
        {
            if (_heartsRoot != null) _heartsRoot.SetActive(false);
            return;
        }
        if (_heartsRoot == null || Hearts.Count != max)
        {
            if (_heartsRoot != null) Object.Destroy(_heartsRoot);
            Hearts.Clear();
            _heartsRoot = new GameObject("HeroHearts", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _heartsRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = _heartsRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            for (int i = 0; i < max; i++)
            {
                var img = new GameObject("Heart" + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var rt = (RectTransform)img.transform;
                rt.SetParent(_heartsRoot.transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(63f, 56f); // 9×8 пикселей × 7
                rt.anchoredPosition = new Vector2((i - (max - 1) * 0.5f) * 72f, -24f);
                img.raycastTarget = false;
                Hearts.Add(img);
            }
        }
        _heartsRoot.SetActive(true);
        for (int i = 0; i < Hearts.Count; i++)
            if (Hearts[i] != null) Hearts[i].sprite = i < hp ? EnemyArt.Heart : EnemyArt.HeartEmpty;
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
            v.Sr.sortingOrder = Mathf.Max(v.Sr.sortingOrder, OrderAt(to));
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                if (v.Go == null) yield break;
                float k = slide ? t : Mathf.SmoothStep(0f, 1f, t);
                float hop = slide ? 0f : 4f * 0.04f * t * (1f - t);
                v.Go.transform.position = Snap(Vector3.Lerp(from, end, k) + Vector3.up * hop);
                yield return null;
            }
            v.Go.transform.position = end;
            v.Cell = to;
            v.Sr.sortingOrder = OrderAt(to);
        }
    }

    static Vector3 DirToWorld(Dir d)
    {
        var o = d.Offset();
        return new Vector3((o.X - o.Y) * 0.16f, (o.X + o.Y) * 0.08f, 0f);
    }

    static IEnumerator Nudge(View v, Dir d, float amount)
    {
        if (v.Go == null) yield break;
        var start = v.Go.transform.position;
        var off = DirToWorld(d).normalized * amount;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
        {
            if (v.Go == null) yield break;
            v.Go.transform.position = Snap(start + off * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        if (v.Go != null) v.Go.transform.position = start;
    }

    static IEnumerator Flash(View v, Color c)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            if (v.Sr == null) yield break;
            v.Sr.color = Color.Lerp(c, v.Armored ? new Color(0.7f, 0.9f, 1f, 1f) : v.Base, t);
            yield return null;
        }
        if (v.Sr != null) v.Sr.color = v.Armored ? new Color(0.7f, 0.9f, 1f, 1f) : v.Base;
    }

    static IEnumerator Die(View v, bool fall)
    {
        PixelSfx.Play(Sfx.Crystal, 0.5f, 0.6f);
        if (v.Sr != null)
            PixelFX.Burst(v.Go.transform.position + Vector3.up * 0.08f, PixelFX.DustColors, 10, 0.5f, 0.2f, 1.4f,
                          v.Sr.sortingLayerID, v.Sr.sortingOrder + 2, PixelFX.Dot, 1f, 0.6f);
        var start = v.Go != null ? v.Go.transform.position : Vector3.zero;
        var scale = v.Go != null ? v.Go.transform.localScale : Vector3.one;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            if (v.Go == null) yield break;
            v.Go.transform.localScale = scale * (1f - t);
            if (fall) v.Go.transform.position = Snap(start + Vector3.down * (t * t * 0.6f));
            yield return null;
        }
        if (v.Go != null) Object.Destroy(v.Go);
    }

    static IEnumerator PopIn(View v)
    {
        var scale = v.Go.transform.localScale;
        PixelSfx.Play(Sfx.Pop, 0.6f, 0.9f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            if (v.Go == null) yield break;
            v.Go.transform.localScale = scale * Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        if (v.Go != null) v.Go.transform.localScale = scale;
    }
}
