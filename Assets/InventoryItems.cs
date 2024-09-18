using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

public class InventoryItems : MonoBehaviour, IPointerDownHandler//, IBeginDragHandler, IDragHandler,  IEndDragHandler
{
    [Header("UI")]
    public Image image;


    [HideInInspector] public Item item;
    [HideInInspector] public int count = 1;
    [HideInInspector] public Transform parentAfterDrag;

    private string But_name;

    private int But_number;

    public Data remove;

    public void InitialItem(Item newItem)
    {
        item = newItem;
        image.sprite = newItem.image;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        But_name = new string(this.transform.parent.gameObject.name.Where(x => char.IsDigit(x)).ToArray());
        But_number = int.Parse(But_name);
        remove.RemoveBlock = But_number;
        remove.RemoveBool = true;
        Debug.Log(But_number);
        Destroy(gameObject);
    }

    /*public void OnBeginDrag(PointerEventData eventData)
    {
        image.raycastTarget = false;
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        image.raycastTarget = true;
        transform.SetParent(parentAfterDrag);
    }*/
}
