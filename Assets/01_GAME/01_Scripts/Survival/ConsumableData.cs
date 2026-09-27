using UnityEngine;

/// <summary>Как предмет употребляется. Влияет только на подсказку: «F — съесть / выпить / уколоть».</summary>
public enum ConsumableKind
{
    /// <summary>Еда — съесть.</summary>
    Food,
    /// <summary>Напиток — выпить.</summary>
    Drink,
    /// <summary>Укол — уколоть.</summary>
    Injection,
}

/// <summary>
/// Что делают еда или стимулятор, когда игрок их употребляет (клавиша F, PlayerConsumption). Подключается к
/// предмету полем ItemData.consumable: сам предмет остаётся обычным (подбирается, носится в инвентаре уборки,
/// ставится на полку «Провизия»), а этот ассет — его «эффект употребления». Создаётся через
/// Assets > Create > Survival > Consumable, ассеты живут в 04_Data/Survival/.
///
/// Роли: еда насовсем (до сна) возвращает немного дневной усталости, её ограничивает сытость; стимулятор
/// временно перекрывает усталость эффектом (StatusEffectData) и потом даёт откат.
/// </summary>
[CreateAssetMenu(fileName = "NewConsumable", menuName = "Survival/Consumable", order = 40)]
public class ConsumableData : ScriptableObject
{
    [Header("Тип")]
    [Tooltip("Еда — «съесть», напиток — «выпить», укол — «уколоть». Влияет только на подсказку.")]
    public ConsumableKind kind = ConsumableKind.Food;

    [Header("Еда")]
    [Tooltip("Сколько дневной усталости возвращает насовсем (до сна): бар становится длиннее.")]
    [Min(0f)] public float fatigueRelief;

    [Tooltip("Сколько выносливости добавляет в бар сразу.")]
    [Min(0f)] public float instantStamina;

    [Tooltip("Сколько места в желудке занимает (сытость 0–100). Не помещается — не съедается. 0 — не занимает.")]
    [Min(0f)] public float satiety;

    [Header("Стимулятор")]
    [Tooltip("Временный эффект (кофе, энергетик, шприц). Длительность, маска и откат — в самом эффекте. Пусто — без эффекта.")]
    public StatusEffectData effect;

    [Tooltip("Не чаще раза в сутки (до сна). Второй раз — отказ («Сердце не выдержит»).")]
    public bool oncePerDay;

    [Header("Употребление")]
    [Tooltip("Сколько секунд предмет подносится ко рту (анимация). Эффект начинается сразу.")]
    [Min(0.05f)] public float useDuration = 0.5f;

    [Tooltip("Звук употребления. Не задан — тихо.")]
    public SoundCue useSound;
}
