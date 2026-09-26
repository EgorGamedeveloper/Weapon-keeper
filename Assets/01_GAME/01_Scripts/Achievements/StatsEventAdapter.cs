using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Единственная связь статистики с игрой: слушает существующие события систем игровой сцены и прибавляет
/// статистику в StatsService. Сами системы о статистике не знают.
///
/// Источники: EnemySpawner.OnEnemySpawned → Enemy.OnDied (убитые), ShelfSlot.OnItemPlaced (расставленное и
/// выброшенное в мусор), RepairPoint.OnRepaired (ремонты, стены), CleanableStain.OnCleaned (пятна и «все
/// отмыты»), Breakable.OnBroken, QuestManager.OnQuestCompleted, DeliveryZone.OnItemDelivered,
/// PlayerWallet.OnBalanceChanged (только рост), PlayerProgression.OnLevelUp (максимальный уровень),
/// PlayerCharacterController.Jumped, InventorySystem.OnInventoryChanged (подобранные); расстояние и время
/// игры — в Update по игровому времени (на паузе не идут).
///
/// Подписка — в Start, и DefaultExecutionOrder(-50) здесь неслучаен:
/// - раньше Start адаптера отрабатывает загрузка сейва (SaveLoadService, -1000; она вообще не поднимает
///   событий), поэтому восстановленное состояние не засчитывается повторно — инвентарь, баланс и уровень
///   адаптер лишь запоминает как точку отсчёта;
/// - позже — Start систем с порядком 0: QuestManager (может сразу завершить восстановленный квест —
///   это настоящее новое завершение, оно засчитывается), EnemySpawnPoint (первый спавн), PlayerProgression
///   (подписывается на полки после адаптера — см. HandleItemPlaced).
/// </summary>
[DefaultExecutionOrder(-50)]
public class StatsEventAdapter : MonoBehaviour
{
    [Header("Источники (пусто — найдутся в сцене)")]
    [Tooltip("Квесты — число завершённых.")]
    public QuestManager questManager;

    [Tooltip("Кошелёк — заработанные деньги (засчитывается только рост баланса).")]
    public PlayerWallet wallet;

    [Tooltip("Прогрессия — максимальный уровень.")]
    public PlayerProgression progression;

    [Tooltip("Игрок — прыжки и пройденное расстояние.")]
    public PlayerCharacterController player;

    [Tooltip("Инвентарь уборки — подобранные предметы.")]
    public InventorySystem tidyUpInventory;

    [Tooltip("Инвентарь экипировки — снятое оружие, вернувшееся в инвентарь уборки, не считается подобранным.")]
    public EquipmentInventory equipmentInventory;

    [Header("Полки")]
    [Tooltip("Категории-мусор (обломки в контейнер): установка туда идёт в статистику debris_disposed, а не items_shelved.")]
    public List<ShelfCategory> trashCategories = new List<ShelfCategory>();

    [Header("Достижения по состоянию сцены")]
    [Tooltip("Scripted-достижение: выдаётся, когда в сцене не осталось неотмытых пятен.")]
    public AchievementData allStainsCleanAchievement;

    private StatsService stats;
    private readonly List<ShelfSlot> slots = new List<ShelfSlot>();
    private readonly List<RepairPoint> repairPoints = new List<RepairPoint>();
    private readonly List<CleanableStain> stains = new List<CleanableStain>();
    private readonly List<Breakable> breakables = new List<Breakable>();
    private readonly List<DeliveryZone> zones = new List<DeliveryZone>();
    private readonly List<EnemySpawner> spawners = new List<EnemySpawner>();
    private readonly HashSet<Enemy> trackedEnemies = new HashSet<Enemy>();
    // Экземпляры, уже побывавшие в инвентарях в этой сессии: повторное появление (снятое оружие вернулось
    // в инвентарь уборки) — не подбор.
    private readonly HashSet<WorldItem> knownItems = new HashSet<WorldItem>();

    private int lastBalance;
    private float playSeconds;
    private Vector3 lastPlayerPosition;
    private bool subscribed;

