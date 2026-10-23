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
        panels ??= new Panels[0];
        numberOfPanels = panels.Length;
        initialPositions = new Vector3[buttonPanels.Length];
        isMoved = new bool[buttonPanels.Length];

        // Раньше при panels.Length > spawnMethods.Length было IndexOutOfRange.
        int n = Mathf.Min(numberOfPanels, spawnMethods.Length, buttonPanels.Length);
        for (int i = 0; i < n; i++)
        {
            if (spawnMethods[i] != null) spawnMethods[i].CheckItems(panels[i]?.commands);
            if (buttonPanels[i] != null) initialPositions[i] = buttonPanels[i].transform.localPosition;
            isMoved[i] = false;
        }

        // Вкладка F2 в сцене выключена (раньше уровней с F2 не было) — включаем её только там, где у уровня
        // есть панель F2 (третья по счёту). Вкладки Main и F1 не трогаем — они как были.
        if (buttonPanels.Length > 2 && buttonPanels[2] != null)
            buttonPanels[2].SetActive(numberOfPanels > 2);

        FunctionPanelStyle.Apply(this); // F1 / F2 — свой цвет, табличка, вкладка
    }

    public void ChoosePanel(GameObject selectedPanel)
    {
        foreach (GameObject panel in panels)
        {
            if (panel != null) panel.SetActive(panel == selectedPanel);
        }
    }

    /*public void AnimateButtons(int selectedButtonIndex)
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
    }*/
}
