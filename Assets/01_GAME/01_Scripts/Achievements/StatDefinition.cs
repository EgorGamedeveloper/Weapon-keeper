using UnityEngine;

/// <summary>Тип статистики в Steamworks.</summary>
public enum StatValueType
{
    Int = 0,
    Float = 1
}

/// <summary>
/// Описание одной статистики: какой StatId она представляет и как заведена в Steamworks (App Admin → Stats):
/// API-имя, тип, «только растёт», ограничения. Ограничения здесь — для документации и защиты от ошибок;
/// в Steam их нужно завести теми же значениями (Docs/Steam/STEAM_SETUP.md).
/// </summary>
[CreateAssetMenu(fileName = "Stat", menuName = "Game/Achievements/Stat Definition", order = 30)]
public class StatDefinition : ScriptableObject
{
    [Header("Статистика")]
    [Tooltip("Какую статистику игры описывает ассет.")]
    public StatId stat;

    [Tooltip("API-имя статистики в Steamworks (например enemies_killed). После публикации менять нельзя.")]
    public string apiName;

    [Tooltip("Тип в Steamworks: INT или FLOAT. Дробные прибавки к INT копятся и уходят целыми.")]
    public StatValueType valueType = StatValueType.Int;

    [Tooltip("Только растёт (Increment Only в Steamworks): Steam отклонит уменьшение значения.")]
    public bool incrementOnly = true;

    [Header("Ограничения (как в Steamworks)")]
    [Tooltip("Минимальное значение.")]
    public int minValue = 0;

    [Tooltip("Максимальное значение. 0 — без ограничения.")]
    [Min(0)] public int maxValue = 0;

    [Tooltip("Max Change: насколько значение может вырасти за одну отправку. 0 — без ограничения.")]
    [Min(0)] public int maxChange = 0;

    [Header("Текст")]
    [Tooltip("Ключ названия в strings.csv (окно отладки, таблица для партнёрки).")]
    public string nameKey;
}
