using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class SaveManager
{
    private static string savePath => Application.persistentDataPath + "/levels.json";

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
        try
        {
            if (File.Exists(savePath))
            {
                var db = JsonUtility.FromJson<LevelSaveDatabase>(File.ReadAllText(savePath));
                if (db != null && db.levels != null) return db;
            }
        }
        catch (System.Exception e)
        {
            // Повреждённый файл сохранения не должен ронять игру.
            Debug.LogWarning("[SaveManager] Не удалось прочитать сохранение: " + e.Message);
        }
        return new LevelSaveDatabase();
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

        LevelSaveData existing = db.levels.Find(l => l.levelNumber == levelNumber);
        if (existing != null)
        {
            if (starsEarned > existing.starsEarned)
                existing.starsEarned = starsEarned;
            existing.levelCompleted = true;
        }
        else
        {
            db.levels.Add(new LevelSaveData
            {
                levelNumber = levelNumber,
                starsEarned = starsEarned,
                levelCompleted = true
            });
        }

        Save(db);
    }

    public static void Init(int totalLevels)
    {
        MigrateLegacySave();
        if (File.Exists(savePath))
            return; 

        var db = new LevelSaveDatabase();
        for (int i = 0; i < totalLevels; i++)
        {
            db.levels.Add(new LevelSaveData
            {
                levelNumber = i,
                starsEarned = 0,
                levelCompleted = (i == 0)
            });
        }

        Save(db);
    }
}
