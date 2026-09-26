// См. SteamManager: без STEAMWORKS_NET и не на настольной платформе Steam-код не компилируется.
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX) || !STEAMWORKS_NET
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Достижения и статистика через Steam (ISteamUserStats). Создаётся StatsService, только если
/// SteamManager.Initialized. Статистику клиент Steam загружает сам до старта игры — RequestCurrentStats
/// не нужен (в текущем Steamworks.NET его уже нет).
///
/// Ошибки (нет такого API-имени — например, у тестового Spacewar 480 свои имена) пишутся в лог один раз
/// на имя. Результаты StoreStats приходят колбэками UserStatsStored_t / UserAchievementStored_t.
/// </summary>
public class SteamAchievementsBackend : IPlatformAchievements
{
    private readonly HashSet<string> warned = new HashSet<string>();

#if !DISABLESTEAMWORKS
    private Callback<UserStatsStored_t> statsStored;
    private Callback<UserAchievementStored_t> achievementStored;
#endif

    public SteamAchievementsBackend()
    {
#if !DISABLESTEAMWORKS
        statsStored = Callback<UserStatsStored_t>.Create(HandleStatsStored);
        achievementStored = Callback<UserAchievementStored_t>.Create(HandleAchievementStored);
#endif
    }

    public string Name => "Steam";
    public bool ShowsOwnNotifications => true;

    public bool IsUnlocked(string apiName)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized || string.IsNullOrEmpty(apiName)) return false;
        return SteamUserStats.GetAchievement(apiName, out bool achieved) && achieved;
#else
        return false;
#endif
    }

    public bool Unlock(string apiName)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized || string.IsNullOrEmpty(apiName)) return false;
        if (SteamUserStats.SetAchievement(apiName)) return true;
        WarnOnce(apiName, $"SetAchievement(\"{apiName}\") отклонён — достижения нет в Steamworks этого App ID или изменения не опубликованы.");
#endif
        return false;
    }

    public bool SetStat(string apiName, int value)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized || string.IsNullOrEmpty(apiName)) return false;
        if (SteamUserStats.SetStat(apiName, value)) return true;
        WarnOnce(apiName, $"SetStat(\"{apiName}\", {value}) отклонён — статистики нет, тип не INT или нарушено ограничение.");
#endif
        return false;
    }

    public bool SetStat(string apiName, float value)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized || string.IsNullOrEmpty(apiName)) return false;
        if (SteamUserStats.SetStat(apiName, value)) return true;
        WarnOnce(apiName, $"SetStat(\"{apiName}\", {value}) отклонён — статистики нет, тип не FLOAT или нарушено ограничение.");
#endif
        return false;
    }

    public bool GetStat(string apiName, out int value)
    {
        value = 0;
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized || string.IsNullOrEmpty(apiName)) return false;
        if (SteamUserStats.GetStat(apiName, out value)) return true;
        WarnOnce(apiName, $"GetStat(\"{apiName}\") — статистики нет в Steamworks этого App ID, считаем с нуля.");
#endif
        return false;
    }

    public bool GetStat(string apiName, out float value)
    {
        value = 0f;
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized || string.IsNullOrEmpty(apiName)) return false;
        if (SteamUserStats.GetStat(apiName, out value)) return true;
        WarnOnce(apiName, $"GetStat(\"{apiName}\") — статистики нет в Steamworks этого App ID, считаем с нуля.");
#endif
        return false;
    }

    public void IndicateProgress(string apiName, uint current, uint max)
    {
#if !DISABLESTEAMWORKS
        // Показывает всплывающий прогресс в оверлее; сам прогресс сохраняет SetStat статистики.
        if (SteamManager.Initialized && current < max) SteamUserStats.IndicateAchievementProgress(apiName, current, max);
#endif
    }

    public bool Store()
    {
#if !DISABLESTEAMWORKS
        return SteamManager.Initialized && SteamUserStats.StoreStats();
#else
        return false;
#endif
    }

    public void ResetAll(bool achievementsToo)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;
        SteamUserStats.ResetAllStats(achievementsToo);
        SteamUserStats.StoreStats();
#endif
    }

    public void Shutdown()
    {
#if !DISABLESTEAMWORKS
        statsStored?.Dispose();
        achievementStored?.Dispose();
        statsStored = null;
        achievementStored = null;
#endif
    }

    private void WarnOnce(string apiName, string message)
    {
        if (warned.Add(apiName)) Debug.LogWarning("[Steam] " + message);
    }

#if !DISABLESTEAMWORKS
    private void HandleStatsStored(UserStatsStored_t data)
    {
        if (data.m_eResult == EResult.k_EResultOK)
            Debug.Log("[Steam] Статистика и достижения отправлены (UserStatsStored_t: OK).");
        else if (data.m_eResult == EResult.k_EResultInvalidParam)
            Debug.LogWarning("[Steam] UserStatsStored_t: InvalidParam — часть статистики нарушила ограничения Steamworks, Steam откатил её значения.");
        else
            Debug.LogWarning($"[Steam] UserStatsStored_t: {data.m_eResult}.");
    }

    private void HandleAchievementStored(UserAchievementStored_t data)
    {
        // Нули в прогрессе — достижение полностью получено; иначе это ответ на IndicateAchievementProgress.
        if (data.m_nCurProgress == 0 && data.m_nMaxProgress == 0)
            Debug.Log($"[Steam] Достижение получено: {data.m_rgchAchievementName}.");
        else
            Debug.Log($"[Steam] Прогресс достижения {data.m_rgchAchievementName}: {data.m_nCurProgress}/{data.m_nMaxProgress}.");
    }
#endif
}
