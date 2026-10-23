using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Мини-сюжет «Crystal Adventure: The Ascent». Создаётся сам при загрузке игровой сцены
/// (как туториал и карта глав), в сцене ничего настраивать не нужно.
///
///  • Титульный экран — один раз за запуск игры: название, Cris с Ela на голове, кнопки
///    Play / Levels / Prologue. Заменяет старое стартовое окошко MenuBox (оно закрывается).
///  • Пролог — «пиксельный комикс» на весь экран: Кристальное Сердце над облаками трескается,
///    осколки падают на остров, из осколка появляется Cris, его находит Ela, и они идут вверх.
///    Показывается при первом Play (потом — кнопкой Prologue). Можно пропустить (Skip).
///  • Вступление перед боссом — пара реплик при первом заходе на уровень босса (StoryLines.Intros).
///
/// Пока идёт сюжет, Play не запускается (Data.DialogManager), а туториал ждёт (TutorialDirector).
/// Тексты — в StoryLines (английский, как в туториале). Всё анимируется по unscaledTime
/// и сдвигается на целые пиксели.
/// </summary>
public class StoryDirector : MonoBehaviour
{
    const string PrologueKey = "story.prologue.seen";
    const string IntroKeyPrefix = "story.intro.";
    const string ChapterKeyPrefix = "story.chapter.done.";
    /// <summary>Боссы глав по порядку: Forest, Winter, Sky.</summary>
    static readonly string[] ChapterBosses = { "LevelBossForest", "LevelBossWinter", "LevelBossSky" };
    const float TypeSpeed = 40f;

    static readonly Color Ink = PixelSprites.Ink;
    static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
    static readonly Color ElaColor = new Color(0.55f, 0.36f, 0.75f, 1f);
    static readonly Color CrisColor = new Color(0.86f, 0.32f, 0.62f, 1f);
    static readonly Color BossColor = new Color(0.30f, 0.42f, 0.70f, 1f);

    /// <summary>Идёт титульный экран, пролог или вступление — игра и туториал ждут.</summary>
    public static bool IsBlocking { get; private set; }

    static bool _titleShown; // титульный экран — один раз за запуск (сцена перезагружается на каждом уровне)

    // ───────────── запуск ─────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Create();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Create();

    static void Create()
    {
        if (FindObjectOfType<StoryDirector>() != null) return;
        if (FindObjectOfType<MovingManager>() == null) return; // только игровая сцена
        new GameObject("StoryDirector").AddComponent<StoryDirector>();
    }

    // ───────────── актёры ─────────────

    sealed class Speaker
    {
        public string Name;
        public Sprite Portrait;
        public Color Tag = ElaColor;
        public Color Tint = Color.white;
        public float Pitch = 1.15f;
    }

    Speaker _narrator, _ela, _cris;
    Sprite _crisSprite, _elaSprite;

    void FindActors()
    {
        var hero = FindObjectOfType<CharacterCode>();
        _crisSprite = hero != null ? (hero.FrontSprite != null ? hero.FrontSprite : hero.MainRenderer != null ? hero.MainRenderer.sprite : null) : null;
        // Ela — маленькая ведьмочка, которая сидит у Cris на голове (дочерний спрайт куба).
        if (hero != null)
            foreach (var r in hero.GetComponentsInChildren<SpriteRenderer>(true))
                if (r != hero.MainRenderer && r.sprite != null) { _elaSprite = r.sprite; break; }
        if (_elaSprite == null)
        {
            var dm = FindObjectOfType<DialogManager>(true);
            if (dm != null && dm.actorImage != null) _elaSprite = dm.actorImage.sprite;
        }
        _narrator = new Speaker { Name = "", Portrait = null };
        _ela = new Speaker { Name = "Ela", Portrait = _elaSprite, Tag = ElaColor, Pitch = 1.2f };
        _cris = new Speaker { Name = "Cris", Portrait = _crisSprite, Tag = CrisColor, Pitch = 0.8f };
    }

    Speaker SpeakerFor(string who)
    {
        switch (who)
        {
            case "Ela": return _ela;
            case "Cris": return _cris;
            case "Frost Diamond": return new Speaker { Name = who, Portrait = EnemyArt.FrostDiamond, Tag = BossColor, Pitch = 0.6f };
            case "Sky Whale": return new Speaker { Name = who, Portrait = EnemyArt.SkyWhale, Tag = BossColor, Pitch = 0.45f };
            case "Forest Guardian": return new Speaker { Name = who, Portrait = _crisSprite, Tint = new Color(0.55f, 1f, 0.45f, 1f), Tag = new Color(0.3f, 0.55f, 0.3f, 1f), Pitch = 0.55f };
            default: return _narrator;
        }
    }

    // ───────────── сценарий ─────────────

    MovingManager _moving;

