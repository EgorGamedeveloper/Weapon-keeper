using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Опыт, уровень и очки способностей игрока. Начисляет XP за завершение квестов (QuestManager)
/// и за обычные действия: каждый предмет, впервые поставленный на полку (в т.ч. обломок в мусорный
/// контейнер), приносит ShelfCategory.xpPerPlacedItem своей категории. Синглтона нет — как и другие
/// новые компоненты проекта, потребители находят его через ссылку в инспекторе.
///
/// Очки навыков (UnlockPoints) тратит PlayerSkills через TrySpendUnlockPoints — при покупке узла
/// дерева прокачки.
/// </summary>
public class PlayerProgression : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — кривая опыта ниже переопределяется значениями из GameConfig.progression при старте.")]
    public GameConfig config;

    [Header("Источники опыта")]
    [Tooltip("Пусто — опыт за завершение квестов не начисляется.")]
    public QuestManager questManager;

    [Header("Кривая опыта")]
    [Tooltip("Опыт, необходимый для перехода с 1 на 2 уровень.")]
    [Min(1)] public int baseXPToLevel2 = 100;

    [Tooltip("Насколько растёт требуемый опыт с каждым следующим уровнем (линейно).")]
    [Min(0)] public int xpGrowthPerLevel = 50;

    [Tooltip("Сколько очков навыков начисляется за каждый левелап. Навыки стоят 3/5/10 очков, " +
             "поэтому за уровень даётся с запасом — игрок сам решает, копить или тратить.")]
    [Min(0)] public int unlockPointsPerLevel = 5;

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXP { get; private set; }
    public int UnlockPoints { get; private set; }

    /// <summary>Опыт, необходимый для перехода на следующий уровень.</summary>
    public int XPToNextLevel => baseXPToLevel2 + (CurrentLevel - 1) * xpGrowthPerLevel;

    /// <summary>Изменился накопленный опыт: (текущий опыт, опыт до следующего уровня).</summary>
    public event Action<int, int> OnXPChanged;

    /// <summary>Начислен опыт (сколько) — до левелапа, если он случится. Для всплывающего «+N XP»
    /// (XPGainPopupUI). RestoreState его не поднимает.</summary>
    public event Action<int> OnXPGained;

    /// <summary>Левелап: (новый уровень, сколько очков способностей начислено).</summary>
    public event Action<int, int> OnLevelUp;

    /// <summary>Изменилось количество очков способностей — точка расширения под будущий SkillTree.</summary>
    public event Action<int> OnUnlockPointsChanged;

    private readonly List<ShelfSlot> subscribedSlots = new List<ShelfSlot>();


    private void Awake()
    {
        if (config == null) return;

        baseXPToLevel2 = config.progression.baseXPToLevel2;
        xpGrowthPerLevel = config.progression.xpGrowthPerLevel;
        unlockPointsPerLevel = config.progression.unlockPointsPerLevel;
    }

    private void OnEnable()
    {
        if (questManager != null) questManager.OnQuestCompleted += HandleQuestCompleted;
    }

    private void OnDisable()
    {
        if (questManager != null) questManager.OnQuestCompleted -= HandleQuestCompleted;
    }

    // Start, а не Awake/OnEnable: Shelf.Awake() проставляет slot.parentShelf, а порядок Awake
    // между компонентами Unity не гарантирует (тот же приём, что в ShelvingProgressTracker).
    private void Start()
    {
        foreach (var slot in FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None))
        {
            slot.OnItemPlaced += HandleItemPlaced;
            subscribedSlots.Add(slot);
        }
    }

    private void OnDestroy()
    {
        foreach (var slot in subscribedSlots)
            if (slot != null) slot.OnItemPlaced -= HandleItemPlaced;
    }

    /// <summary>
    /// Восстановление из сейва: пишет уровень/опыт/очки напрямую, минуя AddXP, и намеренно НЕ
    /// поднимает OnXPChanged/OnLevelUp/OnUnlockPointsChanged — иначе при каждой загрузке игрок
    /// получал бы повторные очки способностей за уже пройденные левелапы. PlayerProgressionUI
    /// не останется с нулями: он сам читает CurrentLevel/CurrentXP в своём OnEnable (см. код там),
    /// не дожидаясь события.
    /// </summary>
    public void RestoreState(int level, int xp, int unlockPoints)
    {
        CurrentLevel = Mathf.Max(1, level);
        CurrentXP = Mathf.Max(0, xp);
        UnlockPoints = Mathf.Max(0, unlockPoints);
    }

    /// <summary>Потратить очки навыков (покупка в PlayerSkills). false — очков не хватает, ничего не списано.</summary>
    public bool TrySpendUnlockPoints(int amount)
    {
        if (amount < 0 || amount > UnlockPoints) return false;
        if (amount == 0) return true;

        UnlockPoints -= amount;
        OnUnlockPointsChanged?.Invoke(UnlockPoints);
        return true;
    }

    /// <summary>Начислить опыт напрямую — публичный метод на случай других будущих источников XP.</summary>
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        OnXPGained?.Invoke(amount);
        CurrentXP += amount;

        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            CurrentLevel++;
            UnlockPoints += unlockPointsPerLevel;

            OnLevelUp?.Invoke(CurrentLevel, unlockPointsPerLevel);
            if (unlockPointsPerLevel > 0) OnUnlockPointsChanged?.Invoke(UnlockPoints);
        }

        OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
    }

    private void HandleQuestCompleted(QuestProgress quest) => AddXP(quest.data.xpReward);

    private void HandleItemPlaced(ShelfSlot slot)
    {
        if (slot.placedItems.Count == 0 || slot.parentShelf == null) return;

        // PlaceItem добавляет предмет в конец списка и только потом поднимает событие.
        WorldItem item = slot.placedItems[slot.placedItems.Count - 1];
        ShelfCategory category = slot.parentShelf.acceptedCategory;
        // Награда один раз на экземпляр предмета: иначе «снял с полки — поставил снова» фармило бы
        // опыт. Флаг живёт на предмете и сохраняется вместе с ним (см. WorldItem.PlacementRewarded).
        if (item == null || category == null || item.PlacementRewarded) return;

        item.SetPlacementRewarded(true);
        AddXP(category.xpPerPlacedItem);
    }
}
