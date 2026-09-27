using System;
using UnityEngine;

/// <summary>
/// Выносливость игрока — два слоя.
///
/// Бар (Current) тратится на рывки — бег, прыжок, удар кувалдой, рычаг, тряпку — и сам восстанавливается
/// через regenDelay после последней траты. Опустел — «одышка»: рывки недоступны, пока бар не наберёт
/// windedRecoverFraction потолка.
///
/// Усталость (Fatigue) копится за день — от тяжёлой работы и от времени (ночью — «сонливость») — и
/// запирает правую часть бара: Cap = Max − (усталость − маска стимуляторов), но не меньше minCapFraction
/// максимума. Снимает её только сон (SleepService); еда насовсем возвращает немного (RelieveFatigue),
/// стимуляторы перекрывают временно (PlayerStatusEffects) и потом дают откат.
///
/// «Вымотан» (действующая усталость ≥ exhaustedThreshold): работать нельзя — это проверяют
/// PlayerItemInteraction, PlayerToolActions и SledgehammerSwing. Ходить, стрелять, бить кувалдой врагов,
/// есть, пить кофе и спать можно всегда.
///
/// Основной цикл «подобрать → поставить на полку» бар не тратит. Числа — GameConfig.stamina, навыки —
/// SkillStat.MaxStamina / StaminaRegen / StaminaMoveCost / WorkFatigue / NightFatigue. В сейв пишется
/// только усталость (RestoreState, без событий); бар после загрузки полный.
/// </summary>
public class PlayerStamina : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — настройки ниже перекрываются из GameConfig.stamina при старте.")]
    public GameConfig config;

    [Tooltip("Бар, расход, усталость и пороги; используются, если конфиг не задан.")]
    public StaminaSettings settings = new StaminaSettings();

    [Header("Ссылки")]
    [Tooltip("Игровые часы: пассивная усталость днём и «сонливость» ночью.")]
    public GameClock clock;

    [Tooltip("Навыки: максимум, восстановление, расход на бег, усталость от работы, ночная сонливость.")]
    public PlayerSkills skills;

    [Tooltip("Временные эффекты: маска усталости стимуляторов и множители.")]
    public PlayerStatusEffects effects;

    [Tooltip("Здоровье: удар зомби сбивает дыхание.")]
    public PlayerHealth health;

    [Tooltip("Инвентарь уборки: тяжёлый ли предмет в руках (ItemData.isHeavy).")]
    public InventorySystem inventory;

    [Tooltip("Режим инвентаря: предмет уборки в руках только в режиме уборки.")]
    public PlayerInventoryModeController modeController;

    /// <summary>Настройки, по которым работает выносливость (из конфига или свои).</summary>
    public StaminaSettings Settings => settings;

    /// <summary>Максимум бара с навыками.</summary>
    public float Max => skills != null ? skills.GetValue(SkillStat.MaxStamina, settings.maxStamina) : settings.maxStamina;

    /// <summary>Сколько сил в баре сейчас.</summary>
    public float Current { get; private set; }

    /// <summary>Накопленная за день усталость (до сна).</summary>
    public float Fatigue { get; private set; }

    /// <summary>Сколько усталости сейчас перекрыто стимуляторами.</summary>
    public float Mask => effects != null ? effects.FatigueMask(Fatigue) : 0f;

    /// <summary>Действующая усталость — за вычетом стимуляторов. По ней считаются потолок и пороги.</summary>
    public float EffectiveFatigue => Mathf.Max(0f, Fatigue - Mask);

    /// <summary>Потолок бара сейчас.</summary>
    public float Cap => CapFor(EffectiveFatigue);

    /// <summary>Каким был бы потолок без стимуляторов: разница с Cap — часть бара «в долг».</summary>
    public float CapWithoutEffects => CapFor(Fatigue);

    /// <summary>Одышка: бар опустел, рывки недоступны, пока он не восстановится.</summary>
    public bool IsWinded { get; private set; }

    /// <summary>«Вы устали» — только сообщение, ограничений нет.</summary>
    public bool IsTired => EffectiveFatigue >= settings.tiredThreshold;

    /// <summary>«Вымотан»: подбирать, ставить, чинить и работать инструментами нельзя.</summary>
    public bool IsExhausted => EffectiveFatigue >= settings.exhaustedThreshold;

    /// <summary>В руках тяжёлый предмет: бег и прыжок недоступны, ходьба медленнее.</summary>
    public bool IsCarryingHeavy { get; private set; }

    /// <summary>Можно бежать.</summary>
    public bool CanSprint => !IsWinded && !IsCarryingHeavy && Current > 0f;

    /// <summary>Есть силы на рывок: удар кувалдой, рычаг, тряпку.</summary>
    public bool CanExert => !IsWinded && Current > 0f;

    /// <summary>Множитель скорости ходьбы (тяжёлый груз).</summary>
    public float MoveSpeedMultiplier => IsCarryingHeavy ? settings.heavyCarrySpeedMultiplier : 1f;

    /// <summary>Одышка началась (true) или прошла (false).</summary>
    public event Action<bool> OnWindedChanged;

    /// <summary>Игрок стал «уставшим» (true) или перестал (false).</summary>
    public event Action<bool> OnTiredChanged;

    /// <summary>Игрок вымотан (true) или снова может работать (false) — например, выпил кофе.</summary>
    public event Action<bool> OnExhaustedChanged;

    private const float CarryStep = 20f;

    private float lastSpendTime = float.NegativeInfinity;
    private bool wasTired;
    private bool wasExhausted;
    private Vector3 lastCarryPosition;
    private float carriedDistance;

    private float MoveCostMultiplier => Skill(SkillStat.StaminaMoveCost) * EffectsCostMultiplier;
    private float EffectsCostMultiplier => effects != null ? effects.StaminaCostMultiplier : 1f;

    private void Awake()
    {
        if (config != null) settings = config.stamina;
    }

    private void Start()
    {
        // После RestoreState (фаза A сейва) и навыков: бар — полный до текущего потолка.
        Current = Cap;
        wasTired = IsTired;
        wasExhausted = IsExhausted;
        lastCarryPosition = FlatPosition();
    }

    private void OnEnable()
    {
        if (health != null) health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDamaged -= HandleDamaged;
    }

    private void Update()
    {
        UpdateHeavyCarry();
        UpdatePassiveFatigue();
        UpdateRegen();
        UpdateStateEvents();
    }

    // ───────────────────────── Траты бара ─────────────────────────

    /// <summary>Бег: списать расход за кадр. Зовёт PlayerCharacterController, пока игрок реально бежит.</summary>
    public void DrainSprint(float deltaTime)
    {
        Spend(settings.sprintPerSecond * deltaTime * MoveCostMultiplier);
    }

    /// <summary>Прыжок: false — сил нет (одышка, тяжёлый груз), прыжка не будет.</summary>
    public bool TryJump()
    {
        if (IsWinded || IsCarryingHeavy || Current <= 0f) return false;
        Spend(settings.jumpCost * MoveCostMultiplier);
        return true;
    }

    /// <summary>Удар кувалдой: false — одышка, замах не начинается. Каждый удар ещё и утомляет.</summary>
    public bool TrySwing()
    {
        if (!CanExert) return false;
        Spend(settings.swingCost * EffectsCostMultiplier);
        AddWorkFatigue(settings.swingFatigue);
        return true;
    }

    /// <summary>
    /// Лом: часть доски boardFraction (0..1) отжата — бар и усталость по доле. Перед тем как двигать рычаг,
    /// вызывающий проверяет CanExert: при одышке лом стоит.
    /// </summary>
    public void WorkPry(float boardFraction)
    {
        if (boardFraction <= 0f) return;
        Spend(settings.pryCostPerBoard * boardFraction * EffectsCostMultiplier);
        AddWorkFatigue(settings.pryFatiguePerBoard * boardFraction);
    }

    /// <summary>Тряпка или швабра: пройдено meters по пятну и снята доля пятна stainFraction (0..1).</summary>
    public void WorkScrub(float meters, float stainFraction)
    {
        if (meters > 0f) Spend(settings.scrubCostPerMeter * meters * EffectsCostMultiplier);
        if (stainFraction > 0f) AddWorkFatigue(settings.scrubFatiguePerStain * stainFraction);
    }

    /// <summary>Кирпич уложен в кладку.</summary>
    public void WorkBrick() => AddWorkFatigue(settings.brickFatigue);

    /// <summary>Деталь установлена в точку ремонта (запчасти генератора, мотор лифта).</summary>
    public void WorkRepairPart() => AddWorkFatigue(settings.repairPartFatigue);

    // ───────────────────────── Усталость ─────────────────────────

    /// <summary>Усталость от тяжёлой работы: с навыком «Крепкая спина» — меньше.</summary>
    public void AddWorkFatigue(float amount)
    {
        if (amount <= 0f) return;
        AddFatigue(amount * Skill(SkillStat.WorkFatigue));
    }

    /// <summary>Добавить усталость как есть (откат стимулятора, время).</summary>
    public void AddFatigue(float amount)
    {
        if (amount <= 0f) return;
        Fatigue = Mathf.Min(settings.maxFatigue, Fatigue + amount);
    }

    /// <summary>Снять усталость насовсем (еда, сон).</summary>
    public void RelieveFatigue(float amount)
    {
        if (amount <= 0f) return;
        Fatigue = Mathf.Max(0f, Fatigue - amount);
    }

    /// <summary>Добавить в бар (шоколадка). Не выше потолка.</summary>
    public void RestoreStamina(float amount)
    {
        if (amount <= 0f) return;
        Current = Mathf.Min(Cap, Current + amount);
        if (IsWinded && Current >= Cap * settings.windedRecoverFraction) SetWinded(false);
    }

    /// <summary>Бар до потолка, одышка снята (утро).</summary>
    public void RefillBar()
    {
        Current = Cap;
        SetWinded(false);
    }

    /// <summary>Восстановление усталости из сейва — без событий. Бар заполнится в Start.</summary>
    public void RestoreState(float fatigue)
    {
        Fatigue = Mathf.Clamp(fatigue, 0f, settings.maxFatigue);
    }

    // ───────────────────────── Кадр ─────────────────────────

    private void Spend(float amount)
    {
        if (amount <= 0f) return;
        lastSpendTime = Time.time;
        Current = Mathf.Max(0f, Current - amount);
        if (Current <= 0f) SetWinded(true);
    }

    private void UpdatePassiveFatigue()
    {
        if (clock == null || clock.DeltaHours <= 0f) return;

        float perHour = clock.IsNight
            ? settings.nightFatiguePerHour * Skill(SkillStat.NightFatigue)
            : settings.dayFatiguePerHour;
        AddFatigue(perHour * clock.DeltaHours);
    }

    private void UpdateRegen()
    {
        float cap = Cap;
        if (Current > cap) Current = cap;

        float deltaTime = Time.deltaTime;
        if (deltaTime > 0f && Current < cap && Time.time - lastSpendTime >= settings.regenDelay)
        {
            float rate = settings.regenPerSecond * Skill(SkillStat.StaminaRegen) * (effects != null ? effects.RegenMultiplier : 1f);
            Current = Mathf.Min(cap, Current + rate * deltaTime);
        }

        if (IsWinded && Current >= cap * settings.windedRecoverFraction) SetWinded(false);
    }

    private void UpdateHeavyCarry()
    {
        bool heavy = false;
        if (inventory != null
            && (modeController == null || modeController.CurrentMode == PlayerInventoryModeController.InventoryMode.TidyUp))
        {
            ItemData item = inventory.GetActiveItem();
            heavy = item != null && item.isHeavy;
        }
        IsCarryingHeavy = heavy;

        Vector3 position = FlatPosition();
        if (heavy)
        {
            // Большой скачок — это не шаги, а перенос (лифт, пробуждение у матраса): не считаем.
            float moved = Vector3.Distance(position, lastCarryPosition);
            if (moved < 3f) carriedDistance += moved;
            while (carriedDistance >= CarryStep)
            {
                carriedDistance -= CarryStep;
                AddWorkFatigue(settings.heavyCarryFatiguePer20m);
            }
        }
        lastCarryPosition = position;
    }

    private void UpdateStateEvents()
    {
        bool tired = IsTired;
        if (tired != wasTired)
        {
            wasTired = tired;
            OnTiredChanged?.Invoke(tired);
        }

        bool exhausted = IsExhausted;
        if (exhausted != wasExhausted)
        {
            wasExhausted = exhausted;
            OnExhaustedChanged?.Invoke(exhausted);
        }
    }

    private void SetWinded(bool winded)
    {
        if (IsWinded == winded) return;
        IsWinded = winded;
        OnWindedChanged?.Invoke(winded);
    }

    private void HandleDamaged(float amount) => Spend(settings.hitCost);

    private float CapFor(float fatigue)
    {
        float max = Max;
        return Mathf.Max(max * settings.minCapFraction, max - fatigue);
    }

    private Vector3 FlatPosition()
    {
        Vector3 position = transform.position;
        position.y = 0f;
        return position;
    }

    private float Skill(SkillStat stat) => skills != null ? skills.GetValue(stat, 1f) : 1f;
}
