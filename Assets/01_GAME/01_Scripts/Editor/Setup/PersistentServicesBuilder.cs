using UnityEditor;
using UnityEngine;

/// <summary>
/// Префаб постоянных сервисов 03_Prefabs/Systems/PersistentServices: PersistentServicesRoot + локализация +
/// настройки (и остальное, что должно жить весь процесс). Кладётся и в Bootstrap, и в игровую сцену —
/// см. PersistentServicesRoot. Префаб пересобирается целиком: ссылки на ассеты проставляются заново.
/// </summary>
public static class PersistentServicesBuilder
{
    public const string PrefabPath = "Assets/01_GAME/03_Prefabs/Systems/PersistentServices.prefab";
    public const string SettingsDefaultsPath = "Assets/01_GAME/04_Data/Settings/SettingsDefaults.asset";

    public static GameObject Build()
    {
        var defaults = UIBuilderKit.LoadOrCreateAsset<SettingsDefaults>(SettingsDefaultsPath);
        var strings = AssetDatabase.LoadAssetAtPath<TextAsset>(LocalizationEditorTools.StringsPath);
        if (strings == null) Debug.LogWarning($"[Setup] Не найден {LocalizationEditorTools.StringsPath} — тексты не будут переводиться.");

        var root = new GameObject("PersistentServices");
        root.AddComponent<PersistentServicesRoot>();

        var localization = root.AddComponent<LocalizationService>();
        localization.stringsTable = strings;

        var settings = root.AddComponent<SettingsService>();
        settings.defaults = defaults;

        return UIBuilderKit.SavePrefab(root, PrefabPath);
    }
}
