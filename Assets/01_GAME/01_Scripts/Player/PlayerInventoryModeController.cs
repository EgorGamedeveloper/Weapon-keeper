using System;
using UnityEngine;

/// <summary>Переключает независимые вкладки: tidy-up (1) и экипировка (2) — общий слот для оружия
/// и инструментов (лом, позже швабра), прокручивается колесом мыши.</summary>
public class PlayerInventoryModeController : MonoBehaviour
{
    public enum InventoryMode { TidyUp, Equipment }

    public InventorySystem tidyUpInventory;
    public EquipmentInventory equipmentInventory;
    public EquippedItemHolder tidyUpHolder;
    public Transform equipmentStorage;
    public KeyCode tidyUpKey = KeyCode.Alpha1;
    public KeyCode equipmentKey = KeyCode.Alpha2;
    public KeyCode equipKey = KeyCode.Q;
    public InventoryMode CurrentMode { get; private set; } = InventoryMode.TidyUp;

    /// <summary>Вызывается при смене режима — например, чтобы спрятать/показать оружие (EquipmentWeaponBridge).</summary>
    public event Action<InventoryMode> OnModeChanged;

    private void Awake()
    {
        if (equipmentStorage == null)
        {
            GameObject storage = new GameObject("EquipmentStorage");
            storage.transform.SetParent(transform, false);
            equipmentStorage = storage.transform;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(tidyUpKey)) SetMode(InventoryMode.TidyUp);
        if (Input.GetKeyDown(equipmentKey)) SetMode(InventoryMode.Equipment);

        if (Input.GetKeyDown(equipKey))
        {
            if (CurrentMode == InventoryMode.TidyUp) MoveActiveItemToEquipment();
            else if (CurrentMode == InventoryMode.Equipment) UnequipActiveWeapon();
        }

        if (CurrentMode == InventoryMode.Equipment)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
                equipmentInventory?.Cycle(scroll > 0 ? 1 : -1);
        }
    }

    public void SetMode(InventoryMode mode)
    {
        CurrentMode = mode;
        if (tidyUpInventory != null) tidyUpInventory.enabled = mode == InventoryMode.TidyUp;
        if (tidyUpHolder != null) tidyUpHolder.SetPresentationEnabled(mode == InventoryMode.TidyUp);
        OnModeChanged?.Invoke(mode);
    }

    /// <summary>Активно ли сейчас оружие в руках (режим экипировки + активный предмет — оружие).</summary>
    public bool IsWeaponEquipped()
    {
        return CurrentMode == InventoryMode.Equipment
            && equipmentInventory != null
            && equipmentInventory.ActiveItem != null
            && equipmentInventory.ActiveItem.itemData != null
            && equipmentInventory.ActiveItem.itemData.IsWeapon;
    }

    /// <summary>Активен ли сейчас инструмент разбора (лом и т.п.) — тот же слот, что и оружие,
    /// просто активная запись сейчас не оружие, а Breakable-инструмент.</summary>
    public bool IsBreakToolEquipped()
    {
        return CurrentMode == InventoryMode.Equipment
            && equipmentInventory != null
            && equipmentInventory.ActiveItem != null
            && equipmentInventory.ActiveItem.itemData != null
            && equipmentInventory.ActiveItem.itemData.canBreakObjects;
    }

    /// <summary>Экипирует активный предмет tidy-up. Оружие может быть экипировано только одно —
    /// если уже есть экипированное оружие, оно возвращается в tidy-up (или роняется, если там нет места).</summary>
    private void MoveActiveItemToEquipment()
    {
        if (tidyUpInventory == null || equipmentInventory == null) return;
        InventoryEntry entry = tidyUpInventory.GetActiveEntry();
        if (entry == null || entry.item == null || !entry.item.canEquip) return;

        bool isWeapon = entry.item.IsWeapon;

        // Важно: сначала забираем именно выбранный игроком предмет, пока activeSlotIndex
        // ещё указывает на него — возврат existingWeapon в tidy-up ниже сдвигает activeSlotIndex
        // на новую запись (см. InventorySystem.AddWorldItem), и если делать это раньше,
        // RemoveActiveWorldItem() заберёт не тот предмет.
        WorldItem item = tidyUpInventory.RemoveActiveWorldItem();
        if (item == null) return;

        if (isWeapon)
        {
            WorldItem existingWeapon = equipmentInventory.items.Find(i => i.itemData != null && i.itemData.IsWeapon);
            if (existingWeapon != null)
            {
                equipmentInventory.Remove(existingWeapon);
                if (tidyUpInventory.CanAddWorldItem(existingWeapon))
                {
                    // Предмет уже в состоянии CarriedHidden (под equipmentStorage) — AddWorldItem
                    // только заносит его в entries, а EquippedItemHolder.RefreshCurrent (подписан на
                    // OnInventoryChanged, который поднимет AddWorldItem) сам перепривяжет и покажет его.
                    tidyUpInventory.AddWorldItem(existingWeapon);
                }
                else
                {
                    existingWeapon.Drop(transform.position + transform.forward, transform.rotation, Vector3.zero);
                }
            }
        }

        item.SetCarriedHidden(equipmentStorage);
        equipmentInventory.Add(item);

        if (isWeapon) SetMode(InventoryMode.Equipment);
    }

    /// <summary>Снимает активное экипированное оружие обратно в tidy-up (если там есть место).</summary>
    private void UnequipActiveWeapon()
    {
        if (tidyUpInventory == null || equipmentInventory == null) return;
        WorldItem active = equipmentInventory.ActiveItem;
        if (active == null || active.itemData == null || !active.itemData.IsWeapon) return;
        if (!tidyUpInventory.CanAddWorldItem(active)) return;

        equipmentInventory.Remove(active);
        // Предмет уже CarriedHidden — RefreshCurrent на OnInventoryChanged перепривяжет и покажет его.
        tidyUpInventory.AddWorldItem(active);
        SetMode(InventoryMode.TidyUp);
    }
}
