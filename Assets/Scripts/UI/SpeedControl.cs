using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка скорости ×1 / ×2 / ×4 — ускоряет выполнение программы.
///
/// Создаётся кодом (MovingManager.Start → SpeedControl.Attach), в сцене ничего настраивать не нужно.
/// Ставится над кнопкой подсказки «?» и выглядит так же; если «?» нет — над правым краем Play.
/// Лежит поверх панели-блокировщика, поэтому скорость можно переключать прямо во время прогона.
///
/// Как ускоряет: на время прогона (MovingManager.State == Playing) Time.timeScale = скорость.
/// Весь UI проекта (карточки, скобки циклов, туториал, музыка, кнопки) уже идёт по unscaledTime,
/// поэтому ускоряется только мир: шаги и прыжки куба, паузы между шагами, эффекты на поле.
/// Победа (State = Finished), Rewind и окно звёзд — снова ×1. Выбор запоминается (PlayerPrefs).
/// </summary>
public class SpeedControl : MonoBehaviour
{
    static readonly float[] Speeds = { 1f, 2f, 4f };
    const string PrefKey = "crystal.speed";

    MovingManager _moving;
    TextMeshProUGUI _label;
    int _index;

    /// <summary>Текущая скорость прогона (1, 2 или 4).</summary>
    public static float Current { get; private set; } = 1f;

    public static SpeedControl Attach(MovingManager moving)
    {
        if (moving == null) return null;
        var existing = FindObjectOfType<SpeedControl>(true);
        if (existing != null) { existing._moving = moving; return existing; }

        var hint = FindObjectOfType<HintSystem>(true);
        var anchor = hint != null ? (RectTransform)hint.transform : moving.Play != null ? (RectTransform)moving.Play.transform : null;
        if (anchor == null || anchor.parent == null) return null;

        var go = new GameObject("SpeedButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = anchor.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(anchor.parent, false);

        if (hint != null)
        {
            // Тот же размер и вид, что у «?», на одну кнопку выше.
            rt.anchorMin = anchor.anchorMin;
            rt.anchorMax = anchor.anchorMax;
            rt.pivot = anchor.pivot;
            rt.sizeDelta = anchor.sizeDelta;
            rt.anchoredPosition = anchor.anchoredPosition + new Vector2(0f, anchor.rect.height + 12f);
        }
        else
        {
            // Нет «?» — квадрат высотой с Play над его правым краем (как HintButtonCreator).
            rt.anchorMin = rt.anchorMax = anchor.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            float size = Mathf.Max(40f, anchor.rect.height);
            rt.sizeDelta = new Vector2(size, size);
            rt.localPosition = anchor.localPosition + new Vector3(anchor.rect.xMax - size * 0.5f, anchor.rect.yMax + size * 0.5f + 8f, 0f);
        }
        rt.SetAsLastSibling(); // поверх панели-блокировщика

        var image = go.GetComponent<Image>();
        var hintImage = hint != null ? hint.GetComponent<Image>() : null;
        if (hintImage != null)
        {
            image.sprite = hintImage.sprite;
            image.type = hintImage.type;
            image.pixelsPerUnitMultiplier = hintImage.pixelsPerUnitMultiplier;
            image.color = hintImage.color;
        }
        else image.color = new Color(0.40f, 0.34f, 0.50f, 1f);

        var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = go.layer;
        var lrt = (RectTransform)label.transform;
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var text = label.GetComponent<TextMeshProUGUI>();
        var hintText = hint != null ? hint.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (hintText != null) text.font = hintText.font;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10;
        text.fontSizeMax = 200;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;

        var control = go.AddComponent<SpeedControl>();
        control._moving = moving;
        control._label = text;
        go.GetComponent<Button>().onClick.AddListener(control.Next);
        ButtonJuice.Ensure(go);
        return control;
    }

    void Awake()
    {
        _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Speeds.Length - 1);
        Current = Speeds[_index];
    }

    void Start() => Refresh();

    /// <summary>Следующая скорость по кругу: ×1 → ×2 → ×4 → ×1.</summary>
    public void Next()
    {
        _index = (_index + 1) % Speeds.Length;
        Current = Speeds[_index];
        PlayerPrefs.SetInt(PrefKey, _index);
        PixelSfx.Play(Sfx.Click, 0.8f, 1f + 0.15f * _index);
        Refresh();
    }

    void Refresh()
    {
        if (_label != null) _label.text = "x" + Current.ToString("0");
    }

    void Update()
    {
        // Ускоряем только прогон; всё остальное (вступление, победа, окна) — в обычном темпе.
        float target = _moving != null && _moving.IsPlaying ? Current : 1f;
        if (!Mathf.Approximately(Time.timeScale, target)) Time.timeScale = target;
    }

    void OnDisable() => Time.timeScale = 1f;
    void OnDestroy() => Time.timeScale = 1f;
}
