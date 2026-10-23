using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Язык игры: английский или русский.
///
/// Тексты в коде и ассетах (туториал, сюжет, кнопки) остаются английскими — это «ключ».
/// Loc.T(english) возвращает перевод на текущем языке; если перевода нет — исходный текст.
/// Поэтому новая строка без перевода просто останется английской, игра не сломается.
///
/// Язык сохраняется в PlayerPrefs ("language"). При первом запуске — по языку системы
/// (русский / украинский / белорусский → русский, остальные → английский).
/// Сменить: Loc.Set / Loc.Toggle (кнопка в настройках и на титульном экране).
/// Тексты, которые должны поменяться сразу, привязываются через LocText.
/// </summary>
public static class Loc
{
    public enum Language { English, Russian }

    const string PrefKey = "language";

    static bool _loaded;
    static Language _current;

    /// <summary>Язык сменился — обновить тексты.</summary>
    public static event Action Changed;

    public static Language Current
    {
        get { EnsureLoaded(); return _current; }
    }

    public static bool IsRussian => Current == Language.Russian;

    /// <summary>Название текущего языка — на нём самом (для кнопки).</summary>
    public static string CurrentName => NameOf(Current);

    public static string NameOf(Language l) => l == Language.Russian ? "Русский" : "English";

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string saved = PlayerPrefs.GetString(PrefKey, "");
        if (saved == "ru") _current = Language.Russian;
        else if (saved == "en") _current = Language.English;
        else
        {
            var sys = Application.systemLanguage;
            _current = sys == SystemLanguage.Russian || sys == SystemLanguage.Ukrainian || sys == SystemLanguage.Belarusian
                ? Language.Russian
                : Language.English;
        }
    }

    public static void Set(Language l)
    {
        EnsureLoaded();
        PlayerPrefs.SetString(PrefKey, l == Language.Russian ? "ru" : "en");
        PlayerPrefs.Save();
        if (_current == l) return;
        _current = l;
        Changed?.Invoke();
    }

    public static void Toggle() => Set(Current == Language.Russian ? Language.English : Language.Russian);

    /// <summary>Перевод английского текста на текущий язык.</summary>
    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english) || !IsRussian) return english;
        if (Ru.TryGetValue(english, out var ru)) return ru;
        // Тексты из ассетов могут отличаться пробелами/переносами — сверяем «сжатую» строку.
        if (RuNormalized.TryGetValue(Normalize(english), out ru)) return ru;
        return english;
    }

    /// <summary>Перевод строки формата и подстановка: Loc.F("Cards: {0}", 5).</summary>
    public static string F(string englishFormat, params object[] args)
    {
        string format = T(englishFormat);
        try { return string.Format(format, args); }
        catch (FormatException) { return string.Format(englishFormat, args); }
    }

    static string Normalize(string s) => Regex.Replace(s.Trim(), @"\s+", " ");

    static Dictionary<string, string> _ruNormalized;
    static Dictionary<string, string> RuNormalized
    {
        get
        {
            if (_ruNormalized != null) return _ruNormalized;
            _ruNormalized = new Dictionary<string, string>();
            foreach (var kv in Ru) _ruNormalized[Normalize(kv.Key)] = kv.Value;
            return _ruNormalized;
        }
    }

    // ───────────────────────────── перевод ─────────────────────────────

    static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
    {
        // ── Меню, кнопки ──
        { "Play", "Играть" },
        { "Levels", "Уровни" },
        { "Prologue", "Пролог" },
        { "Settings", "Настройки" },
        { "Music", "Музыка" },
        { "Sounds", "Звуки" },
        { "Language", "Язык" },
        { "Close", "Закрыть" },
        { "Skip", "Пропустить" },
        { "Start", "Старт" },
        { "Save", "Сохранить" },
        { "Choose a level", "Выбери уровень" },
        { "THE ASCENT", "ВОСХОЖДЕНИЕ" },
        { "Your task is to assemble a combination of buttons in order to get a crystal.", "Твоя задача — собрать комбинацию кнопок, чтобы добыть кристалл." },

        // ── Главы ──
        { "Forest", "Лес" },
        { "Winter", "Зима" },
        { "Sky", "Небо" },

        // ── Окно победы ──
        { "Cards: {0}  (best {1})", "Карточек: {0}  (минимум {1})" },
        { "Cards: {0}", "Карточек: {0}" },
        { "No hints", "Без подсказок" },
        { "Hints used: {0}", "Подсказок: {0}" },
        { "Never near the edge", "Ни разу у края" },
        { "Near the edge: {0}", "У края: {0}" },

        // ── Персонажи ──
        { "Ela", "Эла" },
        { "Cris", "Крис" },
        { "Forest Guardian", "Страж леса" },
        { "Sky Whale", "Небесный кит" },
        { "Frost Diamond", "Ледяной ромб" },

        // ── Пролог ──
        { "High above the clouds, at the very top of the island, shone the Crystal Heart.", "Высоко над облаками, на самой вершине острова, сияло Кристальное Сердце." },
        { "Its light kept every land alive: the Forest, the snowy Peaks and the Sky.", "Его свет дарил жизнь всем землям: Лесу, снежным Вершинам и Небу." },
        { "But one night... the Heart cracked.", "Но однажды ночью... Сердце треснуло." },
        { "Its shards rained down all over the island.", "Его осколки дождём рассыпались по всему острову." },
        { "Whoa! A little cube made of crystal light!", "Ого! Маленький кубик из кристального света!" },
        { "I'm Ela, a crystal witch. And you can only move by commands, huh?", "Я Эла, кристальная ведьма. А ты, значит, ходишь только по командам?" },
        { "Beep!", "Бип!" },
        { "Perfect! I'll write the commands, and you do the walking.", "Отлично! Я пишу команды, а ты шагаешь." },
        { "If we gather the shards and climb all the way up, we can fix the Heart!", "Если соберём осколки и поднимемся на самый верх, мы починим Сердце!" },
        { "Hop! Let's go, partner!", "Хоп! Вперёд, напарник!" },
        { "And so, the Ascent began.", "Так началось Восхождение." },

        // ── Боссы ──
        { "Careful, Cris! A shard turned the Forest Guardian into a root snake!", "Осторожно, Крис! Осколок превратил Стража леса в змея из корней!" },
        { "SSSS... MY... FOREST...", "ШШШ... МОЙ... ЛЕС..." },
        { "Don't touch any part of it. Sneak in right behind its tail!", "Не касайся ни одной его части. Крадись прямо за хвостом!" },
        { "When it stops, spikes grow in front of its head...", "Когда он замирает, перед головой вырастают шипы..." },
        { "...and its tail breaks the ground for good. Watch the red tiles!", "...а хвост навсегда проламывает землю. Следи за красными клетками!" },
        { "Look up, Cris! The Sky Whale is carrying a shard!", "Смотри вверх, Крис! Небесный кит несёт осколок!" },
        { "WOOOOOM...", "УУУУУМ..." },
        { "It can't touch us up there... but it drops bombs right where its shadow is.", "Сверху он нас не заденет... но бросает бомбы прямо туда, где его тень." },
        { "When the shadow turns red, get out from under it!", "Тень покраснела — уходи из-под неё!" },
        { "Brrr... That's the Frost Diamond!", "Бррр... Это Ледяной ромб!" },
        { "COLD... FOREVER...", "ХОЛОД... НАВСЕГДА..." },
        { "Look! It strikes right where you are looking.", "Смотри! Он бьёт туда, куда ты смотришь." },
        { "Lightning hits a cross. Super ice hits a 3x3 and freezes the tiles.", "Молния бьёт крестом. Супер-лёд бьёт квадратом 3x3 и замораживает клетки." },
        { "A step back dodges both. And frozen tiles make you slide!", "Шаг назад спасает от обоих. А по замёрзшим клеткам скользишь!" },

        // ── Туториал: первые шаги ──
        { "Hi, I'm Ela! See that shiny crystal? Let's help Cris get it!", "Привет, я Эла! Видишь блестящий кристалл? Давай поможем Крису его достать!" },
        { "Cris only moves by commands. We build his program right here.", "Крис ходит только по командам. Программу для него собираем вот здесь." },
        { "Tap the arrow card to add one step forward.", "Нажми на карточку со стрелкой — это один шаг вперёд." },
        { "Great, the card is in the program! Now press Play.", "Отлично, карточка в программе! Теперь нажми на треугольник — запуск." },
        { "This time the crystal is two steps away.", "На этот раз кристалл в двух шагах." },
        { "Each card is one step. Add two of them!", "Каждая карточка — один шаг. Добавь две!" },
        { "Added a wrong card? Just tap it in the program to remove it.", "Добавил не ту карточку? Нажми на неё в программе — она уберётся." },
        { "Ready? Press Play!", "Готово? Запускай!" },
        { "Now Cris can turn! Plan the whole path before you press Play.", "Теперь Крис умеет поворачивать! Продумай весь путь, прежде чем запускать." },
        { "Made a mistake? This button clears all your cards.", "Ошибся? Эта кнопка убирает все карточки." },
        { "Stuck? Tap the question mark, and a ghost will show you the way.", "Застрял? Нажми на вопросик — призрак покажет дорогу." },
        { "Fewer cards means more stars. Good luck!", "Меньше карточек — больше звёзд. Удачи!" },

        // ── Туториал: функции ──
        { "Look, a new card: F1. It is a FUNCTION!", "Смотри, новая карточка: F1. Это ФУНКЦИЯ!" },
        { "A function is your own little program. Write the moves once, then use them again and again.", "Функция — это твоя маленькая программа. Запиши ходы один раз и используй снова и снова." },
        { "See the path? It goes like stairs: two steps Forward, two steps Left... and again!", "Видишь дорожку? Она идёт лесенкой: два шага Вперёд, два шага Влево... и снова!" },
        { "Let's teach these stairs to F1. Open the F1 tab.", "Научим F1 этой лесенке. Открой вкладку F1." },
        { "This is the F1 program. Everything here runs every time F1 is used.", "Это программа F1. Всё, что здесь, выполняется каждый раз, когда срабатывает F1." },
        { "Put the Forward card into F1. Two times!", "Положи в F1 карточку Вперёд. Два раза!" },
        { "Now the Left card. Also two times!", "Теперь карточку Влево. Тоже два раза!" },
        { "F1 is ready! Now go back to the Main tab.", "F1 готова! Теперь вернись на вкладку Main." },
        { "Add the F1 card. One card = all four moves!", "Добавь карточку F1. Одна карточка = все четыре хода!" },
        { "Use F1 again whenever the stairs repeat. Finish the path yourself!", "Ставь F1 каждый раз, когда лесенка повторяется. Дальше — сам!" },

        // ── Конец главы и финал (StoryDirector.ChapterComplete, StoryLines.Outros) ──
        { "Chapter complete!", "Глава пройдена!" },
        { "The Ascent is complete!", "Восхождение завершено!" },
        { "Crystals: {0} / {1}", "Кристаллы: {0} / {1}" },
        { "Thank you for playing!", "Спасибо за игру!" },
        { "We did it, Cris! The Forest Guardian is free again!", "Получилось, Крис! Лесной страж снова свободен!" },
        { "The first shard is ours. Look how it glows!", "Первый осколок наш. Смотри, как он сияет!" },
        { "Next stop: the snowy Peaks. Bundle up!", "Дальше — снежные вершины. Одевайся теплее!" },
        { "The Frost Diamond is melting... The second shard!", "Ледяной ромб тает... Второй осколок!" },
        { "Feel that? The wind gets stronger the higher we climb.", "Чувствуешь? Чем выше, тем сильнее ветер." },
        { "Only the Sky is left. Onward and upward, partner!", "Осталось только небо. Вперёд и вверх, напарник!" },
        { "The last shard! Cris, we gathered them all!", "Последний осколок! Крис, мы собрали все!" },
        { "The shards rose into the air and found their places.", "Осколки поднялись в воздух и встали на свои места." },
        { "The Crystal Heart shone again, brighter than ever.", "Кристальное Сердце снова засияло — ярче, чем прежде." },
        { "Thank you, partner. My commands were nothing without your steps.", "Спасибо, напарник. Мои команды ничего не стоят без твоих шагов." },

        // ── Туториал: вторая функция (LevelWinter10) ──
        { "A new card: F2! It is a second function.", "Новая карточка: F2! Это вторая функция." },
        { "Look: this time Main has no arrows. Only F1 and F2!", "Смотри: в этот раз в Main нет стрелок. Только F1 и F2!" },
        { "The path goes down the stairs two times, then up the stairs two times.", "Дорожка два раза спускается лесенкой, а потом два раза поднимается." },
        { "F1 will be the way down. Open the F1 tab.", "F1 будет спуском. Открой вкладку F1." },
        { "Down the stairs: put the Back card two times...", "Спуск: положи карточку Назад два раза..." },
        { "...and one Right card.", "...и одну карточку Вправо." },
        { "F2 will be the way up. Open the F2 tab.", "F2 будет подъёмом. Открой вкладку F2." },
        { "Up the stairs: first one Right card...", "Подъём: сначала одна карточка Вправо..." },
        { "...then the Forward card two times.", "...потом карточка Вперёд два раза." },
        { "Both functions are ready! Go back to the Main tab.", "Обе функции готовы! Вернись на вкладку Main." },
        { "Down two times: put F1 two times.", "Спуститься два раза: положи F1 два раза." },
        { "Up two times: put F2 two times.", "Подняться два раза: положи F2 два раза." },
        { "Ten cards for the whole valley. Press Play!", "Десять карточек на всю долину. Жми Play!" },

        // ── Туториал: циклы ──
        { "Ooh, a new card: LOOP! It repeats other cards for you.", "О, новая карточка: ЦИКЛ! Он повторяет другие карточки за тебя." },
        { "Look at this bridge: one step Forward... and then a LOT of steps Right!", "Посмотри на мостик: один шаг Вперёд... а потом МНОГО шагов Вправо!" },
        { "First, put one Forward card.", "Сначала положи одну карточку Вперёд." },
        { "Now ONE Right card. Just one!", "Теперь ОДНУ карточку Вправо. Только одну!" },
        { "And now the Loop card, right after them.", "А теперь карточку Цикл, сразу после них." },
        { "See the bracket? Everything inside it repeats. But Forward is inside too - we don't want that!", "Видишь скобку? Всё внутри неё повторяется. Но Вперёд тоже внутри — так не надо!" },
        { "Drag the arrow to the right, so only Right stays inside.", "Потяни стрелку вправо, чтобы внутри осталась только Вправо." },
        { "Now tap the x2 badge until it shows x6.", "Теперь нажимай на значок x2, пока не станет x6." },
        { "Six steps Right with just one card. Press Play!", "Шесть шагов Вправо всего одной карточкой. Запускай!" },
        { "Loops save a lot of cards. Use them whenever something repeats!", "Циклы экономят кучу карточек. Используй их, когда что-то повторяется!" },

        // ── Туториал: условия ──
        { "Look, RED tiles! They are special.", "Смотри, КРАСНЫЕ клетки! Они особенные." },
        { "Cards can be painted. A painted card works ONLY when the cube stands on its color.", "Карточки можно красить. Крашеная карточка работает ТОЛЬКО когда куб стоит на клетке её цвета." },
        { "Put a Forward card.", "Положи карточку Вперёд." },
        { "Now a Right card.", "Теперь карточку Вправо." },
        { "Tap the red paint to pick it up.", "Нажми на красную краску, чтобы взять её." },
        { "Now tap the Right card to paint it.", "Теперь нажми на карточку Вправо, чтобы покрасить её." },
        { "A red Right works only on red tiles. Everywhere else it is skipped!", "Красная Вправо работает только на красных клетках. В остальных местах она пропускается!" },
        { "Add the Loop card.", "Добавь карточку Цикл." },
        { "Make it x6.", "Сделай x6." },
        { "Each round: step Forward, and step Right only on red. Press Play!", "Каждый круг: шаг Вперёд, а шаг Вправо — только на красном. Запускай!" },
        { "Painted cards are the IF of real code. A red card next to a blue card is if / else!", "Крашеные карточки — это IF из настоящего кода. Красная рядом с синей — это if / else!" },
    };
}
