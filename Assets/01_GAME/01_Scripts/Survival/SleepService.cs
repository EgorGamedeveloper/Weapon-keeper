using System;
using System.Collections;
using UnityEngine;

/// <summary>Итоги прожитого дня — для экрана сна.</summary>
public class DayReport
{
    /// <summary>Какой день закончился.</summary>
    public int day;

    /// <summary>Игрок не лёг сам, а отключился от усталости.</summary>
    public bool passedOut;

    /// <summary>Сколько игровых часов проспал.</summary>
    public float hoursSlept;

    /// <summary>Выспался (проспал полную норму) — утром эффект «Выспался».</summary>
    public bool wellRested;

    /// <summary>Сколько заработано за день ($, только поступления).</summary>
    public int moneyEarned;

    /// <summary>Сколько опыта получено за день.</summary>
    public int xpGained;

    /// <summary>Сколько предметов расставлено по полкам; −1 — статистика недоступна (игра без Bootstrap).</summary>
    public int itemsShelved = -1;

    /// <summary>Сколько врагов убито; −1 — статистика недоступна.</summary>
    public int enemiesKilled = -1;

    /// <summary>Усталость перед сном (с откатом ночных стимуляторов) и утром.</summary>
    public float fatigueBefore;
    public float fatigueAfter;
}

