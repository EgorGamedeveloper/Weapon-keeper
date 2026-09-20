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
    private void OnValidate()
    {
        var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        var found = new List<ItemData>(guids.Length);
        var seen = new Dictionary<string, ItemData>(guids.Length);

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null) continue;
            found.Add(item);

            if (string.IsNullOrEmpty(item.itemId))
            {
                Debug.LogError($"[ItemCatalog] У предмета '{item.name}' пустой itemId — сейв его не восстановит.", item);
                continue;
            }

            // Дубликат id — самая коварная ошибка: сейв тихо подставит не тот предмет.
            if (seen.TryGetValue(item.itemId, out ItemData clash))
                Debug.LogError($"[ItemCatalog] Одинаковый itemId '{item.itemId}' у '{clash.name}' и '{item.name}'.", item);
            else
                seen[item.itemId] = item;
        }

        items = found.ToArray();
        byId = null;
    }
#endif
}
