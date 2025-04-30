using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using DG.Tweening;
using TMPro;

public class InventoryItems : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public Image image;

    [HideInInspector] public Item item;
    [HideInInspector] public int count = 1;
    [HideInInspector] public Transform parentAfterDrag;

    private MovingManager manager;

    private string But_name;

    private int But_number;

    public Data remove;

    public Sprite frontSprite; 
    public Sprite backSprite; 
    public TextMeshProUGUI countIteration;
    private bool isFront = true;

    public void InitialItem(Item newItem)
    {
        item = newItem;
        image.sprite = newItem.image;

        countIteration.text = item.iteration.ToString();
        countIteration.enabled = false;
    }

    public void ModifyItem(int number) => item.iteration = number;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (isFront)
            {
                But_name = new string(this.transform.parent.gameObject.name.Where(x => char.IsDigit(x)).ToArray());
                But_number = int.Parse(But_name);
                MovingManager.Instance.RemoveCommand(But_number);
                Debug.Log(But_number);
                Destroy(gameObject);
            }
            else
            {
                if (item.iteration < 5)
                {
                    item.iteration++;
                }

                else
                {
                    item.iteration = 1;
                }

                countIteration.text = item.iteration.ToString();
            }
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            FlipCard();
        }
    }



    private void FlipCard()
    {
        transform.DOScaleX(0, 0.2f).OnComplete(() =>
        {
            isFront = !isFront;
            image.sprite = isFront ? frontSprite : backSprite;
            countIteration.enabled = !isFront;

            transform.DOScaleX(1, 0.2f);
        });
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
