using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Особые клетки главы Winter — лёд и рассыпающаяся плита — и снежный вид острова.
///
/// Работает как ColorTiles: TilemapData.LoadTilemapFromData → Load(level, index) запоминает клетки,
/// MapManager.Update → Decorate(cell, block) вешает на видимый блок картинку (Tilemap под блоками не виден).
/// Картинки рисуются кодом по силуэту куба CubesForGame (32×32, опора на 25% высоты) и кладутся поверх
/// блока дочерним спрайтом, поэтому подходят к любому варианту травы:
///   • лёд — голубая глянцевая «шапка» с бликами вместо травы;
///   • плита — трещины на верхней грани и боках, блок чуть темнее;
///   • снег (уровни главы Winter, обычные блоки) — белая шапка.
///
/// Логику (куда скользит куб, какая плита упала) решает ядро CrystalCore / WayTiles; здесь только вид:
/// Crumble(cell) — плита проваливается с пылью, ResetAll() (Rewind) — возвращает все плиты.
/// </summary>
public static class SpecialTiles
{
    const int LevelsPerChapter = 15;   // как UI/LevelMapMenu: Forest / Winter / Sky
    const int WinterChapter = 1;

    static readonly Dictionary<Vector2Int, SuperBlocks> Current = new Dictionary<Vector2Int, SuperBlocks>();
    static readonly Dictionary<Vector2Int, GameObject> Blocks = new Dictionary<Vector2Int, GameObject>();
    static readonly Dictionary<Vector2Int, FallState> Falling = new Dictionary<Vector2Int, FallState>();

    /// <summary>Как было до падения — чтобы Rewind вернул плиту один в один.</summary>
    sealed class FallState
    {
        public Vector3 Home;
        public SpriteRenderer[] Renderers;
        public int[] Orders;
        public Color[] Colors;
        public Coroutine Routine;
    }
    static readonly HashSet<Vector2Int> Broken = new HashSet<Vector2Int>();
    static bool _winter;

    static Sprite _ice, _cracks, _snow;

    /// <summary>Уровень из главы Winter (по номеру уровня в списке игры).</summary>
    public static bool Winter => _winter;

    public static SuperBlocks At(int x, int y) => Current.TryGetValue(new Vector2Int(x, y), out var b) ? b : SuperBlocks.None;
    public static bool IsIce(OverlayTile t) => t != null && At(t.gridLocation.x, t.gridLocation.y) == SuperBlocks.Ice;
    public static bool IsCrumbling(OverlayTile t) => t != null && At(t.gridLocation.x, t.gridLocation.y) == SuperBlocks.Crumbling;
    /// <summary>Плита уже упала — на её месте пустота (до Rewind).</summary>
    public static bool IsBroken(OverlayTile t) => t != null && Broken.Contains(new Vector2Int(t.gridLocation.x, t.gridLocation.y));

    /// <summary>Вызывается при загрузке уровня.</summary>
    public static void Load(LevelSt level, int levelIndex)
    {
        Current.Clear();
        Blocks.Clear();
        Falling.Clear();
        Broken.Clear();
        _hiddenOverlays.Clear();
        _winter = levelIndex / LevelsPerChapter == WinterChapter;
        if (level == null || level.tiles == null) return;

        // Особая клетка — у верхнего блока колонки (как кристаллы и грибы).
        var top = new Dictionary<Vector2Int, TileInfo>();
        foreach (var info in level.tiles)
        {
            if (info == null) continue;
            var key = new Vector2Int(info.position.x, info.position.y);
            if (!top.TryGetValue(key, out var cur) || info.position.z > cur.position.z) top[key] = info;
        }
        foreach (var kv in top)
            if (kv.Value.superTileType == SuperBlocks.Ice || kv.Value.superTileType == SuperBlocks.Crumbling)
                Current[kv.Key] = kv.Value.superTileType;
    }

