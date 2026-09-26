using UnityEngine;

/// <summary>
/// Параметр игрока, который меняет навык. Enum сериализуется числом — новые значения добавлять
/// только в конец, существующие не переставлять и не удалять (иначе поедут ассеты навыков).
/// </summary>
public enum SkillStat
{
    /// <summary>Вместимость инвентаря уборки (InventorySystem.maxItemCount).</summary>
    TidyUpCapacity,
    /// <summary>Дальность подбора предмета (PlayerItemInteraction.pickupRange).</summary>
    PickupRange,
    /// <summary>Скорость ходьбы (PlayerCharacterController.walkSpeed).</summary>
    WalkSpeed,
    /// <summary>Скорость бега (PlayerCharacterController.sprintSpeed).</summary>
    SprintSpeed,
    /// <summary>Высота прыжка (PlayerCharacterController.jumpHeight).</summary>
    JumpHeight,
    /// <summary>Максимум здоровья игрока.</summary>
    MaxHealth,
    /// <summary>Множитель получаемого опыта (база — 1).</summary>
    XPGain,
    /// <summary>Время перезарядки оружия (Weapon.reloadTime).</summary>
    ReloadTime,
    /// <summary>Отдача и разброс оружия (множитель; база — 1).</summary>
    Recoil,
    /// <summary>Ёмкость магазина (Weapon.ammoCapacity).</summary>
    AmmoCapacity,
    /// <summary>Множитель цены лутбоксов в терминале (база — 1).</summary>
    OrderPrice,
    /// <summary>Множитель времени доставки лутбоксов (база — 1).</summary>
    DeliveryTime,
    /// <summary>Множитель денежной награды за заказы на отправку (база — 1).</summary>
    ShippingReward,
}

/// <summary>Как модификатор применяется к базовому значению.</summary>
public enum SkillModifierOp
{
    /// <summary>Прибавить value (например, +1 к вместимости).</summary>
    Add,
    /// <summary>Умножить на value (0.85 — минус 15%).</summary>
    Multiply,
}

/// <summary>Один эффект навыка: какой параметр, как и на сколько меняется.</summary>
[System.Serializable]
public class SkillModifier
{
    [Tooltip("Какой параметр игрока меняется.")]
    public SkillStat stat;

    [Tooltip("Add — прибавка к базе; Multiply — множитель (0.85 = −15%, 1.25 = +25%).")]
    public SkillModifierOp op = SkillModifierOp.Add;

    [Tooltip("Величина изменения.")]
    public float value;
}

/// <summary>
/// Один навык дерева прокачки. Создаётся через Assets > Create > Progression > Skill Data,
/// ассеты живут в 04_Data/Skills/. Узел в окне прокачки рисуется по branch/row/column, связи —
/// по prerequisites, так что новый навык не требует правки сцены или кода окна.
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "Progression/Skill Data", order = 31)]
public class SkillData : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Стабильный id для сейва. Заполняется автоматически из имени ассета. " +
             "После выхода игры менять НЕЛЬЗЯ — сейвы игроков потеряют купленный навык.")]
    public string skillId;

    [Header("Отображение")]
    public string title = "Новый навык";

    [TextArea(2, 4)]
    public string description = "Описание навыка";

    /// <summary>Название на языке игры (strings.csv, ключ skill.&lt;skillId&gt;.title; нет строки — title).</summary>
    public string DisplayTitle => Loc.GetOr(Loc.DataKey("skill", skillId, "title"), title);

    /// <summary>Описание на языке игры (ключ skill.&lt;skillId&gt;.desc; нет строки — description).</summary>
    public string DisplayDescription => Loc.GetOr(Loc.DataKey("skill", skillId, "desc"), description);

    [Tooltip("Иконка узла в дереве.")]
    public Sprite icon;

    [Header("Место в дереве")]
    [Tooltip("Ветка (вкладка окна прокачки).")]
    public SkillBranch branch;

    [Tooltip("Ряд узла сверху вниз, начиная с 0.")]
    [Min(0)] public int row;

    [Tooltip("Колонка узла слева направо, начиная с 0.")]
    [Min(0)] public int column;

    [Header("Покупка")]
    [Tooltip("Стоимость в очках навыков (за уровень даётся PlayerProgression.unlockPointsPerLevel).")]
    [Min(0)] public int cost = 3;

    [Tooltip("Минимальный уровень игрока для покупки.")]
    [Min(1)] public int requiredLevel = 1;

    [Tooltip("Навык можно купить только после ВСЕХ перечисленных навыков.")]
    public SkillData[] prerequisites = System.Array.Empty<SkillData>();

    [Tooltip("Выдаётся бесплатно в начале игры (например, лицензия на пистолеты).")]
    public bool grantedAtStart;

    [Header("Эффекты")]
    [Tooltip("Какие параметры игрока меняет навык.")]
    public SkillModifier[] modifiers = System.Array.Empty<SkillModifier>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Навык, требующий самого себя, никогда не станет доступен — предупреждаем сразу, а не молча.
        if (prerequisites != null && System.Array.IndexOf(prerequisites, this) >= 0)
            Debug.LogWarning($"[SkillData] Навык '{name}' указан среди собственных prerequisites — его нельзя будет купить.", this);

        if (!string.IsNullOrEmpty(skillId)) return;

        // Автозаполнение один раз, из имени ассета: Skill_QuickHands -> quickhands. Дальше id живёт
        // отдельно от имени — как ItemData.itemId и QuestData.questId.
        string source = name.StartsWith("Skill_") ? name.Substring("Skill_".Length) : name;
        var builder = new System.Text.StringBuilder(source.Length);
        foreach (char c in source.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        skillId = builder.ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
