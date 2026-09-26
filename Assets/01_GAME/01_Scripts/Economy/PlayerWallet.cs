using System;
using UnityEngine;

/// <summary>
/// Деньги игрока. Начисляются за завершённые квесты (QuestData.moneyReward) и за выполненные заказы
/// на отправку, тратятся в терминале на поставки. Синглтона нет — потребители (терминал, HUD)
/// держат ссылку в инспекторе, как с PlayerProgression.
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — стартовый баланс и символ валюты берутся из GameConfig.economy.")]
    public GameConfig config;

    [Header("Источники")]
    [Tooltip("Пусто — деньги за квесты не начисляются.")]
    public QuestManager questManager;

    [Header("Валюта")]
    [Tooltip("Баланс в начале новой игры.")]
    [Min(0)] public int startingBalance = 100;

    [Tooltip("Символ валюты в интерфейсе.")]
    public string currencySymbol = "$";

    public int Balance { get; private set; }

    /// <summary>Баланс изменился (передаёт новый баланс). Восстановление из сейва события не поднимает.</summary>
    public event Action<int> OnBalanceChanged;

    // SaveLoadService (-1000) восстанавливает баланс раньше нашего Awake — стартовый баланс не должен его затереть.
    private bool restored;

    private void Awake()
    {
        if (config != null)
        {
            startingBalance = config.economy.startingBalance;
            currencySymbol = config.economy.currencySymbol;
        }

        if (!restored) Balance = startingBalance;
    }

    private void OnEnable()
    {
        if (questManager != null) questManager.OnQuestCompleted += HandleQuestCompleted;
    }

    private void OnDisable()
    {
        if (questManager != null) questManager.OnQuestCompleted -= HandleQuestCompleted;
    }

    public bool CanAfford(int amount) => amount <= Balance;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Balance += amount;
        OnBalanceChanged?.Invoke(Balance);
    }

    /// <summary>Списать деньги. false — не хватает, ничего не списано.</summary>
    public bool TrySpend(int amount)
    {
        if (amount < 0 || amount > Balance) return false;
        if (amount == 0) return true;

        Balance -= amount;
        OnBalanceChanged?.Invoke(Balance);
        return true;
    }

    /// <summary>Восстановление из сейва: без события — WalletUI сам читает Balance в OnEnable.</summary>
    public void RestoreBalance(int balance)
    {
        Balance = Mathf.Max(0, balance);
        restored = true;
    }

    /// <summary>Сумма в формате интерфейса: «$ 1 250».</summary>
    public string Format(int amount) => currencySymbol + " " + amount.ToString("#,0").Replace(',', ' ').Replace(' ', ' ');

    private void HandleQuestCompleted(QuestProgress quest) => Add(quest.data.moneyReward);
}