    IEnumerator Start()
    {
        _moving = FindObjectOfType<MovingManager>();
        yield return null; // сцена достроилась (герой, окна)
        FindActors();

        if (!_titleShown)
        {
            _titleShown = true;
            yield return Block(TitleScreen());
        }

        // Вступление перед боссом — когда уровень загрузился и куб приземлился.
        float timeout = 15f;
        while ((TilemapData.CurrentLevel == null || _moving == null || _moving.character == null || !_moving.character.IsReady) && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        // Босс главы побеждён — один раз показываем «Глава пройдена» (после Sky — финал игры).
        // Показ — при следующей загрузке уровня: окно победы и переход идут как обычно.
        var save = SaveManager.Load();
        for (int ch = 0; ch < ChapterBosses.Length; ch++)
        {
            if (PlayerPrefs.GetInt(ChapterKeyPrefix + ch, 0) != 0) continue;
            if (SaveManager.StarsOf(save, ChapterBosses[ch]) <= 0) continue;
            yield return Block(ChapterComplete(ch, save));
            PlayerPrefs.SetInt(ChapterKeyPrefix + ch, 1);
            PlayerPrefs.Save();
        }

        var level = TilemapData.CurrentLevel;
        if (level != null && StoryLines.Intros.TryGetValue(level.name, out var lines) && PlayerPrefs.GetInt(IntroKeyPrefix + level.name, 0) == 0)
        {
            yield return Block(Intro(lines));
            PlayerPrefs.SetInt(IntroKeyPrefix + level.name, 1);
            PlayerPrefs.Save();
        }
        Destroy(gameObject);
    }

    IEnumerator Block(IEnumerator routine)
    {
        IsBlocking = true;
        var data = _moving != null && _moving.character != null ? _moving.character.tran : null;
        if (data != null) data.DialogManager = true; // Play не запускается
        yield return routine;
        if (data != null) data.DialogManager = false;
        IsBlocking = false;
    }

    void OnDestroy()
    {
        if (IsBlocking)
        {
            IsBlocking = false;
            var data = _moving != null && _moving.character != null ? _moving.character.tran : null;
            if (data != null) data.DialogManager = false;
        }
    }

    // ───────────── UI: холст, сцена, окно реплик ─────────────

    TMP_FontAsset _font;
    RectTransform _root, _stage, _box, _portraitFrame, _portrait, _nameTag, _next;
    CanvasGroup _rootGroup;
    Image _portraitImg, _nameTagImg, _black, _flash;
    TextMeshProUGUI _name, _text;
    bool _tapped, _skip, _typing, _boxShown;
    float[] _revealTime = new float[0];
    int _visible;
    TextMeshProUGUI _waveText; // заголовок с «волной» букв
    float _pitch = 1.15f;

    void BuildCanvas()
    {
        if (_root != null) return;
        _font = GameFont.Main;
        var canvasGo = new GameObject("StoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600; // над картой глав (400) и туториалом (450)
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = NewRect("Root", canvasGo.transform);
        Stretch(_root);
        _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();

        // Тап в любом месте — «дальше». Ловушка лежит под сценой и окном.
        var catcher = NewRect("Catcher", _root);
        Stretch(catcher);
        var catcherImg = catcher.gameObject.AddComponent<Image>();
        catcherImg.color = new Color(0f, 0f, 0f, 0f);
        var catchBtn = catcher.gameObject.AddComponent<Button>();
        catchBtn.transition = Selectable.Transition.None;
        catchBtn.onClick.AddListener(() => _tapped = true);

        _stage = NewRect("Stage", _root);
        Stretch(_stage);

        BuildBox();

        _flash = FullImage("Flash", Color.white);
        _flash.color = new Color(1f, 1f, 1f, 0f);
        _black = FullImage("Black", Color.black);
        _black.color = new Color(0f, 0f, 0f, 0f);
        _box.SetAsLastSibling();
        _black.transform.SetAsLastSibling();
    }

    Image FullImage(string name, Color c)
    {
        var rt = NewRect(name, _root);
        Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = StoryArt.White;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    void BuildBox()
    {
        _box = NewRect("Dialog", _root);
        _box.anchorMin = _box.anchorMax = new Vector2(0.5f, 0f);
        _box.pivot = new Vector2(0.5f, 0f);
        _box.sizeDelta = new Vector2(1180f, 240f);
        _box.anchoredPosition = new Vector2(0f, 36f);
        var bg = _box.gameObject.AddComponent<Image>();
        bg.sprite = PixelSprites.Panel;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 0.2f;
        var btn = _box.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { _tapped = true; PixelSfx.Play(Sfx.Click, 0.6f); });

        _portraitFrame = NewRect("PortraitFrame", _box);
        _portraitFrame.anchorMin = _portraitFrame.anchorMax = new Vector2(0f, 0.5f);
        _portraitFrame.pivot = new Vector2(0f, 0.5f);
        _portraitFrame.anchoredPosition = new Vector2(24f, -6f);
        _portraitFrame.sizeDelta = new Vector2(176f, 176f);
        var frameImg = _portraitFrame.gameObject.AddComponent<Image>();
        frameImg.sprite = PixelSprites.Tag;
        frameImg.type = Image.Type.Sliced;
        frameImg.pixelsPerUnitMultiplier = 0.2f;
        frameImg.color = new Color(0.82f, 0.74f, 0.95f, 1f);
        frameImg.raycastTarget = false;

        _portrait = NewRect("Portrait", _portraitFrame);
        _portrait.anchorMin = new Vector2(0.12f, 0.12f);
        _portrait.anchorMax = new Vector2(0.88f, 0.88f);
        _portrait.offsetMin = _portrait.offsetMax = Vector2.zero;
        _portraitImg = _portrait.gameObject.AddComponent<Image>();
        _portraitImg.preserveAspect = true;
        _portraitImg.raycastTarget = false;

        _nameTag = NewRect("NameTag", _box);
        _nameTag.anchorMin = _nameTag.anchorMax = new Vector2(0f, 1f);
        _nameTag.pivot = new Vector2(0f, 0.5f);
        _nameTag.anchoredPosition = new Vector2(30f, 0f);
        _nameTag.sizeDelta = new Vector2(260f, 56f);
        _nameTagImg = _nameTag.gameObject.AddComponent<Image>();
        _nameTagImg.sprite = PixelSprites.Tag;
        _nameTagImg.type = Image.Type.Sliced;
        _nameTagImg.pixelsPerUnitMultiplier = 0.2f;
        _nameTagImg.raycastTarget = false;
        _name = NewText(_nameTag, "", 32f, TextAlignmentOptions.Center);
        _name.color = Color.white;
        _name.enableAutoSizing = true; // длинные имена («Forest Guardian», «Небесный кит») уменьшаются
        _name.fontSizeMax = 32f;
        _name.fontSizeMin = 18f;

        var textRt = NewRect("Text", _box);
        Stretch(textRt);
        _text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) _text.font = _font;
        _text.fontSize = 36f;
        _text.color = Ink;
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.enableWordWrapping = true;
        _text.raycastTarget = false;

        _next = NewRect("Next", _box);
        _next.anchorMin = _next.anchorMax = new Vector2(1f, 0f);
        _next.sizeDelta = new Vector2(34f, 26f);
        var nextImg = _next.gameObject.AddComponent<Image>();
        nextImg.sprite = PixelSprites.ArrowDown;
        nextImg.color = ElaColor;
        nextImg.raycastTarget = false;
        _next.gameObject.SetActive(false);

        var skip = NewRect("Skip", _box);
        skip.anchorMin = skip.anchorMax = new Vector2(1f, 1f);
        skip.pivot = new Vector2(1f, 0.5f);
        skip.anchoredPosition = new Vector2(-24f, 0f);
        skip.sizeDelta = new Vector2(130f, 48f);
        var skipImg = skip.gameObject.AddComponent<Image>();
        skipImg.sprite = PixelSprites.Tag;
        skipImg.type = Image.Type.Sliced;
        skipImg.pixelsPerUnitMultiplier = 0.2f;
        skipImg.color = new Color(0.55f, 0.5f, 0.62f, 1f);
        var skipBtn = skip.gameObject.AddComponent<Button>();
        skipBtn.onClick.AddListener(() => { _skip = true; _tapped = true; });
        var skipText = NewText(skip, "Skip", 28f, TextAlignmentOptions.Center);
        skipText.color = Color.white;
        LocText.Bind(skipText, "Skip", fit: true);
        ButtonJuice.Ensure(skip.gameObject);

        _box.localScale = Vector3.zero;
    }

    /// <summary>Реплика: окно выезжает (если скрыто), текст печатается по буквам, ждём тапа.</summary>
    IEnumerator Say(Speaker s, string line)
    {
        if (_skip) yield break;
        bool narrator = string.IsNullOrEmpty(s.Name);
        _portraitFrame.gameObject.SetActive(!narrator && s.Portrait != null);
        _nameTag.gameObject.SetActive(!narrator);
        if (!narrator)
        {
            _name.text = Loc.T(s.Name);
            _nameTagImg.color = s.Tag;
        }
        if (s.Portrait != null)
        {
            _portraitImg.sprite = s.Portrait;
            _portraitImg.color = s.Tint;
        }
        _pitch = s.Pitch;
        var textRt = _text.rectTransform;
        textRt.offsetMin = new Vector2(narrator || s.Portrait == null ? 48f : 222f, 34f);
        textRt.offsetMax = new Vector2(-48f, narrator ? -34f : -40f);
        _text.alignment = narrator ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        _text.fontStyle = narrator ? FontStyles.Italic : FontStyles.Normal;

        if (!_boxShown)
        {
            _boxShown = true;
            _text.text = "";
            PixelSfx.Play(Sfx.Open, 0.7f);
            yield return Tween(0.3f, k => _box.localScale = Vector3.one * BackOut(k));
        }
        yield return TypeText(Loc.T(line)); // тексты — по-английски, перевод — Loc
        _next.gameObject.SetActive(true);
        _tapped = false;
        while (!_tapped && !_skip) yield return null;
        _tapped = false;
        _next.gameObject.SetActive(false);
    }

    IEnumerator HideBox()
    {
        if (!_boxShown) yield break;
        _boxShown = false;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f) { _box.localScale = Vector3.one * (1f - t); yield return null; }
        _box.localScale = Vector3.zero;
    }

