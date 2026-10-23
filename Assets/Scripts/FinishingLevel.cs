using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно победы.
///   1. Окно выпрыгивает.
///   2. Звёзды «впечатываются» по одной: увеличиваются, крутятся, разлетаются искры.
///      Незаработанные — серые, коротко вздрагивают.
///   3. Ниже — строки итогов на пиксельных плашках: у каждой иконка, текст печатается,
///      справа впечатывается зелёный «+» или красный «−»:
///      карточки (≤ оптимума?), подсказки (не брал?), край (ни разу не чуть не упал?).
///   Звёзды крупнее, стоят в «гнёздах», за ними расходятся лучи (ярче с каждой звездой).
///   4. Появляется кнопка «Дальше».
/// Клик/тап во время анимации — сразу показать всё.
///
/// Звёзды: если у уровня посчитан оптимум (Редактор уровней → «Решить все уровни»):
///   3★ ≤ оптимум, 2★ ≤ оптимум + запас, иначе 1★. Иначе — общие пороги из инспектора.
///   Если брали подсказку — не больше maxStarsWithHints.
/// </summary>
public class FinishingLevel : MonoBehaviour
{
    [Header("UI Звезды")]
    public Image[] stars;

    public Data data;

    [Header("Если у уровня нет оптимума (количество карточек)")]
    [Tooltip("≤ этого — 3 звезды")] public int threeStarsMax = 5;
    [Tooltip("≤ этого — 2 звезды, иначе 1")] public int twoStarsMax = 7;

    [Header("Звёзды от оптимума уровня")]
    [Tooltip("Запас для 2 звёзд: оптимум + max(minSlack, оптимум × slackPercent)")]
    public int minSlack = 2;
    [Range(0f, 1f)] public float slackPercent = 0.3f;

    [Header("Подсказки")]
    [Range(1, 3)] public int maxStarsWithHints = 2;

