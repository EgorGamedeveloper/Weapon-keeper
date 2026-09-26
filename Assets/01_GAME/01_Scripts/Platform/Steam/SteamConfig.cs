using UnityEngine;

/// <summary>
/// Настройки интеграции со Steam (ассет 04_Data/Platform/SteamConfig). Пока своего App ID нет, стоит
/// тестовый 480 (Spacewar) — как сменить на настоящий, см. Docs/Steam/STEAM_SETUP.md.
/// </summary>
[CreateAssetMenu(fileName = "SteamConfig", menuName = "Game/Platform/Steam Config", order = 20)]
public class SteamConfig : ScriptableObject
{
    /// <summary>Тестовое приложение Valve (Spacewar) — у него свои достижения и статистика, не наши.</summary>
    public const uint SpacewarAppId = 480;

    [Header("Приложение")]
    [Tooltip("App ID игры в Steam. 480 — тестовый Spacewar, пока своего нет. В корне проекта должен лежать " +
             "steam_appid.txt с тем же числом (только для разработки, в билд он не попадает).")]
    public uint appId = SpacewarAppId;

    [Header("Запуск")]
    [Tooltip("Инициализировать Steam при Play в редакторе (нужен запущенный клиент Steam). Выключено — в редакторе " +
             "работают локальные достижения (achievements.sav).")]
    public bool enableInEditor = true;

    [Tooltip("В билде, запущенном не из Steam, перезапустить игру через клиент Steam (SteamAPI.RestartAppIfNecessary). " +
             "Включать ТОЛЬКО с настоящим App ID: с 480 перезапуск ушёл бы в Spacewar.")]
    public bool restartIfNecessary = false;

    [Header("Отладка")]
    [Tooltip("Писать в консоль предупреждения Steam API (приходят только при запуске с параметром -debug_steamapi).")]
    public bool logSteamWarnings = true;

    /// <summary>Настоящий ли App ID (не тестовый Spacewar).</summary>
    public bool HasRealAppId => appId != 0 && appId != SpacewarAppId;
}
