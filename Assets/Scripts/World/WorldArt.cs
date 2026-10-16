using System;
using UnityEngine;

/// <summary>
/// Пиксельная графика окружения, нарисованная кодом в стиле игры (100 px = 1 единица, Point-фильтр):
/// небо, облака, море облаков, камни под островом, летающие обломки, тени, свечение.
/// Никаких сгенерированных картинок — только простые формы и палитра из блоков игры.
/// </summary>
public static class WorldArt
{
    public const float PPU = 100f;

    // Палитра земли — взята с листа CubesForGame (трава сверху, бока).
    public static readonly Color32 Ink = new Color32(34, 32, 52, 255);
    public static readonly Color32 Grass = new Color32(149, 225, 53, 255);
    public static readonly Color32 GrassDark = new Color32(118, 200, 30, 255);
    public static readonly Color32 DirtLeft = new Color32(192, 121, 117, 255);
    public static readonly Color32 DirtRight = new Color32(121, 73, 97, 255);
    static readonly Color32 RockLeft = new Color32(164, 100, 104, 255);
    static readonly Color32 RockLeftDeep = new Color32(132, 78, 92, 255);
    static readonly Color32 RockLeftSpeck = new Color32(204, 138, 128, 255);
    static readonly Color32 RockRight = new Color32(100, 60, 84, 255);
    static readonly Color32 RockRightDeep = new Color32(78, 46, 72, 255);
    static readonly Color32 RockRightSpeck = new Color32(132, 84, 106, 255);
    static readonly Color32 Root = new Color32(98, 64, 58, 255);

    static readonly float[,] Bayer =
    {
        { 0f / 16, 8f / 16, 2f / 16, 10f / 16 },
        { 12f / 16, 4f / 16, 14f / 16, 6f / 16 },
        { 3f / 16, 11f / 16, 1f / 16, 9f / 16 },
        { 15f / 16, 7f / 16, 13f / 16, 5f / 16 },
    };

    public static float Dither(int x, int y) => Bayer[y & 3, x & 3];

    static Material _unlit;

