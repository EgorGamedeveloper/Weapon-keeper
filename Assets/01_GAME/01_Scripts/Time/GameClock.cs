using System;
using UnityEngine;

/// <summary>
/// Игровое время. Всё держится на одной величине — TotalHours, сколько игровых часов прошло с полуночи
/// первого дня; час суток и номер дня выводятся из неё. Часы идут от Time.deltaTime, поэтому пауза
/// (timeScale = 0) их останавливает, а окна терминала и навыков — нет: игра на них сознательно не встаёт.
///
/// День считается от утра (TimeSettings.morningHour), а не от полуночи: после полуночи ещё идёт тот же
/// «рабочий день» — до сна.
///
/// Как этим пользоваться:
/// - сроки (доставка приедет, кофе кончится) хранят момент в TotalHours и сравнивают с ним — тогда
///   пропуск ночи сном (SkipTo) двигает их сам;
/// - расход «в час» (усталость, сытость) берёт DeltaHours — во время пропуска он 0, итоги сна считает
///   SleepService;
/// - расписания («новые заказы в 06:00») слушают OnHourChanged: при пропуске он поднимается для каждого
///   пройденного часа, поэтому утро не проскакивает.
///
/// Синглтона нет — ссылка в инспекторе у каждого потребителя, как у PlayerSkills.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameClock : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — настройки ниже перекрываются из GameConfig.time при старте.")]
    public GameConfig config;

    [Tooltip("Длина часа и границы дня; используются, если конфиг не задан.")]
    public TimeSettings settings = new TimeSettings();

    [Header("Отладка")]
    [Tooltip("Клавиша «+1 игровой час» — только в редакторе и отладочной сборке. Час проходит как обычно: " +
             "с усталостью и сытостью за него, поэтому ей удобно проверять баланс.")]
    public KeyCode debugAdvanceHourKey = KeyCode.F7;

    /// <summary>Сколько игровых часов прошло с полуночи первого дня.</summary>
    public double TotalHours { get; private set; }

    /// <summary>Час суток, 0..24 (дробный: 14.5 — 14:30).</summary>
    public float Hour => (float)(TotalHours % 24.0);

    /// <summary>Номер дня с 1. Новый день начинается утром, а не в полночь.</summary>
    public int Day => DayAt(TotalHours);

    /// <summary>Сколько игровых часов прошло в этом кадре. 0 на паузе и в кадре пропуска сном.</summary>
    public float DeltaHours { get; private set; }

    /// <summary>Ночь: от TimeSettings.nightHour до утра — время «сонливости».</summary>
    public bool IsNight => IsBetween(Hour, settings.nightHour, settings.morningHour);

    /// <summary>Вечер или ночь: от TimeSettings.eveningHour до утра.</summary>
    public bool IsEvening => IsBetween(Hour, settings.eveningHour, settings.morningHour);

    /// <summary>Сколько реальных секунд длится игровой час.</summary>
    public float RealSecondsPerHour => settings.realSecondsPerHour;

    /// <summary>Наступил новый целый час: (час суток 0..23). При пропуске сном — для каждого пройденного часа.</summary>
    public event Action<int> OnHourChanged;

    /// <summary>Наступило утро нового дня: (номер дня).</summary>
    public event Action<int> OnDayStarted;

    /// <summary>Наступил вечер (TimeSettings.eveningHour).</summary>
    public event Action OnEveningStarted;

    /// <summary>Наступила ночь (TimeSettings.nightHour).</summary>
    public event Action OnNightStarted;

    /// <summary>Время пропущено сном: (было, стало) в TotalHours. Поднимается до событий пройденных часов.</summary>
    public event Action<double, double> OnTimeSkipped;

    private bool restored;

    private void Awake()
    {
        if (config != null) settings = config.time;

        // SaveLoadService (-1000) успевает раньше и зовёт RestoreState — тогда время уже на месте.
        if (!restored) TotalHours = settings.newGameStartHour;
    }

    private void OnDisable()
    {
        DeltaHours = 0f;
    }

    private void Update()
    {
        if (Debug.isDebugBuild && Input.GetKeyDown(debugAdvanceHourKey) && Time.timeScale > 0f)
        {
            Advance(1.0);
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            DeltaHours = 0f;
            return;
        }

        Advance(deltaTime / Mathf.Max(0.01f, settings.realSecondsPerHour));
    }

    private void Advance(double hours)
    {
        double from = TotalHours;
        TotalHours += hours;
        DeltaHours = (float)hours;
        RaiseCrossedHours(from, TotalHours);
    }

    /// <summary>
    /// Промотать время вперёд до момента target (сон). DeltaHours в этом кадре — 0: итоги сна за
    /// пропущенные часы считает сам SleepService. События часов поднимаются для каждого пройденного часа.
    /// </summary>
    public void SkipTo(double target)
    {
        if (target <= TotalHours) return;

        double from = TotalHours;
        TotalHours = target;
        DeltaHours = 0f;
        OnTimeSkipped?.Invoke(from, target);
        RaiseCrossedHours(from, target);
    }

    /// <summary>Ближайший будущий момент (в TotalHours), когда на часах будет hourOfDay.</summary>
    public double NextOccurrence(float hourOfDay)
    {
        double dayStart = Math.Floor(TotalHours / 24.0) * 24.0;
        double candidate = dayStart + hourOfDay;
        if (candidate <= TotalHours + 0.0001) candidate += 24.0;
        return candidate;
    }

    /// <summary>Номер дня для произвольного момента.</summary>
    public int DayAt(double totalHours) => Math.Max(1, (int)Math.Floor((totalHours - settings.morningHour) / 24.0) + 1);

    /// <summary>Час суток из TotalHours, 0..24.</summary>
    public static float HourOf(double totalHours) => (float)(totalHours % 24.0);

    /// <summary>«14:30» для часа суток; минуты округляются вниз до шага minuteStep.</summary>
    public static string FormatTime(float hourOfDay, int minuteStep = 1)
    {
        int totalMinutes = Mathf.FloorToInt(Mathf.Repeat(hourOfDay, 24f) * 60f + 0.0001f);
        if (minuteStep > 1) totalMinutes -= totalMinutes % minuteStep;
        return (totalMinutes / 60).ToString("00") + ":" + (totalMinutes % 60).ToString("00");
    }

    /// <summary>Попадает ли час суток в отрезок [start, end), который может переходить через полночь.</summary>
    public static bool IsBetween(float hourOfDay, float start, float end)
    {
        return start <= end
            ? hourOfDay >= start && hourOfDay < end
            : hourOfDay >= start || hourOfDay < end;
    }

    /// <summary>Восстановление из сейва — без событий: всё, что зависит от времени, при загрузке само
    /// читает TotalHours.</summary>
    public void RestoreState(double totalHours)
    {
        TotalHours = Math.Max(0.0, totalHours);
        restored = true;
    }

    private void RaiseCrossedHours(double from, double to)
    {
        long first = (long)Math.Floor(from) + 1;
        long last = (long)Math.Floor(to);
        for (long h = first; h <= last; h++)
        {
            int hourOfDay = (int)(h % 24);
            OnHourChanged?.Invoke(hourOfDay);
            if (hourOfDay == settings.morningHour) OnDayStarted?.Invoke(DayAt(h));
            if (hourOfDay == settings.eveningHour) OnEveningStarted?.Invoke();
            if (hourOfDay == settings.nightHour) OnNightStarted?.Invoke();
        }
    }
}
