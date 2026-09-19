using System;
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Одна ячейка на полке — точка (Transform), в которую можно поставить предмет.
/// Требует Collider (isTrigger = true) для того, чтобы луч игрока мог её обнаружить.
/// При наведении лучом с предметом в руках показывает полупрозрачный "призрак" предмета.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShelfSlot : MonoBehaviour, IPlaceableSlot
{

    [Header("Режим стопки (колонна)")]
    public bool isStackSlot = false;
    [Min(1)] public int maxStack = 4;        // максимум пачек в колонне
    public float stackSpacing = 0.08f;       // высота одной пачки

    public readonly List<WorldItem> placedItems = new(); // вместо одиночного placedWorldItem
    public int StackCount => placedItems.Count;

    [HideInInspector] public Shelf parentShelf;
    [HideInInspector] public ItemData currentItem;

    /// <summary>Предмет поставлен в эту ячейку (слушает ShelvingProgressTracker).</summary>
    public event Action<ShelfSlot> OnItemPlaced;

    /// <summary>Предмет забран из этой ячейки (слушает ShelvingProgressTracker).</summary>
    public event Action<ShelfSlot> OnItemRemoved;

    private GameObject ghostInstance;
    private ItemData ghostItem;
    private GameObject placedInstance;
    private WorldItem placedWorldItem;

    public bool IsEmpty => currentItem == null;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// Позиция, куда встанет СЛЕДУЮЩИЙ предмет = верх стопки. Пустая колонна → база (0).
    public Vector3 GetNextPlacementLocalPosition()
        => isStackSlot ? Vector3.up * (stackSpacing * StackCount) : Vector3.zero;

    public bool CanAccept(ItemData item)
    {
        if (item == null || parentShelf == null || !parentShelf.AcceptsItem(item)) return false;
        if (!isStackSlot) return IsEmpty;                    // старое поведение — одиночный слот
        if (StackCount >= maxStack) return false;
        return StackCount == 0 || placedItems[StackCount - 1].itemData == item; // стопка одного типа
    }

    /// <summary>Показать полупрозрачный "призрак" предмета в ячейке (подсказка игроку).</summary>
    public void ShowGhost(ItemData item)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;

        if (ghostInstance != null && ghostItem == item) return; // уже показан этот же предмет
        HideGhost();

        ghostInstance = GhostPreviewUtility.Create(item, transform, GetNextPlacementLocalPosition(), Quaternion.identity);
        ghostItem = item;
    }

    /// <summary>Скрыть призрак предмета.</summary>
    public void HideGhost()
    {
        if (ghostInstance != null)
        {
            Destroy(ghostInstance);
            ghostInstance = null;
            ghostItem = null;
        }
    }

     /// <summary>Поставить существующий физический предмет в ячейку (наверх стопки для isStackSlot).</summary>
    public void PlaceItem(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.itemData == null || !CanAccept(worldItem.itemData)) return;
        HideGhost();

        // Позу вычисляем ДО добавления: это верх текущей стопки (для пустой колонны — база)
        Vector3 localPos = GetNextPlacementLocalPosition();

        placedItems.Add(worldItem);              // ← стопка теперь реально растёт
        currentItem = worldItem.itemData;
        placedWorldItem = worldItem;
        placedInstance = worldItem.gameObject;

        worldItem.PlaceOnShelf(transform, this, localPos, Quaternion.identity);

        OnItemPlaced?.Invoke(this);
    }

    /// <summary>Освободить ячейку и вернуть существующий предмет для подбора.</summary>
    public WorldItem RemoveItem(WorldItem item)
    {
        int idx = placedItems.IndexOf(item);
        if (idx < 0) return null;

        placedItems.RemoveAt(idx);
        currentItem = StackCount > 0 ? placedItems[StackCount - 1].itemData : null;

        // Оседание: все, кто стоял выше вынятого, опускаем на «этаж» вниз
        for (int i = idx; i < StackCount; i++)
            placedItems[i].transform.DOLocalMove(Vector3.up * (stackSpacing * i), 0.25f); // DOTween уже есть

        OnItemRemoved?.Invoke(this);

        return item;
    }
}
