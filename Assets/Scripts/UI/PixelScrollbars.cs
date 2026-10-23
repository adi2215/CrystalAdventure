using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Полосы прокрутки консоли (под рядом команд) — в пиксельном стиле игры.
///
/// Исправлено (по отзыву автора): стандартная серая «таблетка» Unity не подходила к пиксельной консоли.
/// Теперь дорожка — тёмная утопленная полоска с контуром, ползунок — сиреневая плашка с бликом,
/// пиксель — как у рамки консоли (6 единиц канваса, подогнано до целых пикселей экрана), высота — 3 пикселя.
/// Делается во время игры для всех Scrollbar на экранных канвасах — сцену менять не нужно.
/// </summary>
public static class PixelScrollbars
{
    const float ArtPixel = 6f;
    static Sprite _track, _handle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Apply();

    static void Apply()
    {
        foreach (var bar in Object.FindObjectsOfType<Scrollbar>(true))
        {
            var canvas = bar.GetComponentInParent<Canvas>(true);
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) continue;
            float sf = canvas.rootCanvas.scaleFactor > 0.0001f ? canvas.rootCanvas.scaleFactor : 1f;
            float u = Mathf.Max(1f, Mathf.Round(ArtPixel * sf)) / sf; // пиксель в единицах канваса

            var rt = (RectTransform)bar.transform;
            bool horizontal = bar.direction == Scrollbar.Direction.LeftToRight || bar.direction == Scrollbar.Direction.RightToLeft;
            if (horizontal && rt.anchorMin.y == rt.anchorMax.y) rt.sizeDelta = new Vector2(rt.sizeDelta.x, 3f * u);
            if (!horizontal && rt.anchorMin.x == rt.anchorMax.x) rt.sizeDelta = new Vector2(3f * u, rt.sizeDelta.y);

            Style(bar.GetComponent<Image>(), Track, u);
            if (bar.handleRect != null) Style(bar.handleRect.GetComponent<Image>(), Handle, u);
        }
    }

    static void Style(Image img, Sprite sprite, float u)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / u;
        img.color = Color.white;
        PixelSnapUI.Exclude(img);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    /// <summary>Дорожка: тёмная утопленная полоса с контуром. Sliced, рамка 1 px.</summary>
    static Sprite Track => _track != null ? _track : _track = PixelSprites.Make(new[]
    {
        "###",
        "#k#",
        "###",
    }, new System.Collections.Generic.Dictionary<char, Color> { { '#', PixelSprites.Ink }, { 'k', Hex("#262A4D") } }, new Vector4(1, 1, 1, 1));

    /// <summary>Ползунок: сиреневая плашка, сверху блик. Sliced, рамка 1 px.</summary>
    static Sprite Handle => _handle != null ? _handle : _handle = PixelSprites.Make(new[]
    {
        "#####",
        "#lhl#",
        "#####",
    }, new System.Collections.Generic.Dictionary<char, Color> { { '#', PixelSprites.Ink }, { 'l', Hex("#B9B3E6") }, { 'h', Hex("#D9D6F5") } }, new Vector4(2, 1, 2, 1));
}