    [Header("Анимация")]
    [Tooltip("Пауза между звёздами, сек")] public float starInterval = 0.45f;
    [Tooltip("Скорость печати текста, символов в секунду")] public float typeSpeed = 40f;
    public Color emptyStarColor = new Color(0.35f, 0.32f, 0.42f, 0.6f);
    public Color plusColor = new Color(0.45f, 0.95f, 0.35f, 1f);
    public Color minusColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Header("Вид")]
    [Tooltip("Во сколько раз увеличить звёзды")] public float starScale = 1.1f;
    [Tooltip("Тёмные «гнёзда» под звёздами")] public bool starSockets = true;
    [Tooltip("Лучи за звёздами")] public bool starRays = true;
    [Tooltip("Ширина строк итогов")] public float resultWidth = 500f;
    [Tooltip("Высота строки итогов")] public float resultRowHeight = 42f;
    [Tooltip("Отступ строк итогов от ленты со звёздами")] public float resultGap = 12f;

    [Header("Пиксели (по отзыву автора)")]
    [Tooltip("Размер пикселя рисунков окна (кристаллы, строки итогов, значки) в единицах канваса — как у рамки консоли")]
    public float artPixel = 6f;
    [Tooltip("Старое оформление: золотая лента под кристаллами, гнёзда и лучи. Выключено — оно было не пиксельным")]
    public bool legacyDecor = false;
    [Tooltip("Поднять кристаллы вместе со строками ещё на столько единиц (сверх panelRaise)")]
    public float contentRaise = 45f;
    [Tooltip("Высота строки итогов в пикселях рисунка (значки — 7, остальное — воздух вокруг)")]
    public int rowPixels = 10;
    [Tooltip("Зазор между кристаллами и строками, в пикселях рисунка")]
    public int rowGapPixels = 4;
    [Tooltip("Победа прямо в консоли: слоты и команды прячутся, нижние кнопки остаются (▶ становится «Дальше»), " +
             "кристаллы и строки появляются на их месте. Выключено — прежнее отдельное окно поверх консоли")]
    public bool mergeWithConsole = true;

    [Header("Положение")]
    [Tooltip("На сколько пикселей поднять плашку со звёздами (итоги поднимутся вместе с ней)")]
    public float panelRaise = 90f;

    [Header("Итоги (можно оставить пустым — создадутся сами под звёздами)")]
    public RectTransform resultsRoot;
    [Tooltip("Кнопка «Дальше» — появится в конце. Если пусто, ищется дочерняя кнопка.")]
    public GameObject nextButton;
    public TMP_FontAsset font;

    [Header("Тексты строк итогов ({0}, {1} — числа; по-английски — перевод в Loc)")]
    public string cardsLine = "Cards: {0}  (best {1})";
    public string cardsLineNoBest = "Cards: {0}";
    public string hintsGood = "No hints";
    public string hintsBad = "Hints used: {0}";
    public string edgeGood = "Never near the edge";
    public string edgeBad = "Near the edge: {0}";

    [Header("Звуки (необязательно)")]
    public AudioClip starSound;
    public AudioClip plusSound;
    public AudioClip minusSound;

    bool _skip;
    RectTransform _starPanel;
    Vector2 _starPanelBase;
    AudioSource _audio;
    readonly List<GameObject> _spawned = new List<GameObject>();

    enum RowIcon { Cards, Hint, Edge }
    struct Row { public string text; public bool good; public RowIcon icon; }

    Image _rays;
    float _raysAlpha;
    readonly List<RectTransform> _earnedStars = new List<RectTransform>();
    readonly Dictionary<RectTransform, Vector2> _starHome = new Dictionary<RectTransform, Vector2>();
    readonly Dictionary<RectTransform, float> _starBase = new Dictionary<RectTransform, float>();

    private void OnEnable()
    {
        if (data == null) return;
        var level = TilemapData.CurrentLevel;
        int used = data.commandUsed;
        int starCount = CalculateStars(used, level);
        if (HintSystem.HintsUsed > 0) starCount = Mathf.Min(starCount, maxStarsWithHints);

        SaveManager.SaveLevelResult(data.NextLevel, starCount);

        var rows = new List<Row>();
        if (level != null && level.optimalCards > 0)
            rows.Add(new Row { text = Loc.F(cardsLine, used, level.optimalCards), good = used <= level.optimalCards, icon = RowIcon.Cards });
        else
            rows.Add(new Row { text = Loc.F(cardsLineNoBest, used), good = starCount >= 3, icon = RowIcon.Cards });
        rows.Add(HintSystem.HintsUsed == 0
            ? new Row { text = Loc.T(hintsGood), good = true, icon = RowIcon.Hint }
            : new Row { text = Loc.F(hintsBad, HintSystem.HintsUsed), good = false, icon = RowIcon.Hint });
        int edges = MovingManager.LastRunEdgeHits;
        rows.Add(edges == 0
            ? new Row { text = Loc.T(edgeGood), good = true, icon = RowIcon.Edge }
            : new Row { text = Loc.F(edgeBad, edges), good = false, icon = RowIcon.Edge });

        _skip = false;
        StopAllCoroutines();
        StartCoroutine(Sequence(starCount, rows));
    }

    private void OnDisable()
    {
        foreach (var go in _spawned) if (go != null) Destroy(go);
        _spawned.Clear();
        _rays = null;
        _earnedStars.Clear();
        if (stars != null)
            foreach (var st in stars) if (st != null) st.transform.localScale = Vector3.one;
        foreach (var kv in _starHome) if (kv.Key != null) kv.Key.anchoredPosition = kv.Value;
        RestoreConsole();
    }

    void Update()
    {
        // Тап/клик во время анимации — показать всё сразу.
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) _skip = true;

        // Лучи медленно крутятся и «дышат»
        if (_rays != null)
        {
            _rays.transform.Rotate(0f, 0f, -Time.unscaledDeltaTime * 14f);
            var c = _rays.color;
            c.a = Mathf.MoveTowards(c.a, _raysAlpha * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.2f)), Time.unscaledDeltaTime * 1.5f);
            _rays.color = c;
        }
    }

    public int CalculateStars(int used, LevelSt level)
    {
        if (level != null && level.optimalCards > 0)
        {
            int optimal = level.optimalCards;
            int slack = Mathf.Max(minSlack, Mathf.CeilToInt(optimal * slackPercent));
            if (used <= optimal) return 3;
            if (used <= optimal + slack) return 2;
            return 1;
        }
        return used <= threeStarsMax ? 3 : used <= twoStarsMax ? 2 : 1;
    }

    // ───────────────────────────── последовательность ─────────────────────────────

    IEnumerator Sequence(int starCount, List<Row> rows)
    {
        // Победа в консоли: прячем слоты и команды (с эффектом), рамку окна убираем — видна консоль.
        float hideTime = MergeIntoConsole();
        RaisePanel();
        var next = FindNextButton();
        var nextJuice = next != null ? next.GetComponent<ButtonJuice>() : null;
        if (nextJuice != null) nextJuice.enabled = false; // иначе он каждый кадр перезаписывает масштаб
        if (next != null) next.transform.localScale = Vector3.zero;

        // Звёзды — пустые серые
        float S = Mathf.Max(0.5f, this.starScale);
        var starScale = new Vector3[stars != null ? stars.Length : 0];
        _earnedStars.Clear();
        BuildStarDecor(S);
        // Золотая лента под кристаллами — не пиксельная картинка: прячем (сами кристаллы — её дети, остаются).
        if (!legacyDecor && stars != null && stars.Length > 0 && stars[0] != null)
        {
            var ribbon = stars[0].rectTransform.parent != null ? stars[0].rectTransform.parent.GetComponent<Image>() : null;
            if (ribbon != null) ribbon.enabled = false;
        }
        for (int i = 0; i < starScale.Length; i++)
        {
            if (stars[i] == null) continue;
            starScale[i] = Vector3.one * S;
            // Пиксельный кристалл вместо картинки-звезды высокого разрешения; пустой — тёмный силуэт.
            stars[i].sprite = VictorySprites.Crystal(false);
            stars[i].preserveAspect = false;
            // Пиксель кристалла = пиксель консоли (artPixel), с учётом увеличения S.
            SizeToArtPixels(stars[i].rectTransform, stars[i].sprite, S);
            stars[i].color = Color.white;
            stars[i].transform.localScale = Vector3.one * 0.85f * S;
            stars[i].transform.localRotation = Quaternion.identity;
        }

        if (hideTime > 0f) yield return Wait(hideTime);
        BuildCrystalPlate();

        // 1. Окно выпрыгивает
        PixelSfx.Play(Sfx.Open, 0.8f);
        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        yield return Animate(0.35f, k =>
        {
            group.alpha = Mathf.Clamp01(k * 2f);
            transform.localScale = Vector3.one * BackOut(k, 0.6f);
        });
        group.alpha = 1f;
        transform.localScale = Vector3.one;

        yield return Wait(0.15f);

        // 2. Звёзды по одной
        for (int i = 0; i < starScale.Length; i++)
        {
            var star = stars[i];
            if (star == null) continue;
            if (i < starCount)
            {
                Play(starSound, Sfx.Star, 1f + i * 0.12f);
                var rt = star.rectTransform;
                star.sprite = VictorySprites.Crystal(true);
                float dir = i % 2 == 0 ? -1f : 1f;
                yield return Animate(0.4f, k =>
                {
                    // «Штамп»: прилетает большой, крутится и садится с отскоком
                    float s = k < 0.55f ? Mathf.Lerp(2.4f, 0.85f, EaseIn(k / 0.55f)) : Mathf.Lerp(0.85f, 1f, BackOut((k - 0.55f) / 0.45f, 0f));
                    rt.localScale = Vector3.one * s * S;
                    rt.localRotation = Quaternion.Euler(0f, 0f, dir * 40f * (1f - k));
                    star.color = Color.Lerp(new Color(1f, 1f, 1f, 0f), Color.white, Mathf.Clamp01(k * 3f));
                });
                rt.localScale = Vector3.one * S;
                rt.localRotation = Quaternion.identity;
                star.color = Color.white;
                _earnedStars.Add(rt);
                _raysAlpha = 0.25f + 0.2f * (i + 1);
                PixelFX.BurstUI(rt, PixelFX.CrystalColors, 14, 300f, 18f, 0.75f);
                StartCoroutine(Punch(transform, 0.04f, 0.2f));
                yield return Wait(starInterval);
            }
            else
            {
                StartCoroutine(Punch(star.rectTransform, -0.1f, 0.3f, 0.85f * S)); // пустая — коротко вздрагивает
                yield return Wait(0.15f);
            }
        }

        // 3. Итоги
        var lines = BuildRows(rows);
        for (int i = 0; i < lines.Count; i++)
        {
            var (rowRt, label, mark, good) = lines[i];
            rowRt.gameObject.SetActive(true);
            var rowGroup = rowRt.GetComponent<CanvasGroup>();
            PixelSfx.Play(Sfx.Pop, 0.5f, 0.9f + i * 0.08f);
            // Плашка «раскрывается» от центра ступеньками (пиксельно), вспыхивает белым, значок подпрыгивает.
            if (rowGroup != null) rowGroup.alpha = 1f;
            yield return Animate(0.2f, k => rowRt.localScale = new Vector3(Mathf.Ceil(k * 6f) / 6f, 1f, 1f));
            rowRt.localScale = Vector3.one;
            StartCoroutine(FlashRow(rowRt));
            var iconRt = rowRt.Find("Icon") as RectTransform;
            if (iconRt != null)
            {
                StartCoroutine(Punch(iconRt, 0.35f, 0.25f));
                PixelFX.BurstUI(iconRt, new[] { Color.white, new Color(1f, 0.9f, 0.6f, 1f) }, 6, 120f, 10f, 0.4f);
            }

            int total = label.text.Length;
            label.maxVisibleCharacters = 0;
            for (float c = 0f; c < total; c += Time.unscaledDeltaTime * typeSpeed)
            {
                if (_skip) break;
                label.maxVisibleCharacters = Mathf.CeilToInt(c);
                yield return null;
            }
            label.maxVisibleCharacters = total;

            mark.gameObject.SetActive(true);
            Play(good ? plusSound : minusSound, good ? Sfx.Plus : Sfx.Minus);
            var mrt = mark.rectTransform;
            yield return Animate(0.3f, k =>
            {
                mrt.localScale = Vector3.one * Mathf.LerpUnclamped(1.8f, 1f, BackOut(k, 0f));
                mrt.localRotation = Quaternion.Euler(0f, 0f, (good ? -25f : 25f) * (1f - k));
            });
            mrt.localScale = Vector3.one;
            mrt.localRotation = Quaternion.identity;
            if (good) PixelFX.BurstUI(mrt, new[] { plusColor, Color.white }, 8, 140f, 10f, 0.5f);
            else StartCoroutine(Shake(mrt, 0.35f));
            yield return Wait(0.1f);
        }

        // 4. Кнопка «Дальше» — поверх строк и лучей
        if (next != null)
        {
            next.transform.SetAsLastSibling();
            yield return Animate(0.35f, k => next.transform.localScale = Vector3.one * BackOut(k, 0f));
            next.transform.localScale = Vector3.one;
            if (nextJuice != null) nextJuice.enabled = true;
            var juice = ButtonJuice.Ensure(next);
            if (juice != null) juice.breathe = true;
        }

        // 5. Пока окно открыто — заработанные звёзды иногда искрятся
        while (isActiveAndEnabled && _earnedStars.Count > 0)
        {
            for (float t = 0f; t < 1.3f; t += Time.unscaledDeltaTime) yield return null;
            var star = _earnedStars[Random.Range(0, _earnedStars.Count)];
            if (star != null) PixelFX.BurstUI(star, PixelFX.CrystalColors, 4, 90f, 10f, 0.6f);
        }
    }

    // ───────────────────────────── звёзды: гнёзда и лучи ─────────────────────────────

    void BuildStarDecor(float S)
    {
        if (stars == null || stars.Length == 0 || stars[0] == null) return;
        var starPanel = stars[0].rectTransform.parent as RectTransform;
        if (starPanel == null) return;

        // Звёзды крупнее — раздвигаем их, чтобы не налезали друг на друга.
        foreach (var star in stars)
        {
            if (star == null) continue;
            var srt = star.rectTransform;
            if (!_starHome.ContainsKey(srt)) _starHome[srt] = srt.anchoredPosition;
            if (!_starBase.ContainsKey(srt)) _starBase[srt] = Mathf.Min(srt.rect.width, srt.rect.height); // до замены на кристалл
            srt.anchoredPosition = Vector2.Scale(_starHome[srt], new Vector2(S * 1.12f, S));
        }

        if (starSockets && legacyDecor)
            foreach (var star in stars)
            {
                if (star == null) continue;
                var srt = star.rectTransform;
                var socket = NewImage("StarSocket", starPanel, VictorySprites.Socket);
                var sock = socket.rectTransform;
                sock.anchorMin = srt.anchorMin; sock.anchorMax = srt.anchorMax; sock.pivot = srt.pivot;
                sock.anchoredPosition = srt.anchoredPosition + new Vector2(0f, -srt.rect.height * 0.04f * S);
                float d = (_starBase.TryGetValue(srt, out var baseSize) ? baseSize : Mathf.Min(srt.rect.width, srt.rect.height)) * 0.9f * S;
                sock.sizeDelta = new Vector2(d, d);
                sock.SetSiblingIndex(srt.GetSiblingIndex()); // под звездой
                socket.pixelsPerUnitMultiplier = 1f;
            }

        if (starRays && legacyDecor && transform is RectTransform self)
        {
            _rays = NewImage("StarRays", self, VictorySprites.Rays);
            var rrt = _rays.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.SetSiblingIndex(starPanel.GetSiblingIndex()); // за плашкой со звёздами
            var mid = stars[stars.Length / 2] != null ? stars[stars.Length / 2].rectTransform : stars[0].rectTransform;
            rrt.position = mid.position;
            float size = Mathf.Max(starPanel.rect.height * 4f, 560f);
            rrt.sizeDelta = new Vector2(size, size);
            _rays.color = new Color(1f, 0.88f, 1f, 0f); // сиреневые — под кристаллы
            _raysAlpha = 0f;
        }
    }

    Image NewImage(string name, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        _spawned.Add(go);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.rectTransform.SetParent(parent, false);
        return img;
    }

    // ───────────────────────────── строки итогов ─────────────────────────────

    List<(RectTransform row, TextMeshProUGUI label, Image mark, bool good)> BuildRows(List<Row> rows)
    {
        var result = new List<(RectTransform, TextMeshProUGUI, Image, bool)>();
        var root = resultsRoot != null ? resultsRoot : CreateResultsRoot(rows.Count);
        if (root == null) return result;

        var fontAsset = font != null ? font : FindSceneFont();
        float px = ArtUnit() / ParentScale(root);  // пиксель плашек и значков = пиксель консоли
        float gap = px;
        float rowH = Mathf.Max(8, rowPixels) * px; // значки 7 пикселей + воздух сверху и снизу

        for (int i = 0; i < rows.Count; i++)
        {
            var plate = NewImage("ResultRow", root, VictorySprites.RowPlate);
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 1f / px;
            var rrt = plate.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -i * (rowH + gap));
            rrt.sizeDelta = new Vector2(0f, rowH);
            rrt.gameObject.AddComponent<CanvasGroup>().alpha = 0f;

            // Иконка слева
            var icon = NewImage("Icon", rrt, VictorySprites.Icon((int)rows[i].icon));
            var irt = icon.rectTransform;
            float iconSize = 7f * px;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(iconSize, iconSize);
            irt.anchoredPosition = new Vector2(px * 2f, 0f);

            // Значок «+» / «−» справа
            var mark = NewImage("Mark", rrt, rows[i].good ? VictorySprites.Plus : VictorySprites.Minus);
            var mrt = mark.rectTransform;
            float markSize = 7f * px;
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 0.5f);
            mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.sizeDelta = new Vector2(markSize, markSize);
            mrt.anchoredPosition = new Vector2(-px * 2f - markSize * 0.5f, 0f);
            mark.gameObject.SetActive(false);

            // Текст между ними
            var label = MakeText(rrt, "Label", fontAsset, TextAlignmentOptions.MidlineLeft);
            _spawned.Add(label.gameObject);
            label.rectTransform.offsetMin = new Vector2(px * 4f + iconSize, px * 2f);
            label.rectTransform.offsetMax = new Vector2(-(px * 4f + markSize), -px * 2f);
            label.text = rows[i].text;
            label.color = rows[i].good ? Color.white : new Color(1f, 0.86f, 0.86f, 1f);
            label.maxVisibleCharacters = 0;

            rrt.gameObject.SetActive(false);
            result.Add((rrt, label, mark, rows[i].good));
        }
        return result;
    }

    static TextMeshProUGUI MakeText(RectTransform parent, string name, TMP_FontAsset fontAsset, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var t = go.AddComponent<TextMeshProUGUI>();
        var rt = t.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        if (fontAsset != null) t.font = fontAsset;
        t.enableAutoSizing = true;
        t.fontSizeMin = 8;
        t.fontSizeMax = 30;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    /// <summary>
    /// Плашка итогов: компактная тёмная доска между звёздами и кнопкой «Дальше»
    /// (раньше текст висел в пустоте на всю ширину экрана). Возвращает контейнер строк.
    /// </summary>
    RectTransform CreateResultsRoot(int rowCount)
    {
        var self = transform as RectTransform;
        if (self == null || stars == null || stars.Length == 0 || stars[0] == null) return null;
        var starPanel = stars[0].rectTransform.parent as RectTransform;
        if (starPanel == null) return null;

        // Строки — под нижним краем самих кристаллов (лента под ними спрятана, её низ ничего не значит).
        float panelBottom = CrystalsBottom(self, starPanel);
        var next = FindNextButton();
        float floor = next != null ? TopIn(self, (RectTransform)next.transform) : self.rect.yMin + 10f;

        float pad = 0f;
        float u = ArtUnit();
        float gap = u;
        float rowH = Mathf.Max(8, rowPixels) * u;
        float Need(float h) => rowCount * h + (rowCount - 1) * gap + pad * 2f;
        float margin = rowGapPixels * u; // зазор кристаллы → строки: не впритык и не далеко
        float space = panelBottom - floor - margin * 2f;
        if (space < Need(rowH))
        {
            // Совсем мало места — поднимаем звёзды (над ними обычно пусто).
            float lift = Need(rowH) - space;
            starPanel.anchoredPosition += new Vector2(0f, lift);
            panelBottom += lift;
            space += lift;
        }

        float height = Need(rowH) - pad * 2f;
        // Не шире консоли: по бокам — рамка окна (≈ 9 пикселей рисунка с каждой стороны). Ширина — целое число пикселей.
        float width = Mathf.Min(resultWidth, self.rect.width - 18f * u);
        width = Mathf.Floor(width / u) * u;
        // Строки — сразу под лентой со звёздами (так кнопка «Дальше» остаётся свободной).
        float centerY = panelBottom - margin - height * 0.5f;

        var go = new GameObject("Results", typeof(RectTransform));
        _spawned.Add(go);
        var rt = (RectTransform)go.transform;
        rt.SetParent(self, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        // по горизонтали — по центру плашки со звёздами
        Vector2 starsCenterWorld = starPanel.TransformPoint(starPanel.rect.center);
        float cx = self.InverseTransformPoint(starsCenterWorld).x - self.rect.center.x;
        rt.anchoredPosition = new Vector2(cx, centerY - self.rect.center.y);
        return rt;
    }

    IEnumerator PopIn(RectTransform rt)
    {
        rt.localScale = new Vector3(1f, 0f, 1f);
        yield return Animate(0.3f, k => rt.localScale = new Vector3(1f, BackOut(k, 0f), 1f));
        rt.localScale = Vector3.one;
    }

    /// <summary>Поднять плашку со звёздами на panelRaise (от исходного места — повторный показ не копит сдвиг).</summary>
    void RaisePanel()
    {
        if (stars == null || stars.Length == 0 || stars[0] == null) return;
        var starPanel = stars[0].rectTransform.parent as RectTransform;
        if (starPanel == null) return;
        if (_starPanel != starPanel) { _starPanel = starPanel; _starPanelBase = starPanel.anchoredPosition; }
        starPanel.anchoredPosition = _starPanelBase + new Vector2(0f, panelRaise + contentRaise);
    }

    /// <summary>Нижний край кристаллов (с учётом их увеличения starScale) в координатах окна.</summary>
    float CrystalsBottom(RectTransform self, RectTransform starPanel)
    {
        float S = Mathf.Max(0.5f, starScale);
        float bottom = float.MaxValue;
        if (stars != null)
            foreach (var st in stars)
            {
                if (st == null) continue;
                var rt = st.rectTransform;
                Vector3 centerWorld = rt.parent != null ? rt.parent.TransformPoint(rt.localPosition) : rt.position;
                float cy = self.InverseTransformPoint(centerWorld).y;
                float b = cy - rt.rect.height * S * (rt.pivot.y);
                bottom = Mathf.Min(bottom, b);
            }
        return bottom < float.MaxValue ? bottom : BottomIn(self, starPanel);
    }

    static float BottomIn(RectTransform space, RectTransform child)
    {
        var c = new Vector3[4];
        child.GetWorldCorners(c);
        return space.InverseTransformPoint(c[0]).y;
    }

    static float TopIn(RectTransform space, RectTransform child)
    {
        var c = new Vector3[4];
        child.GetWorldCorners(c);
        return space.InverseTransformPoint(c[1]).y;
    }

    GameObject FindNextButton()
    {
        if (nextButton != null) return nextButton;
        var button = GetComponentInChildren<Button>(true);
        return button != null ? button.gameObject : null;
    }

    TMP_FontAsset FindSceneFont()
    {
        return GameFont.Main;
    }

    /// <summary>Белая вспышка поверх плашки строки (тот же рисунок плашки), гаснет ступеньками.</summary>
    IEnumerator FlashRow(RectTransform row)
    {
        var flash = NewImage("Flash", row, VictorySprites.RowPlate);
        var plate = row.GetComponent<Image>();
        flash.type = Image.Type.Sliced;
        flash.pixelsPerUnitMultiplier = plate != null ? plate.pixelsPerUnitMultiplier : 1f;
        var frt = flash.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
        {
            float a = Mathf.Floor((1f - t) * 4f) / 4f * 0.7f; // 4 ступеньки прозрачности
            flash.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        if (flash != null) Destroy(flash.gameObject);
    }

    // ───────────────────────────── победа в консоли ─────────────────────────────

    // Что спрятали / поменяли — чтобы вернуть как было (OnDisable).
    readonly List<GameObject> _hiddenConsole = new List<GameObject>();
    readonly List<Image> _hiddenFrames = new List<Image>();
    RectTransform _movedNext;
    Vector2 _nextAnchorMin, _nextAnchorMax, _nextPivot, _nextPos, _nextSize;
    Sprite _nextSprite;
    Button _replay;
    Button.ButtonClickedEvent _replayOriginal;
    static readonly HashSet<string> ConsoleKeep = new HashSet<string> { "Play", "Reverse", "Rewind", "HintButton" };

    /// <summary>
    /// Идея автора: не отдельное окно победы, а сама консоль. Её слоты и ряд команд «схлопываются»
    /// ступеньками (с искрами), нижние кнопки остаются: на месте ▶ встаёт кнопка «Дальше» (тот же рисунок,
    /// что у ▶), кнопка справа — как была. Рамка окна победы прячется, чтобы была видна консоль.
    /// Возвращает, сколько ждать, пока всё спрячется.
    /// </summary>
    float MergeIntoConsole()
    {
        if (!mergeWithConsole) return 0f;
        var finishPanel = transform.parent;
        var pc = FindObjectOfType<PanelController>();
        if (pc == null || pc.panels == null || finishPanel == null) return 0f;

        GameObject console = null;
        foreach (var p in pc.panels)
            if (p != null && p.activeInHierarchy && p.transform != finishPanel) { console = p; break; }
        if (console == null) return 0f;
        var play = FindDeep(console.transform, "Play") as RectTransform;
        if (play == null) return 0f;

        // Рамка окна победы — прочь (под ней та же консоль).
        foreach (var img in new[] { finishPanel.GetComponent<Image>(), GetComponent<Image>() })
            if (img != null && img.enabled) { img.enabled = false; _hiddenFrames.Add(img); }

        // Слоты, ряд команд, полоса прокрутки — «схлопываются» по очереди.
        float delay = 0f;
        foreach (Transform child in play.parent)
        {
            if (!child.gameObject.activeSelf || ConsoleKeep.Contains(child.name)) continue;
            _hiddenConsole.Add(child.gameObject);
            StartCoroutine(Collapse(child, delay));
            delay += 0.07f;
        }
        // Rewind стоит там же, где ▶ — его место займёт «Дальше».
        var rewind = play.parent.Find("Rewind");
        if (rewind != null && rewind.gameObject.activeSelf) { rewind.gameObject.SetActive(false); _hiddenConsole.Add(rewind.gameObject); }

        // Кнопка справа (очистить программу) на экране победы — «Сыграть уровень ещё раз»:
        // иначе она сбросила бы уровень под открытым окном победы.
        var reverse = play.parent.Find("Reverse");
        _replay = reverse != null ? reverse.GetComponent<Button>() : null;
        if (_replay != null)
        {
            _replayOriginal = _replay.onClick;
            _replay.onClick = new Button.ButtonClickedEvent();
            _replay.onClick.AddListener(() =>
            {
                var loader = FindObjectOfType<Levelloader>();
                if (loader != null) loader.LoadNextLeve(-1); // -1 — перезапуск текущего уровня
            });
        }

        // «Дальше» — на место ▶ и с его рисунком: нижний ряд кнопок как был.
        var next = FindNextButton();
        if (next != null)
        {
            var nrt = (RectTransform)next.transform;
            _movedNext = nrt;
            _nextAnchorMin = nrt.anchorMin; _nextAnchorMax = nrt.anchorMax; _nextPivot = nrt.pivot;
            _nextPos = nrt.anchoredPosition; _nextSize = nrt.sizeDelta;
            var c = new Vector3[4];
            play.GetWorldCorners(c);
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0.5f);
            var ps = nrt.parent != null ? nrt.parent.lossyScale : Vector3.one;
            nrt.sizeDelta = new Vector2((c[2].x - c[0].x) / Mathf.Max(0.0001f, ps.x), (c[2].y - c[0].y) / Mathf.Max(0.0001f, ps.y));
            nrt.position = (c[0] + c[2]) * 0.5f;
            var nimg = next.GetComponent<Image>();
            var pimg = play.GetComponent<Image>();
            if (nimg != null && pimg != null && pimg.sprite != null)
            {
                _nextSprite = nimg.sprite;
                nimg.sprite = pimg.sprite;
                nimg.type = pimg.type;
                nimg.pixelsPerUnitMultiplier = pimg.pixelsPerUnitMultiplier;
            }
        }
        return delay + 0.25f;
    }

    /// <summary>Элемент консоли схлопывается по высоте ступеньками и исчезает с искрами.</summary>
    IEnumerator Collapse(Transform t, float delay)
    {
        for (float w = 0f; w < delay; w += Time.unscaledDeltaTime) yield return null;
        if (t == null) yield break;
        PixelSfx.Play(Sfx.Remove, 0.4f, 1.1f);
        if (t is RectTransform rt) PixelFX.BurstUI(rt, new[] { Color.white, new Color(0.72f, 0.7f, 0.95f, 1f) }, 8, 160f, 12f, 0.45f);
        Vector3 baseScale = t.localScale;
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / 0.2f)
        {
            float q = 1f - Mathf.Ceil(k * 4f) / 4f; // 4 ступеньки
            t.localScale = new Vector3(baseScale.x * (0.6f + 0.4f * q), baseScale.y * q, baseScale.z);
            yield return null;
        }
        t.localScale = baseScale;
        t.gameObject.SetActive(false);
    }

    void RestoreConsole()
    {
        foreach (var go in _hiddenConsole) if (go != null) go.SetActive(true);
        _hiddenConsole.Clear();
        foreach (var img in _hiddenFrames) if (img != null) img.enabled = true;
        if (_replay != null && _replayOriginal != null) _replay.onClick = _replayOriginal;
        _replay = null;
        _replayOriginal = null;
        _hiddenFrames.Clear();
        if (_movedNext != null)
        {
            _movedNext.anchorMin = _nextAnchorMin; _movedNext.anchorMax = _nextAnchorMax; _movedNext.pivot = _nextPivot;
            _movedNext.sizeDelta = _nextSize; _movedNext.anchoredPosition = _nextPos;
            var nimg = _movedNext.GetComponent<Image>();
            if (nimg != null && _nextSprite != null) nimg.sprite = _nextSprite;
            _movedNext = null;
            _nextSprite = null;
        }
    }

    /// <summary>Тёмная плашка в стиле консоли под тремя кристаллами (чтобы они не висели в пустоте).</summary>
    void BuildCrystalPlate()
    {
        if (stars == null || stars.Length == 0 || stars[0] == null) return;
        var panel = stars[0].rectTransform.parent as RectTransform;
        if (panel == null) return;
        float S = Mathf.Max(0.5f, starScale);
        float u = ArtUnit() / ParentScale(stars[0].rectTransform);
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var st in stars)
        {
            if (st == null) continue;
            var rt = st.rectTransform;
            var p = rt.anchoredPosition;
            float w = rt.rect.width * S, h = rt.rect.height * S;
            minX = Mathf.Min(minX, p.x - w * rt.pivot.x); maxX = Mathf.Max(maxX, p.x + w * (1f - rt.pivot.x));
            minY = Mathf.Min(minY, p.y - h * rt.pivot.y); maxY = Mathf.Max(maxY, p.y + h * (1f - rt.pivot.y));
        }
        if (minX > maxX) return;
        float pad = 3f * u;
        var plate = NewImage("CrystalPlate", panel, VictorySprites.Board);
        plate.type = Image.Type.Sliced;
        plate.pixelsPerUnitMultiplier = 1f / u;
        var prt = plate.rectTransform;
        var a = stars[0].rectTransform;
        prt.anchorMin = a.anchorMin; prt.anchorMax = a.anchorMax; prt.pivot = new Vector2(0.5f, 0.5f);
        float width = Mathf.Ceil((maxX - minX + pad * 2f) / u) * u, height = Mathf.Ceil((maxY - minY + pad * 2f) / u) * u;
        prt.sizeDelta = new Vector2(width, height);
        prt.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        prt.SetAsFirstSibling(); // под кристаллами
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    // ───────────────────────────── пиксели ─────────────────────────────

    /// <summary>Пиксель рисунков окна в единицах канваса: artPixel, подогнанный до целых пикселей экрана.</summary>
    float ArtUnit()
    {
        var canvas = GetComponentInParent<Canvas>();
        float s = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        if (s <= 0.0001f) s = 1f;
        return Mathf.Max(1f, Mathf.Round(artPixel * s)) / s;
    }

    /// <summary>Масштаб родителя относительно канваса (окно может быть увеличено/уменьшено).</summary>
    float ParentScale(RectTransform rt)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || rt.parent == null) return 1f;
        float cs = canvas.rootCanvas.transform.lossyScale.x;
        return cs > 0.0001f ? Mathf.Abs(rt.parent.lossyScale.x / cs) : 1f;
    }

    /// <summary>Размер картинки = её пиксели × artPixel (extraScale — масштаб, который код задаёт сам).</summary>
    void SizeToArtPixels(RectTransform rt, Sprite sprite, float extraScale = 1f)
    {
        if (rt == null || sprite == null) return;
        float u = ArtUnit() / (ParentScale(rt) * Mathf.Max(0.01f, extraScale));
        rt.sizeDelta = new Vector2(sprite.rect.width * u, sprite.rect.height * u);
    }

    // ───────────────────────────── помощники ─────────────────────────────

    IEnumerator Animate(float duration, System.Action<float> step)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            if (_skip) break;
            step(t);
            yield return null;
        }
        step(1f);
    }

    IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !_skip; t += Time.unscaledDeltaTime) yield return null;
    }

    IEnumerator Punch(Transform target, float amount, float duration, float baseSize = 1f)
    {
        Vector3 baseScale = Vector3.one * baseSize;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            target.localScale = baseScale * (1f + amount * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        target.localScale = baseScale;
    }

    IEnumerator Shake(RectTransform target, float duration)
    {
        Vector2 basePos = target.anchoredPosition;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            target.anchoredPosition = basePos + new Vector2(6f * Mathf.Sin(t * 40f) * (1f - t), 0f);
            yield return null;
        }
        target.anchoredPosition = basePos;
    }

    static float EaseIn(float t) => t * t;

    /// <summary>Выезд с отскоком (как Ease.OutBack). from — начальное значение.</summary>
    static float BackOut(float t, float from)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.4f, c3 = c1 + 1f;
        float k = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        return Mathf.LerpUnclamped(from, 1f, k);
    }

    void Play(AudioClip clip, Sfx fallback, float pitch = 1f)
    {
        if (clip == null) { PixelSfx.Play(fallback, 1f, pitch, 0f); return; }
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.PlayOneShot(clip);
    }
}
