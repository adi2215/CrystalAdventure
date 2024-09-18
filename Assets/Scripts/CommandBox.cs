using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandBox : MonoBehaviour
{
    public Transform box;

    public GameObject panel;

    public GameObject Buttons;

    private void OnEnable()
    {
        box.localPosition = new Vector2(1280, 0);
        box.LeanMoveLocalX(620, 1f).setEaseOutExpo().delay = 0.1f;
    }

    public void CloseBox()
    {
        box.LeanMoveLocalX(Screen.width, 1f).setEaseOutQuad().setOnComplete(OnComplete);
        Buttons.SetActive(true);
    }

    void OnComplete()
    {
        gameObject.SetActive(false);
    }

    public void OnActivate()
    {
        panel.SetActive(true);
    }

}
