using UnityEditor;
using UnityEngine;

/// <summary>
/// Префаб постоянных сервисов 03_Prefabs/Systems/PersistentServices: PersistentServicesRoot + Steam +
/// локализация + настройки + статистика и достижения (с отладочной панелью F9 и канвасом тостов) +
/// звуки интерфейса (UISoundFeedback, звуки — из SoundAssetsBuilder).
/// Кладётся и в Bootstrap, и в игровую сцену — см. PersistentServicesRoot. Префаб пересобирается целиком:
/// ссылки на ассеты проставляются заново.
/// </summary>
public static class PersistentServicesBuilder
{
    public const string PrefabPath = "Assets/01_GAME/03_Prefabs/Systems/PersistentServices.prefab";
    public const string SettingsDefaultsPath = "Assets/01_GAME/04_Data/Settings/SettingsDefaults.asset";
    public const string SteamConfigPath = "Assets/01_GAME/04_Data/Platform/SteamConfig.asset";

    public static GameObject Build()
    {
        var defaults = UIBuilderKit.LoadOrCreateAsset<SettingsDefaults>(SettingsDefaultsPath);
        var strings = AssetDatabase.LoadAssetAtPath<TextAsset>(LocalizationEditorTools.StringsPath);
        if (strings == null) Debug.LogWarning($"[Setup] Не найден {LocalizationEditorTools.StringsPath} — тексты не будут переводиться.");

        var steamConfig = UIBuilderKit.LoadOrCreateAsset<SteamConfig>(SteamConfigPath);

        var root = new GameObject("PersistentServices");
        root.AddComponent<PersistentServicesRoot>();

        var steam = root.AddComponent<SteamManager>();
        steam.config = steamConfig;
        root.AddComponent<SteamRichPresence>();

        var localization = root.AddComponent<LocalizationService>();
        localization.stringsTable = strings;

        var settings = root.AddComponent<SettingsService>();
        settings.defaults = defaults;

        var catalog = AssetDatabase.LoadAssetAtPath<AchievementCatalog>(AchievementAssetsCreator.CatalogPath);
        if (catalog == null) catalog = AchievementAssetsCreator.CreateOrUpdateAssets();
        var stats = root.AddComponent<StatsService>();
        stats.catalog = catalog;

        root.AddComponent<AchievementDebugOverlay>();

        var uiSounds = root.AddComponent<UISoundFeedback>();
        uiSounds.hoverSound = SoundAssetsBuilder.Cue("UI_Hover");
        uiSounds.clickSound = SoundAssetsBuilder.Cue("UI_Click");
        uiSounds.windowOpenSound = SoundAssetsBuilder.Cue("UI_Open");
        uiSounds.windowCloseSound = SoundAssetsBuilder.Cue("UI_Close");
        AchievementsUIBuilder.BuildToastCanvas(root.transform);

        return UIBuilderKit.SavePrefab(root, PrefabPath);
    }

    /// <summary>Есть ли префаб и все ли сервисы в нём (префаб, собранный старой версией меню, пересобирается).</summary>
    public static bool IsUpToDate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return prefab != null
               && prefab.GetComponent<PersistentServicesRoot>() != null
               && prefab.GetComponent<SteamManager>() != null
               && prefab.GetComponent<SteamRichPresence>() != null
               && prefab.GetComponent<LocalizationService>() != null
               && prefab.GetComponent<SettingsService>() != null
               && prefab.GetComponent<StatsService>() != null
               && prefab.GetComponent<AchievementDebugOverlay>() != null
               && prefab.GetComponent<UISoundFeedback>() != null
               && prefab.GetComponentInChildren<AchievementToastUI>(true) != null;
    }
}
