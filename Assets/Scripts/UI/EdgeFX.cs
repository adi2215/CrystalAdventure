using System.Collections;
using UnityEngine;

/// <summary>
/// Эффект «чуть не упал» на краю: пиксельный «!» над кубом, камешки сыплются с края,
/// камера коротко вздрагивает. Спрайты рисуются кодом (пиксель-арт), ассеты не нужны.
/// </summary>
public class EdgeFX : MonoBehaviour
{
    static Sprite _bang, _pebble;

    static readonly Color Outline = new Color32(66, 38, 54, 255);
    static readonly Color Fill = new Color32(255, 214, 64, 255);
    static readonly Color[] PebbleColors =
    {
        new Color32(176, 92, 104, 255),  // «земля» сбоку клетки
        new Color32(130, 66, 82, 255),
        new Color32(194, 248, 62, 255),  // трава сверху
    };

    public static void Play(MonoBehaviour owner, Vector3 edgePoint, Vector3 direction, SpriteRenderer hero)
    {
        var go = new GameObject("EdgeFX");
        var fx = go.AddComponent<EdgeFX>();
        fx.StartCoroutine(fx.Run(owner != null ? owner.transform : null, edgePoint, direction, hero));
    }

    IEnumerator Run(Transform heroTransform, Vector3 edgePoint, Vector3 direction, SpriteRenderer hero)
    {
        int order = hero != null ? hero.sortingOrder : 0;
        int layer = hero != null ? hero.sortingLayerID : 0;
        float top = hero != null ? hero.bounds.max.y : (heroTransform != null ? heroTransform.position.y + 0.3f : edgePoint.y + 0.3f);

        // «!» над головой
        var bang = new GameObject("Bang").AddComponent<SpriteRenderer>();
        bang.transform.SetParent(transform, false);
        bang.sprite = BangSprite();
        bang.sortingLayerID = layer;
        bang.sortingOrder = order + 20;
        Vector3 bangBase = new Vector3(heroTransform != null ? heroTransform.position.x : edgePoint.x, top + 0.04f, edgePoint.z);

        // камешки
        const int count = 6;
        var pebbles = new SpriteRenderer[count];
        var vel = new Vector3[count];
        Vector3 outward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.down;
        for (int i = 0; i < count; i++)
        {
            var p = new GameObject("Pebble").AddComponent<SpriteRenderer>();
            p.transform.SetParent(transform, false);
            p.sprite = PebbleSprite();
            p.color = PebbleColors[i % PebbleColors.Length];
            p.sortingLayerID = layer;
            p.sortingOrder = order + 2;
            p.transform.position = edgePoint + new Vector3(Random.Range(-0.03f, 0.03f), Random.Range(-0.02f, 0.01f), 0f);
            p.transform.localScale = Vector3.one * Random.Range(0.8f, 1.6f);
            vel[i] = outward * Random.Range(0.15f, 0.35f) + new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.05f, 0.25f), 0f);
            pebbles[i] = p;
        }

        var cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 lastShake = Vector3.zero;

        const float life = 1.1f;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;

            // «!»: выскакивает с отскоком, покачивается, в конце тает
            float pop = k < 0.12f ? Mathf.SmoothStep(0f, 1.25f, k / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.12f) / 0.1f));
            bang.transform.position = bangBase + Vector3.up * (0.02f * Mathf.Sin(t * 12f));
            bang.transform.localScale = Vector3.one * pop;
            bang.transform.rotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(t * 18f) * (1f - k));
            bang.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);

            // камешки падают с края
            for (int i = 0; i < count; i++)
            {
                vel[i] += Vector3.down * (2.2f * Time.deltaTime);
                pebbles[i].transform.position += vel[i] * Time.deltaTime;
                var c = pebbles[i].color;
                c.a = Mathf.Clamp01(1f - k * 1.2f);
                pebbles[i].color = c;
            }

            // короткое вздрагивание камеры (смещение добавляется и снимается, не мешая её движению)
            if (cam != null)
            {
                Vector3 shake = t < 0.18f ? (Vector3)(Random.insideUnitCircle * 0.012f * (1f - t / 0.18f)) : Vector3.zero;
                cam.position += shake - lastShake;
                lastShake = shake;
            }
            yield return null;
        }
        if (cam != null) cam.position -= lastShake;
        Destroy(gameObject);
    }

    static Sprite BangSprite()
    {
        if (_bang != null) return _bang;
        // 5×13 пикселей, сверху вниз; # — контур, Y — заливка
        string[] rows =
        {
            ".###.",
            "#YYY#",
            "#YYY#",
            "#YYY#",
            "#YYY#",
            ".#Y#.",
            ".#Y#.",
            "..#..",
            ".....",
            ".###.",
            "#YYY#",
            "#YYY#",
            ".###.",
        };
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char ch = rows[h - 1 - y][x];
                tex.SetPixel(x, y, ch == '#' ? Outline : ch == 'Y' ? Fill : Color.clear);
            }
        tex.Apply();
        _bang = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 80f);
        return _bang;
    }

    static Sprite PebbleSprite()
    {
        if (_pebble != null) return _pebble;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        _pebble = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        return _pebble;
    }
}
