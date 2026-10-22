using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Главы игры: Forest → Winter → Sky (снизу вверх по горе).
///
/// Раньше глава считалась как «номер уровня / 15», поэтому уровни, дописанные в конец списка
/// (лесной босс, шипы, враги, Winter, Sky), попадали не в свою главу: лесной босс оказывался в Winter,
/// а лёгкие черновики 16–18 — со снегом на блоках.
/// Теперь главу закрывает уровень с боссом: всё до LevelBossForest включительно — Forest, дальше до
/// LevelBossWinter — Winter, остальное — Sky. Длина главы любая, порядок задаёт список уровней
/// (кнопка Crystal Adventure → Уровни → Расставить уровни по главам).
/// Если боссов в списке нет — по-старому, по 15 уровней.
/// </summary>
public static class LevelChapters
{
    public const int Forest = 0, Winter = 1, Sky = 2;

    /// <summary>Названия глав по-английски (перевод — Loc.T).</summary>
    public static readonly string[] Names = { "Forest", "Winter", "Sky" };

    const int LegacyLevelsPerChapter = 15;
    const string BossPrefix = "LevelBoss";

    public static bool IsBoss(LevelSt level) => level != null && level.name.StartsWith(BossPrefix);

    /// <summary>Список уровней игры из сцены (null — сцены с уровнями нет).</summary>
    public static IList<LevelSt> CurrentList
    {
        get
        {
            var data = Object.FindObjectOfType<TilemapData>();
            return data != null ? data.tilemapData : null;
        }
    }

    /// <summary>Глава уровня с номером index (с нуля) в списке levels.</summary>
    public static int ChapterOf(IList<LevelSt> levels, int index)
    {
        if (index < 0) return Forest;
        bool anyBoss = false;
        if (levels != null)
            foreach (var l in levels)
                if (IsBoss(l)) { anyBoss = true; break; }
        if (!anyBoss) return Mathf.Clamp(index / LegacyLevelsPerChapter, 0, Names.Length - 1);

        int chapter = 0;
        int last = Mathf.Min(index, levels.Count);
        for (int i = 0; i < last; i++)
            if (IsBoss(levels[i])) chapter++;           // босс закрывает главу — следующие уровни уже в новой
        return Mathf.Clamp(chapter, 0, Names.Length - 1);
    }

    public static int ChapterOf(int index) => ChapterOf(CurrentList, index);

    /// <summary>Название главы на текущем языке.</summary>
    public static string Name(int chapter) => Loc.T(Names[Mathf.Clamp(chapter, 0, Names.Length - 1)]);
}
