using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Маленькие пиксельные эффекты без ассетов: искры (кристалл, звёзды), пыль при приземлении.
/// Спрайты рисуются кодом в стиле игры (Point-фильтр).
/// </summary>
public class PixelFX : MonoBehaviour
{
    static Sprite _sparkle, _dot;

    public static readonly Color[] CrystalColors =
    {
        new Color32(250, 226, 252, 255), new Color32(236, 188, 244, 255), new Color32(214, 150, 232, 255), Color.white,
    };
    public static readonly Color[] StarColors =
    {
        new Color32(255, 236, 120, 255), new Color32(255, 214, 64, 255), new Color32(255, 255, 255, 255), new Color32(255, 170, 60, 255),
    };
    public static readonly Color[] DustColors =
    {
        new Color32(225, 253, 82, 255), new Color32(194, 248, 62, 255), new Color32(176, 92, 104, 255), new Color32(230, 230, 210, 255),
    };

    /// <summary>Искорка-«плюсик» 5×5.</summary>
    public static Sprite Sparkle
    {
        get
        {
            if (_sparkle != null) return _sparkle;
            string[] rows = { "..#..", "..#..", "#####", "..#..", "..#.." };
            var tex = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    tex.SetPixel(x, y, rows[4 - y][x] == '#' ? Color.white : Color.clear);
            tex.Apply();
            _sparkle = Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 100f);
            return _sparkle;
        }
    }

    /// <summary>Квадратик 2×2 — пыль, камешки.</summary>
    public static Sprite Dot
    {
        get
        {
            if (_dot != null) return _dot;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            return _dot;
        }
    }

    // ───────────── в мире (SpriteRenderer) ─────────────

    /// <summary>Разлёт частиц в мире. up — насколько летят вверх, gravity — как быстро падают.</summary>
    public static void Burst(Vector3 pos, Color[] colors, int count, float speed, float up, float gravity,
                             int sortingLayerID, int sortingOrder, Sprite sprite = null, float scale = 1f, float life = 0.8f)
    {
        var go = new GameObject("PixelBurst");
        go.transform.position = pos;
        var fx = go.AddComponent<PixelFX>();
        fx.StartCoroutine(fx.RunWorld(colors, count, speed, up, gravity, sortingLayerID, sortingOrder, sprite ?? Sparkle, scale, life));
    }

    IEnumerator RunWorld(Color[] colors, int count, float speed, float up, float gravity, int layer, int order, Sprite sprite, float scale, float life)
    {
        var parts = new SpriteRenderer[count];
        var vel = new Vector3[count];
        var spin = new float[count];
        for (int i = 0; i < count; i++)
        {
            var sr = new GameObject("p").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sprite;
            sr.color = colors[i % colors.Length];
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
            sr.transform.localScale = Vector3.one * scale * Random.Range(0.7f, 1.3f);
            float a = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
            vel[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, 0f) * speed * Random.Range(0.6f, 1.2f) + Vector3.up * up;
            spin[i] = Random.Range(-360f, 360f);
            parts[i] = sr;
        }
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;
            for (int i = 0; i < count; i++)
            {
                vel[i] += Vector3.down * gravity * Time.deltaTime;
                vel[i] *= 1f - 1.5f * Time.deltaTime; // воздух
                parts[i].transform.localPosition += vel[i] * Time.deltaTime;
                parts[i].transform.localRotation = Quaternion.Euler(0f, 0f, spin[i] * t);
                var c = parts[i].color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                parts[i].color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ───────────── в интерфейсе (Image) ─────────────

    /// <summary>Разлёт искр в UI вокруг элемента (например, звезды). Скорость — в пикселях интерфейса.</summary>
    public static void BurstUI(RectTransform around, Color[] colors, int count, float speed, float size = 14f, float life = 0.7f)
    {
        if (around == null) return;
        var go = new GameObject("UIBurst", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(around.parent, false);
        rt.anchorMin = around.anchorMin;
        rt.anchorMax = around.anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = around.anchoredPosition;
        rt.sizeDelta = Vector2.zero;
        rt.SetAsLastSibling();
        var fx = go.AddComponent<PixelFX>();
        fx.StartCoroutine(fx.RunUI(colors, count, speed, size, life));
    }

    IEnumerator RunUI(Color[] colors, int count, float speed, float size, float life)
    {
        var parts = new Image[count];
        var vel = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            var img = new GameObject("s", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(transform, false);
            img.sprite = Sparkle;
            img.raycastTarget = false;
            img.color = colors[i % colors.Length];
            float s = size * Random.Range(0.7f, 1.3f);
            img.rectTransform.sizeDelta = new Vector2(s, s);
            float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.6f, 1.2f);
            parts[i] = img;
        }
        for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
        {
            float k = t / life;
            for (int i = 0; i < count; i++)
            {
                vel[i] *= 1f - 3f * Time.unscaledDeltaTime;
                parts[i].rectTransform.anchoredPosition += vel[i] * Time.unscaledDeltaTime;
                parts[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 200f * t * (i % 2 == 0 ? 1f : -1f));
                var c = parts[i].color;
                c.a = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
                parts[i].color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