    IEnumerator TypeText(string text)
    {
        _text.text = text ?? "";
        _text.ForceMeshUpdate();
        int total = _text.textInfo.characterCount;
        _revealTime = new float[total];
        for (int i = 0; i < total; i++) _revealTime[i] = float.MaxValue;
        _visible = 0;
        _text.maxVisibleCharacters = 0;
        _typing = true;
        _tapped = false;
        float acc = 0f;
        while (_visible < total)
        {
            if (_tapped || _skip)
            {
                for (int i = _visible; i < total; i++) _revealTime[i] = Time.unscaledTime;
                _visible = total;
                _tapped = false;
                break;
            }
            acc += Time.unscaledDeltaTime * TypeSpeed;
            while (acc >= 1f && _visible < total)
            {
                acc -= 1f;
                char ch = _text.textInfo.characterInfo[_visible].character;
                _revealTime[_visible] = Time.unscaledTime;
                _visible++;
                if (!char.IsWhiteSpace(ch) && _visible % 2 == 0)
                    PixelSfx.Play(Sfx.Blip, 0.5f, _pitch + Random.Range(-0.06f, 0.06f), 0.04f);
                if (ch == '.' || ch == '!' || ch == '?') acc -= 6f;
                else if (ch == ',') acc -= 3f;
            }
            _text.maxVisibleCharacters = _visible;
            yield return null;
        }
        _text.maxVisibleCharacters = total;
        _typing = false;
    }

    void Update()
    {
        AnimateText();
        AnimateWave();
        if (_portrait != null)
        {
            float t = Time.unscaledTime;
            _portrait.anchoredPosition = _typing
                ? new Vector2(0f, Mathf.Round(Mathf.Abs(Mathf.Sin(t * 14f)) * 3f) * 2f)
                : new Vector2(0f, Mathf.Round(Mathf.Sin(t * 2f) * 1.5f) * 2f);
        }
        if (_next != null && _next.gameObject.activeSelf)
            _next.anchoredPosition = new Vector2(-34f, 26f + Mathf.Round(Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 3f) * 2f);
    }

    /// <summary>Буквы реплики выскакивают снизу и чуть покачиваются (как в туториале).</summary>
    void AnimateText()
    {
        if (_text == null || string.IsNullOrEmpty(_text.text) || _revealTime.Length == 0) return;
        _text.ForceMeshUpdate();
        var info = _text.textInfo;
        float now = Time.unscaledTime;
        for (int i = 0; i < info.characterCount && i < _revealTime.Length; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible || i >= _visible) continue;
            float age = now - _revealTime[i];
            float pop = age < 0.16f ? BackOut(age / 0.16f) : 1f;
            float jump = age < 0.22f ? (1f - age / 0.22f) * 12f : 0f;
            float wave = Mathf.Sin(now * 6f + i * 0.55f) * 1.6f;
            float dy = Mathf.Round((jump + wave) / 2f) * 2f;
            int mat = ch.materialReferenceIndex, v = ch.vertexIndex;
            var verts = info.meshInfo[mat].vertices;
            Vector3 center = (verts[v] + verts[v + 2]) * 0.5f;
            for (int j = 0; j < 4; j++) verts[v + j] = center + (verts[v + j] - center) * pop + new Vector3(0f, dy, 0f);
        }
        _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    /// <summary>Заголовок на титульном экране: буквы идут волной (по 2 пикселя).</summary>
    void AnimateWave()
    {
        if (_waveText == null) return;
        _waveText.ForceMeshUpdate();
        var info = _waveText.textInfo;
        float now = Time.unscaledTime;
        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            float dy = Mathf.Round(Mathf.Sin(now * 3f - i * 0.45f) * 3f) * 2f;
            int mat = ch.materialReferenceIndex, v = ch.vertexIndex;
            var verts = info.meshInfo[mat].vertices;
            for (int j = 0; j < 4; j++) verts[v + j] += new Vector3(0f, dy, 0f);
        }
        _waveText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    // ───────────── титульный экран ─────────────

