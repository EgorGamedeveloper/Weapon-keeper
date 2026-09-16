using UnityEngine;

/// <summary>Переключает независимые вкладки tidy-up (1) и экипировки/инструментов (2).</summary>
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
        if (CurrentMode == InventoryMode.TidyUp && Input.GetKeyDown(equipKey)) MoveActiveItemToEquipment();

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
    }

    private void MoveActiveItemToEquipment()
    {
        if (tidyUpInventory == null || equipmentInventory == null) return;
        InventoryEntry entry = tidyUpInventory.GetActiveEntry();
        if (entry == null || entry.item == null || !entry.item.canEquip) return;

        WorldItem item = tidyUpInventory.RemoveActiveWorldItem();
        if (item == null) return;
        item.SetCarriedHidden(equipmentStorage);
        equipmentInventory.Add(item);
    }
}
