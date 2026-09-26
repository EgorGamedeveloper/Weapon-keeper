using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Статистика и достижения игрока. Считает значения статистик (Increment, SetMax), проверяет цели
/// достижений из AchievementCatalog и передаёт всё бэкенду платформы: Steam, если SteamManager
/// инициализирован, иначе локальному файлу achievements.sav.
///
/// Статистика общая для игрока, а не для сейва: живёт в PersistentServices, её не сбрасывает «Новая игра».
/// События игры сюда приносит StatsEventAdapter игровой сцены — сервис о них не знает.
///
/// Отправка (StoreStats у Steam) — редко, у Steam есть ограничение частоты: сразу при получении
/// достижения (иначе Steam не покажет уведомление), при выходе в меню и из игры (OnApplicationQuit),
/// при паузе — не чаще minSecondsBetweenStores, и по таймеру раз в storeIntervalMinutes, если что-то
/// изменилось.
///
/// IndicateAchievementProgress показывает всплывающий прогресс на шагах AchievementData.progressSteps,
/// но сам прогресс не сохраняет — прогресс хранит статистика (SetStat), поэтому достижение с прогрессом
/// всегда привязано к статистике.
///
/// Статический Instance — осознанное исключение из правила «без синглтонов» (как у SettingsService):
/// сервис живёт весь процесс в PersistentServices, а обращаются к нему адаптер игровой сцены, меню
/// паузы, тосты и окно достижений.
/// </summary>
[DefaultExecutionOrder(-1700)]
[DisallowMultipleComponent]
public class StatsService : MonoBehaviour
{
    [Header("Данные")]
    [Tooltip("Каталог статистик и достижений (04_Data/Achievements/AchievementCatalog).")]
    public AchievementCatalog catalog;

    [Header("Отправка статистики")]
    [Tooltip("Раз в сколько минут (реального времени) отправлять изменившуюся статистику, если не было других поводов.")]
    [Min(1f)] public float storeIntervalMinutes = 5f;

    [Tooltip("Не чаще, чем раз в столько секунд, отправлять статистику по некритичным поводам (пауза).")]
    [Min(0f)] public float minSecondsBetweenStores = 30f;

    /// <summary>Живой экземпляр (null — сервиса статистики в игре нет).</summary>
    public static StatsService Instance { get; private set; }

    /// <summary>Хранилище платформы: Steam или локальный файл.</summary>
    public IPlatformAchievements Backend { get; private set; }

    /// <summary>Показывать ли тосты игры о достижениях (Steam рисует свои уведомления сам).</summary>
    public bool ShowToasts => Backend != null && !Backend.ShowsOwnNotifications;

    /// <summary>Каталог (для окна достижений и отладки).</summary>
    public AchievementCatalog Catalog => catalog;

    /// <summary>Достижение только что получено.</summary>
    public event Action<AchievementData> OnAchievementUnlocked;

    /// <summary>Значение статистики изменилось: (статистика, новое значение).</summary>
    public event Action<StatId, double> OnStatChanged;

    private readonly Dictionary<StatId, double> values = new Dictionary<StatId, double>();
    // Дробные прибавки к целочисленным статистикам (метры, секунды): копятся здесь, в Steam уходят целыми.
    private readonly Dictionary<StatId, double> fractions = new Dictionary<StatId, double>();
    // Достижения, которые платформа отказалась выдать (нет API-имени): не пытаться снова каждую прибавку.
    private readonly HashSet<string> rejectedUnlocks = new HashSet<string>();
    private readonly HashSet<StatId> warnedStats = new HashSet<StatId>();
    private bool dirty;
    private float lastStoreTime;

    // Без перезагрузки домена (Enter Play Mode Options) статика пережила бы прошлый запуск.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // SteamManager инициализируется раньше (DefaultExecutionOrder -2000).
        Backend = SteamManager.Initialized ? (IPlatformAchievements)new SteamAchievementsBackend() : new LocalAchievementsBackend();
        Debug.Log($"[Achievements] Бэкенд достижений: {Backend.Name}.");

