using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{

    [SerializeField] private List<InventorySlot> inventorySlots = new ();

    [SerializeField] private GameObject inventoryItemPrefab;

    [SerializeField] private MovingManager addElement;

   /* private void Start()
    {
        ChangeSelectedSlot(0);
    }*/

    /*private void Update()
    {
        if (Input.inputString != null)
        {
            bool isNumber = int.TryParse(Input.inputString, out int number);
            if (isNumber && number > 0 && number < 5)
            {
                ChangeSelectedSlot(number - 1);
            }
            if (Input.mouseScrollDelta.y != 0)
            {
                numberSlot -= (int)Input.mouseScrollDelta.y;
                if (numberSlot > 0 && numberSlot < 5)
                    ChangeSelectedSlot(numberSlot - 1);
                else if (numberSlot < 0)
                {
                    numberSlot = maxNumberSlot;
                    ChangeSelectedSlot(numberSlot - 1);
                }
                else if (numberSlot > maxNumberSlot - 1)
                {
                    numberSlot = 1;
                    ChangeSelectedSlot(numberSlot - 1);
                }
            }



            if (Page < 0)
                 Page = Pages.Length - 1;
             else if (Page > Pages.Length - 1)
                 Page = 0;
        }
    }*/

    /*void ChangeSelectedSlot(int newValue)
    {
        if (selectedSlot >= 0)
        {
            inventorySlots[selectedSlot].DeSelected();
        }

        inventorySlots[newValue].Selected();
        selectedSlot = newValue;
    }*/

    void Start() 
    { 
        foreach (var slot in GetComponentsInChildren<InventorySlot>())
        {
            inventorySlots.Add(slot);
        }
    }

    public void AddItem(Item item, SlotType type)
    {
        Debug.Log(inventorySlots.Count);
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            InventorySlot slot = inventorySlots[i];
            InventoryItems itemInSlot = slot.GetComponentInChildren<InventoryItems>();
            if (itemInSlot == null)
            {
                SpawnItem(item, slot, type);
                return;
            }
        }
    }

    public void SpawnItem(Item item, InventorySlot slot, SlotType type)
    {
        GameObject newItemGo = Instantiate(inventoryItemPrefab, slot.transform);
        InventoryItems inventoryItem = newItemGo.GetComponent<InventoryItems>();
        inventoryItem.InitialItem(item);
        addElement.MoveCharacter(item, type);
        Debug.Log(item.type.ToString());
    }

    public void GetDestroyItem()
    {
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            InventorySlot slot = inventorySlots[i];
            InventoryItems itemInSlot = slot.GetComponentInChildren<InventoryItems>();
            if (itemInSlot != null)
            {
                Item item = itemInSlot.item;
                Destroy(itemInSlot.gameObject);
            }
        }
    }


}
