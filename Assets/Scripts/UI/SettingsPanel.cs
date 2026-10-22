using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно настроек: громкость музыки (GameMusic.Volume) и звуков (PixelSfx.Volume), по 10 делений,
/// и язык (English / Русский — Loc; тексты окна и игры меняются сразу).
/// Создаётся кодом поверх всего (свой холст), значения сразу сохраняются в PlayerPrefs.
/// Открывается с титульного экрана (StoryDirector); можно звать откуда угодно: SettingsPanel.Open().
///
/// Делитель громкости — пиксельная полоска из 10 квадратиков: тап по квадратику ставит громкость,
/// «−» / «+» — на одно деление (до нуля — звук выключен).
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    const int Steps = 10;
    static readonly Color Ink = PixelSprites.Ink;
    static readonly Color On = new Color(1f, 0.82f, 0.3f, 1f);
    static readonly Color Off = new Color(0.55f, 0.48f, 0.62f, 1f);

    public bool IsOpen { get; private set; }

    TMP_FontAsset _font;
    CanvasGroup _group;
    RectTransform _window;

    /// <summary>Открыть окно (sortingOrder — над чем показывать; титульный экран — 600).</summary>
    public static SettingsPanel Open(int sortingOrder = 700)
    {
        var go = new GameObject("SettingsPanel");
        var panel = go.AddComponent<SettingsPanel>();
        panel.Build(sortingOrder);
        return panel;
    }

    void Build(int sortingOrder)
    {
        IsOpen = true;
        _font = GameFont.Main;
        var canvasGo = new GameObject("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = NewRect("Root", canvasGo.transform);
        Stretch(root);
        _group = root.gameObject.AddComponent<CanvasGroup>();

        // Затемнение — тап мимо окна закрывает его.
        var dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0.08f, 0.04f, 0.14f, 0.6f);
        var dimBtn = root.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        _window = NewRect("Window", root);
        Place(_window, Vector2.zero, new Vector2(980f, 730f));
        var bg = _window.gameObject.AddComponent<Image>();
        bg.sprite = PixelSprites.Panel;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 0.2f;
        _window.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // клик по окну не закрывает

        var title = Text(_window, "Settings", 64f);
        Place(title.rectTransform, new Vector2(0f, 285f), new Vector2(800f, 90f));
        LocText.Bind(title, "Settings", fit: true);

        VolumeRow(new Vector2(0f, 145f), "Music", () => GameMusic.Volume, v => GameMusic.Volume = v, false);
        VolumeRow(new Vector2(0f, 5f), "Sounds", () => PixelSfx.Volume, v => PixelSfx.Volume = v, true);
        LanguageRow(new Vector2(0f, -135f));

        var close = Button(_window, "Close", new Vector2(0f, -280f), new Vector2(300f, 96f), On, Close);
        LocText.Bind(close.GetComponentInChildren<TextMeshProUGUI>(), "Close", fit: true);
        ButtonJuice.Ensure(close, true);

        PixelSfx.Play(Sfx.Open, 0.8f);
        StartCoroutine(Appear());
    }

    void VolumeRow(Vector2 pos, string label, System.Func<float> get, System.Action<float> set, bool beep)
    {
        var row = NewRect(label, _window);
        Place(row, pos, new Vector2(900f, 100f));

        var name = Text(row, label, 46f);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        Place(name.rectTransform, new Vector2(-300f, 0f), new Vector2(260f, 90f));
        LocText.Bind(name, label, fit: true);

        var segs = new Image[Steps];
        void Refresh()
        {
            int level = Mathf.RoundToInt(get() * Steps);
            for (int i = 0; i < Steps; i++) segs[i].color = i < level ? On : Off;
        }
        void SetLevel(int level)
        {
            level = Mathf.Clamp(level, 0, Steps);
            set(level / (float)Steps);
            PlayerPrefs.Save();
            Refresh();
            if (beep) PixelSfx.Play(Sfx.Blip, 1f, 0.8f + level * 0.05f, 0f); // слышно новую громкость
            else PixelSfx.Play(Sfx.Click, 0.6f);
        }

        Button(row, "-", new Vector2(-110f, 0f), new Vector2(72f, 72f), Off, () => SetLevel(Mathf.RoundToInt(get() * Steps) - 1));
        for (int i = 0; i < Steps; i++)
        {
            int index = i;
            var seg = NewRect("Seg" + i, row);
            Place(seg, new Vector2(-40f + i * 38f, 0f), new Vector2(30f, 54f));
            var img = seg.gameObject.AddComponent<Image>();
            img.sprite = PixelSprites.Tag;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.2f;
            var b = seg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            // Тап по горящему последнему делению — выключить его (так можно дойти до нуля).
            b.onClick.AddListener(() => SetLevel(Mathf.RoundToInt(get() * Steps) == index + 1 ? index : index + 1));
            segs[i] = img;
        }
        Button(row, "+", new Vector2(370f, 0f), new Vector2(72f, 72f), Off, () => SetLevel(Mathf.RoundToInt(get() * Steps) + 1));
        Refresh();
    }

    /// <summary>Язык: кнопка с названием текущего языка, тап — другой язык.</summary>
    void LanguageRow(Vector2 pos)
    {
        var row = NewRect("Language", _window);
        Place(row, pos, new Vector2(900f, 100f));

        var name = Text(row, "Language", 46f);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        Place(name.rectTransform, new Vector2(-300f, 0f), new Vector2(260f, 90f));
        LocText.Bind(name, "Language", fit: true);

        var button = Button(row, Loc.CurrentName, new Vector2(130f, 0f), new Vector2(440f, 84f), On, () =>
        {
            Loc.Toggle();
            PixelSfx.Play(Sfx.Click, 0.8f, Loc.IsRussian ? 0.9f : 1.1f);
        });
        LocText.Bind(button.GetComponentInChildren<TextMeshProUGUI>(), () => Loc.CurrentName, fit: true);
        ButtonJuice.Ensure(button, true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        PixelSfx.Play(Sfx.Close, 0.8f);
        StartCoroutine(Disappear());
    }

    IEnumerator Appear()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
        {
            _group.alpha = t;
            _window.localScale = Vector3.one * BackOut(t);
            yield return null;
        }
        _group.alpha = 1f;
        _window.localScale = Vector3.one;
    }

    IEnumerator Disappear()
    {
        _group.blocksRaycasts = false;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
        {
            _group.alpha = 1f - t;
            yield return null;
        }
        Destroy(gameObject);
    }

    // ───────────── помощники ─────────────

    GameObject Button(RectTransform parent, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var rt = NewRect(label + "Button", parent);
        Place(rt, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = PixelSprites.Tag;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.2f;
        img.color = color;
        rt.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
        var t = Text(rt, label, size.y * 0.55f);
        Stretch(t.rectTransform);
        return rt.gameObject;
    }

    TextMeshProUGUI Text(RectTransform parent, string text, float size)
    {
        var rt = NewRect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = Ink;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    static float BackOut(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.6f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
