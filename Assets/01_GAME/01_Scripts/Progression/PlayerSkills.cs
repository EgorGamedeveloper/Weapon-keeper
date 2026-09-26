using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Состояние навыка для игрока — по нему окно прокачки красит узел.</summary>
public enum SkillState
{
    /// <summary>Уже куплен (или выдан на старте).</summary>
    Owned,
    /// <summary>Требования выполнены, очков хватает — можно покупать.</summary>
    Available,
    /// <summary>Требования выполнены, но не хватает очков навыков.</summary>
    NotEnoughPoints,
    /// <summary>Не хватает уровня или не куплены предыдущие навыки.</summary>
    Locked,
}

/// <summary>
/// Купленные навыки игрока. Тратит очки PlayerProgression, проверяет уровень и предыдущие навыки,
/// отдаёт итоговые значения параметров (GetValue). Синглтона нет — потребители (инвентарь,
/// контроллер, мост оружия, окно прокачки) держат ссылку в инспекторе и перечитывают значения
/// по OnSkillsChanged.
///
/// Производные значения нигде не хранятся: каждый раз считаются заново от базового значения
/// потребителя и набора купленных навыков — поэтому сейву достаточно списка id.
/// </summary>
public class PlayerSkills : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Источник уровня и очков навыков.")]
    public PlayerProgression progression;

    [Tooltip("Каталог всех навыков: из него берутся стартовые навыки и id при загрузке сейва.")]
    public SkillCatalog catalog;

    /// <summary>Набор купленных навыков изменился (покупка). Восстановление из сейва события не поднимает.</summary>
    public event Action OnSkillsChanged;

    private readonly HashSet<SkillData> owned = new HashSet<SkillData>();

    /// <summary>Все купленные навыки, включая выданные на старте.</summary>
    public IReadOnlyCollection<SkillData> Owned => owned;

    private int PlayerLevel => progression != null ? progression.CurrentLevel : 1;
    private int AvailablePoints => progression != null ? progression.UnlockPoints : 0;

    // Awake, а не Start: потребители читают GetValue в своём Awake/Start, стартовые навыки должны
    // быть уже на месте. SaveLoadService (-1000) успевает раньше — добавление в HashSet
    // идемпотентно, поэтому порядок «сначала сейв, потом стартовые» ничего не задваивает.
    private void Awake()
    {
        if (catalog == null) return;

        foreach (var skill in catalog.skills)
            if (skill != null && skill.grantedAtStart) owned.Add(skill);
    }

    public bool IsOwned(SkillData skill) => skill != null && owned.Contains(skill);

    public SkillState GetState(SkillData skill)
    {
        if (skill == null) return SkillState.Locked;
        if (owned.Contains(skill)) return SkillState.Owned;
        if (PlayerLevel < skill.requiredLevel) return SkillState.Locked;

        foreach (var prerequisite in skill.prerequisites)
            if (prerequisite != null && !owned.Contains(prerequisite)) return SkillState.Locked;

        return AvailablePoints < skill.cost ? SkillState.NotEnoughPoints : SkillState.Available;
    }

    /// <summary>Купить навык. false — навык недоступен (см. GetState), ничего не списано.</summary>
    public bool TryPurchase(SkillData skill)
    {
        if (GetState(skill) != SkillState.Available) return false;
        if (progression == null || !progression.TrySpendUnlockPoints(skill.cost)) return false;

        owned.Add(skill);
        OnSkillsChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Итоговое значение параметра: (база + сумма всех Add) × произведение всех Multiply
    /// по купленным навыкам. Без навыков на этот параметр возвращает базу без изменений.
    /// </summary>
    public float GetValue(SkillStat stat, float baseValue)
    {
        float add = 0f;
        float multiplier = 1f;

        foreach (var skill in owned)
        {
            foreach (var modifier in skill.modifiers)
            {
                if (modifier == null || modifier.stat != stat) continue;
                if (modifier.op == SkillModifierOp.Add) add += modifier.value;
                else multiplier *= modifier.value;
            }
        }

        return (baseValue + add) * multiplier;
    }

    /// <summary>Найти навык по id из сейва. null — каталога нет или id неизвестен.</summary>
    public SkillData FindBySkillId(string skillId) => catalog != null ? catalog.GetById(skillId) : null;

    /// <summary>
    /// Восстановление из сейва: добавляет навыки без списания очков и без OnSkillsChanged — очки
    /// уже сохранены за вычетом покупок (PlayerProgression.RestoreState), а потребители при старте
    /// сами читают GetValue.
    /// </summary>
    public void RestoreOwned(IEnumerable<string> skillIds)
    {
        foreach (var id in skillIds)
        {
            SkillData skill = FindBySkillId(id);
            if (skill != null) owned.Add(skill);
        }
    }
}
