using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Проверки сборки для Steam:
/// - перезапуск через Steam (SteamConfig.restartIfNecessary) с тестовым App ID 480 валит сборку: игроков
///   уводило бы в Spacewar;
/// - релизная сборка с тестовым App ID — предупреждение (достижения Steam в ней не работают);
/// - steam_appid.txt не должен оказаться в папке билда: это файл только для разработки (с ним Steam
///   считает любой запуск exe запуском тестового приложения). Unity сам его не копирует, но если он
///   туда попал (скрипт сборки, ручное копирование), он удаляется с предупреждением.
/// </summary>
public class SteamBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var config = AssetDatabase.LoadAssetAtPath<SteamConfig>(PersistentServicesBuilder.SteamConfigPath);
        if (config == null) return;

        if (config.restartIfNecessary && !config.HasRealAppId)
            throw new BuildFailedException("[Steam] В SteamConfig включён restartIfNecessary с тестовым App ID " +
                                           $"{config.appId}: игра перезапускалась бы в Spacewar. Выключите флаг или задайте свой App ID.");

        if (!config.HasRealAppId && !EditorUserBuildSettings.development)
            Debug.LogWarning($"[Steam] Релизная сборка с тестовым App ID {config.appId} (Spacewar): достижения и статистика " +
                             "Steam в ней работать не будут. См. Docs/Steam/STEAM_SETUP.md.");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        string outputPath = report.summary.outputPath;
        string folder = Directory.Exists(outputPath) ? outputPath : Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(folder)) return;

        string appIdFile = Path.Combine(folder, "steam_appid.txt");
        if (!File.Exists(appIdFile)) return;

        File.Delete(appIdFile);
        Debug.LogWarning($"[Steam] Из билда удалён steam_appid.txt ({appIdFile}) — он нужен только для разработки.");
    }
}
