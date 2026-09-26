// Steamworks.NET есть только на настольных платформах и только когда пакет установлен: символ STEAMWORKS_NET
// пакет сам добавляет в Scripting Define Symbols при импорте. Без него весь Steam-код ниже не компилируется,
// а игра работает без Steam (проект собирается и без пакета).
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX) || !STEAMWORKS_NET
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Подключение к Steam (Steamworks.NET): инициализация SteamAPI, колбэки каждый кадр, завершение при выходе.
/// Живёт в PersistentServices через все сцены. Без клиента Steam (не запущен, билд без Steam, редактор с
/// выключенным enableInEditor) игра работает как обычно: Initialized = false, все вызовы — пустые.
///
/// Все обращения к Steamworks проекта — только в Platform/Steam и только под #if !DISABLESTEAMWORKS; наружу
/// сервис отдаёт обычные типы (строки, bool, event Action), чтобы остальной код не зависел от пакета.
///
/// Статический Instance — осознанное исключение из правила «без синглтонов»: Steam API — глобальный ресурс
/// процесса, его инициализируют ровно один раз (повторный SteamAPI.Init после Shutdown в том же процессе
/// не поддерживается), и к нему обращаются несвязанные системы (настройки — язык, статистика, меню паузы —
/// оверлей).
///
/// Завершение (SteamAPI.Shutdown) — в OnDestroy, а не в OnApplicationQuit: как в каноническом
/// SteamManager Steamworks.NET. OnApplicationQuit других объектов (StatsService отправляет статистику)
/// может прийти позже, а OnDestroy постоянного объекта при выходе — после них.
/// </summary>
[DefaultExecutionOrder(-2000)]
[DisallowMultipleComponent]
public class SteamManager : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Настройки Steam (04_Data/Platform/SteamConfig): App ID, запуск в редакторе, перезапуск через Steam.")]
    public SteamConfig config;

    /// <summary>Живой экземпляр (null — Steam-сервиса в игре нет).</summary>
    public static SteamManager Instance { get; private set; }

    /// <summary>SteamAPI инициализирован и им можно пользоваться.</summary>
    public static bool Initialized => Instance != null && Instance.initialized;

    /// <summary>Оверлей Steam открылся (true) или закрылся (false) — меню паузы ставит игру на паузу.</summary>
    public event Action<bool> OnOverlayActivated;

    private bool initialized;

#if !DISABLESTEAMWORKS
    private static bool everInitialized;
    private SteamAPIWarningMessageHook_t warningHook;
    private Callback<GameOverlayActivated_t> overlayCallback;
#endif

    // Без перезагрузки домена (Enter Play Mode Options) статика пережила бы прошлый запуск.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
#if !DISABLESTEAMWORKS
        everInitialized = false;
#endif
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Initialize();
    }

    private void Update()
    {
#if !DISABLESTEAMWORKS
        if (initialized) SteamAPI.RunCallbacks();
#endif
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

#if !DISABLESTEAMWORKS
        if (!initialized) return;
        initialized = false;
        overlayCallback?.Dispose();
        overlayCallback = null;
        SteamAPI.Shutdown();
#endif
    }

    // ───────────────────────── Для остальных систем ─────────────────────────

    /// <summary>Язык игры, выбранный в Steam (код Steam: russian, english...). null — Steam не подключён.</summary>
    public string GetCurrentGameLanguage()
    {
#if !DISABLESTEAMWORKS
        if (initialized) return SteamApps.GetCurrentGameLanguage();
#endif
        return null;
    }

    /// <summary>Строка статуса для друзей (Rich Presence). Ключ steam_display — токен из файла локализации
    /// rich presence (Docs/Steam), остальные ключи — подстановки в токен (%ключ%).</summary>
    public void SetRichPresence(string key, string value)
    {
#if !DISABLESTEAMWORKS
        if (initialized && !SteamFriends.SetRichPresence(key, value))
            Debug.LogWarning($"[Steam] SetRichPresence(\"{key}\", \"{value}\") отклонён Steam.");
#endif
    }

    /// <summary>Сбросить статус для друзей.</summary>
    public void ClearRichPresence()
    {
#if !DISABLESTEAMWORKS
        if (initialized) SteamFriends.ClearRichPresence();
#endif
    }

    // ───────────────────────── Инициализация ─────────────────────────

    private void Initialize()
    {
#if DISABLESTEAMWORKS
        Debug.Log("[Steam] Steamworks.NET недоступен для этой платформы или не установлен — игра работает без Steam.");
#else
        if (config == null)
        {
            Debug.LogWarning("[Steam] Не назначен SteamConfig — Steam не инициализирован.", this);
            return;
        }

        if (Application.isEditor && !config.enableInEditor)
        {
            Debug.Log("[Steam] В редакторе Steam выключен (SteamConfig.enableInEditor) — достижения пишутся локально.");
            return;
        }

        if (everInitialized)
        {
            // Повторный SteamAPI.Init в том же процессе не поддерживается.
            Debug.LogError("[Steam] SteamAPI уже инициализировался в этом процессе — повторная инициализация пропущена.", this);
            return;
        }

        if (!Packsize.Test())
            Debug.LogError("[Steam] Packsize Test не пройден: версия Steamworks.NET не подходит этой платформе.", this);
        if (!DllCheck.Test())
            Debug.LogError("[Steam] DllCheck Test не пройден: библиотеки steam_api не той версии.", this);

        try
        {
            // Запуск не из Steam: клиент перезапустит игру сам (с тестовым 480 ушёл бы в Spacewar — см. SteamConfig).
            if (!Application.isEditor && config.restartIfNecessary && SteamAPI.RestartAppIfNecessary(new AppId_t(config.appId)))
            {
                Debug.Log("[Steam] Игра запущена не через Steam — перезапуск через клиент Steam.");
                Application.Quit();
                return;
            }

            ESteamAPIInitResult result = SteamAPI.InitEx(out string error);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                Debug.LogWarning($"[Steam] SteamAPI не инициализирован ({result}): {error}. Игра работает без Steam — " +
                                 "достижения и статистика пишутся локально.");
                return;
            }
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError("[Steam] Не найдена библиотека steam_api — игра работает без Steam.\n" + e.Message, this);
            return;
        }

        initialized = true;
        everInitialized = true;

        if (config.logSteamWarnings)
        {
            // Делегат хранится в поле: иначе сборщик мусора заберёт его, пока Steam продолжает его вызывать.
            warningHook = HandleSteamWarning;
            SteamClient.SetWarningMessageHook(warningHook);
        }

        overlayCallback = Callback<GameOverlayActivated_t>.Create(HandleOverlayActivated);

        uint runningAppId = SteamUtils.GetAppID().m_AppId;
        Debug.Log($"[Steam] Steam инициализирован: App ID {runningAppId}, язык игры {SteamApps.GetCurrentGameLanguage()}.");
        if (runningAppId != config.appId)
            Debug.LogWarning($"[Steam] Запущен App ID {runningAppId}, а в SteamConfig — {config.appId}: проверьте steam_appid.txt.");
#endif
    }

#if !DISABLESTEAMWORKS
    [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
    private static void HandleSteamWarning(int severity, System.Text.StringBuilder text)
    {
        Debug.LogWarning("[Steam] " + text);
    }

    private void HandleOverlayActivated(GameOverlayActivated_t data)
    {
        OnOverlayActivated?.Invoke(data.m_bActive != 0);
    }
#endif
}
