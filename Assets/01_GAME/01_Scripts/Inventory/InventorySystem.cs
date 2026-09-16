using System;
using UnityEngine;

/// <summary>Данные одного слота инвентаря.</summary>
[System.Serializable]
public class InventorySlotData
{
    public ItemData item;
    public bool IsEmpty => item == null;
}

[System.Serializable]
public class InventoryEntry
{
    public ItemData item;
    public System.Collections.Generic.List<WorldItem> instances = new System.Collections.Generic.List<WorldItem>();
    public int Count => instances.Count;
}

/// <summary>
/// Tidy-up инвентарь: одинаковые типы всегда группируются в одну запись UI,
/// а каждая единица остаётся отдельным физическим WorldItem.
/// </summary>
public class InventorySystem : MonoBehaviour
{
    [Header("Ограничения")]
    [Tooltip("Максимальное общее число физических предметов в tidy-up инвентаре. Увеличивайте при прогрессии игрока.")]
    [Min(1)] public int maxItemCount = 5;

    [Tooltip("Список типов подобранных предметов. Каждый экземпляр остаётся существующим WorldItem.")]
    public System.Collections.Generic.List<InventoryEntry> entries = new System.Collections.Generic.List<InventoryEntry>();

    [Tooltip("Устаревшее поле для старого UI. Новые интерфейсы должны использовать entries.")]
    public InventorySlotData[] slots = System.Array.Empty<InventorySlotData>();
    public int activeSlotIndex = 0;

    [Tooltip("Инвертировать направление прокрутки колеса мыши.")]
    public bool invertScroll = false;

    /// <summary>Вызывается при смене активного слота (индекс нового активного слота).</summary>
    public event Action<int> OnActiveSlotChanged;

    /// <summary>Вызывается при любом изменении содержимого инвентаря.</summary>
    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (entries == null) entries = new System.Collections.Generic.List<InventoryEntry>();
    }

    private void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            int dir = scroll > 0 ? 1 : -1;
            if (invertScroll) dir = -dir;
            CycleActiveEntry(dir);
        }
    }

    public void SetActiveSlot(int index)
    {
        if (index < 0 || index >= entries.Count) return;
        activeSlotIndex = index;
        OnActiveSlotChanged?.Invoke(activeSlotIndex);
    }

    public ItemData GetActiveItem()
    {
        return GetActiveEntry()?.item;
    }

    public bool HasItemInActiveSlot()
    {
        return GetActiveEntry() != null;
    }

    public int TotalItemCount
    {
        get
        {
            int total = 0;
            foreach (var entry in entries)
                total += entry.Count;
            return total;
        }
    }

    public bool CanAddWorldItem(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.itemData == null) return false;
        InventoryEntry existingEntry = entries.Find(candidate => candidate.item == worldItem.itemData);
        return (existingEntry != null && existingEntry.instances.Contains(worldItem)) || TotalItemCount < maxItemCount;
    }

    /// <summary>Добавляет существующий физический экземпляр в запись его типа.</summary>
    public bool AddWorldItem(WorldItem worldItem)
    {
        if (!CanAddWorldItem(worldItem)) return false;
        InventoryEntry entry = entries.Find(candidate => candidate.item == worldItem.itemData);
        if (entry == null)
        {
            entry = new InventoryEntry { item = worldItem.itemData };
            entries.Add(entry);
        }
        if (!entry.instances.Contains(worldItem)) entry.instances.Add(worldItem);
        activeSlotIndex = entries.IndexOf(entry);
        NotifyChanged();
        return true;
    }

    /// <summary>Убрать предмет из активного слота (например, при установке на полку).</summary>
    public WorldItem RemoveActiveWorldItem()
    {
        InventoryEntry entry = GetActiveEntry();
        if (entry == null) return null;
        WorldItem result = entry.instances[entry.instances.Count - 1];
        entry.instances.RemoveAt(entry.instances.Count - 1);
        if (entry.instances.Count == 0) entries.Remove(entry);
        activeSlotIndex = Mathf.Clamp(activeSlotIndex, 0, Mathf.Max(0, entries.Count - 1));
        NotifyChanged();
        return result;
    }

    public InventoryEntry GetActiveEntry() => activeSlotIndex >= 0 && activeSlotIndex < entries.Count ? entries[activeSlotIndex] : null;

    public void CycleActiveEntry(int direction)
    {
        if (entries.Count == 0) return;
        activeSlotIndex = (activeSlotIndex + direction + entries.Count) % entries.Count;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnInventoryChanged?.Invoke();
        OnActiveSlotChanged?.Invoke(activeSlotIndex);
    }
}
