using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Кнопки «взять карточку» на панели. Показывает только карточки, разрешённые на уровне.
/// Кнопки сами получают ButtonJuice: покачиваются, пружинят при нажатии,
/// а карточка вылетает из нажатой кнопки в слот.
/// </summary>
public class SpawnScript : MonoBehaviour
{
    public InventoryManager inventoryManager;
    public GameObject[] objMethods;
    public SlotType type;

    void Awake()
    {
        if (objMethods == null) return;
        foreach (var obj in objMethods)
            if (obj != null) ButtonJuice.Ensure(obj, wobble: true);
    }

    public void CheckItems(Item[] methods)
    {
        if (objMethods == null) return;
        var allowed = new HashSet<string>((methods ?? new Item[0]).Where(i => i != null).Select(i => i.type.ToString()));

        foreach (var obj in objMethods)
        {
            if (obj != null) obj.SetActive(allowed.Contains(obj.name));
        }
    }

    /// <summary>Кнопка команды для этого типа карточки (для подсказок).</summary>
    public GameObject FindButton(ItemType itemType)
    {
        if (objMethods == null) return null;
        string name = itemType.ToString();
        return objMethods.FirstOrDefault(o => o != null && o.name == name);
    }

    public void PickUpItem(Item method)
    {
        if (inventoryManager == null)
        {
            Debug.LogWarning($"[SpawnScript] {name}: не назначен inventoryManager", this);
            return;
        }

        var source = ButtonJuice.RecentlyPressed();
        Vector3? from = source != null ? source.transform.position : (Vector3?)null;
        bool added = inventoryManager.TryAddItem(method, type, from);
        if (!added && source != null) source.Deny(); // слоты заполнены или идёт выполнение
    }
}
