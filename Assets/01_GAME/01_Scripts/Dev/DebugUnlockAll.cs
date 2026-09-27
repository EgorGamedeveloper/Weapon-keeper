using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Тестовый режим «всё открыто» — только в редакторе, в сборку не попадает. Пока включён, при загрузке
/// каждой сцены с игроком ему выдаются:
/// - все навыки-лицензии (навыки без модификаторов: продажа амуниции и оружия, лицензии оружейника,
///   швабра, мойка) — терминал и дерево навыков считают их купленными;
/// - все инструменты из каталога предметов (ItemData.IsTool), которых ещё нет в экипировке: лом, кувалда,
///   швабра, мойка, катушка провода.
/// Сцену и ассеты не меняет. Включается и выключается пунктом меню
/// Tools/Weapon Keeper/Debug/Unlock All Licenses And Tools (галочка хранится в EditorPrefs этой машины).
///
/// Внимание: при запуске через Bootstrap сохранение включено — выданное попадёт в настоящий сейв.
/// Play прямо на TestScene сейв не пишет.
/// </summary>
public static class DebugUnlockAll
{
#if UNITY_EDITOR
    private const string PrefKey = "WeaponKeeper.DebugUnlockAll";
    private const string MenuPath = "Tools/Weapon Keeper/Debug/Unlock All Licenses And Tools";
    private const string ItemCatalogPath = "Assets/01_GAME/04_Data/Config/ItemCatalog.asset";

    /// <summary>Режим включён (на этой машине).</summary>
    public static bool Enabled
    {
        get => UnityEditor.EditorPrefs.GetBool(PrefKey, false);
        set => UnityEditor.EditorPrefs.SetBool(PrefKey, value);
    }

    [UnityEditor.MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Debug.Log($"[DebugUnlockAll] Тестовый режим «всё открыто» {(Enabled ? "включён" : "выключен")}.");
        if (Enabled && Application.isPlaying) Apply();
    }

    [UnityEditor.MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        UnityEditor.Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // sceneLoaded приходит после Awake объектов сцены (сейв уже восстановлен) и до их Start: стартовые
    // инструменты (StartingEquipment.Start) увидят выданное и не задвоят его.
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Enabled) Apply();
    }

    /// <summary>Выдать все лицензии и инструменты сейчас.</summary>
    public static void Apply()
    {
        int licenses = 0, tools = 0;

        var skills = Object.FindAnyObjectByType<PlayerSkills>();
        if (skills != null && skills.catalog != null)
        {
            var ids = new List<string>();
            foreach (var skill in skills.catalog.skills)
                if (skill != null && (skill.modifiers == null || skill.modifiers.Length == 0) && !skills.IsOwned(skill))
                    ids.Add(skill.skillId);
            skills.RestoreOwned(ids);
            licenses = ids.Count;
        }

        var equipment = Object.FindAnyObjectByType<EquipmentInventory>();
        var modeController = Object.FindAnyObjectByType<PlayerInventoryModeController>();
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
        if (equipment != null && modeController != null && catalog != null)
        {
            foreach (var item in catalog.items)
            {
                if (item == null || !item.IsTool || item.worldPrefab == null || HasTool(equipment, item)) continue;
                WorldItem worldItem = Object.Instantiate(item.worldPrefab).GetComponent<WorldItem>();
                if (worldItem != null && modeController.AddToEquipment(worldItem)) tools++;
            }
        }

        if (skills != null || equipment != null)
            Debug.Log($"[DebugUnlockAll] Выдано лицензий: {licenses}, инструментов: {tools}.");
    }

    private static bool HasTool(EquipmentInventory equipment, ItemData item)
    {
        foreach (var owned in equipment.items)
            if (owned != null && owned.itemData == item) return true;
        return false;
    }
#endif
}
