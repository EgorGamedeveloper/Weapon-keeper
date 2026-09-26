using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Один пункт меню на весь блок «настройки + локализация + Steam»:
/// Tools/Weapon Keeper/Setup Settings &amp; Steam — создать недостающие ассеты (SettingsDefaults, SteamConfig,
/// статистика и достижения), собрать недостающие префабы (окно настроек, пауза, окно достижений,
/// PersistentServices) и расставить всё в открытой сцене со ссылками: в Bootstrap — сервисы, окно настроек
/// и кнопка «Настройки»; в игровой сцене — сервисы, окно настроек, пауза, окно достижений, адаптер
/// статистики, Volume настроек и каналы звука. Повторный запуск безопасен.
///
/// Уже существующие префабы UI не пересобираются (правки в них сохраняются) — для пересборки есть пункты
/// Settings/Build Settings UI и Achievements/Build Achievements UI.
///
/// Вариант «(Bootstrap + TestScene)» делает то же самое для обеих сцен по очереди и сохраняет их.
/// </summary>
public static class WeaponKeeperSetup
{
    public const string BootstrapScenePath = "Assets/01_GAME/02_Scenes/Bootstrap.unity";
    public const string GameplayScenePath = "Assets/01_GAME/02_Scenes/TestScene.unity";
    private const string Title = "Setup Settings & Steam";

    [MenuItem("Tools/Weapon Keeper/Setup Settings & Steam", priority = 0)]
    private static void SetupOpenScene()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;

        var report = new StringBuilder();
        PrepareAssets(report);

        Scene scene = SceneManager.GetActiveScene();
        SetupScene(scene, report);
        CheckEnvironment(report);
        SceneSetupUtility.Finish(scene, Title, report);
    }

    [MenuItem("Tools/Weapon Keeper/Setup Settings & Steam (Bootstrap + TestScene)", priority = 1)]
    private static void SetupBothScenes()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var report = new StringBuilder();
        PrepareAssets(report);

        foreach (string path in new[] { BootstrapScenePath, GameplayScenePath })
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path)))
            {
                report.AppendLine($"✖ Нет сцены {path}.");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            report.AppendLine().AppendLine($"— {scene.name} —");
            SetupScene(scene, report);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("✔ Сцена сохранена.");
        }

        CheckEnvironment(report);
        Debug.Log($"[{Title}]\n{report}");
        EditorUtility.DisplayDialog(Title, report.ToString(), "OK");
    }

    /// <summary>Ассеты и префабы (общие для всех сцен).</summary>
    private static void PrepareAssets(StringBuilder report)
    {
        UIBuilderKit.ResetStringCache();
        UIBuilderKit.LoadOrCreateAsset<SettingsDefaults>(PersistentServicesBuilder.SettingsDefaultsPath);
        UIBuilderKit.LoadOrCreateAsset<SteamConfig>(PersistentServicesBuilder.SteamConfigPath);
        AchievementCatalog catalog = AchievementAssetsCreator.CreateOrUpdateAssets();
        report.AppendLine($"✔ Ассеты: SettingsDefaults, SteamConfig, статистик — {catalog.stats.Count}, достижений — {catalog.achievements.Count}.");

        string problems = catalog.Validate();
        if (!string.IsNullOrEmpty(problems)) report.AppendLine("✖ Каталог достижений: " + problems);

        BuildIfMissing(SettingsUIBuilder.SettingsWindowPath, SettingsUIBuilder.BuildSettingsWindow, report);
        BuildIfMissing(SettingsUIBuilder.PauseMenuPath, SettingsUIBuilder.BuildPauseMenu, report);
        BuildIfMissing(AchievementsUIBuilder.WindowPath, AchievementsUIBuilder.BuildAchievementsWindow, report);

        if (!PersistentServicesBuilder.IsUpToDate())
        {
            PersistentServicesBuilder.Build();
            report.AppendLine("✔ Собран префаб " + PersistentServicesBuilder.PrefabPath + ".");
        }

        AssetDatabase.SaveAssets();
    }

    private static void BuildIfMissing(string path, Func<GameObject> build, StringBuilder report)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        build();
        report.AppendLine("✔ Собран префаб " + path + ".");
    }

    private static void SetupScene(Scene scene, StringBuilder report)
    {
        SettingsSceneSetup.SetupScene(scene, report);
        AchievementsSceneSetup.SetupScene(scene, report);
        SettingsSceneSetup.AssignAudioChannels(scene, report);
    }

    private static void CheckEnvironment(StringBuilder report)
    {
        report.AppendLine();
        if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt")))
            report.AppendLine("• В корне проекта нет steam_appid.txt — без него Steam в редакторе не запустится.");

#if !STEAMWORKS_NET
        report.AppendLine("• Не задан символ STEAMWORKS_NET — Steam-код выключен. Проверьте, что пакет Steamworks.NET " +
                          "установлен (Window → Package Manager) и в Project Settings → Steamworks.NET разрешено " +
                          "управлять символами; платформа сборки — Windows/Mac/Linux.");
#endif

        report.AppendLine("• Проверьте шрифт: Tools/Weapon Keeper/Localization/Check Font (Cyrillic + Latin).");
    }
}
