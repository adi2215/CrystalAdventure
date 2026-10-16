using UnityEngine;

/// <summary>Стартовое меню уровня. Исправлено наслаивание анимаций при быстрых кликах.</summary>
public class MenuBox : MonoBehaviour
{
    public Data save;
    public Transform box;
    public Levelloader level;

    public void CloseBox()
    {
        save.menuclose = true;
        Move(-Screen.height);
    }

    public void OpenBox()
    {
        save.menuclose = false;
        Move(-180f);
    }

    void Move(float y)
    {
        if (box == null) return;
        LeanTween.cancel(box.gameObject);
        box.LeanMoveLocalY(y, 1f).setEaseOutQuad();
    }

    private void OnEnable()
    {
        if (save != null && save.menuclose)
            CloseBox();
    }

    public void ButtonClose()
    {
        if (level != null) level.LoadNextLeve(0);
    }
}