    /// <summary>Неосвещаемый материал для неба, облаков, теней (цвет — ровно такой, как нарисован).</summary>
    public static Material Unlit
    {
        get
        {
            if (_unlit != null) return _unlit;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) _unlit = new Material(shader) { name = "WorldUnlit" };
            return _unlit;
        }
    }

    /// <summary>Собрать спрайт. px(x, y) — y считается СВЕРХУ (как в редакторе картинок).</summary>
    public static Sprite Build(int w, int h, Func<int, int, Color32> px, Vector2 pivot, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };
        var data = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                data[(h - 1 - y) * w + x] = px(x, y);
        tex.SetPixels32(data);
        tex.Apply(false, true);
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, PPU);
        s.name = name;
        return s;
    }

    static Color32 Lerp(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
    static Color32 Clear => new Color32(0, 0, 0, 0);

    // ───────────────────────────── небо ─────────────────────────────

    /// <summary>Небо: градиент полосами с пиксельным дизерингом, сияние солнца слева сверху, звёзды ночью.</summary>
    public static Sprite Sky(IslandTheme th, int w, int h, int seed)
    {
        var rng = new System.Random(seed);
        var stars = new bool[w * h];
        if (th.stars)
            for (int i = 0; i < w * h / 160; i++)
                stars[rng.Next(w * h)] = true;

        const int bands = 16;
        float sunX = w * 0.1f, sunY = h * 0.08f, glowR = h * 1.05f;
        return Build(w, h, (x, y) =>
        {
            float t = y / (float)(h - 1);
            float tq = Mathf.Clamp01(Mathf.Floor(t * bands + Dither(x, y)) / bands);
            Color c = tq < 0.55f ? Color.Lerp(th.skyTop, th.skyMid, tq / 0.55f)
                                 : Color.Lerp(th.skyMid, th.skyHorizon, (tq - 0.55f) / 0.45f);

            float dx = (x - sunX) * 0.8f, dy = y - sunY;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / glowR;
            float g = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f) * th.sunGlowStrength;
            float gq = Mathf.Floor(g * 6f + Dither(x + 1, y + 2)) / 6f;
            c = Color.Lerp(c, th.sunGlow, Mathf.Clamp01(gq));

            if (stars[y * w + x] && t < 0.75f)
                c = Color.Lerp(c, Color.white, 0.55f + 0.45f * (float)((x * 7 + y * 13) % 5) / 4f);
            c.a = 1f;
            return c;
        }, new Vector2(0.5f, 0.5f), "Sky");
    }

    // ──────────────────────────── облака ────────────────────────────

    struct Puff { public float x, y, r; }

    /// <summary>Пушистое облако с плоским низом: светлый край сверху, тень снизу и справа.</summary>
    public static Sprite Cloud(int seed, int w, int h, Color light, Color mid, Color shadow)
    {
        var rng = new System.Random(seed);
        int baseRow = h - 2;
        int n = 3 + rng.Next(3);
        var puffs = new Puff[n];
        for (int i = 0; i < n; i++)
        {
            float f = (i + 0.5f) / n;
            float r = h * (0.30f + 0.34f * (1f - Mathf.Abs(f - 0.5f) * 1.6f)) * (0.85f + 0.25f * (float)rng.NextDouble());
            float cx = w * (0.14f + 0.72f * f) + (float)(rng.NextDouble() * 4 - 2);
            cx = Mathf.Clamp(cx, r + 1, w - r - 1);
            puffs[i] = new Puff { x = cx, y = baseRow - r * 0.55f, r = r };
        }
        float left = w, right = 0;
        foreach (var p in puffs) { left = Mathf.Min(left, p.x - p.r * 0.7f); right = Mathf.Max(right, p.x + p.r * 0.7f); }
        float ph = (float)rng.NextDouble() * 6f;

        bool Inside(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y > baseRow) return false;
            if (y >= baseRow - 3 && x >= left && x <= right) return true; // ровный низ
            foreach (var p in puffs)
            {
                float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y;
                if (dx * dx + dy * dy <= p.r * p.r) return true;
            }
            return false;
        }

        return Build(w, h, (x, y) =>
        {
            if (!Inside(x, y)) return Clear;
            int band = 2 + Mathf.RoundToInt(1.2f + 1.2f * Mathf.Sin(x * 0.45f + ph));
            if (y >= baseRow - band) return shadow;
            if (!Inside(x + 1, y) && y > baseRow - h * 0.55f) return shadow;
            if (!Inside(x, y - 1)) return light;
            if (!Inside(x, y - 2) && ((x + y) & 1) == 0) return light;
            if (!Inside(x - 1, y) && y < baseRow - band - 1) return light;
            return mid;
        }, new Vector2(0.5f, 0f), "Cloud");
    }

    /// <summary>Море облаков — бесшовная по ширине полоса (повторяется по кругу), низ уходит за экран.</summary>
    public static Sprite CloudSea(int seed, int w, int h, Color light, Color mid, Color shadow, Color deep)
    {
        var rng = new System.Random(seed);
        var top = new float[w];
        for (int x = 0; x < w; x++) top[x] = h;
        float cx = 0f;
        while (cx < w)
        {
            float r = 7f + (float)rng.NextDouble() * 9f;
            float cy = 4f + r + (float)rng.NextDouble() * 9f;
            for (int dx = -(int)r - 1; dx <= (int)r + 1; dx++)
            {
                float yy = r * r - dx * dx;
                if (yy < 0) continue;
                int x = ((Mathf.RoundToInt(cx) + dx) % w + w) % w;
                top[x] = Mathf.Min(top[x], cy - Mathf.Sqrt(yy));
            }
            cx += r * (1.0f + (float)rng.NextDouble() * 0.6f);
        }
        return Build(w, h, (x, y) =>
        {
            float d = y - top[x];
            if (d < 0f) return Clear;
            if (d < 1f) return light;
            if (d < 2f && ((x + y) & 1) == 0) return light;
            if (d < 9f) return mid;
            if (d < 11f) return Dither(x, y) < (d - 9f) / 2f ? shadow : mid;
            if (d < 20f) return shadow;
            if (d < 23f) return Dither(x, y) < (d - 20f) / 3f ? deep : shadow;
            return deep;
        }, new Vector2(0f, 1f), "CloudSea");
    }

    // ─────────────────────── камни, обломки ────────────────────────

    /// <summary>
    /// «Корень» острова под блоком 32×32: каменистый клин, свисающий под нижними гранями куба.
    /// Строки совпадают с картинкой блока (пивот как у блока), поэтому ставится дочерним объектом в (0,0).
    /// </summary>
    public static Sprite Rock(int seed)
    {
        var rng = new System.Random(seed);
        const int w = 32, h = 48;
        float depthMax = 9f + (float)rng.NextDouble() * 8f;
        var depth = new int[w];
        float wobble = (float)rng.NextDouble() * 6f;
        // Глубина меняется парами столбцов — крупнее и спокойнее, без «бахромы».
        var noise = new float[w / 2 + 1];
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)(rng.NextDouble() * 1.2 - 0.6);
        for (int c = 0; c < w; c++)
        {
            float e = Mathf.Abs((c | 1) - 15.5f) / 16f;
            float d = depthMax * (1f - Mathf.Pow(e, 1.3f)) + Mathf.Sin((c / 2) * 1.3f + wobble) * 1.2f + noise[c / 2];
            depth[c] = Mathf.Max(0, Mathf.RoundToInt(d));
        }
        // корешки
        var root = new int[w];
        int roots = rng.Next(3);
        for (int i = 0; i < roots; i++)
        {
            int c = 4 + rng.Next(w - 8);
            root[c] = 2 + rng.Next(5);
        }
        var speck = new bool[w * h];
        for (int i = 0; i < 18; i++) speck[rng.Next(w * h)] = true;

        int Edge(int c) => 24 + Mathf.Min(c, 31 - c) / 2; // нижний край куба на этом столбце
        int Bottom(int c) => c < 0 || c >= w ? -1 : Edge(c) + depth[c];

        return Build(w, h, (x, y) =>
        {
            int e = Edge(x), b = Bottom(x);
            if (y <= e) return Clear;
            if (y > b)
            {
                if (root[x] > 0 && y <= b + root[x]) return y == b + root[x] ? Ink : Root;
                return Clear;
            }
            bool left = x < 16;
            // контур: самый низ столбца и бока там, где сосед короче
            if (y == b || y > Bottom(x - 1) || y > Bottom(x + 1)) return Ink;
            float k = (y - e) / Mathf.Max(1f, depth[x]);
            bool deep = k > 0.55f || (k > 0.35f && Dither(x, y) < (k - 0.35f) / 0.2f);
            if (speck[y * w + x] && !deep) return left ? RockLeftSpeck : RockRightSpeck;
            if (left) return deep ? RockLeftDeep : RockLeft;
            return deep ? RockRightDeep : RockRight;
        }, new Vector2(0.5f, 0.5f), "IslandRock");
    }

    /// <summary>Маленький летающий кусочек острова: травка сверху, земля, каменный низ.</summary>
    public static Sprite Debris(int seed)
    {
        var rng = new System.Random(seed);
        int w = 8 + 2 * rng.Next(3);          // 8, 10, 12
        int q = w / 4;                          // половина высоты ромба
        int side = 3 + rng.Next(2);
        int tip = 3 + rng.Next(4);
        int h = q * 2 + side + q + tip + 2;
        float cx = (w - 1) / 2f;
        return Build(w, h, (x, y) =>
        {
            float dx = Mathf.Clamp01(Mathf.Abs(x - cx) / (w / 2f));
            float top = q * dx;                                          // верхний край ромба
            float topFace = q * (2f - dx);                               // нижний край верхней грани
            float sideBottom = topFace + side;
            float bottom = q + side + q * (1f - dx) + tip * Mathf.Pow(1f - dx, 1.5f);
            float fy = y + 0.5f;
            if (fy < top || fy > bottom) return Clear;
            if (fy < top + 1f || fy > bottom - 1f || x == 0 || x == w - 1) return Ink;
            if (fy < topFace) return fy > topFace - 1f ? GrassDark : Grass;
            bool left = x < cx;
            if (fy < sideBottom) return fy < topFace + 1f ? GrassDark : (left ? DirtLeft : DirtRight);
            return left ? RockLeft : RockRight;
        }, new Vector2(0.5f, 0.5f), "IslandDebris");
    }

    // ───────────────────────── тени, свет ──────────────────────────

    /// <summary>Мягкая изометрическая тень-эллипс 2:1 (белая — цвет и прозрачность задаёт SpriteRenderer).</summary>
    public static Sprite ShadowBlob(int w, int h)
    {
        float cx = (w - 1) / 2f, cy = (h - 1) / 2f, rx = w / 2f, ry = h / 2f;
        return Build(w, h, (x, y) =>
        {
            float ex = (x - cx) / rx, ey = (y - cy) / ry;
            float e = ex * ex + ey * ey;
            if (e < 0.5f) return new Color32(255, 255, 255, 255);
            if (e < 1f && Dither(x, y) < (1f - e) / 0.5f) return new Color32(255, 255, 255, 255);
            return Clear;
        }, new Vector2(0.5f, 0.5f), "ShadowBlob");
    }

    /// <summary>Тень от соседнего (более высокого) блока на верхней грани — полоса вдоль левого верхнего края.</summary>
    public static Sprite EdgeShadow()
    {
        const int w = 32, h = 32;
        return Build(w, h, (x, y) =>
        {
            // верхняя грань — ромб с центром (15.5, 7.5), полуоси 16 и 8
            float dx = (x + 0.5f - 16f) / 16f, dy = (y + 0.5f - 8f) / 8f;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1f) return Clear;
            // расстояние до левого верхнего края ромба (от левой вершины к верхней)
            float dist = (1f + dx + dy) * 8f / 1.4142f;     // в «пикселях»
            if (dist < 3.5f) return new Color32(255, 255, 255, 255);
            if (dist < 6f && Dither(x, y) < (6f - dist) / 2.5f) return new Color32(255, 255, 255, 255);
            return Clear;
        }, new Vector2(0.5f, 0.25f), "EdgeShadow");
    }

    /// <summary>Круглое свечение полосами (для кристаллов).</summary>
    public static Sprite Glow(int size)
    {
        float c = (size - 1) / 2f, r = size / 2f;
        return Build(size, size, (x, y) =>
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
            if (d >= 1f) return Clear;
            float a = Mathf.Pow(1f - d, 1.4f);
            float q = Mathf.Floor(a * 4f + Dither(x, y)) / 4f;
            return new Color32(255, 255, 255, (byte)(Mathf.Clamp01(q) * 150));
        }, new Vector2(0.5f, 0.5f), "Glow");
    }

    /// <summary>Один пиксель (пылинки).</summary>
    public static Sprite Pixel()
    {
        return Build(1, 1, (x, y) => new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f), "Pixel");
    }
}