    private void Start()
    {
        stats = StatsService.Instance;
        if (stats == null)
        {
            Debug.LogWarning("[Achievements] В игре нет StatsService — статистика этой сцены не считается " +
                             "(Tools/Weapon Keeper/Setup Settings & Steam).", this);
            enabled = false;
            return;
        }

        FindMissingReferences();
        Subscribe();

        // Точки отсчёта — уже после загрузки сейва (см. комментарий у класса).
        if (wallet != null) lastBalance = wallet.Balance;
        if (progression != null) stats.SetMax(StatId.MaxLevel, progression.CurrentLevel);
        if (player != null) lastPlayerPosition = player.transform.position;
        RememberInventoryItems();
        CheckAllStainsClean();
    }

    private void OnDestroy() => Unsubscribe();

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return; // пауза

        playSeconds += deltaTime;
        if (playSeconds >= 60f)
        {
            playSeconds -= 60f;
            stats.Increment(StatId.PlayTimeMin);
        }

        if (player == null) return;

        // Пройдено = меньшее из «сдвинулся» и «шёл»: упёрся в стену — не шёл, лифт или загрузка сейва
        // перенесли — не шагал.
        Vector3 position = player.transform.position;
        Vector3 delta = position - lastPlayerPosition;
        lastPlayerPosition = position;
        float moved = new Vector2(delta.x, delta.z).magnitude;
        float walked = Mathf.Min(moved, player.HorizontalSpeed * deltaTime * 1.1f);
        if (walked > 0.0001f) stats.Increment(StatId.DistanceWalkedM, walked);
    }

    // ───────────────────────── Подписка ─────────────────────────

    private void FindMissingReferences()
    {
        if (questManager == null) questManager = FindAnyObjectByType<QuestManager>();
        if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
        if (progression == null) progression = FindAnyObjectByType<PlayerProgression>();
        if (player == null) player = FindAnyObjectByType<PlayerCharacterController>();
        if (tidyUpInventory == null) tidyUpInventory = FindAnyObjectByType<InventorySystem>();
        if (equipmentInventory == null) equipmentInventory = FindAnyObjectByType<EquipmentInventory>();
    }

    private void Subscribe()
    {
        subscribed = true;

        // Include: пятна и точки ремонта бывают выключены до поры (RevealOnBreak), разобранное — спрятано.
        slots.AddRange(FindObjectsByType<ShelfSlot>(FindObjectsInactive.Include));
        foreach (ShelfSlot slot in slots) slot.OnItemPlaced += HandleItemPlaced;

        repairPoints.AddRange(FindObjectsByType<RepairPoint>(FindObjectsInactive.Include));
        foreach (RepairPoint point in repairPoints) point.OnRepaired += HandleRepaired;

        stains.AddRange(FindObjectsByType<CleanableStain>(FindObjectsInactive.Include));
        foreach (CleanableStain stain in stains) stain.OnCleaned += HandleCleaned;

        breakables.AddRange(FindObjectsByType<Breakable>(FindObjectsInactive.Include));
        foreach (Breakable breakable in breakables) breakable.OnBroken += HandleBroken;

        zones.AddRange(FindObjectsByType<DeliveryZone>(FindObjectsInactive.Include));
        foreach (DeliveryZone zone in zones) zone.OnItemDelivered += HandleDelivered;

        spawners.AddRange(FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include));
        foreach (EnemySpawner spawner in spawners) spawner.OnEnemySpawned += TrackEnemy;
        // Враги, поставленные в сцену руками, а не через спавнер.
        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsInactive.Exclude)) TrackEnemy(enemy);

        if (questManager != null) questManager.OnQuestCompleted += HandleQuestCompleted;
        if (wallet != null) wallet.OnBalanceChanged += HandleBalanceChanged;
        if (progression != null) progression.OnLevelUp += HandleLevelUp;
        if (player != null) player.Jumped += HandleJumped;
        if (tidyUpInventory != null) tidyUpInventory.OnInventoryChanged += HandleInventoryChanged;
        if (equipmentInventory != null) equipmentInventory.OnChanged += HandleEquipmentChanged;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        foreach (ShelfSlot slot in slots) if (slot != null) slot.OnItemPlaced -= HandleItemPlaced;
        foreach (RepairPoint point in repairPoints) if (point != null) point.OnRepaired -= HandleRepaired;
        foreach (CleanableStain stain in stains) if (stain != null) stain.OnCleaned -= HandleCleaned;
        foreach (Breakable breakable in breakables) if (breakable != null) breakable.OnBroken -= HandleBroken;
        foreach (DeliveryZone zone in zones) if (zone != null) zone.OnItemDelivered -= HandleDelivered;
        foreach (EnemySpawner spawner in spawners) if (spawner != null) spawner.OnEnemySpawned -= TrackEnemy;
        foreach (Enemy enemy in trackedEnemies) if (enemy != null) enemy.OnDied -= HandleEnemyDied;

        if (questManager != null) questManager.OnQuestCompleted -= HandleQuestCompleted;
        if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
        if (progression != null) progression.OnLevelUp -= HandleLevelUp;
        if (player != null) player.Jumped -= HandleJumped;
        if (tidyUpInventory != null) tidyUpInventory.OnInventoryChanged -= HandleInventoryChanged;
        if (equipmentInventory != null) equipmentInventory.OnChanged -= HandleEquipmentChanged;
    }

    // ───────────────────────── Обработчики ─────────────────────────

    private void TrackEnemy(Enemy enemy)
    {
        if (enemy != null && trackedEnemies.Add(enemy)) enemy.OnDied += HandleEnemyDied;
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        trackedEnemies.Remove(enemy);
        stats.Increment(StatId.EnemiesKilled);
    }

    private void HandleItemPlaced(ShelfSlot slot)
    {
        if (slot.placedItems.Count == 0) return;

        // Засчитывается только первая установка экземпляра — по тому же флагу, по которому PlayerProgression
        // даёт опыт: «снял с полки — поставил снова» статистику не накручивает. Адаптер подписан раньше
        // PlayerProgression (он подписывается в своём Start, порядок 0), поэтому видит флаг до того, как тот
        // его поставит.
        WorldItem item = slot.placedItems[slot.placedItems.Count - 1];
        if (item == null || item.PlacementRewarded) return;

        bool trash = slot.parentShelf != null && trashCategories.Contains(slot.parentShelf.acceptedCategory);
        stats.Increment(trash ? StatId.DebrisDisposed : StatId.ItemsShelved);
    }

    private void HandleRepaired(RepairPoint point)
    {
        stats.Increment(StatId.RepairsCompleted);
        // Кладка (места под кирпичи) — это стена; остальные точки — проводка, лифт, генератор.
        if (point.GetComponentInChildren<RepairSlot>(true) != null) stats.Increment(StatId.WallsRepaired);
    }

    private void HandleCleaned(CleanableStain stain)
    {
        stats.Increment(StatId.StainsCleaned);
        CheckAllStainsClean();
    }

    private void HandleBroken(Breakable breakable) => stats.Increment(StatId.ObjectsBroken);

    private void HandleDelivered(DeliveryZone zone, WorldItem item) => stats.Increment(StatId.ItemsDelivered);

    private void HandleQuestCompleted(QuestProgress quest) => stats.Increment(StatId.QuestsCompleted);

    private void HandleLevelUp(int newLevel, int pointsGranted) => stats.SetMax(StatId.MaxLevel, newLevel);

    private void HandleJumped() => stats.Increment(StatId.Jumps);

    private void HandleBalanceChanged(int balance)
    {
        int delta = balance - lastBalance;
        lastBalance = balance;
        if (delta > 0) stats.Increment(StatId.MoneyEarned, delta);
    }

    private void HandleInventoryChanged()
    {
        foreach (InventoryEntry entry in tidyUpInventory.entries)
            foreach (WorldItem item in entry.instances)
                if (item != null && knownItems.Add(item)) stats.Increment(StatId.ItemsPickedUp);
    }

    private void HandleEquipmentChanged()
    {
        // Экипировка минуя инвентарь уборки (стартовый лом, загрузка) — не подбор, но экземпляр запоминаем.
        foreach (WorldItem item in equipmentInventory.items)
            if (item != null) knownItems.Add(item);
    }

    private void RememberInventoryItems()
    {
        if (tidyUpInventory != null)
            foreach (InventoryEntry entry in tidyUpInventory.entries)
                foreach (WorldItem item in entry.instances)
                    if (item != null) knownItems.Add(item);

        if (equipmentInventory != null) HandleEquipmentChanged();
    }

    private void CheckAllStainsClean()
    {
        if (allStainsCleanAchievement == null || stains.Count == 0) return;
        foreach (CleanableStain stain in stains)
            if (stain != null && !stain.IsClean) return;
        stats.Unlock(allStainsCleanAchievement);
    }
}
