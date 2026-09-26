using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр всех типов предметов. Сохранение хранит строковый ItemData.itemId (а не ссылку
/// на ассет — её в JSON не положишь), и при загрузке именно каталог превращает id обратно
/// в ItemData, из которого спавнится предмет.
///
/// Список собирается автоматически по всем ItemData проекта: забыть добавить новый предмет
/// и узнать об этом из сломанного сейва — слишком дорогая ошибка.
/// </summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Inventory/Item Catalog", order = 10)]
public class ItemCatalog : ScriptableObject
{
    [Tooltip("Все ItemData проекта. Пересобирается автоматически в редакторе, руками не правится.")]
    public ItemData[] items = System.Array.Empty<ItemData>();

    private Dictionary<string, ItemData> byId;

    /// <summary>Сбрасываем кеш при загрузке ассета и после перезагрузки домена — иначе словарь,
    /// построенный по старому (в том числе пустому) списку, переживёт правку items.</summary>
    private void OnEnable() => byId = null;

    /// <summary>Найти тип предмета по id из сейва. Возвращает null, если id неизвестен.</summary>
    public ItemData GetById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (byId == null)
        {
            byId = new Dictionary<string, ItemData>(items.Length);
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                byId[item.itemId] = item;
            }
        }

        return byId.TryGetValue(itemId, out ItemData found) ? found : null;
    }

#if UNITY_EDITOR
    // Пересборка отложена на delayCall: в самом OnValidate трогать AssetDatabase небезопасно.
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Rebuild();
        };
    }

    /// <summary>
    /// Пересобрать список по всем ItemData проекта. Помечает ассет изменённым только если список
    /// реально поменялся — иначе правка не доживала до диска, и в сборку уходил устаревший каталог.
    /// Зовётся из OnValidate и из ItemCatalogAutoRebuild (импорт/удаление/перенос ассетов).
    /// </summary>
    public bool Rebuild()
    {
        var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        var found = new List<ItemData>(guids.Length);
        foreach (string guid in guids)
        {
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) found.Add(item);
        }
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        bool changed = found.Count != items.Length;
        for (int i = 0; !changed && i < found.Count; i++)
            changed = found[i] != items[i];

        if (!changed) return false;

        items = found.ToArray();
        byId = null;
        UnityEditor.EditorUtility.SetDirty(this);
        return true;
    }

    /// <summary>Проблемы, из-за которых сейв не сможет восстановить предметы: пустой каталог,
    /// пустые и повторяющиеся itemId. Пустая строка — всё в порядке.</summary>
    public string Validate()
    {
        if (items.Length == 0) return $"каталог '{name}' пуст";

        var problems = new List<string>();
        var seen = new Dictionary<string, ItemData>(items.Length);
        foreach (var item in items)
        {
            if (item == null) continue;
            if (string.IsNullOrEmpty(item.itemId))
                problems.Add($"у '{item.name}' пустой itemId");
            else if (seen.TryGetValue(item.itemId, out ItemData clash))
                problems.Add($"одинаковый itemId '{item.itemId}' у '{clash.name}' и '{item.name}'");
            else
                seen[item.itemId] = item;
        }
        return string.Join("; ", problems);
    }
#endif
}
