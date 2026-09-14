using UnityEngine;

/// <summary>
/// Полка. Содержит массив ячеек (ShelfSlot) и категорию предметов, которые на неё можно ставить.
/// </summary>
public class Shelf : MonoBehaviour
{
    [Header("Настройки полки")]
    [Tooltip("Категория предметов, которую принимает эта полка.")]
    public ShelfCategory acceptedCategory;

    [Tooltip("Ячейки полки. Если не заполнено вручную — соберутся автоматически из дочерних объектов.")]
    public ShelfSlot[] slots;

    private void Awake()
    {
        if (slots == null || slots.Length == 0)
            slots = GetComponentsInChildren<ShelfSlot>();

        foreach (var slot in slots)
        {
            if (slot != null)
                slot.parentShelf = this;
        }
    }

    /// <summary>Проверка, подходит ли предмет по категории этой полке.</summary>
    public bool AcceptsItem(ItemData item)
    {
        if (item == null || acceptedCategory == null) return false;
        return item.shelfType == acceptedCategory;
    }
}