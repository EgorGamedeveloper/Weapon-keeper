using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Пара "предмет экипировки — его модель в руке" (визуал существует в сцене всегда, просто включается/выключается).</summary>
[Serializable]
public class ToolVisual
{
    public ItemData item;
    public GameObject visual;
}

/// <summary>
/// Показывает модель активного инструмента (лом, позже швабра) в руке игрока. В отличие от оружия
/// (EquipmentWeaponBridge, спавнит префаб Easy Weapons динамически), инструменты — простые
/// заглушки-модели, всегда существующие в сцене: показываем ту, чей ItemData сейчас активен
/// в EquipmentInventory, остальные прячем.
/// </summary>
public class ToolPresenter : MonoBehaviour
{
    public EquipmentInventory equipmentInventory;

    [Tooltip("Вне режима Equipment модель инструмента прячется, даже если он всё ещё активная запись экипировки.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Все инструменты, чью модель нужно показывать/прятать (сейчас — лом, позже добавится швабра).")]
    public List<ToolVisual> tools = new List<ToolVisual>();

    private void Update()
    {
        bool equipmentModeActive = modeController != null
            && modeController.CurrentMode == PlayerInventoryModeController.InventoryMode.Equipment;

        ItemData active = equipmentModeActive && equipmentInventory != null && equipmentInventory.ActiveItem != null
            ? equipmentInventory.ActiveItem.itemData
            : null;

        foreach (var tool in tools)
        {
            if (tool.visual == null) continue;
            tool.visual.SetActive(tool.item != null && tool.item == active);
        }
    }
}
