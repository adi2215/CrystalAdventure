using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ряд слотов под карточки (Main / Function 1 / Function 2).
/// Карточка появляется в UI ТОЛЬКО если MovingManager её принял —
/// раньше при полном ряду или во время игры UI и программа расходились.
/// Появление и удаление карточек анимированы (InventoryItems.PlayAppear / PlayRemove).
/// </summary>
public class InventoryManager : MonoBehaviour
{
    [SerializeField] private List<InventorySlot> inventorySlots = new();

    [SerializeField] private GameObject inventoryItemPrefab;

    [SerializeField] private MovingManager addElement;

    void Awake()
    {
        inventorySlots.Clear();
        inventorySlots.AddRange(GetComponentsInChildren<InventorySlot>(true));
        if (addElement == null) addElement = FindObjectOfType<MovingManager>();
    }

    public void AddItem(Item item, SlotType type) => TryAddItem(item, type, null);

    /// <summary>Положить карточку в первый свободный слот. fromWorld — откуда она вылетает (кнопка).</summary>
    public bool TryAddItem(Item item, SlotType type, Vector3? fromWorld)
    {
        if (item == null || addElement == null || !addElement.CanEdit) return false;

        for (int i = 0; i < inventorySlots.Count; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot == null || CardIn(slot) != null) continue;

            if (!addElement.SetCommand(type, i, item, item.iteration)) return false;
            var card = SpawnItem(item, slot, type, i);
            if (card != null) card.PlayAppear(fromWorld);
            return true;
        }
        return false; // все слоты заняты
    }

    public void SpawnItem(Item item, InventorySlot slot, SlotType type) =>
        SpawnItem(item, slot, type, inventorySlots.IndexOf(slot));

    InventoryItems SpawnItem(Item item, InventorySlot slot, SlotType type, int index)
    {
        if (inventoryItemPrefab == null) return null;
        GameObject newItemGo = Instantiate(inventoryItemPrefab, slot.transform);
        var inventoryItem = newItemGo.GetComponent<InventoryItems>();
        if (inventoryItem != null) inventoryItem.InitialItem(item, addElement, type, index);
        return inventoryItem;
    }

    /// <summary>Живая (не улетающая) карточка в слоте.</summary>
    static InventoryItems CardIn(InventorySlot slot)
    {
        foreach (var card in slot.GetComponentsInChildren<InventoryItems>())
            if (!card.IsRemoving) return card;
        return null;
    }

    /// <summary>Сколько слотов в ряду программы.</summary>
    public int SlotCount => inventorySlots.Count;

    /// <summary>Прямоугольник слота (для скобок циклов).</summary>
    public RectTransform SlotRect(int index) =>
        index >= 0 && index < inventorySlots.Count && inventorySlots[index] != null ? inventorySlots[index].transform as RectTransform : null;

    /// <summary>Карточка в слоте с номером index (для подсказок).</summary>
    public InventoryItems FindCard(int index)
    {
        if (index < 0 || index >= inventorySlots.Count || inventorySlots[index] == null) return null;
        return CardIn(inventorySlots[index]);
    }

    /// <summary>Убрать все карточки (кнопка очистки) — «волной» по очереди.</summary>
    public void GetDestroyItem()
    {
        int n = 0;
        foreach (var slot in inventorySlots)
        {
            if (slot == null) continue;
            var card = CardIn(slot);
            if (card != null) card.PlayRemove(0.06f * n++);
        }
    }
}
