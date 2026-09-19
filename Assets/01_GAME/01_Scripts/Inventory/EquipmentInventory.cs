using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Постоянные предметы игрока: оружие и будущие инструменты. Не зависит от tidy-up.</summary>
public class EquipmentInventory : MonoBehaviour
{
    public List<WorldItem> items = new List<WorldItem>();
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
