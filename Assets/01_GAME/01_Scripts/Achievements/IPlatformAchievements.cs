/// <summary>
/// Хранилище достижений и статистики платформы. Интерфейс — осознанное исключение из правила «без
/// интерфейсов» (как IPlaceableSlot): реализаций две уже сейчас — Steam (SteamAchievementsBackend) и
/// локальный файл для билдов без Steam и Play в редакторе (LocalAchievementsBackend), — а будущие платформы
/// (другие магазины, консоли) добавятся новой реализацией без правки StatsService и игры.
///
/// Имена — API-имена из Steamworks (у локального бэкенда — те же, для единообразия).
/// </summary>
public interface IPlatformAchievements
{
    /// <summary>Имя бэкенда для логов и окна отладки.</summary>
    string Name { get; }

    /// <summary>Платформа сама показывает уведомление о полученном достижении (оверлей Steam) — тост игры не нужен.</summary>
    bool ShowsOwnNotifications { get; }

    bool IsUnlocked(string apiName);

    /// <summary>Выдать достижение. false — платформа отказала (нет такого API-имени и т.п.).</summary>
    bool Unlock(string apiName);

    bool SetStat(string apiName, int value);
    bool SetStat(string apiName, float value);
    bool GetStat(string apiName, out int value);
    bool GetStat(string apiName, out float value);

    /// <summary>Показать всплывающий прогресс достижения. Сам прогресс не сохраняет — для этого SetStat.</summary>
    void IndicateProgress(string apiName, uint current, uint max);

    /// <summary>Сохранить/отправить изменения (у Steam — StoreStats, частоту ограничивает StatsService).</summary>
    bool Store();

    /// <summary>Сбросить статистику (и достижения) — только для отладки.</summary>
    void ResetAll(bool achievementsToo);

    /// <summary>Освободить ресурсы (колбэки Steam).</summary>
    void Shutdown();
}
