using UnityEngine;

/// <summary>
/// Выезжающая панель с правилами. Исправлено: при быстрых открытиях/закрытиях
/// анимации наслаивались, и OnComplete от старого закрытия прятал уже открытую панель.
/// </summary>
public class CommandBox : MonoBehaviour
{
    public Transform box;
    public GameObject panel;
    public GameObject Buttons;

    private void OnEnable()
    {
        if (box == null) return;
        LeanTween.cancel(box.gameObject);
        box.localPosition = new Vector2(1280, 0);
        box.LeanMoveLocalX(620, 1f).setEaseOutExpo().setDelay(0.1f);
    }

    public void CloseBox()
    {
        if (Buttons != null) Buttons.SetActive(true);
        if (box == null) { gameObject.SetActive(false); return; }
        LeanTween.cancel(box.gameObject);
        box.LeanMoveLocalX(Screen.width, 1f).setEaseOutQuad().setOnComplete(OnComplete);
    }

    void OnComplete()
    {
        gameObject.SetActive(false);
    }

    public void OnActivate()
    {
        if (panel != null) panel.SetActive(true);
    }
}
