using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Собирает и применяет сейв. Единственная точка, которая знает про SaveGameData целиком —
/// остальные системы дают ей только маленькие Restore-методы (см. RepairPoint, CleanableStain,
/// Breakable, ShelfSlot, QuestManager, PlayerProgression, PlayerSkills, EquipmentInventory).
///
/// Порядок выполнения критичен, отсюда DefaultExecutionOrder(-1000) — раньше вообще всех
/// остальных скриптов проекта (у PlayerItemInteraction -200, у EquipmentWeaponBridge -100):
///
/// Awake (раньше Awake любого другого скрипта, включая MouseRotator и трекеры прогресса):
///   1) поворот камеры — MouseRotator.Start() ещё не читал transform, значит наш Awake-writе
///      станет его точкой отсчёта (см. комментарий у RestoreCameraOrientation);
///   2) точки ремонта/пятна/поломки — восстанавливаются ДО BuildingRestorationTracker.Awake,
///      который иначе засчитал бы их как "изначально такими" по-своему, но верно (он сам читает
///      IsRepaired/IsClean при подсчёте старта — здесь важно просто успеть раньше);
///   3) все WorldItem уничтожаются и спавнятся заново из сейва — сейв авторитетен (но только если
///      каждый id из сейва есть в каталоге: иначе загрузка отменяется целиком, см. ValidateAgainstCatalog);
///   4) прогрессия и квесты — прогрессия первой (QuestManager.IsUnlocked читает её уровень). Активные
///      квесты здесь только регистрируются: подписка на цели и пересчёт прогресса — в QuestManager.Start,
///      когда полки уже собрали свои ячейки, а PlayerProgression/HUD подписались на события.
///
/// Start (раньше Start любого другого скрипта, включая StartingEquipment.Start — см. его
/// собственную защиту от повторного добавления лома):
///   5) tidy-up инвентарь, 6) экипировка, 7) режим (оружие прячет/достаёт EquipmentWeaponBridge).
///
/// Сохранение работает только в сессии, запущенной через Bootstrap: Play на TestScene напрямую — это
/// тестовый прогон, он не должен перезаписывать настоящий сейв игрока при выходе.
///
/// Все шаги 1-7 идут БЕЗ игровых событий (OnRepaired/OnCleaned/OnBroken/OnItemPlaced/OnLevelUp
/// и т.п.) — иначе трекеры, квесты и прогрессия задваивали бы реакцию на каждую загрузку.
/// Единственное осознанное исключение — EquipmentInventory.SetActiveIndex/Add: их OnChanged
/// специально поднимается, это штатный путь, которым EquipmentWeaponBridge спавнит оружие.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class SaveLoadService : MonoBehaviour
{
    [Header("Игрок")]
    [Tooltip("Корень игрока (Regular_Character) — позиция и поворот тела (yaw).")]
    public Transform player;

    [Tooltip("Head/Pivot — поворот камеры по вертикали (pitch). MouseRotator висит и там, и на корне.")]
    public Transform cameraPivot;

    [Tooltip("Tidy-up инвентарь игрока (слот 1).")]
    public InventorySystem tidyUpInventory;

    [Tooltip("Инвентарь экипировки (оружие, лом).")]
    public EquipmentInventory equipmentInventory;

    [Tooltip("Контроллер режимов инвентаря — режим и хранилище для спрятанных предметов.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Перед сохранением досаживает в инвентарь предмет, который в этот момент летит в руку " +
             "(он не принадлежит ни одному контейнеру); туда же выбрасываются предметы, не влезшие в инвентарь при загрузке.")]
    public PlayerItemInteraction itemInteraction;

    [Header("Прогресс")]
    [Tooltip("Квесты: завершённые и активные.")]
    public QuestManager questManager;

    [Tooltip("Уровень, опыт и очки способностей.")]
    public PlayerProgression playerProgression;

    [Tooltip("Купленные навыки. Пусто — навыки не сохраняются.")]
    public PlayerSkills playerSkills;

    [Tooltip("Деньги игрока. Пусто — баланс не сохраняется.")]
    public PlayerWallet wallet;

    [Tooltip("Поставки через терминал (доставки в пути, невскрытые ящики).")]
    public SupplyService supplyService;

    [Tooltip("Заказы на отправку (входящие, активный заказ, содержимое коробки).")]
    public ShippingService shippingService;

    [Header("Куда спавнить восстановленные предметы")]
    [Tooltip("Родитель для заново заспавненных WorldItem (свободные предметы мира). Предметы, " +
             "уходящие на полку/в руки, сразу же переродительствуются — им это поле не важно. " +
             "Пусто — спавнятся в корень сцены.")]
    public Transform looseItemsContainer;

    [Header("Отладка")]
    [Tooltip("Клавиша ручного сохранения. Работает только в редакторе и development-сборке.")]
    public KeyCode debugSaveKey = KeyCode.F5;

    private GameBootstrap bootstrap;
    private SaveGameData pendingLoad;
    private bool savingEnabled;

    private void Awake()
    {
        bootstrap = FindFirstObjectByType<GameBootstrap>();

        // Сцену запустили напрямую, без меню: это тестовый прогон — настоящий сейв не трогаем.
        savingEnabled = bootstrap != null;
        if (bootstrap == null || !bootstrap.LoadSaveOnStart) return;

        pendingLoad = bootstrap.TakePendingSave();
        if (pendingLoad == null) return;

        if (pendingLoad.sceneName != gameObject.scene.name)
            Debug.LogWarning($"[SaveLoadService] Сейв сделан в сцене '{pendingLoad.sceneName}', загружается в '{gameObject.scene.name}'.");

        MigrateToCurrent(pendingLoad);

        if (!ValidateAgainstCatalog(pendingLoad))
        {
            // Сцена остаётся в исходном виде, а файл сейва — нетронутым: автосейв этой сессии
            // иначе навсегда заменил бы прогресс игрока состоянием "без предметов".
            savingEnabled = false;
            pendingLoad = null;
            return;
        }

        ApplyPhaseA(pendingLoad);
    }

    private void Start()
    {
        if (pendingLoad != null) ApplyPhaseB(pendingLoad);
    }

    private void Update()
    {
        if (Debug.isDebugBuild && Input.GetKeyDown(debugSaveKey)) SaveNow();
    }

    /// <summary>Автосейв при закрытии игры — чтобы «Продолжить» действительно продолжало
    /// последнюю сессию, а не требовало ручного сохранения перед выходом.</summary>
    private void OnApplicationQuit() => SaveNow();

    /// <summary>Собрать и записать сейв прямо сейчас. Публичный — автосейв (выход/меню) будет
    /// звать этот же метод.</summary>
    public void SaveNow()
    {
        if (!savingEnabled) return;

        // Предмет, летящий в руку, не принадлежит ни одному контейнеру — досаживаем его в инвентарь
        // мгновенно. Раньше сохранение в этот момент просто пропускалось, и выход из игры посреди
        // подбора терял всю сессию.
        if (itemInteraction != null) itemInteraction.FinishPendingPickup();
        SaveFileService.Save(CaptureState());
    }

    // ───────────────────────── Сбор состояния ─────────────────────────

    public SaveGameData CaptureState()
    {
        var data = new SaveGameData
        {
            version = SaveGameData.CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            sceneName = gameObject.scene.name
        };

        CapturePlayer(data);
        CaptureWorld(data);
        CaptureInventories(data);
        CaptureQuestsAndProgression(data);

        return data;
    }

    private void CapturePlayer(SaveGameData data)
    {
        if (player == null) return;

        data.player = new PlayerSave
        {
            posX = player.position.x,
            posY = player.position.y,
            posZ = player.position.z,
            yaw = player.eulerAngles.y,
            pitch = cameraPivot != null ? NormalizeAngle(cameraPivot.localEulerAngles.x) : 0f
        };
    }

    /// <summary>Один проход по WorldItem (свободные предметы) и один проход по PersistentId
    /// (полки/точки ремонта/пятна/поломки) — вместо того чтобы каждый блок сборки сканировал
    /// сцену заново. На нынешних ~30+18 объектах разницы не видно, но при росте до тысяч
    /// предметов именно повторные FindObjectsByType, а не размер итогового JSON, стали бы
    /// узким местом (см. план).</summary>
    private void CaptureWorld(SaveGameData data)
    {
        var looseList = new List<LooseItemSave>();
        foreach (var item in FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.itemData == null || item.State != WorldItem.ItemState.InWorld) continue;

            Vector3 pos = item.transform.position;
            Quaternion rot = item.transform.rotation;
            looseList.Add(new LooseItemSave
            {
                itemId = item.itemData.itemId,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                rotX = rot.x, rotY = rot.y, rotZ = rot.z, rotW = rot.w,
                placementRewarded = item.PlacementRewarded
            });
        }
        data.looseItems = looseList.ToArray();

        var repairList = new List<RepairPointSave>();
        var cleanedList = new List<string>();
        var brokenList = new List<string>();
        var slotList = new List<SlotSave>();

        foreach (var pid in FindObjectsByType<PersistentId>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(pid.Id)) continue;

            var repair = pid.GetComponent<RepairPoint>();
            if (repair != null)
            {
                repairList.Add(new RepairPointSave
                {
                    id = pid.Id,
                    filledCount = repair.FilledCount,
                    isRepaired = repair.IsRepaired,
                    filledSlots = repair.GetFilledSlots()
                });
                continue;
            }

            var stain = pid.GetComponent<CleanableStain>();
            if (stain != null)
            {
                if (stain.IsClean) cleanedList.Add(pid.Id);
                continue;
            }

            var breakable = pid.GetComponent<Breakable>();
            if (breakable != null)
            {
                if (breakable.IsBroken) brokenList.Add(pid.Id);
                continue;
            }

            var slot = pid.GetComponent<ShelfSlot>();
            if (slot != null && slot.StackCount > 0)
            {
                var itemIds = new string[slot.StackCount];
                var rewarded = new bool[slot.StackCount];
                for (int i = 0; i < slot.StackCount; i++)
                {
                    itemIds[i] = slot.placedItems[i].itemData.itemId;
                    rewarded[i] = slot.placedItems[i].PlacementRewarded;
                }
                slotList.Add(new SlotSave { slotId = pid.Id, itemIds = itemIds, placementRewarded = rewarded });
            }
        }

        data.repairPoints = repairList.ToArray();
        data.cleanedStainIds = cleanedList.ToArray();
        data.brokenBreakableIds = brokenList.ToArray();
        data.shelfSlots = slotList.ToArray();
    }

    private void CaptureInventories(SaveGameData data)
    {
        if (tidyUpInventory != null)
        {
            var tidyList = new List<TidyUpEntrySave>();
            foreach (var entry in tidyUpInventory.entries)
            {
                if (entry.item == null) continue;
                int rewardedCount = 0;
                foreach (var instance in entry.instances)
                    if (instance != null && instance.PlacementRewarded) rewardedCount++;
                tidyList.Add(new TidyUpEntrySave { itemId = entry.item.itemId, count = entry.Count, rewardedCount = rewardedCount });
            }
            data.tidyUp = tidyList.ToArray();
            data.tidyUpActiveIndex = tidyUpInventory.activeSlotIndex;
        }

        if (equipmentInventory != null)
        {
            var eqList = new List<string>();
            var eqRewarded = new List<bool>();
            foreach (var item in equipmentInventory.items)
            {
                if (item == null || item.itemData == null) continue;
                eqList.Add(item.itemData.itemId);
                eqRewarded.Add(item.PlacementRewarded);
            }
            data.equipmentItemIds = eqList.ToArray();
            data.equipmentPlacementRewarded = eqRewarded.ToArray();
            data.equipmentActiveIndex = equipmentInventory.activeIndex;
        }

        if (modeController != null) data.inventoryMode = (int)modeController.CurrentMode;
    }

    private void CaptureQuestsAndProgression(SaveGameData data)
    {
        if (questManager != null)
        {
            var completedList = new List<string>();
            foreach (var id in questManager.CompletedQuestIds) completedList.Add(id);
            data.completedQuestIds = completedList.ToArray();

            var activeList = new List<string>();
            var deliverList = new List<DeliverQuestSave>();
            foreach (var progress in questManager.ActiveQuests)
            {
                if (progress.data == null) continue;
                activeList.Add(progress.data.questId);
                if (progress.data.type == QuestData.QuestType.DeliverItem)
                    deliverList.Add(new DeliverQuestSave { questId = progress.data.questId, currentCount = progress.currentCount });
            }
            data.activeQuestIds = activeList.ToArray();
            data.activeDeliverCounts = deliverList.ToArray();
        }

        if (playerProgression != null)
        {
            data.playerLevel = playerProgression.CurrentLevel;
            data.playerXP = playerProgression.CurrentXP;
            data.playerUnlockPoints = playerProgression.UnlockPoints;
        }

        if (playerSkills != null)
        {
            var skillIds = new List<string>();
            foreach (var skill in playerSkills.Owned)
                if (skill != null && !string.IsNullOrEmpty(skill.skillId)) skillIds.Add(skill.skillId);
            data.purchasedSkillIds = skillIds.ToArray();
        }

        if (wallet != null)
            data.walletBalance = wallet.Balance;

        if (supplyService != null)
        {
            data.supplyDeliveries = supplyService.CaptureDeliveries();
            data.deliveredCrates = supplyService.CaptureCrates();
        }

        if (shippingService != null)
            shippingService.Capture(data);
    }

    // ───────────────────────── Применение сейва ─────────────────────────

    /// <summary>Довести сейв старой версии до текущей схемы. v1 → v2: флагов «опыт за полку выдан»
    /// ещё не было — всё, что стоит на полках, за установку уже вознаграждено, остальное считаем
    /// невознаграждённым (максимум одна лишняя награда на предмет, один раз). v2 → v3: мест кладки
    /// в старых сейвах не было, пустой filledSlots уже верен — менять нечего.</summary>
    private static void MigrateToCurrent(SaveGameData data)
    {
        if (data.version < 2)
        {
            foreach (var slot in data.shelfSlots)
            {
                slot.placementRewarded = new bool[slot.itemIds.Length];
                for (int i = 0; i < slot.placementRewarded.Length; i++) slot.placementRewarded[i] = true;
            }
        }

        data.version = SaveGameData.CurrentVersion;
    }

    /// <summary>
    /// Проверить сейв по каталогу ДО того, как ApplyPhaseA уничтожит предметы сцены: если хоть один id
    /// не находится (каталог не назначен, предмет удалён/переименован, у ItemData нет worldPrefab),
    /// загрузка отменяется целиком. Иначе такие предметы молча исчезли бы, а автосейв закрепил бы потерю.
    /// </summary>
    private bool ValidateAgainstCatalog(SaveGameData data)
    {
        ItemCatalog catalog = bootstrap != null ? bootstrap.itemCatalog : null;
        if (catalog == null)
        {
            Debug.LogError("[SaveLoadService] У GameBootstrap не назначен ItemCatalog — сейв не загружен.");
            return false;
        }

        var missing = new HashSet<string>();
        void Check(string id)
        {
            ItemData item = catalog.GetById(id);
            if (item == null || item.worldPrefab == null) missing.Add(id ?? "<null>");
        }

        foreach (var loose in data.looseItems) Check(loose.itemId);
        foreach (var slot in data.shelfSlots) foreach (var id in slot.itemIds) Check(id);
        foreach (var entry in data.tidyUp) Check(entry.itemId);
        foreach (var id in data.equipmentItemIds) Check(id);

        if (missing.Count == 0) return true;

        Debug.LogError($"[SaveLoadService] В каталоге нет предметов из сейва ({string.Join(", ", missing)}) — сейв не загружен, файл не тронут.");
        return false;
    }

    private void ApplyPhaseA(SaveGameData data)
    {
        RestoreCameraOrientation(data.player);

        var byId = new Dictionary<string, PersistentId>();
        foreach (var pid in FindObjectsByType<PersistentId>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!string.IsNullOrEmpty(pid.Id)) byId[pid.Id] = pid;

        foreach (var rp in data.repairPoints)
        {
            if (!byId.TryGetValue(rp.id, out var pid)) continue;
            var repair = pid.GetComponent<RepairPoint>();
            if (repair != null) repair.RestoreState(rp.filledCount, rp.isRepaired, rp.filledSlots);
        }

        foreach (var stainId in data.cleanedStainIds)
        {
            if (!byId.TryGetValue(stainId, out var pid)) continue;
            var stain = pid.GetComponent<CleanableStain>();
            if (stain != null) stain.RestoreClean();
        }

        foreach (var breakId in data.brokenBreakableIds)
        {
            if (!byId.TryGetValue(breakId, out var pid)) continue;
            var breakable = pid.GetComponent<Breakable>();
            if (breakable != null) breakable.RestoreBroken();
        }

        // Сейв авторитетен: всё, что сейчас лежит в мире/на полках по разметке сцены — не в счёт,
        // уничтожаем и спавним заново строго по записям сейва. DestroyImmediate, а не Destroy —
        // иначе трекеры прогресса, которые считают объекты в своих Start() того же кадра,
        // застали бы ещё живых "покойников".
        foreach (var item in FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroyImmediate(item.gameObject);

        ItemCatalog catalog = bootstrap.itemCatalog;

        foreach (var loose in data.looseItems)
        {
            WorldItem item = SpawnItem(loose.itemId, catalog);
            if (item == null) continue;
            item.SetPlacementRewarded(loose.placementRewarded);
            item.transform.SetPositionAndRotation(
                new Vector3(loose.posX, loose.posY, loose.posZ),
                new Quaternion(loose.rotX, loose.rotY, loose.rotZ, loose.rotW));
        }

        foreach (var slotSave in data.shelfSlots)
        {
            if (!byId.TryGetValue(slotSave.slotId, out var pid)) continue;
            var slot = pid.GetComponent<ShelfSlot>();
            if (slot == null) continue;

            for (int i = 0; i < slotSave.itemIds.Length; i++)
            {
                WorldItem item = SpawnItem(slotSave.itemIds[i], catalog);
                if (item == null) continue;
                item.SetPlacementRewarded(i < slotSave.placementRewarded.Length && slotSave.placementRewarded[i]);
                slot.RestorePlacement(item);
            }
        }

        // Прогрессия — раньше квестов: QuestManager.IsUnlocked читает PlayerProgression.CurrentLevel.
        if (playerProgression != null)
            playerProgression.RestoreState(data.playerLevel, data.playerXP, data.playerUnlockPoints);

        // Навыки — сразу за прогрессией и тоже без событий: очки уже сохранены за вычетом покупок.
        if (playerSkills != null)
            playerSkills.RestoreOwned(data.purchasedSkillIds);

        if (wallet != null)
            wallet.RestoreBalance(data.walletBalance);

        // Терминал — тоже без событий. Коробку заказа ShippingService найдёт сам в своём Start:
        // она восстанавливается обычным предметом (на полу — здесь, в инвентаре — в фазе B).
        if (supplyService != null)
            supplyService.RestoreState(data.supplyDeliveries, data.deliveredCrates);
        if (shippingService != null)
            shippingService.RestoreState(data, bootstrap != null ? bootstrap.itemCatalog : null);

        if (questManager != null)
        {
            foreach (var questId in data.completedQuestIds)
            {
                QuestData questData = questManager.FindByQuestId(questId);
                if (questData != null) questManager.RestoreCompleted(questData);
            }

            foreach (var questId in data.activeQuestIds)
            {
                QuestData questData = questManager.FindByQuestId(questId);
                if (questData == null) continue;

                int count = 0;
                foreach (var d in data.activeDeliverCounts)
                {
                    if (d.questId != questId) continue;
                    count = d.currentCount;
                    break;
                }

                questManager.RestoreActive(questData, count);
            }
        }
    }

    private void ApplyPhaseB(SaveGameData data)
    {
        ItemCatalog catalog = bootstrap != null ? bootstrap.itemCatalog : null;

        if (tidyUpInventory != null && modeController != null)
        {
            foreach (var entrySave in data.tidyUp)
            {
                for (int i = 0; i < entrySave.count; i++)
                {
                    WorldItem item = SpawnItem(entrySave.itemId, catalog);
                    if (item == null) continue;
                    item.SetPlacementRewarded(i < entrySave.rewardedCount);
                    item.SetCarriedHidden(modeController.equipmentStorage);

                    // Сейв мог записать больше, чем вмещает инвентарь сейчас (вместимость — из конфига):
                    // лишнее не прячем навсегда, а кладём перед игроком.
                    if (!tidyUpInventory.AddWorldItem(item)) DropOverflowItem(item);
                }
            }

            if (tidyUpInventory.entries.Count > 0)
                tidyUpInventory.SetActiveSlot(Mathf.Clamp(data.tidyUpActiveIndex, 0, tidyUpInventory.entries.Count - 1));
        }

        if (equipmentInventory != null && modeController != null)
        {
            for (int i = 0; i < data.equipmentItemIds.Length; i++)
            {
                WorldItem item = SpawnItem(data.equipmentItemIds[i], catalog);
                if (item == null) continue;
                item.SetPlacementRewarded(i < data.equipmentPlacementRewarded.Length && data.equipmentPlacementRewarded[i]);
                item.SetCarriedHidden(modeController.equipmentStorage);
                equipmentInventory.Add(item);
            }

            equipmentInventory.SetActiveIndex(data.equipmentActiveIndex);
        }

        if (modeController != null)
            modeController.SetMode((PlayerInventoryModeController.InventoryMode)data.inventoryMode);
    }

    /// <summary>
    /// MouseRotator.Start() делает `originalRotation = transform.localRotation` ОДИН раз, читая
    /// то, что на тот момент лежит в трансформе, и дальше в Update() каждый кадр пишет поверх
    /// этого — то есть любая запись transform.rotation ПОСЛЕ его Start() бесполезна. Но Awake
    /// у ВСЕХ объектов сцены гарантированно отрабатывает раньше Start у ЛЮБОГО объекта, а этот
    /// компонент — самый ранний Awake в проекте (DefaultExecutionOrder(-1000)). Значит запись
    /// здесь становится точкой отсчёта для MouseRotator, а не теряется.
    /// </summary>
    private void RestoreCameraOrientation(PlayerSave save)
    {
        if (save == null) return;

        if (player != null)
            player.SetPositionAndRotation(new Vector3(save.posX, save.posY, save.posZ), Quaternion.Euler(0f, save.yaw, 0f));

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(save.pitch, 0f, 0f);
    }

    private void DropOverflowItem(WorldItem item)
    {
        if (itemInteraction != null)
        {
            itemInteraction.DropInFront(item);
            return;
        }

        Vector3 position = player != null ? player.position + player.forward : item.transform.position;
        item.Drop(position, Quaternion.identity, Vector3.zero);
    }

    private WorldItem SpawnItem(string itemId, ItemCatalog catalog)
    {
        ItemData itemData = catalog != null ? catalog.GetById(itemId) : null;
        if (itemData == null || itemData.worldPrefab == null) return null;

        GameObject instance = Instantiate(itemData.worldPrefab, looseItemsContainer);
        return instance.GetComponent<WorldItem>();
    }

    private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;
}
