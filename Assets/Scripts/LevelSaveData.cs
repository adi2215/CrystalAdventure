[System.Serializable]
public class LevelSaveData
{
    public int levelNumber;
    public int starsEarned;
    public bool levelCompleted;
    /// <summary>
    /// Имя ассета уровня (LevelSt). По нему звёзды находят свой уровень, даже если уровни
    /// переставили местами (SaveManager при загрузке пересчитывает levelNumber по имени).
    /// </summary>
    public string levelName;
}
