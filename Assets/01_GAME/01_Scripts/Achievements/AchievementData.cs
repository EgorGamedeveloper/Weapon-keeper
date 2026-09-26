using UnityEngine;

/// <summary>Как выдаётся достижение.</summary>
public enum AchievementCondition
{
    /// <summary>Статистика progressStat достигла target.</summary>
    StatThreshold = 0,

    /// <summary>Выдаёт код по ссылке на ассет (например, «отмыть все пятна» — StatsEventAdapter).</summary>
    Scripted = 1
}

/// <summary>
/// Достижение: API-имя в Steamworks (App Admin → Achievements), ключи названия и описания, иконки и условие.
/// Список всех достижений — AchievementCatalog.
/// </summary>
[CreateAssetMenu(fileName = "Achievement", menuName = "Game/Achievements/Achievement", order = 31)]
public class AchievementData : ScriptableObject
{
    [Header("Steam")]
    [Tooltip("API-имя достижения в Steamworks (ACH_FIRST_KILL...). После публикации менять нельзя.")]
    public string apiName;

    [Header("Текст (ключи strings.csv)")]
    [Tooltip("Ключ названия.")]
    public string nameKey;

    [Tooltip("Ключ описания.")]
    public string descriptionKey;

    [Header("Иконки")]
    [Tooltip("Иконка полученного достижения. В Steam иконки загружаются отдельно в партнёрке (256×256 JPG).")]
    public Sprite icon;

    [Tooltip("Иконка неполученного (обычно серая). Пусто — затемнённая основная.")]
    public Sprite lockedIcon;

    [Tooltip("Скрытое: до получения название и описание не показываются.")]
    public bool hidden;

    [Header("Условие")]
    [Tooltip("StatThreshold — выдаётся, когда статистика progressStat достигает target. Scripted — выдаёт код " +
             "по ссылке на этот ассет (например, StatsEventAdapter — «все пятна отмыты»).")]
    public AchievementCondition condition = AchievementCondition.StatThreshold;

    [Tooltip("Статистика прогресса.")]
    public StatId progressStat;

    [Tooltip("Цель — значение статистики, при котором достижение выдаётся.")]
    [Min(1)] public int target = 1;

    [Tooltip("Доли цели (0..1), на которых Steam показывает всплывающий прогресс. Пусто — без промежуточных уведомлений.")]
    public float[] progressSteps = { 0.25f, 0.5f, 0.75f };

    [Header("Порядок")]
    [Tooltip("Порядок в окне достижений (меньше — выше).")]
    public int order;

    /// <summary>Есть ли у достижения шкала прогресса (несколько шагов до цели).</summary>
    public bool HasProgress => condition == AchievementCondition.StatThreshold && target > 1;
}
