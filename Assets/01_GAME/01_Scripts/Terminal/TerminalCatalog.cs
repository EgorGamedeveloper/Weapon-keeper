using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр данных терминала: все лутбоксы и шаблоны заказов. Нужен сейву (id → ассет) и сервисам
/// терминала (что показывать). Собирается автоматически по проекту — как ItemCatalog.
/// </summary>
[CreateAssetMenu(fileName = "TerminalCatalog", menuName = "Terminal/Terminal Catalog", order = 42)]
public class TerminalCatalog : ScriptableObject
{
    [Tooltip("Все LootBoxData проекта. Пересобирается автоматически в редакторе.")]
    public LootBoxData[] lootBoxes = System.Array.Empty<LootBoxData>();

    [Tooltip("Все ShippingOrderData проекта. Пересобирается автоматически в редакторе.")]
    public ShippingOrderData[] orders = System.Array.Empty<ShippingOrderData>();

    public LootBoxData GetLootBox(string id)
    {
        foreach (var box in lootBoxes) if (box != null && box.lootBoxId == id) return box;
        return null;
    }

    public ShippingOrderData GetOrder(string id)
    {
        foreach (var order in orders) if (order != null && order.orderId == id) return order;
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lootBoxes = FindAll<LootBoxData>();
        orders = FindAll<ShippingOrderData>();
    }

    private static T[] FindAll<T>() where T : Object
    {
        var result = new List<T>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) result.Add(asset);
        }
        return result.ToArray();
    }
#endif
}
