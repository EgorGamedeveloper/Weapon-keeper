using System;
using UnityEngine;

/// <summary>
/// Опыт, уровень и очки способностей игрока. Начисляет XP за завершение квестов (QuestManager)
/// и за расстановку товара по полкам (ShelvingProgressTracker). Синглтона нет — как и другие
/// новые компоненты проекта, потребители находят его через ссылку в инспекторе.
///
/// Очки способностей (UnlockPoints) в этой итерации только копятся: сама система их траты
/// (скорость ходьбы, вместимость инвентаря, доступ к редкому оружию/поставкам) появится позже
/// как отдельный компонент/каталог способностей, подписанный на OnUnlockPointsChanged.
/// </summary>
public class PlayerProgression : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — кривая опыта ниже переопределяется значениями из GameConfig.progression при старте.")]
    public GameConfig config;

    [Header("Источники опыта")]
    [Tooltip("Пусто — опыт за завершение квестов не начисляется.")]
    public QuestManager questManager;

    [Tooltip("Пусто — опыт за расстановку товара по полкам не начисляется.")]
    public ShelvingProgressTracker shelvingTracker;

    [Header("Кривая опыта")]
    [Tooltip("Опыт, необходимый для перехода с 1 на 2 уровень.")]
    [Min(1)] public int baseXPToLevel2 = 100;

    [Tooltip("Насколько растёт требуемый опыт с каждым следующим уровнем (линейно).")]
    [Min(0)] public int xpGrowthPerLevel = 50;

    [Tooltip("Сколько очков способностей (unlock points) начисляется за каждый левелап.")]
    [Min(0)] public int unlockPointsPerLevel = 1;

    [Tooltip("Опыт за одну единицу товара, расставленную по полке (см. ShelvingProgressTracker).")]
    [Min(0)] public int xpPerShelvedUnit = 2;

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXP { get; private set; }
    public int UnlockPoints { get; private set; }

    /// <summary>Опыт, необходимый для перехода на следующий уровень.</summary>
    public int XPToNextLevel => baseXPToLevel2 + (CurrentLevel - 1) * xpGrowthPerLevel;

    /// <summary>Изменился накопленный опыт: (текущий опыт, опыт до следующего уровня).</summary>
    public event Action<int, int> OnXPChanged;

    /// <summary>Левелап: (новый уровень, сколько очков способностей начислено).</summary>
    public event Action<int, int> OnLevelUp;

    /// <summary>Изменилось количество очков способностей — точка расширения под будущий SkillTree.</summary>
    public event Action<int> OnUnlockPointsChanged;

    private int lastPlacedUnits;

    private void Awake()
    {
        if (config == null) return;

        baseXPToLevel2 = config.progression.baseXPToLevel2;
        xpGrowthPerLevel = config.progression.xpGrowthPerLevel;
        unlockPointsPerLevel = config.progression.unlockPointsPerLevel;
        xpPerShelvedUnit = config.progression.xpPerShelvedUnit;
    }

    private void OnEnable()
    {
        if (questManager != null) questManager.OnQuestCompleted += HandleQuestCompleted;
        if (shelvingTracker != null) shelvingTracker.OnShelvingProgressChanged += HandleShelvingProgressChanged;
    }

    private void OnDisable()
    {
        if (questManager != null) questManager.OnQuestCompleted -= HandleQuestCompleted;
        if (shelvingTracker != null) shelvingTracker.OnShelvingProgressChanged -= HandleShelvingProgressChanged;
    }

    /// <summary>Начислить опыт напрямую — публичный метод на случай других будущих источников XP.</summary>
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

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

    private void HandleShelvingProgressChanged(int placed, int total, float percent)
    {
        int delta = placed - lastPlacedUnits;
        lastPlacedUnits = placed;
        if (delta > 0) AddXP(delta * xpPerShelvedUnit);
    }
}
