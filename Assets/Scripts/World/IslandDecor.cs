using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Украшения острова, которые ставятся на уже построенные блоки карты (зовёт MapManager):
///  • блоки получают освещаемый материал (свет и карты нормалей);
///  • под каждым блоком свисает каменистый «корень» — остров выглядит отколотым от земли;
///  • на блок падает тень от более высокого соседа слева сверху (со стороны солнца);
///  • вокруг острова парят маленькие обломки.
/// </summary>
public static class IslandDecor
{
    static Sprite[] _rocks;
    static Sprite _edgeShadow;
    static Sprite[] _debris;
    static Material _lit;

    public static readonly Color ShadowColor = new Color(0.10f, 0.08f, 0.22f, 1f);

    /// <summary>Sprite-Lit-Default: берём у героя (он уже освещаемый), иначе ищем шейдер.</summary>
    public static Material LitMaterial
    {
        get
        {
            if (_lit != null) return _lit;
            var hero = Object.FindObjectOfType<CharacterCode>();
            var sr = hero != null ? hero.GetComponent<SpriteRenderer>() : null;
            var m = sr != null ? sr.sharedMaterial : null;
            if (m != null && m.shader != null && m.shader.name.Contains("Sprite-Lit")) _lit = m;
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader != null) _lit = new Material(shader) { name = "IslandLit" };
            }
            return _lit;
        }
    }

    public static void Build(IList<Vector3Int> cells, IList<GameObject> blocks, Tilemap tilemap, Transform islandRoot)
    {
        if (cells == null || blocks == null) return;
        if (_rocks == null)
        {
            _rocks = new Sprite[4];
            for (int i = 0; i < _rocks.Length; i++) _rocks[i] = WorldArt.Rock(101 + i * 17);
            _edgeShadow = WorldArt.EdgeShadow();
            _debris = new Sprite[4];
            for (int i = 0; i < _debris.Length; i++) _debris[i] = WorldArt.Debris(301 + i * 29);
        }

        var lit = IslandAtmosphere.LightingWanted ? LitMaterial : null;
        var heights = new Dictionary<Vector2Int, int>();
        int count = Mathf.Min(cells.Count, blocks.Count);

        for (int i = 0; i < count; i++)
        {
            var sr = blocks[i] != null ? blocks[i].GetComponent<SpriteRenderer>() : null;
            if (!Visible(sr)) continue;
            heights[new Vector2Int(cells[i].x, cells[i].y)] = cells[i].z;
        }

        for (int i = 0; i < count; i++)
        {
            var block = blocks[i];
            var sr = block != null ? block.GetComponent<SpriteRenderer>() : null;
            if (!Visible(sr)) continue;
            var cell = cells[i];

            if (lit != null) sr.sharedMaterial = lit;

            // Камни под блоком (рисуются позади всех блоков — видно только то, что свисает снизу)
            int h = (cell.x * 73856093 ^ cell.y * 19349663) & 0x7fffffff;
            var rock = Child(block.transform, "Rock", _rocks[h % _rocks.Length], sr.sortingLayerID, sr.sortingOrder - 1, lit);
            rock.transform.localPosition = new Vector3(0f, 0f, 0.001f);

            // Тень от высокого соседа слева сверху
            if (heights.TryGetValue(new Vector2Int(cell.x, cell.y + 1), out int nz) && nz > cell.z)
            {
                var sh = Child(block.transform, "EdgeShadow", _edgeShadow, sr.sortingLayerID, sr.sortingOrder, null);
                sh.transform.localPosition = new Vector3(0f, -0.0005f, -0.0005f);
                var c = ShadowColor; c.a = 0.30f; sh.color = c;
            }
        }

        if (tilemap != null && islandRoot != null) AddDebris(cells, tilemap, islandRoot, lit);
    }

    static bool Visible(SpriteRenderer sr) => sr != null && sr.sprite != null && sr.color.a > 0.01f;

    static SpriteRenderer Child(Transform parent, string name, Sprite sprite, int layer, int order, Material mat)
    {
        var existing = parent.Find(name);
        if (existing != null) Object.Destroy(existing.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat != null) sr.sharedMaterial = mat;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        return sr;
    }

    static void AddDebris(IList<Vector3Int> cells, Tilemap tilemap, Transform root, Material lit)
    {
        var old = root.Find("IslandDebris");
        if (old != null) Object.Destroy(old.gameObject);
        if (cells.Count == 0) return;

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        int seed = 17;
        foreach (var c in cells)
        {
            var p = tilemap.GetCellCenterWorld(c);
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            seed = seed * 31 + c.x * 7 + c.y * 13;
        }
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();

        var holder = new GameObject("IslandDebris").transform;
        holder.SetParent(root, false);
        holder.position = Vector3.zero;

        // Слева, справа и снизу — вне острова, чтобы не закрывать блоки
        var spots = new List<Vector3>
        {
            new Vector3(minX - 0.22f - R() * 0.12f, Mathf.Lerp(minY, maxY, 0.3f + R() * 0.4f) - 0.05f, 0f),
            new Vector3(maxX + 0.22f + R() * 0.12f, Mathf.Lerp(minY, maxY, 0.2f + R() * 0.4f) - 0.05f, 0f),
            new Vector3(Mathf.Lerp(minX, maxX, 0.15f + R() * 0.25f), minY - 0.30f - R() * 0.08f, 0f),
            new Vector3(Mathf.Lerp(minX, maxX, 0.6f + R() * 0.3f), minY - 0.24f - R() * 0.10f, 0f),
            new Vector3(minX - 0.12f - R() * 0.1f, minY - 0.12f - R() * 0.06f, 0f),
        };
        for (int i = 0; i < spots.Count; i++)
        {
            var sr = Child(holder, "Chunk" + i, _debris[rng.Next(_debris.Length)], 0, -1, lit);
            sr.transform.position = spots[i];
            var bob = sr.gameObject.AddComponent<FloatBob>();
            bob.amplitude = 0.012f + R() * 0.012f;
            bob.period = 3.2f + R() * 2.4f;
            bob.phase = R() * 6f;
        }
    }
}
