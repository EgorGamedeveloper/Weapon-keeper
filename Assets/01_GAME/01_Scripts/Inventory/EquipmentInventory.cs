using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Постоянные предметы игрока: оружие и будущие инструменты. Не зависит от tidy-up.</summary>
public class EquipmentInventory : MonoBehaviour
{
    [Tooltip("Предметы экипировки (оружие, инструменты) — живые WorldItem, спрятанные в хранилище.")]
    public List<WorldItem> items = new List<WorldItem>();
    [Tooltip("Индекс активного предмета в items.")]
    public int activeIndex;
    public event Action OnChanged;

    public WorldItem ActiveItem => activeIndex >= 0 && activeIndex < items.Count ? items[activeIndex] : null;

    public void Add(WorldItem item)
    {
        if (item == null || items.Contains(item)) return;
        items.Add(item);
        activeIndex = items.Count - 1;
        OnChanged?.Invoke();
    }

    /// <summary>Выставить активный индекс напрямую (например, при восстановлении из сейва).
    /// В отличие от Restore-методов других систем, здесь событие OnChanged поднимается
    /// намеренно — это и есть штатный путь, которым EquipmentWeaponBridge заспавнит оружие.</summary>
    public void SetActiveIndex(int index)
    {
        if (items.Count == 0) return;
        activeIndex = Mathf.Clamp(index, 0, items.Count - 1);
        OnChanged?.Invoke();
    }

    public void Cycle(int direction)
    {
        if (items.Count == 0) return;
        activeIndex = (activeIndex + direction + items.Count) % items.Count;
        OnChanged?.Invoke();
    }

    /// <summary>Убрать конкретный предмет из экипировки (например, при снятии оружия).</summary>
    public void Remove(WorldItem item)
    {
        int idx = items.IndexOf(item);
        if (idx < 0) return;
        items.RemoveAt(idx);
        if (activeIndex >= items.Count) activeIndex = items.Count - 1;
        OnChanged?.Invoke();
    }
}