    /// <summary>Повесить картинку на видимый блок клетки (из MapManager, когда блоки созданы).</summary>
    public static void Decorate(Vector3Int cell, GameObject block)
    {
        if (block == null) return;
        var sr = block.GetComponent<SpriteRenderer>();
        if (sr == null || sr.color.a <= 0f) return; // пустые / облачные блоки не трогаем
        var key = new Vector2Int(cell.x, cell.y);
        var type = At(cell.x, cell.y);

        Sprite overlay = null;
        if (type == SuperBlocks.Ice) overlay = IceSprite;
        else if (type == SuperBlocks.Crumbling)
        {
            overlay = CracksSprite;
            sr.color = new Color(sr.color.r * 0.82f, sr.color.g * 0.74f, sr.color.b * 0.66f, sr.color.a);
        }
        else if (_winter && ColorTiles.At(cell.x, cell.y) == TileColor.None) overlay = SnowSprite;

        // Позицию НЕ запоминаем здесь: блоки ещё влетают на место (вступление MapManager).
        if (type == SuperBlocks.Crumbling) Blocks[key] = block;
        if (overlay == null) return;

        var child = new GameObject(type == SuperBlocks.None ? "Snow" : type.ToString());
        child.transform.SetParent(block.transform, false);
        var csr = child.AddComponent<SpriteRenderer>();
        csr.sprite = overlay;
        csr.sortingLayerID = sr.sortingLayerID;
        csr.sortingOrder = sr.sortingOrder + 1;
        csr.sharedMaterial = sr.sharedMaterial; // тот же свет, что у блока
    }

    /// <summary>Плита под клеткой проваливается (герой с неё ушёл).</summary>
    public static void Crumble(OverlayTile tile)
    {
        if (tile == null) return;
        var key = new Vector2Int(tile.gridLocation.x, tile.gridLocation.y);
        if (At(key.x, key.y) != SuperBlocks.Crumbling || !Broken.Add(key)) return;

        PixelSfx.Play(Sfx.Remove, 0.7f, 0.8f);
        if (Blocks.TryGetValue(key, out var block) && block != null)
        {
            var sr = block.GetComponent<SpriteRenderer>();
            if (sr != null)
                PixelFX.Burst(block.transform.position + Vector3.up * 0.12f, PixelFX.DustColors, 10, 0.5f, 0.15f, 1.6f,
                              sr.sortingLayerID, sr.sortingOrder + 3, PixelFX.Dot, 1f, 0.6f);
            // Запоминаем всё сейчас: остров уже собран, блок стоит на своём месте.
            var renderers = block.GetComponentsInChildren<SpriteRenderer>(true);
            var state = new FallState
            {
                Home = block.transform.localPosition,
                Renderers = renderers,
                Orders = renderers.Select(r => r.sortingOrder).ToArray(),
                Colors = renderers.Select(r => r.color).ToArray(),
            };
            Falling[key] = state;
            // Сортировка в проекте по оси Y: опускаясь, блок рисовался бы ПОВЕРХ передних клеток.
            // На время падения уводим его (с трещинами и камнями) под остров.
            foreach (var r in renderers) r.sortingOrder -= FallOrderShift;
            var host = ActiveBlockManager.Instance;
            if (host != null) state.Routine = host.StartCoroutine(Fall(block, state));
            else block.SetActive(false);
        }
        if (tile.gameObject != null) tile.gameObject.SetActive(false); // клетка-подсветка тоже исчезает
        _hiddenOverlays[key] = tile;
    }

    static readonly Dictionary<Vector2Int, OverlayTile> _hiddenOverlays = new Dictionary<Vector2Int, OverlayTile>();

    /// <summary>
    /// Rewind: все упавшие плиты возвращаются на место.
    ///
    /// Исправлено: плита не возвращалась — «домашняя» позиция запоминалась, пока блоки ещё влетали
    /// на остров (на 2 единицы ниже), и Rewind ставил её туда. Ещё падение, начатое прямо перед
    /// Rewind, доигрывалось и снова прятало плиту — теперь оно останавливается.
    /// </summary>
    public static void ResetAll()
    {
        var host = ActiveBlockManager.Instance;
        foreach (var key in Broken)
        {
            if (Blocks.TryGetValue(key, out var block) && block != null)
            {
                if (Falling.TryGetValue(key, out var st))
                {
                    if (st.Routine != null && host != null) host.StopCoroutine(st.Routine);
                    block.transform.localPosition = st.Home;
                    for (int i = 0; i < st.Renderers.Length; i++)
                    {
                        if (st.Renderers[i] == null) continue;
                        st.Renderers[i].sortingOrder = st.Orders[i];
                        st.Renderers[i].color = st.Colors[i];
                    }
                }
                block.SetActive(true);
            }
            if (_hiddenOverlays.TryGetValue(key, out var tile) && tile != null) tile.gameObject.SetActive(true);
        }
        Broken.Clear();
        Falling.Clear();
        _hiddenOverlays.Clear();
    }

    const int FallOrderShift = 5;