    IEnumerator TitleScreen()
    {
        BuildCanvas();
        _black.color = Color.black;
        BuildTitleBackdrop();
        var loops = new List<Coroutine> { StartCoroutine(DriftClouds()) };

        var titleShadow = NewText(_stage, "CRYSTAL ADVENTURE", 124f, TextAlignmentOptions.Center);
        titleShadow.color = new Color(0.16f, 0.08f, 0.22f, 0.85f);
        Place(titleShadow.rectTransform, new Vector2(8f, 322f), new Vector2(1800f, 160f));
        var title = NewText(_stage, "CRYSTAL ADVENTURE", 124f, TextAlignmentOptions.Center);
        title.color = Color.white;
        Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(1800f, 160f));
        var sub = NewText(_stage, "The Ascent", 76f, TextAlignmentOptions.Center);
        sub.color = Gold;
        Place(sub.rectTransform, new Vector2(0f, 214f), new Vector2(1200f, 100f));
        _waveText = sub;

        // Cris с Ela на голове парят над облаками.
        var heroes = NewRect("Heroes", _stage);
        Place(heroes, new Vector2(0f, -120f), Vector2.zero);
        var cris = Art(heroes, _crisSprite, new Vector2(0f, -60f), 9f);
        var ela = Art(heroes, _elaSprite, Vector2.zero, 4f);
        if (ela != null) ela.rectTransform.anchoredPosition = OnTopOf(cris, ela, SitOverlap(9f, 4f));
        loops.Add(StartCoroutine(Bob(heroes, 6f, 1.6f)));

        string choice = null;
        var buttons = new List<GameObject>
        {
            MenuButton("Play", new Vector2(0f, -330f), new Vector2(380f, 120f), Gold, 60f, () => choice = "play"),
            MenuButton("Levels", new Vector2(-380f, -470f), new Vector2(300f, 84f), new Color(0.47f, 0.86f, 0.55f, 1f), 40f, () => choice = "levels"),
            MenuButton("Prologue", new Vector2(0f, -470f), new Vector2(300f, 84f), new Color(0.82f, 0.74f, 0.95f, 1f), 40f, () => choice = "prologue"),
            MenuButton("Settings", new Vector2(380f, -470f), new Vector2(300f, 84f), new Color(0.62f, 0.80f, 1f, 1f), 40f, () => choice = "settings"),
        };

        // Язык — в правом верхнем углу: тап переключает English / Русский, надписи меняются сразу.
        var language = MenuButton("Language", new Vector2(790f, 475f), new Vector2(260f, 72f), new Color(0.62f, 0.80f, 1f, 1f), 34f, () =>
        {
            Loc.Toggle();
            PixelSfx.Play(Sfx.Click, 0.8f, Loc.IsRussian ? 0.9f : 1.1f);
        });
        LocText.Bind(language.GetComponentInChildren<TextMeshProUGUI>(), () => Loc.CurrentName, fit: true);
        buttons.Add(language);

        yield return Tween(0.6f, k => _black.color = new Color(0f, 0f, 0f, 1f - k));
        PixelSfx.Play(Sfx.Open, 0.8f);

        // Меню: настройки и карта уровней открываются ПОВЕРХ титульного экрана и возвращают в него.
        bool levelChosen = false;
        while (true)
        {
            choice = null;
            while (choice == null) yield return null;
            PixelSfx.Play(Sfx.Click, 0.8f);
            if (choice == "settings")
            {
                var settings = SettingsPanel.Open(700);
                while (settings != null && settings.IsOpen) yield return null;
                continue;
            }
            if (choice == "levels")
            {
                var map = FindObjectOfType<LevelMapMenu>();
                if (map == null) { choice = "play"; break; }
                map.OpenOverMenu(700);
                while (map != null && map.IsOpen) yield return null;
                if (Levelloader.IsLoading) { levelChosen = true; break; } // выбрали уровень — он уже загружается
                continue;                                                 // закрыли карту — снова меню
            }
            break; // play / prologue
        }
        foreach (var b in buttons) Destroy(b);

        // Play (не в первый раз) — продолжить с первого непройденного уровня, если открыт другой.
        if (choice == "play" && !levelChosen && PlayerPrefs.GetInt(PrologueKey, 0) != 0)
            levelChosen = ContinueFromSave();

        if (levelChosen)
        {
            // Уровень грузится (герой улетает, облака) — меню быстро растворяется, чтобы переход было видно.
            var oldBox = FindObjectOfType<MenuBox>(true);
            if (oldBox != null) oldBox.CloseBox();
            yield return Tween(0.25f, k => _rootGroup.alpha = 1f - k, ignoreSkip: true);
            foreach (var c in loops) if (c != null) StopCoroutine(c);
            _waveText = null;
            Destroy(_root.parent.gameObject);
            _root = null;
            yield break;
        }

        bool firstTime = PlayerPrefs.GetInt(PrologueKey, 0) == 0;
        yield return Tween(0.4f, k => _black.color = new Color(0f, 0f, 0f, k));
        foreach (var c in loops) if (c != null) StopCoroutine(c);
        _waveText = null;
        ClearStage();

        if (choice == "prologue" || (choice == "play" && firstTime))
        {
            yield return Prologue();
            PlayerPrefs.SetInt(PrologueKey, 1);
            PlayerPrefs.Save();
        }

        // Старое стартовое окошко больше не нужно — титульный экран его заменяет.
        var oldMenu = FindObjectOfType<MenuBox>(true);
        if (oldMenu != null) oldMenu.CloseBox();

