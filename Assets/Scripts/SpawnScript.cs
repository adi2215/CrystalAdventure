using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpawnScript : MonoBehaviour
{
    public InventoryManager inventoryManager;
    public GameObject[] objMethods;
    public SlotType type;

    public void CheckItems(Item[] methods)
    {
        var namesSet = new HashSet<string>(methods.Select(item => item.type.ToString()));

        foreach (var obj in objMethods)
        {
            if (!namesSet.Contains(obj.name))  
            {
                obj.SetActive(false);  
            }
        }
    }

    public void PickUpItem(Item method)
    {
        inventoryManager.AddItem(method, type);
    }
}
