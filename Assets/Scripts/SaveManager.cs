using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Сохранение звёзд по уровням (levels.json в persistentDataPath).
///
/// Исправлено: звёзды хранились только по номеру уровня в списке, поэтому любая перестановка
/// уровней (например, расстановка по главам) «переносила» звёзды на чужие уровни. Теперь у записи
/// есть имя ассета уровня: при загрузке номер пересчитывается по имени под текущий порядок.
/// Старые сохранения (version 0) получают имена по прежнему порядку уровней — LegacyOrder.
/// </summary>
public static class SaveManager
{
    private static string savePath => Application.persistentDataPath + "/levels.json";

    const int CurrentVersion = 2;

    /// <summary>
    /// Порядок уровней до расстановки по главам (октябрь 2026). Номер в старом сохранении → имя уровня.
    /// Список только дописывался в конец, поэтому подходит и для сохранений, где уровней было меньше.
    /// </summary>
    static readonly string[] LegacyOrder =
    {
        "LevelOne", "LevelTwo", "LevelThree", "LevelFourth", "LevelSix", "LevelEight", "LevelNine",
        "LevelLoop", "LevelIf", "LevelTen", "LevelTwelve", "LevelThirteen", "LevelFourteen", "LevelFifteen",
        "LevelSixteen", "LevelFive", "LevelSeven", "LevelEleven",
        "LevelWinter1", "LevelWinter2", "LevelWinter3", "LevelWinter4",
        "LevelEnemy1", "LevelEnemy2", "LevelEnemy3", "LevelBossForest", "LevelBossWinter",
        "LevelSpikes1", "LevelSpikes2",
        "LevelSky1", "LevelSky2", "LevelSky3", "LevelBossSky",
    };

    /// <summary>Под этим Product Name игра выходила раньше — оттуда переносим старое сохранение.</summary>
    private const string LegacyProductName = "BetaCube";
    private static bool _migrationChecked;

    /// <summary>
    /// Перенос сохранения после переименования игры (BetaCube → Crystal Adventure - The Ascent).
    ///
    /// На Windows/Mac/Linux Application.persistentDataPath = .../{Company Name}/{Product Name},
    /// поэтому смена Product Name в Player Settings «теряла» прогресс уже вышедших игроков:
    /// игра начинала смотреть в новую пустую папку. Если в новой папке сохранения нет,
    /// а в соседней папке BetaCube оно есть — копируем (старый файл не трогаем, на всякий случай).
    /// На Android/iOS путь зависит от bundle id, а не от Product Name, — там просто ничего не найдётся.
    /// </summary>
    private static void MigrateLegacySave()
    {
        if (_migrationChecked) return;
        _migrationChecked = true;
        try
        {
            if (File.Exists(savePath)) return;
            if (Application.productName == LegacyProductName) return;

            var parent = Directory.GetParent(Application.persistentDataPath);
            if (parent == null) return;

            string legacy = Path.Combine(parent.FullName, LegacyProductName, "levels.json");
            if (!File.Exists(legacy)) return;

            Directory.CreateDirectory(Application.persistentDataPath);
            File.Copy(legacy, savePath);
            Debug.Log("[SaveManager] Сохранение перенесено из " + legacy);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveManager] Не удалось перенести старое сохранение: " + e.Message);
        }
    }

    public static LevelSaveDatabase Load()
    {
        MigrateLegacySave();
        LevelSaveDatabase db = null;
        try
        {
            if (File.Exists(savePath))
                db = JsonUtility.FromJson<LevelSaveDatabase>(File.ReadAllText(savePath));
        }
        catch (System.Exception e)
        {
            // Повреждённый файл сохранения не должен ронять игру.
            Debug.LogWarning("[SaveManager] Не удалось прочитать сохранение: " + e.Message);
        }
        if (db == null || db.levels == null) return new LevelSaveDatabase { version = CurrentVersion };

        if (db.version < CurrentVersion)
        {
            AddLegacyNames(db);
            Save(db);
        }
        ResolveNumbers(db);
        return db;
    }

    /// <summary>Старое сохранение: имя уровня — по номеру в прежнем порядке.</summary>
    static void AddLegacyNames(LevelSaveDatabase db)
    {
        foreach (var l in db.levels)
            if (l != null && string.IsNullOrEmpty(l.levelName) && l.levelNumber >= 0 && l.levelNumber < LegacyOrder.Length)
                l.levelName = LegacyOrder[l.levelNumber];
        db.version = CurrentVersion;
    }

    /// <summary>Номер уровня — по имени, под текущий порядок списка (-1 — уровня больше нет в игре).</summary>
    static void ResolveNumbers(LevelSaveDatabase db)
    {
        var list = LevelChapters.CurrentList;
        if (list == null) return; // сцена без уровней — оставляем как записано
        foreach (var l in db.levels)
        {
            if (l == null || string.IsNullOrEmpty(l.levelName)) continue;
            l.levelNumber = IndexOf(list, l.levelName);
        }
    }

    static int IndexOf(System.Collections.Generic.IList<LevelSt> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].name == name) return i;
        return -1;
    }

    /// <summary>Имя уровня с этим номером в текущем списке (null — неизвестно).</summary>
    static string NameAt(int levelNumber)
    {
        var list = LevelChapters.CurrentList;
        return list != null && levelNumber >= 0 && levelNumber < list.Count && list[levelNumber] != null ? list[levelNumber].name : null;
    }

    public static void Save(LevelSaveDatabase db)
    {
        try
        {
            // Пишем во временный файл и подменяем — при вылете посреди записи сохранение не обнулится.
            string tmp = savePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(db, true));
            if (File.Exists(savePath)) File.Delete(savePath);
            File.Move(tmp, savePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveManager] Не удалось сохранить: " + e.Message);
        }
    }

    public static void SaveLevelResult(int levelNumber, int starsEarned)
    {
        LevelSaveDatabase db = Load();
        string name = NameAt(levelNumber);

        LevelSaveData existing = name != null ? db.levels.Find(l => l != null && l.levelName == name) : null;
        if (existing == null)
            existing = db.levels.Find(l => l != null && l.levelNumber == levelNumber && string.IsNullOrEmpty(l.levelName));
        if (existing != null)
        {
            if (starsEarned > existing.starsEarned)
                existing.starsEarned = starsEarned;
            existing.levelCompleted = true;
            existing.levelNumber = levelNumber;
            if (name != null) existing.levelName = name;
        }
        else
        {
            db.levels.Add(new LevelSaveData
            {
                levelNumber = levelNumber,
                starsEarned = starsEarned,
                levelCompleted = true,
                levelName = name,
            });
        }

        Save(db);
    }

    public static void Init(int totalLevels)
    {
        MigrateLegacySave();
        if (File.Exists(savePath))
            return; 

        var db = new LevelSaveDatabase { version = CurrentVersion };
        for (int i = 0; i < totalLevels; i++)
        {
            db.levels.Add(new LevelSaveData
            {
                levelNumber = i,
                starsEarned = 0,
                levelCompleted = (i == 0),
                levelName = NameAt(i),
            });
        }

        Save(db);
    }
}
