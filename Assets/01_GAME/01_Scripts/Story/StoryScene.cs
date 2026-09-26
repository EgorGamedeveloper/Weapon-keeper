using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Доступ сюжета к игровой сцене: системы (квесты, кошелёк, прогрессия, инвентари, терминал) и объекты по
/// id из каталога сцены — PersistentId у точек ремонта, разрушаемых объектов, пятен, полок и точек спавна,
/// zoneId у зон доставки, storyId у StoryObject, id ассетов у предметов, категорий, врагов, заказов и ящиков.
///
/// Системы берутся из полей StoryDirector, а пустые ищутся в сцене один раз. Объекты ищутся среди
/// неактивных тоже (FindObjectsInactive.Include): двери и пятна бывают выключены до поры (RevealOnBreak).
/// </summary>
public class StoryScene
{
    public QuestManager Quests { get; private set; }
    public PlayerWallet Wallet { get; private set; }
    public PlayerProgression Progression { get; private set; }
    public InventorySystem TidyUp { get; private set; }
    public EquipmentInventory Equipment { get; private set; }
    public ShippingService Shipping { get; private set; }
    public SupplyService Supply { get; private set; }
    public ItemCatalog Items { get; private set; }
    public PlayerSkills Skills { get; private set; }

    private readonly Dictionary<Type, Component[]> cache = new Dictionary<Type, Component[]>();
    private readonly HashSet<string> warned = new HashSet<string>();

    public static StoryScene Collect(StoryDirector director)
    {
        var scene = new StoryScene
        {
            Quests = director.questManager != null ? director.questManager : Object.FindAnyObjectByType<QuestManager>(),
            Wallet = Object.FindAnyObjectByType<PlayerWallet>(),
            Progression = Object.FindAnyObjectByType<PlayerProgression>(),
            TidyUp = Object.FindAnyObjectByType<InventorySystem>(),
            Equipment = Object.FindAnyObjectByType<EquipmentInventory>(),
            Shipping = Object.FindAnyObjectByType<ShippingService>(),
            Supply = Object.FindAnyObjectByType<SupplyService>(),
            Skills = Object.FindAnyObjectByType<PlayerSkills>(),
            Items = director.itemCatalog,
        };

        if (scene.Items == null)
        {
            GameBootstrap bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap != null) scene.Items = bootstrap.itemCatalog;
        }
        return scene;
    }

    /// <summary>Все компоненты типа в сцене, включая выключенные (один поиск на тип).</summary>
    public T[] All<T>() where T : Component
    {
        if (!cache.TryGetValue(typeof(T), out Component[] found))
        {
            found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            cache[typeof(T)] = found;
        }
        return (T[])found;
    }

    /// <summary>Объект по PersistentId (точка ремонта, разрушаемый объект, пятно, полка, точка спавна).</summary>
    public T FindByPersistentId<T>(string id) where T : Component
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (T component in All<T>())
        {
            if (component == null) continue;
            PersistentId persistentId = component.GetComponent<PersistentId>();
            if (persistentId != null && persistentId.Id == id) return component;
        }
        return null;
    }

    /// <summary>Объект сюжета по storyId.</summary>
    public StoryObject FindStoryObject(string storyId)
    {
        if (string.IsNullOrEmpty(storyId)) return null;
        foreach (StoryObject storyObject in All<StoryObject>())
            if (storyObject != null && storyObject.storyId == storyId) return storyObject;
        return null;
    }

    /// <summary>Кат-сцена по storyId.</summary>
    public StoryCutscene FindCutscene(string storyId)
    {
        if (string.IsNullOrEmpty(storyId)) return null;
        foreach (StoryCutscene cutscene in All<StoryCutscene>())
            if (cutscene != null && cutscene.storyId == storyId) return cutscene;
        return null;
    }

    /// <summary>Ассет предмета по itemId (через ItemCatalog; без каталога — null).</summary>
    public ItemData FindItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || Items == null) return null;
        return Items.GetById(itemId);
    }

    /// <summary>Одно предупреждение на ключ: граф ссылается на то, чего нет в сцене.</summary>
    public void Warn(string key, string message, Object context = null)
    {
        if (warned.Add(key)) Debug.LogWarning("[Story] " + message, context);
    }
}