        yield return FinishOverlay();
    }

    void BuildTitleBackdrop()
    {
        Fill(StoryArt.Sky(new Color(0.20f, 0.16f, 0.42f, 1f), new Color(0.98f, 0.66f, 0.62f, 1f)));
        Stars(26, 120f);
        var mountain = Art(_stage, StoryArt.Mountain, new Vector2(560f, -540f), 5f);
        var heart = Art(_stage, StoryArt.Heart, new Vector2(560f, -540f + 5f * 88f + 40f), 3f);
        if (heart != null) StartCoroutine(Glow(heart));
        if (mountain != null) mountain.color = new Color(0.85f, 0.85f, 1f, 1f);
        for (int i = 0; i < 6; i++)
            Art(_stage, StoryArt.Cloud(i), new Vector2(-900f + i * 360f, -380f + (i % 3) * 120f), 5f + i % 2).name = "Cloud";
    }

    /// <summary>
    /// Открыть первый непройденный уровень (SaveManager.FirstUnfinished), если сейчас загружен другой.
    /// true — уровень начал грузиться (меню растворяется, как при выборе на карте).
    ///
    /// Исправлено: Play всегда продолжал с уровня, загруженного при запуске (обычно с первого),
    /// и после перезапуска игры приходилось искать своё место на карте уровней.
    /// </summary>
    static bool ContinueFromSave()
    {
        var list = LevelChapters.CurrentList;
        if (list == null || list.Count == 0) return false;
        int target = SaveManager.FirstUnfinished();
        int current = TilemapData.CurrentLevel != null ? list.IndexOf(TilemapData.CurrentLevel) : -1;
        if (target == current || target < 0) return false;
        var loader = FindObjectOfType<Levelloader>();
        if (loader == null) return false;
        loader.OpenLevel(target);
        return true;
    }

    GameObject MenuButton(string label, Vector2 pos, Vector2 size, Color color, float fontSize, System.Action onClick)
    {
        var rt = NewRect(label + "Button", _stage);
        Place(rt, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = PixelSprites.Tag;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.2f;
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());
        var t = NewText(rt, label, fontSize, TextAlignmentOptions.Center);
        t.color = Ink;
        LocText.Bind(t, label, fit: true); // язык можно сменить прямо на титульном экране
        ButtonJuice.Ensure(rt.gameObject, true);
        return rt.gameObject;
    }

    /// <summary>Сцена закончилась: окно прячется, чёрный экран растворяется — под ним игра.</summary>
    IEnumerator FinishOverlay()
    {
        yield return HideBox();
        _black.color = Color.black;
        ClearStage();
        _skip = false;
        yield return Tween(0.5f, k => _rootGroup.alpha = 1f - k);
        Destroy(_root.parent.gameObject);
        _root = null;
    }

    // ───────────── пролог ─────────────

    IEnumerator Prologue()
    {
        BuildCanvas();
        _skip = false;
        _rootGroup.alpha = 1f;
        _black.color = Color.black;
        var L = StoryLines.Prologue;
        int n = 0;

        // 1. Сердце над облаками. Камера медленно поднимается к вершине.
        Fill(StoryArt.Sky(new Color(0.08f, 0.06f, 0.22f, 1f), new Color(0.42f, 0.30f, 0.62f, 1f)));
        Stars(40, 0f);
        var world = NewRect("World", _stage);
        Stretch(world);
        const float Peak = -420f; // низ горы; вершина ≈ Peak + 6 × 87
        Art(world, StoryArt.Mountain, new Vector2(0f, Peak), 6f);
        var heart = Art(world, StoryArt.Heart, new Vector2(0f, Peak + 6f * 88f + 76f), 5f);
        var glow = Art(world, StoryArt.Glow, heart.rectTransform.anchoredPosition, 10f);
        glow.color = new Color(1f, 0.6f, 0.85f, 0.35f);
        glow.transform.SetSiblingIndex(heart.transform.GetSiblingIndex());
        var heartGlow = StartCoroutine(Glow(heart));
        for (int i = 0; i < 5; i++) Art(world, StoryArt.Cloud(i), new Vector2(-760f + i * 380f, -40f + (i % 2) * 150f), 5f).name = "Cloud";
        var drift = StartCoroutine(DriftClouds());
        world.anchoredPosition = new Vector2(0f, -260f);
        StartCoroutine(Tween(4f, k => world.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(-260f, 0f, Smooth(k)) / 2f) * 2f)));
        yield return Tween(0.8f, k => _black.color = new Color(0f, 0f, 0f, 1f - k));
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);

        // 2. Трещина: вспышка, тряска, осколки разлетаются и падают вниз.
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        if (!_skip)
        {
            PixelSfx.Play(Sfx.Edge, 1f, 0.5f);
            var crack = Art(world, StoryArt.HeartCrack, heart.rectTransform.anchoredPosition, 5f);
            StartCoroutine(Flash(0.8f));
            yield return Shake(world, 14f, 0.45f);
            StopCoroutine(heartGlow);
            heart.color = new Color(0.55f, 0.45f, 0.6f, 1f);
            glow.color = new Color(1f, 0.6f, 0.85f, 0.08f);
            PixelSfx.Play(Sfx.Whoosh, 0.9f, 0.8f);
            for (int i = 0; i < 14; i++)
            {
                float a = Mathf.Lerp(20f, 160f, i / 13f) * Mathf.Deg2Rad;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(500f, 900f);
                StartCoroutine(FlyShard(world, heart.rectTransform.anchoredPosition, v));
            }
            yield return Wait(1.4f);
            if (crack != null) crack.color = new Color(1f, 1f, 1f, 0.9f);
        }
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        StopCoroutine(drift);

        // 3. Лес: осколок падает, из света появляется Cris.
        yield return FadeToBlack();
        yield return HideBox();
        ClearStage();
        Fill(StoryArt.Sky(new Color(0.45f, 0.72f, 1f, 1f), new Color(0.82f, 0.94f, 1f, 1f)));
        for (int i = 0; i < 4; i++) Art(_stage, StoryArt.Cloud(i + 2), new Vector2(-700f + i * 480f, 300f - (i % 2) * 90f), 5f).name = "Cloud";
        drift = StartCoroutine(DriftClouds());
        const float GroundY = -250f;
        Art(_stage, StoryArt.ForestGround, new Vector2(0f, GroundY), 12f);
        Art(_stage, StoryArt.Tree(0), new Vector2(-720f, GroundY - 4f), 9f);
        Art(_stage, StoryArt.Tree(1), new Vector2(-430f, GroundY - 4f), 8f);
        var bush = Art(_stage, StoryArt.Tree(2), new Vector2(560f, GroundY - 4f), 9f);
        Art(_stage, StoryArt.Tree(3), new Vector2(800f, GroundY - 4f), 8f);
        yield return Tween(0.6f, k => _black.color = new Color(0f, 0f, 0f, 1f - k));

        Image cris = null, ela = null;
        if (!_skip)
        {
            var shard = Art(_stage, StoryArt.Shard, new Vector2(0f, 640f), 10f);
            PixelSfx.Play(Sfx.Whoosh, 0.7f, 1.3f);
            yield return Tween(0.7f, k =>
            {
                shard.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(640f, GroundY + 40f, k * k) / 2f) * 2f);
                shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(k * 8f) * 45f);
            });
            Destroy(shard.gameObject);
            PixelSfx.Play(Sfx.Crystal, 1f, 0.8f);
            StartCoroutine(Flash(0.6f));
        }
        cris = Art(_stage, _crisSprite, new Vector2(0f, GroundY), 9f);
        if (cris != null)
        {
            PixelFX.BurstUI(cris.rectTransform, new[] { Color.white, new Color(1f, 0.6f, 0.85f, 1f), Gold }, 18, 520f, 22f, 0.8f);
            yield return Tween(0.4f, k => cris.rectTransform.localScale = Vector3.one * BackOut(k));
            yield return Wait(0.4f);
            // оглядывается
            for (int i = 0; i < 3 && !_skip; i++)
            {
                cris.rectTransform.localScale = new Vector3(i % 2 == 0 ? -1f : 1f, 1f, 1f);
                PixelSfx.Play(Sfx.Step, 0.4f, 1.3f);
                yield return Wait(0.35f);
            }
            cris.rectTransform.localScale = Vector3.one;
        }
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text); // Cris: ...?

        // 4. Из-за куста выпрыгивает Ela.
        ela = Art(_stage, _elaSprite, Vector2.zero, 5f);
        if (ela != null)
        {
            float feet = GroundY + Height(ela) * ela.rectTransform.pivot.y;
            ela.rectTransform.anchoredPosition = new Vector2(560f, feet);
            if (bush != null) ela.transform.SetSiblingIndex(bush.transform.GetSiblingIndex()); // сначала за кустом
            yield return Hop(ela.rectTransform, new Vector2(560f, feet), new Vector2(300f, feet), 180f, 0.5f);
        }
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        // Ela запрыгивает Cris на голову — так она и ездит в игре.
        if (ela != null && cris != null && !_skip)
        {
            ela.transform.SetAsLastSibling();
            var top = OnTopOf(cris, ela, SitOverlap(9f, 5f));
            yield return Hop(ela.rectTransform, ela.rectTransform.anchoredPosition, top, 260f, 0.55f);
            PixelSfx.Play(Sfx.Land, 0.9f, 1.2f);
            PixelFX.BurstUI(ela.rectTransform, new[] { Color.white, Gold }, 10, 300f, 16f, 0.6f);
        }
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        StopCoroutine(drift);

        // 5. Путь наверх: тропинка от леса к вершине и надпись THE ASCENT.
        yield return FadeToBlack();
        yield return HideBox();
        ClearStage();
        Fill(StoryArt.Sky(new Color(0.30f, 0.45f, 0.85f, 1f), new Color(1f, 0.80f, 0.62f, 1f)));
        Stars(14, 260f);
        const float Base = -480f;
        Art(_stage, StoryArt.Mountain, new Vector2(0f, Base), 7f);
        var heartPos = new Vector2(0f, Base + 7f * 88f + 80f);
        var heart2 = Art(_stage, StoryArt.Heart, heartPos, 5f);
        if (heart2 != null) heart2.color = new Color(0.55f, 0.45f, 0.6f, 1f);
        Art(_stage, StoryArt.HeartCrack, heartPos, 5f);
        for (int i = 0; i < 3; i++) Art(_stage, StoryArt.Cloud(i + 1), new Vector2(-330f + i * 330f, Base + 7f * 66f + (i % 2) * 40f), 4f);
        yield return Tween(0.6f, k => _black.color = new Color(0f, 0f, 0f, 1f - k));
        var path = new[]
        {
            // от леса у подножия, через снег — к Сердцу на вершине (гора ×7: вершина ≈ y 130)
            new Vector2(-250f, -430f), new Vector2(-150f, -400f), new Vector2(-40f, -370f), new Vector2(80f, -330f),
            new Vector2(160f, -280f), new Vector2(100f, -230f), new Vector2(0f, -190f), new Vector2(-80f, -140f),
            new Vector2(-30f, -90f), new Vector2(40f, -40f), new Vector2(20f, 10f), new Vector2(0f, 60f), new Vector2(0f, 105f),
        };
        for (int i = 0; i < path.Length && !_skip; i++)
        {
            var dot = Art(_stage, StoryArt.Star, path[i], 8f);
            dot.color = Gold;
            PixelSfx.Play(Sfx.Blip, 0.5f, 0.9f + i * 0.05f, 0f);
            yield return Wait(0.09f);
        }
        var ascent = NewText(_stage, Loc.T("THE ASCENT"), 150f, TextAlignmentOptions.Center);
        ascent.color = Gold;
        Place(ascent.rectTransform, new Vector2(0f, 420f), new Vector2(1600f, 200f));
        PixelSfx.Play(Sfx.Star, 1f, 0.9f);
        yield return Tween(0.35f, k => ascent.rectTransform.localScale = Vector3.one * BackOut(k));
        yield return Say(SpeakerFor(L[n].Who), L[n++].Text);
        yield return FadeToBlack();
    }

    IEnumerator FlyShard(RectTransform parent, Vector2 from, Vector2 velocity)
    {
        var s = Art(parent, StoryArt.Shard, from, 6f);
        if (s == null) yield break;
        var p = from;
        float spin = Random.Range(-1f, 1f) > 0f ? 45f : -45f;
        for (float t = 0f; t < 2.2f && s != null; t += Time.unscaledDeltaTime)
        {
            velocity += new Vector2(0f, -1400f) * Time.unscaledDeltaTime;
            p += velocity * Time.unscaledDeltaTime;
            s.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.x / 2f) * 2f, Mathf.Round(p.y / 2f) * 2f);
            s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(t * 8f) * spin);
            yield return null;
        }
        if (s != null) Destroy(s.gameObject);
    }

    // ───────────── вступление перед боссом ─────────────

    IEnumerator Intro(StoryLines.Line[] lines)
    {
        BuildCanvas();
        _skip = false;
        _rootGroup.alpha = 1f;
        var dim = FullImage("Dim", new Color(0.08f, 0.04f, 0.14f, 0f));
        dim.transform.SetSiblingIndex(_stage.GetSiblingIndex());
        yield return Tween(0.3f, k => dim.color = new Color(0.08f, 0.04f, 0.14f, 0.55f * k));
        foreach (var line in lines)
        {
            if (_skip) break;
            yield return Say(SpeakerFor(line.Who), line.Text);
        }
        yield return HideBox();
        yield return Tween(0.3f, k => _rootGroup.alpha = 1f - k);
        Destroy(_root.parent.gameObject);
        _root = null;
    }

    /// <summary>
    /// «Глава пройдена»: небо главы, гора со звездой на высоте главы (лес — у подножия, снег — посередине,
    /// небо — у вершины), название главы, звёзды за главу, реплики Элы (StoryLines.Outros).
    /// После главы Sky — финал: Сердце снова сияет и «Спасибо за игру!».
    /// </summary>
    IEnumerator ChapterComplete(int ch, LevelSaveDatabase save)
    {
        BuildCanvas();
        _skip = false;
        _rootGroup.alpha = 1f;
        bool final = ch >= ChapterBosses.Length - 1;

        yield return Tween(0.4f, k => _black.color = new Color(0f, 0f, 0f, k), ignoreSkip: true);
        ClearStage();
        if (ch == 0) Fill(StoryArt.Sky(new Color(0.33f, 0.60f, 0.90f, 1f), new Color(0.98f, 0.93f, 0.76f, 1f)));
        else if (ch == 1) Fill(StoryArt.Sky(new Color(0.40f, 0.50f, 0.72f, 1f), new Color(0.86f, 0.91f, 0.98f, 1f)));
        else Fill(StoryArt.Sky(new Color(0.20f, 0.16f, 0.42f, 1f), new Color(0.98f, 0.66f, 0.62f, 1f)));
        Stars(final ? 30 : 10, final ? 60f : 260f);
        for (int i = 0; i < 4; i++)
            Art(_stage, StoryArt.Cloud(i), new Vector2(-900f + i * 520f, -420f + (i % 2) * 140f), 5f).name = "Cloud";

        // Гора и Сердце — как на титульном экране; звезда отмечает, докуда дошли.
        const float mountainX = 560f, mountainY = -540f, scale = 5f;
        var mountain = Art(_stage, StoryArt.Mountain, new Vector2(mountainX, mountainY), scale);
        var heart = Art(_stage, StoryArt.Heart, new Vector2(mountainX, mountainY + scale * 88f + 40f), 3f);
        if (heart != null)
        {
            if (final) StartCoroutine(Glow(heart));
            else heart.color = new Color(0.55f, 0.5f, 0.65f, 1f); // Сердце ещё не собрано
        }
        float[] heights = { 0.22f, 0.58f, 0.95f };
        var mark = Art(_stage, StoryArt.Star, new Vector2(mountainX, mountainY + scale * 88f * heights[Mathf.Clamp(ch, 0, 2)]), 10f);
        if (mark != null) { mark.color = Gold; StartCoroutine(Twinkle(mark)); }

        // Заголовок, глава, звёзды.
        var titleRt = NewRect("Title", _stage);
        Place(titleRt, new Vector2(-380f, 300f), new Vector2(1000f, 120f));
        var title = NewText(titleRt, Loc.T(final ? "The Ascent is complete!" : "Chapter complete!"), 70f, TextAlignmentOptions.Center);
        title.color = Color.white;
        var nameRt = NewRect("Chapter", _stage);
        Place(nameRt, new Vector2(-380f, 180f), new Vector2(1000f, 120f));
        var chapterName = NewText(nameRt, LevelChapters.Name(ch), 96f, TextAlignmentOptions.Center);
        chapterName.color = Gold;

        int got = 0, count = 0;
        var list = LevelChapters.CurrentList;
        if (list != null)
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && LevelChapters.ChapterOf(list, i) == ch) { count++; got += SaveManager.StarsOf(save, list[i].name); }
        var starsRt = NewRect("Stars", _stage);
        Place(starsRt, new Vector2(-380f, 70f), new Vector2(1000f, 90f));
        var stars = NewText(starsRt, Loc.F("Stars: {0} / {1}", got, count * 3), 54f, TextAlignmentOptions.Center);
        stars.color = Color.white;

        yield return Tween(0.5f, k => _black.color = new Color(0f, 0f, 0f, 1f - k), ignoreSkip: true);
        PixelSfx.Play(Sfx.Star, 1f, final ? 0.8f : 1f);
        if (final && heart != null) yield return Flash(0.6f);

        foreach (var line in StoryLines.Outros[Mathf.Clamp(ch, 0, StoryLines.Outros.Length - 1)])
        {
            if (_skip) break;
            yield return Say(SpeakerFor(line.Who), line.Text);
        }
        yield return HideBox();

        if (final)
        {
            var thanksRt = NewRect("Thanks", _stage);
            Place(thanksRt, new Vector2(-380f, -80f), new Vector2(1000f, 120f));
            var thanks = NewText(thanksRt, Loc.T("Thank you for playing!"), 64f, TextAlignmentOptions.Center);
            thanks.color = Gold;
            _skip = false;
            _tapped = false;
            while (!_tapped && !_skip) yield return null;
        }

        yield return Tween(0.4f, k => _rootGroup.alpha = 1f - k, ignoreSkip: true);
        Destroy(_root.parent.gameObject);
        _root = null;
    }

    // ───────────── сцена: картинки и движение ─────────────

    void Fill(Sprite sky)
    {
        var rt = NewRect("Sky", _stage);
        Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sky;
        img.raycastTarget = false;
    }

    void Stars(int count, float minY)
    {
        for (int i = 0; i < count; i++)
        {
            var s = Art(_stage, StoryArt.Star, new Vector2(Random.Range(-940f, 940f), Random.Range(minY, 520f)), Random.value < 0.25f ? 6f : 3f);
            if (s == null) continue;
            s.color = new Color(1f, 1f, 1f, Random.Range(0.4f, 0.9f));
            StartCoroutine(Twinkle(s));
        }
    }

    /// <summary>Картинка на сцене: размер = пиксели спрайта × scale, опора — как у спрайта.</summary>
    Image Art(RectTransform parent, Sprite sprite, Vector2 pos, float scale)
    {
        if (sprite == null) return null;
        var rt = NewRect(sprite.name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var size = sprite.rect.size;
        rt.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
        rt.sizeDelta = size * scale;
        rt.anchoredPosition = pos;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static float Height(Image img) => img != null ? img.rectTransform.sizeDelta.y : 0f;

    /// <summary>
    /// На сколько Ela «садится» в Cris: верхушка куба — острый угол ромба, а сидит она на середине
    /// верхней грани (6 пикселей рисунка ниже), плюс 2 пустых пикселя снизу у её спрайта.
    /// </summary>
    static float SitOverlap(float crisScale, float elaScale) => 6f * crisScale + 2f * elaScale;

    /// <summary>Куда поставить top, чтобы он стоял на верхушке below (заходя на overlap пикселей вниз).</summary>
    static Vector2 OnTopOf(Image below, Image top, float overlap)
    {
        if (below == null || top == null) return Vector2.zero;
        var b = below.rectTransform;
        float belowTop = b.anchoredPosition.y + Height(below) * (1f - b.pivot.y);
        return new Vector2(b.anchoredPosition.x, belowTop - overlap + Height(top) * top.rectTransform.pivot.y);
    }

    void ClearStage()
    {
        StopAllCoroutinesExceptRunning();
        if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
        for (int i = _stage.childCount - 1; i >= 0; i--) Destroy(_stage.GetChild(i).gameObject);
    }

    // Фоновые анимации (облака, звёзды) живут в отдельном списке, чтобы их можно было погасить при смене кадра.
    readonly List<Coroutine> _ambient = new List<Coroutine>();

    void StopAllCoroutinesExceptRunning()
    {
        foreach (var c in _ambient) if (c != null) StopCoroutine(c);
        _ambient.Clear();
    }

    IEnumerator Twinkle(Image s)
    {
        yield return null;
        float phase = Random.value * 6f;
        while (s != null)
        {
            var c = s.color;
            c.a = Mathf.Sin(Time.unscaledTime * 2.5f + phase) > 0.6f ? 1f : 0.45f;
            s.color = c;
            yield return null;
        }
    }

    IEnumerator Glow(Image heart)
    {
        var rt = heart.rectTransform;
        var home = rt.anchoredPosition;
        while (heart != null)
        {
            float t = Time.unscaledTime;
            rt.anchoredPosition = home + new Vector2(0f, Mathf.Round(Mathf.Sin(t * 1.8f) * 4f) * 2f);
            yield return null;
        }
    }

    IEnumerator DriftClouds()
    {
        var clouds = new List<RectTransform>();
        foreach (var img in _stage.GetComponentsInChildren<Image>(true))
            if (img.name == "Cloud") clouds.Add(img.rectTransform);
        var x = new float[clouds.Count];
        for (int i = 0; i < clouds.Count; i++) x[i] = clouds[i].anchoredPosition.x;
        while (true)
        {
            for (int i = 0; i < clouds.Count; i++)
            {
                if (clouds[i] == null) continue;
                x[i] += Time.unscaledDeltaTime * (14f + (i % 3) * 8f);
                if (x[i] > 1150f) x[i] = -1150f;
                var p = clouds[i].anchoredPosition;
                clouds[i].anchoredPosition = new Vector2(Mathf.Round(x[i] / 2f) * 2f, p.y);
            }
            yield return null;
        }
    }

    IEnumerator Bob(RectTransform rt, float amp, float speed)
    {
        var home = rt.anchoredPosition;
        while (rt != null)
        {
            rt.anchoredPosition = home + new Vector2(0f, Mathf.Round(Mathf.Sin(Time.unscaledTime * speed) * amp) * 2f);
            yield return null;
        }
    }

    IEnumerator Hop(RectTransform rt, Vector2 from, Vector2 to, float height, float time)
    {
        PixelSfx.Play(Sfx.Jump, 0.6f, 1.3f);
        yield return Tween(time, k =>
        {
            var p = Vector2.Lerp(from, to, k) + new Vector2(0f, 4f * height * k * (1f - k));
            rt.anchoredPosition = new Vector2(Mathf.Round(p.x / 2f) * 2f, Mathf.Round(p.y / 2f) * 2f);
        });
    }

    IEnumerator Shake(RectTransform rt, float amp, float time)
    {
        var home = rt.anchoredPosition;
        for (float t = 0f; t < time && !_skip; t += Time.unscaledDeltaTime)
        {
            float a = amp * (1f - t / time);
            rt.anchoredPosition = home + new Vector2(Mathf.Round(Random.Range(-a, a) / 2f) * 2f, Mathf.Round(Random.Range(-a, a) / 2f) * 2f);
            yield return null;
        }
        rt.anchoredPosition = home;
    }

    IEnumerator Flash(float strength)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
        {
            _flash.color = new Color(1f, 1f, 1f, strength * (1f - t));
            yield return null;
        }
        _flash.color = new Color(1f, 1f, 1f, 0f);
    }

    IEnumerator FadeToBlack() => Tween(0.45f, k => _black.color = new Color(0f, 0f, 0f, k), ignoreSkip: true);

    // ───────────── помощники ─────────────

    IEnumerator Tween(float time, System.Action<float> step, bool ignoreSkip = false)
    {
        for (float t = 0f; t < 1f && (ignoreSkip || !_skip); t += Time.unscaledDeltaTime / time) { step(t); yield return null; }
        step(1f);
    }

    IEnumerator Wait(float s)
    {
        for (float t = 0f; t < s && !_skip; t += Time.unscaledDeltaTime) yield return null;
    }

    new Coroutine StartCoroutine(IEnumerator routine)
    {
        var c = base.StartCoroutine(routine);
        _ambient.Add(c);
        return c;
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

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

    TextMeshProUGUI NewText(RectTransform parent, string text, float size, TextAlignmentOptions align)
    {
        var rt = NewRect("Text", parent);
        Stretch(rt);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }
}
