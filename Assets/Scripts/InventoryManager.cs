using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{

    [SerializeField] private List<InventorySlot> inventorySlots = new ();

    [SerializeField] private GameObject inventoryItemPrefab;

    [SerializeField] private MovingManager addElement;

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
