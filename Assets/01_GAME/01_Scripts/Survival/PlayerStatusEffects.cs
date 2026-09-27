using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Действующий эффект: какой, до какого момента (GameClock.TotalHours) и с какой силой.</summary>
public class ActiveStatusEffect
{
    public StatusEffectData data;

    /// <summary>Момент окончания в игровых часах (GameClock.TotalHours).</summary>
    public double expiresAt;

    /// <summary>Полная длительность, игровые часы — для кольца оставшегося времени в HUD.</summary>
    public float durationHours;

    /// <summary>Сила 0..1: каждый следующий стимулятор за день слабее (толерантность).</summary>
    public float strength = 1f;

    /// <summary>Сколько игровых часов осталось.</summary>
    public float RemainingHours(double now) => Mathf.Max(0f, (float)(expiresAt - now));
}

/// <summary>
/// Временные эффекты игрока: стимуляторы, «Отходняк», «Выспался», «Разбитость». Сроки — в игровых часах
/// (GameClock.TotalHours), поэтому пропуск ночи сном их сам «проматывает». Отсюда PlayerStamina берёт
/// маску усталости и множители, а когда стимулятор кончается, здесь же применяется его откат.
///
/// Одинаковые эффекты не складываются — повторный обновляет срок (и оставляет большую силу). Разные
/// складываются: маски суммируются, множители перемножаются. Стимуляторы слабеют с каждой дозой за день
/// (StaminaSettings.stimulantTolerancePerDose); сон сбрасывает счётчик.
/// </summary>
public class PlayerStatusEffects : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — толерантность к стимуляторам берётся из GameConfig.stamina.")]
    public GameConfig config;

    [Header("Ссылки")]
    [Tooltip("Игровые часы: по ним идут сроки эффектов.")]
    public GameClock clock;

    [Tooltip("Выносливость: сюда добавляется откат стимулятора.")]
    public PlayerStamina stamina;

    [Tooltip("Навыки «Кофеман»: длительность и откат стимуляторов.")]
    public PlayerSkills skills;

    [Header("Каталог")]
    [Tooltip("Все эффекты, которые могут оказаться на игроке: по ним сейв находит эффект по id.")]
    public StatusEffectData[] knownEffects = Array.Empty<StatusEffectData>();

    [Header("Толерантность")]
    [Tooltip("Каждый следующий стимулятор до сна слабее на эту долю.")]
    [Range(0f, 1f)] public float stimulantTolerancePerDose = 0.25f;

    [Tooltip("Слабее этой доли стимулятор не становится.")]
    [Range(0f, 1f)] public float stimulantMinStrength = 0.25f;

    /// <summary>Набор эффектов изменился: наложен, кончился, снят сном. Восстановление из сейва события не поднимает.</summary>
    public event Action OnEffectsChanged;

    /// <summary>Действующие эффекты.</summary>
    public IReadOnlyList<ActiveStatusEffect> Active => active;

    /// <summary>Сколько стимуляторов принято с последнего сна.</summary>
    public int StimulantsToday { get; private set; }

    /// <summary>Сила следующей дозы стимулятора с учётом толерантности.</summary>
    public float NextStimulantStrength => Mathf.Max(stimulantMinStrength, 1f - stimulantTolerancePerDose * StimulantsToday);

    private readonly List<ActiveStatusEffect> active = new List<ActiveStatusEffect>();

    private double Now => clock != null ? clock.TotalHours : 0.0;

    private void Awake()
    {
        if (config == null) return;
        stimulantTolerancePerDose = config.stamina.stimulantTolerancePerDose;
        stimulantMinStrength = config.stamina.stimulantMinStrength;
    }

    private void Update()
    {
        if (clock == null || active.Count == 0) return;

        double now = clock.TotalHours;
        bool changed = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (i >= active.Count || active[i].expiresAt > now) continue;

            ActiveStatusEffect ended = active[i];
            active.RemoveAt(i);
            End(ended, startFollowUp: true);
            changed = true;
        }

        if (changed) OnEffectsChanged?.Invoke();
    }

    /// <summary>
    /// Наложить эффект. hours ≤ 0 — длительность из самого эффекта. Стимулятор получает силу по
    /// толерантности и длительность по навыку «Кофеман», счётчик доз за день растёт.
    /// </summary>
    public void Apply(StatusEffectData data, float hours = 0f)
    {
        if (data == null) return;

        float duration = hours > 0f ? hours : data.durationHours;
        float strength = 1f;
        if (data.isStimulant)
        {
            strength = NextStimulantStrength;
            duration *= Skill(SkillStat.StimulantDuration);
            StimulantsToday++;
        }

        double expiresAt = Now + duration;
        ActiveStatusEffect existing = Find(data);
        if (existing != null)
        {
            // Повторный — обновляет срок, а не складывается.
            existing.expiresAt = Math.Max(existing.expiresAt, expiresAt);
            existing.durationHours = (float)(existing.expiresAt - Now);
            existing.strength = Mathf.Max(existing.strength, strength);
        }
        else
        {
            active.Add(new ActiveStatusEffect { data = data, expiresAt = expiresAt, durationHours = duration, strength = strength });
        }

        OnEffectsChanged?.Invoke();
    }

    /// <summary>Действует ли эффект.</summary>
    public bool Has(StatusEffectData data) => Find(data) != null;

    /// <summary>Сколько усталости сейчас перекрыто эффектами (не больше самой усталости).</summary>
    public float FatigueMask(float fatigue)
    {
        float mask = 0f;
        foreach (var effect in active)
        {
            if (effect.data.MasksAllFatigue) return fatigue;
            mask += effect.data.fatigueMask * effect.strength;
        }
        return Mathf.Clamp(mask, 0f, fatigue);
    }

    /// <summary>Итоговый множитель скорости восстановления бара.</summary>
    public float RegenMultiplier
    {
        get
        {
            float multiplier = 1f;
            foreach (var effect in active) multiplier *= Mathf.Lerp(1f, effect.data.regenMultiplier, effect.strength);
            return multiplier;
        }
    }

    /// <summary>Итоговый множитель расхода бара на бег, прыжки и удары.</summary>
    public float StaminaCostMultiplier
    {
        get
        {
            float multiplier = 1f;
            foreach (var effect in active) multiplier *= Mathf.Lerp(1f, effect.data.staminaCostMultiplier, effect.strength);
            return multiplier;
        }
    }

    /// <summary>
    /// Сон: снимаются все эффекты. Откат стимуляторов добавляется сразу — ночной кофе «отсыпается» вместе
    /// с остальной усталостью, — а следующие эффекты («Отходняк») не начинаются: их проспали.
    /// Счётчик доз за день сбрасывается.
    /// </summary>
    public void ClearForSleep()
    {
        foreach (var effect in active) End(effect, startFollowUp: false);
        active.Clear();
        StimulantsToday = 0;
        OnEffectsChanged?.Invoke();
    }

    /// <summary>Восстановление одного эффекта из сейва — без событий и без отката. Неизвестный id пропускается.</summary>
    public void RestoreEffect(string effectId, double expiresAt, float durationHours, float strength)
    {
        StatusEffectData data = FindById(effectId);
        if (data == null || Find(data) != null) return;
        active.Add(new ActiveStatusEffect
        {
            data = data,
            expiresAt = expiresAt,
            durationHours = Mathf.Max(0.01f, durationHours),
            strength = Mathf.Clamp01(strength),
        });
    }

    /// <summary>Восстановление счётчика доз за день из сейва — без событий.</summary>
    public void RestoreStimulantsToday(int count) => StimulantsToday = Mathf.Max(0, count);

    /// <summary>Найти эффект по id из сейва. null — нет в каталоге knownEffects.</summary>
    public StatusEffectData FindById(string effectId)
    {
        if (string.IsNullOrEmpty(effectId)) return null;
        foreach (var data in knownEffects)
            if (data != null && data.effectId == effectId) return data;
        return null;
    }

    private void End(ActiveStatusEffect effect, bool startFollowUp)
    {
        StatusEffectData data = effect.data;
        if (data.crashFatigue > 0f && stamina != null)
        {
            float crash = data.crashFatigue * effect.strength;
            if (data.isStimulant) crash *= Skill(SkillStat.StimulantCrash);
            stamina.AddFatigue(crash);
        }

        if (startFollowUp && data.followUpEffect != null && data.followUpEffect != data)
            Apply(data.followUpEffect);
    }

    private ActiveStatusEffect Find(StatusEffectData data)
    {
        foreach (var effect in active)
            if (effect.data == data) return effect;
        return null;
    }

    private float Skill(SkillStat stat) => skills != null ? skills.GetValue(stat, 1f) : 1f;
}
