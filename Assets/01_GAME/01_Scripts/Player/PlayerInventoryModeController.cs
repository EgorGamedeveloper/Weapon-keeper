using System;
using UnityEngine;

/// <summary>Переключает независимые вкладки: tidy-up (1) и экипировка (2) — общий слот для оружия
/// и инструментов (лом, позже швабра), прокручивается колесом мыши.</summary>
public class PlayerInventoryModeController : MonoBehaviour
{
    public enum InventoryMode { TidyUp, Equipment }

    [Header("Конфиг")]
    [Tooltip("Если задан — клавиши ниже перекрываются из GameConfig при старте.")]
    public GameConfig config;

    [Header("Ссылки")]
    [Tooltip("Tidy-up инвентарь (режим 1).")]
    public InventorySystem tidyUpInventory;
    [Tooltip("Инвентарь экипировки: оружие и инструменты (режим 2).")]
    public EquipmentInventory equipmentInventory;
    [Tooltip("Держатель предмета tidy-up в руке — прячется вне режима TidyUp.")]
    public EquippedItemHolder tidyUpHolder;
    [Tooltip("Контейнер для спрятанных предметов экипировки. Пусто — создаётся в Awake.")]
    public Transform equipmentStorage;
    [Header("Клавиши")]
    [Tooltip("Переключиться на tidy-up инвентарь.")]
    public KeyCode tidyUpKey = KeyCode.Alpha1;
    [Tooltip("Переключиться на экипировку.")]
    public KeyCode equipmentKey = KeyCode.Alpha2;
    [Tooltip("Экипировать активный предмет tidy-up / снять экипированное оружие.")]
    public KeyCode equipKey = KeyCode.Q;

    [Header("Звуки")]
    [Tooltip("Переключение вкладки (1/2) и прокрутка экипировки колесом.")]
    public SoundCue switchSound;

    [Tooltip("Предмет экипирован или снят (Q).")]
    public SoundCue equipSound;
    public InventoryMode CurrentMode { get; private set; } = InventoryMode.TidyUp;

    /// <summary>Вызывается при смене режима — например, чтобы спрятать/показать оружие (EquipmentWeaponBridge).</summary>
    public event Action<InventoryMode> OnModeChanged;

    private void Awake()
    {
        if (config != null)
        {
            tidyUpKey = config.input.tidyUpKey;
            equipmentKey = config.input.equipmentKey;
            equipKey = config.input.equipKey;
        }

        if (equipmentStorage == null)
        {
            GameObject storage = new GameObject("EquipmentStorage");
            storage.transform.SetParent(transform, false);
            equipmentStorage = storage.transform;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(tidyUpKey) && CurrentMode != InventoryMode.TidyUp)
        {
            SoundPlayer.Play2D(switchSound);
            SetMode(InventoryMode.TidyUp);
        }
        if (Input.GetKeyDown(equipmentKey) && CurrentMode != InventoryMode.Equipment)
        {
            SoundPlayer.Play2D(switchSound);
            SetMode(InventoryMode.Equipment);
        }

        if (Input.GetKeyDown(equipKey))
        {
            if (CurrentMode == InventoryMode.TidyUp) MoveActiveItemToEquipment();
            else if (CurrentMode == InventoryMode.Equipment) UnequipActiveWeapon();
        }

        if (CurrentMode == InventoryMode.Equipment)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f && equipmentInventory != null && equipmentInventory.items.Count > 1)
            {
                equipmentInventory.Cycle(scroll > 0 ? 1 : -1);
                SoundPlayer.Play2D(switchSound);
            }
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

    /// <summary>Какой инструмент сейчас в руках (лом, кувалда) — тот же слот, что и оружие, просто
    /// активная запись экипировки не оружие, а инструмент. None — не инструмент или режим уборки.</summary>
    public ToolKind ActiveToolKind
    {
        get
        {
            if (CurrentMode != InventoryMode.Equipment || equipmentInventory == null) return ToolKind.None;
            WorldItem active = equipmentInventory.ActiveItem;
            return active != null && active.itemData != null ? active.itemData.toolKind : ToolKind.None;
        }
    }

    /// <summary>
    /// Положить инструмент (лом, кувалда, швабра, мойка, катушка) прямо в экипировку — так подбирается
    /// любой предмет с ItemData.toolKind: инструментам нечего делать в инвентаре уборки. Режим не
    /// переключается: игрок сам берёт инструмент клавишей 2 и колесом.
    /// </summary>
    public bool AddToEquipment(WorldItem item)
    {
        if (item == null || equipmentInventory == null) return false;
        item.SetCarriedHidden(equipmentStorage);
        equipmentInventory.Add(item);
        return true;
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
        SoundPlayer.Play2D(equipSound);

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
        SoundPlayer.Play2D(equipSound);
        SetMode(InventoryMode.TidyUp);
    }
}
