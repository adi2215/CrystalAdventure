using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    public Image image;
    
    public void OnDrop(PointerEventData eventData)
    {
        if (transform.childCount == 0)
        {
            InventoryItems inventoryItem = eventData.pointerDrag.GetComponent<InventoryItems>();
            inventoryItem.parentAfterDrag = transform;
        }
    }
}
