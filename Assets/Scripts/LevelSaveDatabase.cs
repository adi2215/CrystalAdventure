using System.Collections.Generic;

[System.Serializable]
public class LevelSaveDatabase
{
    /// <summary>Версия формата: 0 — звёзды только по номеру уровня, 2 — у записей есть имя уровня.</summary>
    public int version;
    public List<LevelSaveData> levels = new List<LevelSaveData>();
}