/// <summary>
/// Сон и отключка — конец игрового дня.
///
/// Лечь (SleepSpot, клик по матрасу) можно с SleepSettings.sleepFromHour до утра, а раньше — только если
/// игрок уже вымотан. Нельзя, пока хоть один враг гонится за игроком или атакует: сначала отбиться.
/// Кто не лёг до passOutHour, отключается где стоит и приходит в себя у матраса в passOutWakeHour —
/// с половинным отдыхом и «Разбитостью».
///
/// Что делает сон: снимает стимуляторы (их откат добавляется сразу — ночной кофе «отсыпается»), снимает
/// усталость пропорционально проспанным часам (hoursForFullRest — вся), тратит сытость за ночь, проматывает
/// часы до утра (доставки за ночь доезжают сами — сроки в игровом времени), вешает утренний эффект,
/// показывает итоги дня и делает автосохранение. На время экрана сна мир стоит (timeScale = 0).
/// </summary>
public class SleepService : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — настройки ниже перекрываются из GameConfig.sleep при старте.")]
    public GameConfig config;

    [Tooltip("Когда можно лечь, сколько сна нужно, отключка; используются, если конфиг не задан.")]
    public SleepSettings settings = new SleepSettings();

    [Header("Ссылки")]
    [Tooltip("Игровые часы.")]
    public GameClock clock;

    [Tooltip("Выносливость: сон снимает усталость и заполняет бар.")]
    public PlayerStamina stamina;

    [Tooltip("Временные эффекты: сон их снимает и вешает утренние.")]
    public PlayerStatusEffects effects;

    [Tooltip("Еда: сытость за ночь убывает, отметки «раз в сутки» сбрасываются.")]
    public PlayerConsumption consumption;

    [Tooltip("Навык «Солдатский сон»: сколько часов нужно для полного отдыха.")]
    public PlayerSkills skills;

    [Tooltip("Здоровье: мёртвый не спит и не отключается.")]
    public PlayerHealth health;

    [Tooltip("Блокировка ввода на время экрана сна.")]
    public GameplayInputBlocker inputBlocker;

    [Tooltip("Экран сна: затемнение, часы, итоги дня.")]
    public SleepOverlayUI overlay;

    [Tooltip("Сейв: утро после сна — точка автосохранения.")]
    public SaveLoadService saveLoad;

    [Tooltip("Корень игрока (с CharacterController) — после отключки переносится к матрасу.")]
    public Transform player;

    [Tooltip("Матрас, у которого игрок приходит в себя после отключки.")]
    public SleepSpot homeSpot;

    [Header("Утренние эффекты")]
    [Tooltip("Проспал полную норму — «Выспался».")]
    public StatusEffectData wellRestedEffect;

    [Tooltip("Отключился от усталости — «Разбитость».")]
    public StatusEffectData groggyEffect;

    [Header("Итоги дня")]
    [Tooltip("Кошелёк: сколько заработано за день.")]
    public PlayerWallet wallet;

    [Tooltip("Прогрессия: сколько опыта получено за день.")]
    public PlayerProgression progression;

    [Header("Звуки")]
    [Tooltip("Игрок лёг спать.")]
    public SoundCue lieDownSound;

    [Tooltip("Игрок отключился от усталости.")]
    public SoundCue passOutSound;

    [Tooltip("Утро.")]
    public SoundCue wakeSound;

    [Header("Враги")]
    [Tooltip("Как часто перепроверять, не гонится ли кто-то за игроком, с (поиск врагов не бесплатный).")]
    [Min(0.05f)] public float enemyCheckInterval = 0.25f;

    /// <summary>Идёт экран сна.</summary>
    public bool IsSleeping { get; private set; }

    /// <summary>Игрок лёг спать или отключился.</summary>
    public event Action OnSleepStarted;

    /// <summary>Наступило утро: (итоги прошедшего дня).</summary>
    public event Action<DayReport> OnWokeUp;

    private int moneyToday;
    private int xpToday;
    private int lastBalance;
    private bool statsSnapshotTaken;
    private double itemsShelvedAtMorning;
    private double enemiesKilledAtMorning;
    private float nextEnemyCheck;
    private bool underAttackCached;
    private float timeScaleBeforeSleep = 1f;

    private void Awake()
    {
        if (config != null) settings = config.sleep;
    }

    private void Start()
    {
        if (wallet != null) lastBalance = wallet.Balance;
        BeginDay();
    }

    private void OnEnable()
    {
        if (wallet != null) wallet.OnBalanceChanged += HandleBalanceChanged;
        if (progression != null) progression.OnXPGained += HandleXPGained;
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
        if (progression != null) progression.OnXPGained -= HandleXPGained;

        // Сцену выгрузили посреди экрана сна — время и ввод не должны остаться остановленными.
        if (!IsSleeping) return;
        IsSleeping = false;
        Time.timeScale = timeScaleBeforeSleep > 0f ? timeScaleBeforeSleep : 1f;
        if (inputBlocker != null) inputBlocker.Release(this);
    }

    private void Update()
    {
        if (IsSleeping || clock == null || stamina == null) return;
        if (health != null && health.IsDead) return;
        if (!GameClock.IsBetween(clock.Hour, settings.passOutHour, settings.wakeHour)) return;

        // Открыто окно или пауза — отключится, когда их закроют.
        if (Time.timeScale <= 0f || (inputBlocker != null && inputBlocker.IsBlocked)) return;
        StartCoroutine(SleepRoutine(homeSpot, true));
    }

    // ───────────────────────── Правила ─────────────────────────

    /// <summary>Можно ли лечь прямо сейчас; reason — почему нельзя (для подсказки у матраса).</summary>
    public bool CanSleep(out string reason)
    {
        reason = "";
        if (IsSleeping || clock == null || stamina == null) return false;
        if (health != null && health.IsDead) return false;

        if (IsUnderAttack())
        {
            reason = Loc.Get("hud.sleep.enemies");
            return false;
        }

        bool sleepTime = GameClock.IsBetween(clock.Hour, settings.sleepFromHour, settings.wakeHour);
        if (!sleepTime && !stamina.IsExhausted)
        {
            reason = Loc.Get("hud.sleep.too_early", GameClock.FormatTime(settings.sleepFromHour));
            return false;
        }

        return true;
    }

    /// <summary>Подсказка у матраса: что будет по клику или почему нельзя.</summary>
    public string GetSleepHint()
    {
        return CanSleep(out string reason) ? Loc.Get("hud.sleep.hint", GameClock.FormatTime(settings.wakeHour)) : reason;
    }

    /// <summary>Лечь спать у матраса spot. Ничего не делает, если сейчас нельзя (CanSleep).</summary>
    public void Sleep(SleepSpot spot)
    {
        if (!CanSleep(out _)) return;
        StartCoroutine(SleepRoutine(spot, false));
    }

    /// <summary>Сколько часов сна нужно для полного отдыха (с навыком «Солдатский сон» — меньше).</summary>
    public float HoursForFullRest => Mathf.Max(0.5f, settings.hoursForFullRest * (skills != null ? skills.GetValue(SkillStat.SleepHoursNeeded, 1f) : 1f));

    // ───────────────────────── Сон ─────────────────────────

    private IEnumerator SleepRoutine(SleepSpot spot, bool passOut)
    {
        IsSleeping = true;
        OnSleepStarted?.Invoke();
        if (inputBlocker != null) inputBlocker.Acquire(this);

        // Мир замирает на время экрана сна: враги не подойдут, а пропуск времени сделает SkipTo.
        timeScaleBeforeSleep = Time.timeScale;
        Time.timeScale = 0f;

        SoundPlayer.Play2D(passOut ? passOutSound : lieDownSound);
        if (overlay != null) yield return overlay.FadeOut(settings.fadeDuration, passOut);

        double from = clock.TotalHours;
        double to = clock.NextOccurrence(passOut ? settings.passOutWakeHour : settings.wakeHour);
        float hoursSlept = (float)(to - from);
        float hoursNeeded = HoursForFullRest;
        float efficiency = passOut ? settings.passOutRestEfficiency : 1f;

        DayReport report = BuildReport(passOut, hoursSlept);

        // Сначала откат ночных стимуляторов, потом отдых — кофе «отсыпается» вместе с остальной усталостью.
        if (effects != null) effects.ClearForSleep();
        report.fatigueBefore = stamina.Fatigue;
        stamina.RelieveFatigue(hoursSlept / hoursNeeded * stamina.Settings.maxFatigue * efficiency);
        report.fatigueAfter = stamina.Fatigue;

        if (consumption != null)
        {
            consumption.PassHours(hoursSlept);
            consumption.ResetDaily();
        }

        if (overlay != null) yield return overlay.SpinClock(from, to, settings.clockSpinDuration);
        clock.SkipTo(to);

        // Утренние эффекты — до обеда.
        report.wellRested = !passOut && hoursSlept >= hoursNeeded - 0.01f;
        float morningHours = Mathf.Max(0.5f, settings.morningEffectsUntilHour - GameClock.HourOf(to));
        if (effects != null)
        {
            if (report.wellRested && wellRestedEffect != null) effects.Apply(wellRestedEffect, morningHours);
            if (passOut && groggyEffect != null) effects.Apply(groggyEffect, morningHours);
        }

        stamina.RefillBar();
        if (passOut) MovePlayerTo(spot != null ? spot : homeSpot);

        BeginDay();
        if (saveLoad != null) saveLoad.SaveNow();

        if (overlay != null) yield return overlay.ShowSummary(report);

        Time.timeScale = timeScaleBeforeSleep > 0f ? timeScaleBeforeSleep : 1f;
        SoundPlayer.Play2D(wakeSound);
        if (overlay != null) yield return overlay.FadeIn(settings.fadeDuration);

        if (inputBlocker != null) inputBlocker.Release(this);
        IsSleeping = false;
        OnWokeUp?.Invoke(report);
    }

    private DayReport BuildReport(bool passOut, float hoursSlept)
    {
        var report = new DayReport
        {
            day = clock.Day,
            passedOut = passOut,
            hoursSlept = hoursSlept,
            moneyEarned = moneyToday,
            xpGained = xpToday,
        };

        StatsService stats = StatsService.Instance;
        if (stats != null && statsSnapshotTaken)
        {
            report.itemsShelved = Mathf.Max(0, (int)(stats.GetValue(StatId.ItemsShelved) - itemsShelvedAtMorning));
            report.enemiesKilled = Mathf.Max(0, (int)(stats.GetValue(StatId.EnemiesKilled) - enemiesKilledAtMorning));
        }
        return report;
    }

    /// <summary>Начало дня: счётчики итогов с нуля.</summary>
    private void BeginDay()
    {
        moneyToday = 0;
        xpToday = 0;

        StatsService stats = StatsService.Instance;
        statsSnapshotTaken = stats != null;
        if (!statsSnapshotTaken) return;
        itemsShelvedAtMorning = stats.GetValue(StatId.ItemsShelved);
        enemiesKilledAtMorning = stats.GetValue(StatId.EnemiesKilled);
    }

    private void MovePlayerTo(SleepSpot spot)
    {
        if (spot == null || player == null) return;

        // CharacterController не даёт переставить себя, пока включён.
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        Vector3 target = spot.WakePosition;
        if (controller != null)
        {
            // Точка у матраса — на полу, а корень игрока — в центре капсулы: без подъёма на нижнюю половину
            // капсулы игрок оказался бы наполовину в полу и провалился сквозь него.
            float bottom = (controller.center.y - controller.height * 0.5f) * player.lossyScale.y;
            target.y += controller.skinWidth - bottom + 0.05f;
            controller.enabled = false;
        }
        player.position = target;
        Physics.SyncTransforms();
        if (controller != null) controller.enabled = wasEnabled;
    }

    private bool IsUnderAttack()
    {
        if (Time.unscaledTime < nextEnemyCheck) return underAttackCached;
        nextEnemyCheck = Time.unscaledTime + enemyCheckInterval;

        underAttackCached = false;
        foreach (var enemy in FindObjectsByType<Enemy>())
        {
            if (enemy.State != Enemy.EnemyState.Chasing && enemy.State != Enemy.EnemyState.Attacking) continue;
            underAttackCached = true;
            break;
        }
        return underAttackCached;
    }

    private void HandleBalanceChanged(int balance)
    {
        int delta = balance - lastBalance;
        lastBalance = balance;
        if (delta > 0) moneyToday += delta;
    }

    private void HandleXPGained(int amount)
    {
        if (amount > 0) xpToday += amount;
    }
}
