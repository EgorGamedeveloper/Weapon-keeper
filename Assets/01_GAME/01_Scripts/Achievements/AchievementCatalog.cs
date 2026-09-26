using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Все статистики и достижения игры (04_Data/Achievements/AchievementCatalog). По нему StatsService
/// переводит StatId в API-имена Steam и проверяет цели достижений. Создаётся и пополняется меню
/// Tools/Weapon Keeper/Achievements/Create Achievement Assets.
/// </summary>
[CreateAssetMenu(fileName = "AchievementCatalog", menuName = "Game/Achievements/Achievement Catalog", order = 32)]
public class AchievementCatalog : ScriptableObject
{
    // В Steamworks API-имена — латиница, цифры и подчёркивание.
    private static readonly Regex ApiNamePattern = new Regex("^[A-Za-z0-9_]+$");

    [Header("Статистика")]
    [Tooltip("Описания всех статистик (StatDefinition). На каждый StatId — не больше одной.")]
    public List<StatDefinition> stats = new List<StatDefinition>();

    [Header("Достижения")]
    [Tooltip("Все достижения игры.")]
    public List<AchievementData> achievements = new List<AchievementData>();

    /// <summary>Описание статистики или null.</summary>
    public StatDefinition GetStat(StatId id)
    {
        foreach (StatDefinition definition in stats)
            if (definition != null && definition.stat == id) return definition;
        return null;
    }

    /// <summary>Достижение по API-имени или null.</summary>
    public AchievementData GetAchievement(string apiName)
    {
        if (string.IsNullOrEmpty(apiName)) return null;
        foreach (AchievementData achievement in achievements)
            if (achievement != null && achievement.apiName == apiName) return achievement;
        return null;
    }

    /// <summary>Проблемы каталога одной строкой (пусто — всё в порядке).</summary>
    public string Validate()
    {
        var problems = new StringBuilder();
        var statIds = new HashSet<StatId>();
        var apiNames = new HashSet<string>();

        foreach (StatDefinition definition in stats)
        {
            if (definition == null) { problems.Append("пустая ссылка в stats; "); continue; }
            if (!statIds.Add(definition.stat)) problems.Append($"статистика {definition.stat} описана дважды; ");
            CheckApiName(definition.apiName, definition.name, apiNames, problems);
        }

        foreach (AchievementData achievement in achievements)
        {
            if (achievement == null) { problems.Append("пустая ссылка в achievements; "); continue; }
            CheckApiName(achievement.apiName, achievement.name, apiNames, problems);
            if (achievement.condition == AchievementCondition.StatThreshold && !statIds.Contains(achievement.progressStat))
                problems.Append($"{achievement.apiName}: нет StatDefinition для {achievement.progressStat}; ");
        }

        return problems.ToString();
    }

    private static void CheckApiName(string apiName, string assetName, HashSet<string> seen, StringBuilder problems)
    {
        if (string.IsNullOrEmpty(apiName)) problems.Append($"{assetName}: пустое API-имя; ");
        else if (!ApiNamePattern.IsMatch(apiName)) problems.Append($"{assetName}: API-имя «{apiName}» — только латиница, цифры, _; ");
        else if (!seen.Add(apiName)) problems.Append($"API-имя {apiName} повторяется; ");
    }
}
