using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Пара "предмет экипировки — его модель в руке" (визуал существует в сцене всегда, просто включается/выключается).</summary>
[Serializable]
public class ToolVisual
{
    [Tooltip("Предмет экипировки (лом, кувалда).")]
    public ItemData item;
    [Tooltip("Его модель в руке — включается, пока предмет активен.")]
    public GameObject visual;
}

/// <summary>
/// Показывает модель активного инструмента (лом, кувалда) в руке игрока. В отличие от оружия
/// (EquipmentWeaponBridge, спавнит префаб Easy Weapons динамически), инструменты — модели из
/// примитивов, всегда существующие в сцене под HandPoint: показываем ту, чей ItemData сейчас активен
/// в EquipmentInventory, остальные прячем. Двигают модели их действия: PlayerToolActions (лом-рычаг)
/// и SledgehammerSwing (замах); пивот модели лома — кончик лапки.
/// </summary>
public class ToolPresenter : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Инвентарь экипировки: показывается модель его активного инструмента.")]
    public EquipmentInventory equipmentInventory;

    [Tooltip("Вне режима Equipment модель инструмента прячется, даже если он всё ещё активная запись экипировки.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Все инструменты, чью модель нужно показывать/прятать (лом, кувалда).")]
    public List<ToolVisual> tools = new List<ToolVisual>();

    /// <summary>Модель инструмента этого вида в руке (лом, кувалда) — её двигают PlayerToolActions
    /// (лом переносится к доске) и SledgehammerSwing (замах). null — такого инструмента в списке нет.</summary>
    public Transform GetVisual(ToolKind kind)
    {
        foreach (var tool in tools)
            if (tool.visual != null && tool.item != null && tool.item.toolKind == kind)
                return tool.visual.transform;
        return null;
    }

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
