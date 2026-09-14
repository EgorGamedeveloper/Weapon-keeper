using System;
using UnityEngine;

/// <summary>Данные одного слота инвентаря.</summary>
[System.Serializable]
public class InventorySlotData
{
    public ItemData item;
    public bool IsEmpty => item == null;
}

/// <summary>
/// Инвентарь игрока фиксированного размера (по умолчанию 5).
/// Колесо мыши переключает активный слот (тот предмет, что игрок держит в руке).
/// </summary>
public class InventorySystem : MonoBehaviour
{
    [Tooltip("Количество слотов инвентаря. Можно расширить в будущем.")]
    public int slotCount = 5;

    public InventorySlotData[] slots;
    public int activeSlotIndex = 0;

    [Tooltip("Инвертировать направление прокрутки колеса мыши.")]
    public bool invertScroll = false;

    /// <summary>Вызывается при смене активного слота (индекс нового активного слота).</summary>
    public event Action<int> OnActiveSlotChanged;

    /// <summary>Вызывается при любом изменении содержимого инвентаря.</summary>
    public event Action OnInventoryChanged;

    private void Awake()
    {
        slots = new InventorySlotData[slotCount];
        for (int i = 0; i < slotCount; i++)
            slots[i] = new InventorySlotData();
    }

    private void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            int dir = scroll > 0 ? 1 : -1;
            if (invertScroll) dir = -dir;
            SetActiveSlot((activeSlotIndex + dir + slotCount) % slotCount);
        }
    }

    public void SetActiveSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return;
        activeSlotIndex = index;
        OnActiveSlotChanged?.Invoke(activeSlotIndex);
    }

    public ItemData GetActiveItem()
    {
        return slots[activeSlotIndex].item;
    }

    public bool HasItemInActiveSlot()
    {
        return !slots[activeSlotIndex].IsEmpty;
    }

    /// <summary>Добавить предмет в первый свободный слот. Возвращает false, если инвентарь полон.</summary>
    public bool AddItem(ItemData item)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].item = item;
                OnInventoryChanged?.Invoke();
                OnActiveSlotChanged?.Invoke(activeSlotIndex);
                return true;
            }
        }
        Debug.Log("Инвентарь полон!");
        return false;
    }

    /// <summary>Убрать предмет из активного слота (например, при установке на полку).</summary>
    public void RemoveActiveItem()
    {
        slots[activeSlotIndex].item = null;
        OnInventoryChanged?.Invoke();
        OnActiveSlotChanged?.Invoke(activeSlotIndex);
    }
}
