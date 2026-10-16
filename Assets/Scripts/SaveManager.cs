using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class SaveManager
{
    private static string savePath => Application.persistentDataPath + "/levels.json";

    public static LevelSaveDatabase Load()
    {
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
