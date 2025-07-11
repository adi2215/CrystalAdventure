using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PanelController : MonoBehaviour
{
    public SpawnScript[] spawnMethods;
    public GameObject[] panels;
    public GameObject[] buttonPanels;
    public float animationDuration = 0.2f;
    public float selectedMoveX = 20f;     

    private Vector3[] initialPositions;
    private bool[] isMoved;  
    private int numberOfPanels;        

    public void CheckPanels(Panels[] panels)
    {
        for (int i = buttonPanels.Length - 1; i >= panels.Length; i--)
        {
            buttonPanels[i].SetActive(false);
        }

        numberOfPanels = panels.Length;
        initialPositions = new Vector3[buttonPanels.Length];
        isMoved = new bool[buttonPanels.Length];

        for (int i = 0; i < numberOfPanels; i++)
        {
            Debug.Log("23423efe");
            spawnMethods[i].CheckItems(panels[i].commands);
            initialPositions[i] = buttonPanels[i].transform.localPosition;
            isMoved[i] = false; 
        }

        AnimateButtons(0);
    }

    public void ChoosePanel(GameObject selectedPanel)
    {
        foreach (GameObject panel in panels)
        {
            panel.SetActive(panel == selectedPanel); 
        }
    }

    public void AnimateButtons(int selectedButtonIndex)
    {
        for (int i = 0; i < numberOfPanels; i++)
        {
            Transform rectTransform = buttonPanels[i].GetComponent<Transform>();

            if (i == selectedButtonIndex)
            {
                if (isMoved[i])
                    continue;
                    
                // Сдвигаем выбранную кнопку влево относительно её текущей позиции
                rectTransform.DOLocalMoveX(rectTransform.localPosition.x - selectedMoveX, animationDuration)
                             .SetEase(Ease.OutQuad);

                isMoved[i] = true; 
            }
            
            else
            {
                // Сдвигаем остальные кнопки вправо относительно их текущей позиции
                rectTransform.DOLocalMoveX(initialPositions[i].x, animationDuration)
                             .SetEase(Ease.OutQuad);

                isMoved[i] = false;
            }
        }
    }
}
