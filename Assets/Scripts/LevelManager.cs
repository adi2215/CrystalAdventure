using UnityEngine;
using System.Linq;

public class LevelManager : MonoBehaviour
{
    public LevelState[] levels;

    void Awake()
    {
        SaveManager.Init(totalLevels: 3);
    }

    void Start()
    {
        levels = levels.OrderBy(l => l.levelNumber).ToArray();

        var db = SaveManager.Load();

        for (int i = 0; i < levels.Length; i++)
        {
            int levelNumber = levels[i].levelNumber;
            var levelData = db.levels.Find(l => l.levelNumber == levelNumber);

            bool isUnlocked = levelNumber == 0 ||
                db.levels.Find(l => l.levelNumber == levelNumber - 1)?.levelCompleted == true;

            if (levelData?.levelCompleted == true)
            {
                levels[i].LevelComplete();
            }

            levels[i].SetUnlocked(isUnlocked);
        }
    }


}

