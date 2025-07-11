using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class SaveManager
{
    private static string savePath => Application.persistentDataPath + "/levels.json";

    public static LevelSaveDatabase Load()
    {
        if (File.Exists(savePath))
        {
            Debug.Log("Load path: " + Application.persistentDataPath);
            string json = File.ReadAllText(savePath);
            return JsonUtility.FromJson<LevelSaveDatabase>(json);
        }
        else
        {
            Debug.Log("File not found, creating new db at: " + Application.persistentDataPath);
            return new LevelSaveDatabase();
        }
    }

    public static void Save(LevelSaveDatabase db)
    {
        string json = JsonUtility.ToJson(db, true);
        File.WriteAllText(savePath, json);
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
