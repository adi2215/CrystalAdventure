using System.Collections.Generic;

/// <summary>
/// Тексты сюжета (английский, как в туториале). Who — кто говорит: "" — рассказчик,
/// "Ela", "Cris" или имя босса (портреты — StoryDirector.SpeakerFor).
///
/// Пролог: StoryDirector.Prologue берёт реплики по порядку, между ними — анимации кадров,
/// поэтому число реплик менять вместе с ним (12 штук). Вступления перед боссами — по имени
/// ассета уровня, показываются один раз; их можно свободно дописывать.
/// </summary>
public static class StoryLines
{
    public struct Line
    {
        public string Who, Text;
        public Line(string who, string text) { Who = who; Text = text; }
    }

    static Line N(string text) => new Line("", text);
    static Line Ela(string text) => new Line("Ela", text);
    static Line Cris(string text) => new Line("Cris", text);

    public static readonly Line[] Prologue =
    {
        // 1. Сердце над облаками
        N("High above the clouds, at the very top of the island, shone the Crystal Heart."),
        N("Its light kept every land alive: the Forest, the snowy Peaks and the Sky."),
        // 2. Трещина
        N("But one night... the Heart cracked."),
        N("Its shards rained down all over the island."),
        // 3–4. Лес: Cris и Ela
        Cris("...?"),
        Ela("Whoa! A little cube made of crystal light!"),
        Ela("I'm Ela, a crystal witch. And you can only move by commands, huh?"),
        Cris("Beep!"),
        Ela("Perfect! I'll write the commands, and you do the walking."),
        Ela("If we gather the shards and climb all the way up, we can fix the Heart!"),
        Ela("Hop! Let's go, partner!"),
        // 5. Путь наверх
        N("And so, the Ascent began."),
    };

    /// <summary>
    /// После босса главы — экран «Глава пройдена» (StoryDirector.ChapterComplete): номер главы
    /// (0 Forest, 1 Winter, 2 Sky) → реплики. Глава 2 — финал игры.
    /// </summary>
    public static readonly Line[][] Outros =
    {
        new[]
        {
            Ela("We did it, Cris! The Forest Guardian is free again!"),
            Ela("The first shard is ours. Look how it glows!"),
            Ela("Next stop: the snowy Peaks. Bundle up!"),
        },
        new[]
        {
            Ela("The Frost Diamond is melting... The second shard!"),
            Ela("Feel that? The wind gets stronger the higher we climb."),
            Ela("Only the Sky is left. Onward and upward, partner!"),
        },
        new[]
        {
            Ela("The last shard! Cris, we gathered them all!"),
            N("The shards rose into the air and found their places."),
            N("The Crystal Heart shone again, brighter than ever."),
            Ela("Thank you, partner. My commands were nothing without your steps."),
            Cris("Beep!"),
        },
    };

    /// <summary>Вступления перед боссами: имя ассета LevelSt → реплики.</summary>
    public static readonly Dictionary<string, Line[]> Intros = new Dictionary<string, Line[]>
    {
        {
            "LevelBossForest", new[]
            {
                Ela("Careful, Cris! A shard turned the Forest Guardian into a root snake!"),
                new Line("Forest Guardian", "SSSS... MY... FOREST..."),
                Ela("Don't touch any part of it. Sneak in right behind its tail!"),
                Ela("When it stops, spikes grow in front of its head..."),
                Ela("...and its tail breaks the ground for good. Watch the red tiles!"),
            }
        },
        {
            "LevelBossSky", new[]
            {
                Ela("Look up, Cris! The Sky Whale is carrying a shard!"),
                new Line("Sky Whale", "WOOOOOM..."),
                Ela("It can't touch us up there... but it drops bombs right where its shadow is."),
                Ela("When the shadow turns red, get out from under it!"),
            }
        },
        {
            "LevelBossWinter", new[]
            {
                Ela("Brrr... That's the Frost Diamond!"),
                new Line("Frost Diamond", "COLD... FOREVER..."),
                Ela("Look! It strikes right where you are looking."),
                Ela("Lightning hits a cross. Super ice hits a 3x3 and freezes the tiles."),
                Ela("A step back dodges both. And frozen tiles make you slide!"),
            }
        },
    };
}
