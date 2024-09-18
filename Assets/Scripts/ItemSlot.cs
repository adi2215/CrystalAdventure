using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemSlot : MonoBehaviour, IDropHandler
{
    public ButtonsManager chosen;

    GameObject obj;

    public int id;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            eventData.pointerDrag.GetComponent<RectTransform>().anchoredPosition = GetComponent<RectTransform>().anchoredPosition;
            GetComponent<RectTransform>().sizeDelta = eventData.pointerDrag.GetComponent<RectTransform>().sizeDelta + new Vector2(-30, -10);
            chosen.Chosen[id] = eventData.pointerDrag.gameObject;
        }
    }


}
