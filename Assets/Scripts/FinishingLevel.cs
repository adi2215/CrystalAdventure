using UnityEngine;
using UnityEngine.UI;

public class FinishingLevel : MonoBehaviour
{
    [Header("UI Звезды")]
    public Image[] stars; 

    public Data data;

    private int usedCommands = 0;

    private int starCount = 3;

    private void OnEnable() 
    {
        usedCommands = data.commandUsed;
        CalculateStars();
        ShowStars();
    }

    private void CalculateStars()
    {
        if (usedCommands >= 8)
            starCount = 1;
        else if (usedCommands > 5)
            starCount = 2;
        else
            starCount = 3;
    }

    private void ShowStars()
    {
        SaveManager.SaveLevelResult(data.NextLevel, starCount);

        for (int i = 0; i < stars.Length; i++)
        {
            Color color = stars[i].color;
            color.a = (i < starCount) ? 1f : 0.3f; 
            stars[i].color = color;
        }
    }
}
