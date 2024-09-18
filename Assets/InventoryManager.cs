using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{

    public InventorySlot[] inventorySlots;

    public GameObject inventoryItemPrefab;

    int selectedSlot = -1;
    int numberSlot = 0;

    int maxNumberSlot = 4;

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

    public void AddItem(Item item)
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            InventorySlot slot = inventorySlots[i];
            InventoryItems itemInSlot = slot.GetComponentInChildren<InventoryItems>();
            if (itemInSlot == null)
            {
                SpawnItem(item, slot);
                return;
            }
        }
    }

    public void SpawnItem(Item item, InventorySlot slot)
    {
        GameObject newItemGo = Instantiate(inventoryItemPrefab, slot.transform);
        InventoryItems inventoryItem = newItemGo.GetComponent<InventoryItems>();
        inventoryItem.InitialItem(item);
    }

    public void GetDestroyItem()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
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
