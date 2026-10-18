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

        // Новые карточки (Атака, Щит, Супер-атака…): кнопок для них в сцене нет — делаем клоном соседней.
        foreach (var item in (methods ?? new Item[0]).Where(i => i != null))
        {
            CardIcons.Ensure(item);
            if (objMethods.Any(o => o != null && o.name == item.type.ToString())) continue;
            var button = CreateButton(item);
            if (button != null) objMethods = objMethods.Concat(new[] { button }).ToArray();
        }

        foreach (var obj in objMethods)
        {
            if (obj != null) obj.SetActive(allowed.Contains(obj.name));
        }
    }

    /// <summary>
    /// Кнопка для карточки, которой нет в сцене: копия первой кнопки панели с иконкой карточки.
    /// У копии отключаются вызовы PickUpItem старой карточки (остальное, например звук клика, остаётся)
    /// и добавляется свой. Встаёт в конец ряда (у ряда HorizontalLayoutGroup).
    /// </summary>
    GameObject CreateButton(Item item)
    {
        var template = objMethods.FirstOrDefault(o => o != null);
        if (template == null) return null;
        var go = Instantiate(template, template.transform.parent);
        go.name = item.type.ToString();
        go.transform.SetAsLastSibling();
        var image = go.GetComponent<UnityEngine.UI.Image>();
        if (image != null && item.image != null) image.sprite = item.image;
        var button = go.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == nameof(PickUpItem))
                    button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            button.onClick.AddListener(() => PickUpItem(item));
        }
        ButtonJuice.Ensure(go, wobble: true);
        return go;
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
