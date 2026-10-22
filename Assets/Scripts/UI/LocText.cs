using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Текст, который сам переводится при смене языка (Loc.Changed).
///
/// • Тексты, созданные кодом: LocText.Bind(text, "Play").
/// • Текст, который считается функцией (например, название текущего языка): LocText.Bind(text, () => ...).
/// • Тексты сцены: при загрузке сцены к знакомым надписям (SceneTexts) компонент добавляется сам —
///   в сцене ничего настраивать не нужно.
///
/// fit: true — шрифт уменьшается, если перевод не влезает (русские слова бывают длиннее).
/// </summary>
[DisallowMultipleComponent]
public class LocText : MonoBehaviour
{
    [Tooltip("Исходный текст по-английски (ключ перевода, см. Loc)")]
    public string source;

    Func<string> _getter;
    TMP_Text _text;

    /// <summary>Надписи сцены, которые переводятся автоматически.</summary>
    static readonly HashSet<string> SceneTexts = new HashSet<string>
    {
        "Your task is to assemble a combination of buttons in order to get a crystal.",
        "Start", "Close", "Play", "Save",
    };

    public static LocText Bind(TMP_Text text, string englishSource, bool fit = false)
    {
        var l = Attach(text, fit);
        if (l == null) return null;
        l._getter = null;
        l.source = englishSource;
        l.Apply();
        return l;
    }

    public static LocText Bind(TMP_Text text, Func<string> getter, bool fit = false)
    {
        var l = Attach(text, fit);
        if (l == null) return null;
        l._getter = getter;
        l.Apply();
        return l;
    }

    static LocText Attach(TMP_Text text, bool fit)
    {
        if (text == null) return null;
        var l = text.GetComponent<LocText>();
        if (l == null) l = text.gameObject.AddComponent<LocText>();
        l._text = text;
        if (fit && !text.enableAutoSizing)
        {
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Max(8f, text.fontSize * 0.5f);
            text.enableAutoSizing = true;
        }
        return l;
    }

    void OnEnable()
    {
        Loc.Changed += Apply;
        Apply();
    }

    void OnDisable()
    {
        Loc.Changed -= Apply;
    }

    public void Apply()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
        if (_text == null) return;
        if (_getter != null) _text.text = _getter();
        else if (!string.IsNullOrEmpty(source)) _text.text = Loc.T(source);
    }

    // ───────────── тексты сцены ─────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindSceneTexts();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindSceneTexts();

    static void BindSceneTexts()
    {
        foreach (var t in FindObjectsOfType<TMP_Text>(true))
        {
            if (t == null || t.GetComponent<LocText>() != null) continue;
            string s = t.text != null ? t.text.Trim() : "";
            if (SceneTexts.Contains(s)) Bind(t, s, fit: true);
        }
    }
}
