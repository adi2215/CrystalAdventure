using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Цветной блок на карте: трава на блоке красного / синего / жёлтого цвета, блок мягко «дышит».
///
/// Перекрашенные блоки — готовые картинки в Resources/ColorTiles (CubesForGame_Red.png и т.д.)
/// с той же нарезкой, что у оригинального листа: спрайт подменяется по имени, поэтому
/// положение и размер совпадают один в один. Если для листа нет перекрашенной картинки —
/// блок просто тонируется цветом.
/// </summary>
public class ColorTilePulse : MonoBehaviour
{
    SpriteRenderer _sr;
    Color _base;
    float _phase;

    public static ColorTilePulse Attach(SpriteRenderer sr, TileColor color)
    {
        if (sr == null || color == TileColor.None) return null;
        var p = sr.GetComponent<ColorTilePulse>();
        if (p == null) p = sr.gameObject.AddComponent<ColorTilePulse>();
        p._sr = sr;
        p._phase = (sr.transform.position.x + sr.transform.position.y) * 2f;

        var recolored = ColorTileSkin.Find(sr.sprite, color);
        if (recolored != null)
        {
            sr.sprite = recolored;
            p._base = Color.white;
        }
        else p._base = ColorTiles.Tint(color);
        sr.color = new Color(p._base.r, p._base.g, p._base.b, sr.color.a);
        return p;
    }

    void Update()
    {
        if (_sr == null) return;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f + _phase);
        var c = Color.Lerp(_base * 0.85f, _base, k);
        c.a = _sr.color.a;
        _sr.color = c;
    }
}

public static class ColorTileSkin
{
    const string Folder = "ColorTiles/";
    static readonly Dictionary<string, Dictionary<string, Sprite>> Sheets = new Dictionary<string, Dictionary<string, Sprite>>();

    /// <summary>
    /// Перекрашенный вариант спрайта блока: «CubesForGame_3» → Resources/ColorTiles/CubesForGame_Red → спрайт «CubesForGame_3».
    /// null — такого листа нет.
    /// </summary>
    public static Sprite Find(Sprite src, TileColor color)
    {
        if (src == null || color == TileColor.None) return null;
        string name = src.name.Replace("(Clone)", "").Trim();
        int cut = name.LastIndexOf('_');
        if (cut <= 0) return null;
        string sheet = name.Substring(0, cut) + "_" + color;

        if (!Sheets.TryGetValue(sheet, out var sprites))
        {
            sprites = new Dictionary<string, Sprite>();
            foreach (var s in Resources.LoadAll<Sprite>(Folder + sheet))
                if (s != null) sprites[s.name] = s;
            Sheets[sheet] = sprites;
            if (sprites.Count == 0) Debug.Log($"[ColorTiles] Нет перекрашенного листа Resources/{Folder}{sheet} — блок будет просто тонирован.");
        }
        return sprites.TryGetValue(name, out var found) ? found : null;
    }
}