    static IEnumerator Fall(GameObject block, FallState state)
    {
        var start = state.Home;
        var renderers = state.Renderers;
        const float time = 0.45f;
        for (float t = 0f; t < 1f && block != null; t += Time.deltaTime / time)
        {
            // Сначала дрожит, потом проваливается — на целые пиксели (0.01 = 1 px).
            float shake = t < 0.3f ? Mathf.Round(Mathf.Sin(t * 90f)) * 0.01f : 0f;
            float drop = t < 0.3f ? 0f : Mathf.Round((t - 0.3f) * (t - 0.3f) * 120f) * 0.01f;
            block.transform.localPosition = start + new Vector3(shake, -drop, 0f);
            float a = t < 0.3f ? 1f : 1f - (t - 0.3f) / 0.7f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = state.Colors[i]; c.a *= a; renderers[i].color = c;
            }
            yield return null;
        }
        if (block != null) block.SetActive(false);
        state.Routine = null;
    }

    // ───────────── картинки (рисуются кодом по силуэту куба 32×32) ─────────────

    /// <summary>Верх куба: трава занимает ряды 11..31 (снизу), ромб грани — ряды 16..31.</summary>
    static bool InCap(int x, int y)
    {
        int half;
        if (y >= 25 && y <= 31) half = 2 * (32 - y);
        else if (y >= 18 && y <= 24) half = 16;
        else if (y >= 11 && y <= 17) half = 2 * y - 19;
        else return false;
        return x >= 16 - half && x <= 15 + half;
    }

    static bool OnTopFace(int y) => y >= 18;

    static Sprite MakeSprite(Color[] px)
    {
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.25f), 100f, 0, SpriteMeshType.FullRect);
    }

    static Sprite IceSprite
    {
        get
        {
            if (_ice != null) return _ice;
            var px = new Color[32 * 32];
            var light = new Color(0.80f, 0.95f, 1f, 1f);
            var mid = new Color(0.60f, 0.85f, 0.98f, 1f);
            var deep = new Color(0.42f, 0.70f, 0.92f, 1f);
            var shine = Color.white;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    if (!InCap(x, y)) continue;
                    Color c = OnTopFace(y) ? (y > 26 ? light : mid) : deep;     // грань светлее, бока — глубже
                    if (!OnTopFace(y) && (x % 5 == 2) && y < 15) c = mid;       // сосульки на боках
                    // Блики: две диагональные полоски на верхней грани.
                    if (OnTopFace(y) && ((x - 2 * (y - 18) == 6) || (x - 2 * (y - 18) == 7) || (x - 2 * (y - 18) == 14))) c = shine;
                    px[y * 32 + x] = c;
                }
            _ice = MakeSprite(px);
            return _ice;
        }
    }

    static Sprite SnowSprite
    {
        get
        {
            if (_snow != null) return _snow;
            var px = new Color[32 * 32];
            var white = new Color(0.97f, 0.98f, 1f, 1f);
            var shade = new Color(0.82f, 0.88f, 0.96f, 1f);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    if (!InCap(x, y)) continue;
                    // Снег на боках неровный: «капли» разной длины.
                    if (!OnTopFace(y) && y < 16 - ((x * 7) % 4)) continue;
                    px[y * 32 + x] = OnTopFace(y) && y > 20 ? white : shade;
                }
            _snow = MakeSprite(px);
            return _snow;
        }
    }

    static Sprite CracksSprite
    {
        get
        {
            if (_cracks != null) return _cracks;
            var px = new Color[32 * 32];
            var ink = new Color(0.26f, 0.15f, 0.21f, 0.9f);
            // Трещины: ломаная по верхней грани и две по бокам (координаты снизу вверх).
            int[,] lines =
            {
                { 9, 24 }, { 10, 23 }, { 11, 23 }, { 12, 22 }, { 13, 22 }, { 14, 21 }, { 15, 21 }, { 16, 22 }, { 17, 22 }, { 18, 23 },
                { 19, 23 }, { 20, 24 }, { 21, 24 }, { 22, 25 }, { 15, 20 }, { 15, 19 }, { 16, 26 }, { 17, 27 }, { 17, 28 },
                { 6, 9 }, { 7, 8 }, { 7, 7 }, { 8, 6 }, { 8, 5 },
                { 24, 10 }, { 23, 9 }, { 23, 8 }, { 24, 7 }, { 25, 6 },
            };
            for (int i = 0; i < lines.GetLength(0); i++) px[lines[i, 1] * 32 + lines[i, 0]] = ink;
            _cracks = MakeSprite(px);
            return _cracks;
        }
    }
}