        if (catalog == null)
            Debug.LogWarning("[Achievements] Не назначен AchievementCatalog — статистика не считается.", this);
        else
        {
            string problems = catalog.Validate();
            if (!string.IsNullOrEmpty(problems)) Debug.LogWarning("[Achievements] Проблемы каталога: " + problems, catalog);
        }

        LoadValues();
        lastStoreTime = Time.unscaledTime;
    }

    private void Start()
    {
        // Статистика уже выше цели (новое достижение в обновлении игры, прогресс с другого ПК) — выдаём сразу.
        if (catalog == null) return;
        foreach (AchievementData achievement in catalog.achievements)
            if (achievement != null && achievement.condition == AchievementCondition.StatThreshold
                && GetValue(achievement.progressStat) >= achievement.target)
                Unlock(achievement);
    }

    private void Update()
    {
        if (dirty && Time.unscaledTime - lastStoreTime >= storeIntervalMinutes * 60f) StoreNow();
    }

    // OnApplicationQuit приходит раньше любого OnDestroy — SteamManager ещё не завершил SteamAPI.
    private void OnApplicationQuit()
    {
        if (dirty) StoreNow();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        Backend?.Shutdown();
    }

    // ───────────────────────── Статистика ─────────────────────────

    /// <summary>Текущее значение статистики.</summary>
    public double GetValue(StatId id) => values.TryGetValue(id, out double value) ? value : 0d;

    /// <summary>Прибавить целое (убит зомби, прыжок...).</summary>
    public void Increment(StatId id, int amount = 1)
    {
        if (amount > 0) Add(id, amount);
    }

    /// <summary>Прибавить дробное (метры, секунды). У целочисленной статистики дроби копятся до целого.</summary>
    public void Increment(StatId id, float amount)
    {
        if (amount > 0f) Add(id, amount);
    }

    /// <summary>Поднять статистику до значения, если оно больше текущего (максимальный уровень).</summary>
    public void SetMax(StatId id, double value)
    {
        StatDefinition definition = GetDefinition(id);
        if (definition == null) return;

        double previous = GetValue(id);
        if (value > previous) SetValue(definition, previous, value);
    }

    // ───────────────────────── Достижения ─────────────────────────

    public bool IsUnlocked(AchievementData achievement) =>
        achievement != null && Backend != null && Backend.IsUnlocked(achievement.apiName);

    /// <summary>Выдать достижение (для Scripted — вызывает код игры; для остальных — сам сервис).</summary>
    public void Unlock(AchievementData achievement)
    {
        if (achievement == null || Backend == null || string.IsNullOrEmpty(achievement.apiName)) return;
        if (rejectedUnlocks.Contains(achievement.apiName) || Backend.IsUnlocked(achievement.apiName)) return;

        if (!Backend.Unlock(achievement.apiName))
        {
            rejectedUnlocks.Add(achievement.apiName);
            return;
        }

        OnAchievementUnlocked?.Invoke(achievement);
        // Steam показывает уведомление только после StoreStats.
        RequestStore(true);
    }

    /// <summary>Выдать по API-имени (окно отладки).</summary>
    public void Unlock(string apiName)
    {
        AchievementData achievement = catalog != null ? catalog.GetAchievement(apiName) : null;
        if (achievement != null)
        {
            Unlock(achievement);
            return;
        }

        if (Backend != null && Backend.Unlock(apiName)) RequestStore(true);
    }

    /// <summary>Прогресс достижения 0..1 (полученное — 1).</summary>
    public float GetProgress(AchievementData achievement)
    {
        if (achievement == null) return 0f;
        if (IsUnlocked(achievement)) return 1f;
        if (achievement.condition != AchievementCondition.StatThreshold || achievement.target <= 0) return 0f;
        return Mathf.Clamp01((float)(GetValue(achievement.progressStat) / achievement.target));
    }

    // ───────────────────────── Отправка ─────────────────────────

    /// <summary>Отправить изменения. force — повод важный (достижение, выход в меню): без ограничения частоты.</summary>
    public void RequestStore(bool force)
    {
        if (Backend == null || (!dirty && !force)) return;
        if (!force && Time.unscaledTime - lastStoreTime < minSecondsBetweenStores) return;
        StoreNow();
    }

    /// <summary>Сбросить всю статистику и достижения (только для отладки).</summary>
    public void ResetAll()
    {
        if (Backend == null) return;
        Backend.ResetAll(true);
        values.Clear();
        fractions.Clear();
        rejectedUnlocks.Clear();
        dirty = false;
        LoadValues();
        Debug.Log("[Achievements] Статистика и достижения сброшены.");
    }

    private void StoreNow()
    {
        Backend.Store();
        dirty = false;
        lastStoreTime = Time.unscaledTime;
    }

    // ───────────────────────── Внутреннее ─────────────────────────

    private void LoadValues()
    {
        values.Clear();
        if (catalog == null || Backend == null) return;

        foreach (StatDefinition definition in catalog.stats)
        {
            if (definition == null) continue;

            double value = 0d;
            if (definition.valueType == StatValueType.Int)
            {
                if (Backend.GetStat(definition.apiName, out int intValue)) value = intValue;
            }
            else if (Backend.GetStat(definition.apiName, out float floatValue)) value = floatValue;

            values[definition.stat] = value;
        }
    }

    private StatDefinition GetDefinition(StatId id)
    {
        StatDefinition definition = catalog != null ? catalog.GetStat(id) : null;
        if (definition == null && catalog != null && warnedStats.Add(id))
            Debug.LogWarning($"[Achievements] В каталоге нет статистики {id} — прибавки к ней не считаются.", catalog);
        return definition;
    }

    private void Add(StatId id, double amount)
    {
        StatDefinition definition = GetDefinition(id);
        if (definition == null) return;

        double previous = GetValue(id);
        double next = previous + amount;

        if (definition.valueType == StatValueType.Int)
        {
            double pending = (fractions.TryGetValue(id, out double fraction) ? fraction : 0d) + amount;
            double whole = Math.Floor(pending);
            fractions[id] = pending - whole;
            if (whole < 1d) return;
            next = previous + whole;
        }

        SetValue(definition, previous, next);
    }

    private void SetValue(StatDefinition definition, double previous, double next)
    {
        if (definition.maxValue > 0) next = Math.Min(next, definition.maxValue);
        if (next <= previous && definition.incrementOnly) return;

        values[definition.stat] = next;
        if (definition.valueType == StatValueType.Int) Backend.SetStat(definition.apiName, (int)next);
        else Backend.SetStat(definition.apiName, (float)next);
        dirty = true;

        OnStatChanged?.Invoke(definition.stat, next);
        EvaluateAchievements(definition.stat, previous, next);
    }

    private void EvaluateAchievements(StatId stat, double previous, double next)
    {
        if (catalog == null) return;

        foreach (AchievementData achievement in catalog.achievements)
        {
            if (achievement == null || achievement.condition != AchievementCondition.StatThreshold || achievement.progressStat != stat) continue;
            if (rejectedUnlocks.Contains(achievement.apiName) || Backend.IsUnlocked(achievement.apiName)) continue;

            if (next >= achievement.target)
            {
                Unlock(achievement);
                continue;
            }

            if (achievement.progressSteps == null) continue;
            foreach (float step in achievement.progressSteps)
            {
                if (step <= 0f || step >= 1f) continue;
                double threshold = step * achievement.target;
                if (previous >= threshold || next < threshold) continue;

                Backend.IndicateProgress(achievement.apiName, (uint)next, (uint)achievement.target);
                break;
            }
        }
    }
}
