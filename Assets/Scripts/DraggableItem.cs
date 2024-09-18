using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler //IPointerDownHandler
{
    [SerializeField] private Canvas canvas;
    RectTransform parentAfterDrag;

    public Vector2 currentPosition;

    Vector3 mouseDragPosition;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        parentAfterDrag = GetComponent<RectTransform>();
        //currentPosition = gameObject.transform.position;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        parentAfterDrag.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
    }

    /*public void OnPointerDown(PointerEventData eventData)
    {
        if (Vector3.Distance(transform.position, MagnetPos) < 1)
        {

        }
    }*/

}
