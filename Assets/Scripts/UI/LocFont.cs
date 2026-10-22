using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Русские буквы тем же шрифтом, что и остальной текст.
///
/// Шрифт игры (SourceCodePro-Bold SDF, GameFont.Main) — статический атлас только с латиницей.
/// Без этого кириллица бралась бы из запасного шрифта TMP (LiberationSans) — другого начертания.
/// При старте из того же TTF (копия в Resources/Fonts) создаётся динамический шрифт и добавляется
/// основному в запасные: недостающие буквы дорисуются сами, тем же начертанием.
/// В редакторе запасной шрифт убирается при выходе из Play, чтобы не попасть в ассет.
/// </summary>
public static class LocFont
{
    const string FontPath = "Fonts/SourceCodePro-Bold";

    static TMP_FontAsset _cyrillic;
    static bool _tried;
    static readonly List<TMP_FontAsset> Patched = new List<TMP_FontAsset>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting -= Cleanup;
        Application.quitting += Cleanup;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

    public static void Ensure()
    {
        var main = GameFont.Main;
        if (main == null || main == TMP_Settings.defaultFontAsset) return;
        if (main.HasCharacter('Я')) return; // в атласе уже есть кириллица

        if (_cyrillic == null && !_tried)
        {
            _tried = true;
            var font = Resources.Load<Font>(FontPath);
            if (font == null)
            {
                Debug.LogWarning("[LocFont] Нет Resources/" + FontPath + ".ttf — русские буквы возьмутся из запасного шрифта TMP.");
                return;
            }
            _cyrillic = TMP_FontAsset.CreateFontAsset(font);
            if (_cyrillic != null) _cyrillic.name = "SourceCodePro-Bold Cyrillic (runtime)";
        }
        if (_cyrillic == null) return;

        if (main.fallbackFontAssetTable == null) main.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!main.fallbackFontAssetTable.Contains(_cyrillic))
        {
            main.fallbackFontAssetTable.Insert(0, _cyrillic);
            if (!Patched.Contains(main)) Patched.Add(main);
        }
    }

    static void Cleanup()
    {
        foreach (var f in Patched)
            if (f != null && f.fallbackFontAssetTable != null) f.fallbackFontAssetTable.Remove(_cyrillic);
        Patched.Clear();
    }
}
