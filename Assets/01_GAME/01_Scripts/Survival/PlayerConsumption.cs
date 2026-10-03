using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Еда и стимуляторы. Клавиша F (GameConfig.input.useKey) употребляет активный предмет инвентаря уборки,
/// если у его ItemData задан consumable: эффект — сразу, а сам предмет подносится ко рту и исчезает.
///
/// Сытость (0–100) — только ограничитель, штрафов за голод нет: еда, которая не помещается, не съедается
/// («Не лезет»). Сытость убывает со временем (GameClock.DeltaHours) и во сне (SleepService зовёт PassHours).
/// Предметы «не чаще раза в сутки» (шприц) отмечаются до следующего сна.
///
/// Не работает в режиме работы с объектом (тряпка, лом), при открытом окне и на паузе.
/// </summary>
public class PlayerConsumption : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — клавиша и сытость берутся из GameConfig при старте.")]
    public GameConfig config;

    [Header("Ссылки")]
    [Tooltip("Инвентарь уборки: употребляется его активный предмет.")]
    public InventorySystem inventory;

    [Tooltip("Режим инвентаря: употреблять можно только в режиме уборки (предмет в руке).")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Камера игрока: к ней предмет подносится «ко рту».")]
    public Camera playerCamera;

    [Tooltip("Рука: если употребляемый экземпляр был спрятан (в руке виден другой из стопки), он сначала появляется в руке.")]
    public EquippedItemHolder itemHolder;

    [Tooltip("Выносливость: еда возвращает усталость и добавляет в бар.")]
    public PlayerStamina stamina;

    [Tooltip("Временные эффекты стимуляторов.")]
    public PlayerStatusEffects effects;

    [Tooltip("Игровые часы: сытость убывает со временем.")]
    public GameClock clock;

    [Tooltip("Режим работы с объектом: пока он идёт, есть нельзя.")]
    public PlayerToolActions toolActions;

    [Tooltip("Блокировка ввода: при открытом окне есть нельзя.")]
    public GameplayInputBlocker inputBlocker;

    [Tooltip("Сообщения у нижнего края экрана («Не лезет»).")]
    public SurvivalHintUI hintUI;

    [Header("Клавиша")]
    [Tooltip("Употребить предмет из рук. Перекрывается GameConfig.input.useKey, если задан конфиг.")]
    public KeyCode useKey = KeyCode.F;

    [Header("Сытость")]
    [Tooltip("На сколько сытость убывает в игровой час. Перекрывается GameConfig.stamina.satietyDecayPerHour.")]
    [Min(0f)] public float satietyDecayPerHour = 8f;

    [Tooltip("Сытость в начале новой игры. Перекрывается GameConfig.stamina.newGameSatiety.")]
    [Range(0f, 100f)] public float newGameSatiety = 50f;

    [Header("Анимация")]
    [Tooltip("Куда подносится предмет — точка перед камерой (x — вправо, y — вверх, z — вперёд), м.")]
    public Vector3 mouthOffset = new Vector3(0f, -0.12f, 0.2f);

    /// <summary>Наибольшая сытость.</summary>
    public const float MaxSatiety = 100f;

    /// <summary>Сытость 0–100.</summary>
    public float Satiety { get; private set; }

    /// <summary>Предмет употреблён: (его данные).</summary>
    public event Action<ItemData> OnConsumed;

    private readonly HashSet<string> usedToday = new HashSet<string>();
    private bool restored;
    private float busyUntil;

    /// <summary>Id предметов «не чаще раза в сутки», уже употреблённых с последнего сна (для сейва).</summary>
    public IEnumerable<string> UsedTodayIds => usedToday;

    private void Awake()
    {
        if (config != null)
        {
            useKey = config.input.useKey;
            satietyDecayPerHour = config.stamina.satietyDecayPerHour;
            newGameSatiety = config.stamina.newGameSatiety;
        }

        // SaveLoadService (-1000) успевает раньше и зовёт RestoreState.
        if (!restored) Satiety = newGameSatiety;
    }

    private void Update()
    {
        if (clock != null && clock.DeltaHours > 0f) PassHours(clock.DeltaHours);

        if (!Input.GetKeyDown(useKey) || !CanUseNow()) return;
        TryConsumeActive();
    }

    /// <summary>Прошло hours игровых часов (кадр или сон): сытость убывает.</summary>
    public void PassHours(float hours)
    {
        if (hours <= 0f) return;
        Satiety = Mathf.Max(0f, Satiety - satietyDecayPerHour * hours);
    }

    /// <summary>Изменить сытость на amount (минус — голод). Для сюжетных событий; ограничено 0…MaxSatiety.</summary>
    public void AddSatiety(float amount)
    {
        Satiety = Mathf.Clamp(Satiety + amount, 0f, MaxSatiety);
    }

    /// <summary>Сон: отметки «раз в сутки» сбрасываются.</summary>
    public void ResetDaily() => usedToday.Clear();

    /// <summary>Активный предмет в руке, если его можно употребить; иначе null.</summary>
    public ItemData ActiveConsumable
    {
        get
        {
            if (inventory == null) return null;
            if (modeController != null && modeController.CurrentMode != PlayerInventoryModeController.InventoryMode.TidyUp) return null;
            ItemData item = inventory.GetActiveItem();
            return item != null && item.consumable != null ? item : null;
        }
    }

    /// <summary>Почему предмет сейчас нельзя употребить; null — можно.</summary>
    public string GetRefusal(ItemData item)
    {
        ConsumableData consumable = item != null ? item.consumable : null;
        if (consumable == null) return null;

        if (consumable.oncePerDay && usedToday.Contains(item.itemId))
            return Loc.Get("hud.use.once_per_day");

        if (consumable.satiety > 0f && Satiety + consumable.satiety > MaxSatiety + 0.01f)
            return Loc.Get("hud.use.full", Mathf.RoundToInt(Satiety));

        return null;
    }

    /// <summary>Употребить активный предмет инвентаря уборки. false — нечего или нельзя (причина — в HUD).</summary>
    public bool TryConsumeActive()
    {
        ItemData item = ActiveConsumable;
        if (item == null) return false;

        string refusal = GetRefusal(item);
        if (refusal != null)
        {
            if (hintUI != null) hintUI.ShowMessage(refusal, true);
            return false;
        }

        WorldItem instance = inventory.RemoveActiveWorldItem();
        if (instance == null) return false;

        ConsumableData consumable = item.consumable;
        Apply(item, consumable);
        busyUntil = Time.time + consumable.useDuration;
        SoundPlayer.Play2D(consumable.useSound);
        PlayUseAnimation(instance, item, consumable.useDuration);
        OnConsumed?.Invoke(item);
        return true;
    }

    /// <summary>Восстановление из сейва — без событий.</summary>
    public void RestoreState(float satiety, IEnumerable<string> usedTodayIds)
    {
        Satiety = Mathf.Clamp(satiety, 0f, MaxSatiety);
        usedToday.Clear();
        if (usedTodayIds != null)
            foreach (var id in usedTodayIds)
                if (!string.IsNullOrEmpty(id)) usedToday.Add(id);
        restored = true;
    }

    private bool CanUseNow()
    {
        if (Time.time < busyUntil) return false;
        if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked) return false;
        if (inputBlocker != null && inputBlocker.IsBlocked) return false;
        if (toolActions != null && toolActions.IsActive) return false;
        return true;
    }

    private void Apply(ItemData item, ConsumableData consumable)
    {
        if (consumable.satiety > 0f) Satiety = Mathf.Min(MaxSatiety, Satiety + consumable.satiety);
        if (consumable.oncePerDay) usedToday.Add(item.itemId);

        // Сначала эффект (он может удлинить бар), потом добавка в бар — чтобы она не упёрлась в старый потолок.
        if (effects != null && consumable.effect != null) effects.Apply(consumable.effect);
        if (stamina != null)
        {
            stamina.RelieveFatigue(consumable.fatigueRelief);
            stamina.RestoreStamina(consumable.instantStamina);
        }
    }

    /// <summary>Предмет из руки подносится ко рту, уменьшаясь, и исчезает. Только визуал: эффект уже применён.</summary>
    private void PlayUseAnimation(WorldItem instance, ItemData item, float duration)
    {
        // В руке виден первый экземпляр записи, а забирается последний — если он был спрятан, показываем его в руке.
        if (instance.State != WorldItem.ItemState.HeldVisible && itemHolder != null)
            instance.SetHeldVisible(itemHolder.HeldItemTransform, item.handPositionOffset, Quaternion.Euler(item.handRotationOffset));

        Transform visual = instance.transform;
        if (playerCamera != null) visual.SetParent(playerCamera.transform, true);

        DOTween.Sequence()
            .Append(visual.DOLocalMove(mouthOffset, duration).SetEase(Ease.InOutSine))
            .Join(visual.DOScale(visual.localScale * 0.4f, duration).SetEase(Ease.InQuad))
            .SetLink(visual.gameObject)
            .OnComplete(() => { if (visual != null) Destroy(visual.gameObject); });
    }
}
