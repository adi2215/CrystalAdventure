using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuBox : MonoBehaviour
{
    public Data save;
    public Transform box;
    public Levelloader level;

    void OnGUI()
    {
        GUI.enabled = false;
    }

    public void CloseBox()
    {
        save.menuclose = true;
        box.LeanMoveLocalY(-Screen.height, 1f).setEaseOutQuad();
    }

    public void OpenBox()
    {
        save.menuclose = false;
        box.LeanMoveLocalY(-180f, 1f).setEaseOutQuad();
    }

    private void OnEnable()
    {
        if (save.menuclose)
            CloseBox();
    }

    public void ButtonClose()
    {
        level.LoadNextLeve();
    }
}
