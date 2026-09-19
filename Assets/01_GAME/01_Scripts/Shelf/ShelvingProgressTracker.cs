using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Считает прогресс расстановки товара: сколько единиц уже стоит на полках из общего числа
/// на уровне (и разбросанных, и уже расставленных). Мусорные категории сюда не входят —
/// отслеживаются только категории из trackedCategories.
/// UI здесь не строится: прогресс-бар позже подпишется на OnShelvingProgressChanged.
/// </summary>
public class ShelvingProgressTracker : MonoBehaviour
{
    [Tooltip("Категории полок, которые считаются товаром (оружие, патроны). Мусор сюда НЕ добавлять.")]
    public List<ShelfCategory> trackedCategories = new List<ShelfCategory>();

    /// <summary>Всего единиц товара на уровне.</summary>
    public int TotalUnits { get; private set; }

    /// <summary>Сколько из них стоит на полках.</summary>
    public int PlacedUnits { get; private set; }

    public float ProgressPercent => TotalUnits == 0 ? 0f : (float)PlacedUnits / TotalUnits * 100f;

    /// <summary>Прогресс изменился: (расставлено, всего, процент).</summary>
    public event Action<int, int, float> OnShelvingProgressChanged;

    private readonly List<Shelf> trackedShelves = new List<Shelf>();
    private readonly List<ShelfSlot> subscribedSlots = new List<ShelfSlot>();

    // Start, а не Awake: Shelf.Awake() наполняет slots и проставляет parentShelf,
    // а порядок Awake между компонентами Unity не гарантирует.
    private void Start()
    {
        foreach (var shelf in FindObjectsByType<Shelf>(FindObjectsSortMode.None))
        {
            if (shelf.acceptedCategory == null || !trackedCategories.Contains(shelf.acceptedCategory)) continue;
            trackedShelves.Add(shelf);

            foreach (var slot in shelf.slots)
            {
                if (slot == null) continue;
                slot.OnItemPlaced += HandleSlotChanged;
                slot.OnItemRemoved += HandleSlotChanged;
                subscribedSlots.Add(slot);
            }
        }

        TotalUnits = CountTrackedWorldItems();
        Recompute();
    }

    private void OnDestroy()
    {
        foreach (var slot in subscribedSlots)
        {
            if (slot == null) continue;
            slot.OnItemPlaced -= HandleSlotChanged;
            slot.OnItemRemoved -= HandleSlotChanged;
        }
    }

    /// <summary>Увеличить общее число единиц — для будущей доставки новой партии товара.</summary>
    public void RegisterAdditionalUnits(int delta)
    {
        TotalUnits += delta;
        Recompute();
    }

    private void HandleSlotChanged(ShelfSlot slot) => Recompute();

    private int CountTrackedWorldItems()
    {
        int total = 0;
        foreach (var item in FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
        {
            if (item.itemData != null && item.itemData.shelfType != null
                && trackedCategories.Contains(item.itemData.shelfType))
                total++;
        }
        return total;
    }

    /// <summary>
    /// Пересчёт по содержимому ячеек, а не по WorldItem.State: ShelfSlot.RemoveItem поднимает
    /// событие ДО того, как вызывающий код успевает сменить состояние предмета (это делает
    /// PlayerItemInteraction через BeginPickup уже после возврата), а placedItems обновляется
    /// синхронно и всегда актуален.
    /// </summary>
    private void Recompute()
    {
        int placed = 0;
        foreach (var shelf in trackedShelves)
        {
            if (shelf == null) continue;
            foreach (var slot in shelf.slots)
                if (slot != null) placed += slot.StackCount;
        }

        PlacedUnits = placed;
        OnShelvingProgressChanged?.Invoke(PlacedUnits, TotalUnits, ProgressPercent);
    }
}
