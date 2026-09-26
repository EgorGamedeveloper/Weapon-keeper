using System;

/// <summary>
/// Плоский снимок состояния игры для одного сейва. Всё [Serializable], только публичные поля,
/// без словарей и полиморфизма — под штатный JsonUtility, без сторонних библиотек.
///
/// Хранится только то, что нельзя пересчитать из состояния сцены при загрузке: счётчики обоих
/// трекеров прогресса (BuildingRestorationTracker/ShelvingProgressTracker), физическое состояние
/// WorldItem (родитель/физика/рендереры), прогресс квестов CleanStains/RepairPoints/ShelveItems —
/// всё это производные величины, второй источник правды для них не заводим. Единственное
/// исключение — DeliverItem: доставленный предмет уничтожается, пересчитать не из чего.
///
/// Версии: 1 — исходная схема; 2 — у каждого сохранённого предмета появился флаг «опыт за
/// установку на полку уже выдан» (placementRewarded/rewardedCount). Сейв версии 1 мигрируется в
/// SaveLoadService.MigrateToCurrent, сейв новее CurrentVersion игра не загружает. 3 — у точки ремонта
/// появились заполненные места кладки (RepairPointSave.filledSlots); в старых сейвах кладки не было,
/// пустой массив и есть правильное значение.
/// </summary>
[Serializable]
public class SaveGameData
{
    /// <summary>Версия схемы, которую пишет текущая сборка.</summary>
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public string savedAtUtc;
    public string sceneName;

    public PlayerSave player;

    public LooseItemSave[] looseItems = Array.Empty<LooseItemSave>();
    public SlotSave[] shelfSlots = Array.Empty<SlotSave>();
    public RepairPointSave[] repairPoints = Array.Empty<RepairPointSave>();
    public string[] cleanedStainIds = Array.Empty<string>();
    public string[] brokenBreakableIds = Array.Empty<string>();

    public TidyUpEntrySave[] tidyUp = Array.Empty<TidyUpEntrySave>();
    public int tidyUpActiveIndex;

    public string[] equipmentItemIds = Array.Empty<string>();
    /// <summary>Параллельно equipmentItemIds: выдан ли уже опыт за установку этого предмета на полку.</summary>
    public bool[] equipmentPlacementRewarded = Array.Empty<bool>();
    public int equipmentActiveIndex;

    public int inventoryMode;

    // — квесты —
    public string[] completedQuestIds = Array.Empty<string>();
    public string[] activeQuestIds = Array.Empty<string>();
    public DeliverQuestSave[] activeDeliverCounts = Array.Empty<DeliverQuestSave>();

    // — сюжет — (пройденные ноды сюжетного графа; в старых сейвах поля нет → пусто, сюжет идёт с начала)
    public string[] storyDoneNodes = Array.Empty<string>();

    // — прогрессия —
    public int playerLevel = 1;
    public int playerXP;
    public int playerUnlockPoints;
    public string[] purchasedSkillIds = Array.Empty<string>();

    // — экономика — (в старых сейвах поля нет → 0; PlayerWallet тогда стартует с нуля, это ок для dev-сейвов)
    public int walletBalance;

    // — терминал — (в старых сейвах полей нет → пусто: нет доставок, заказов и коробки)
    public SupplyDeliverySave[] supplyDeliveries = Array.Empty<SupplyDeliverySave>();
    public DeliveredCrateSave[] deliveredCrates = Array.Empty<DeliveredCrateSave>();
    public string[] shippingInboxIds = Array.Empty<string>();
    public string shippingActiveOrderId = "";
    public string[] shippingBoxContentIds = Array.Empty<string>();
    public bool shippingBoxCancelled;
    public float shippingRefillTimer;
}

/// <summary>Лутбокс в пути: сколько секунд осталось ехать.</summary>
[Serializable]
public class SupplyDeliverySave
{
    public string lootBoxId;
    public float remainingSeconds;
    public float totalSeconds;
}

/// <summary>Привезённый, но ещё не вскрытый ящик.</summary>
[Serializable]
public class DeliveredCrateSave
{
    public string lootBoxId;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
}

/// <summary>Позиция, поворот тела (yaw) и поворот камеры по вертикали (pitch) — отдельно от тела,
/// т.к. MouseRotator крутит их на разных объектах (корень игрока и Head/Pivot).</summary>
[Serializable]
public class PlayerSave
{
    public float posX, posY, posZ;
    public float yaw;
    public float pitch;
}

/// <summary>Свободно лежащий в мире предмет — то, из чего он заново спавнится.</summary>
[Serializable]
public class LooseItemSave
{
    public string itemId;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
    public bool placementRewarded;
}

/// <summary>Содержимое одной ячейки полки, порядок — стопка снизу вверх.</summary>
[Serializable]
public class SlotSave
{
    public string slotId;
    public string[] itemIds = Array.Empty<string>();
    /// <summary>Параллельно itemIds (версия 2+).</summary>
    public bool[] placementRewarded = Array.Empty<bool>();
}

/// <summary>Состояние одной точки ремонта.</summary>
[Serializable]
public class RepairPointSave
{
    public string id;
    public int filledCount;
    public bool isRepaired;
    /// <summary>Режим кладки: заполненные места по порядку RepairPoint.Slots (версия 3+).</summary>
    public bool[] filledSlots = Array.Empty<bool>();
}

/// <summary>Запись tidy-up инвентаря: тип предмета + сколько штук.</summary>
[Serializable]
public class TidyUpEntrySave
{
    public string itemId;
    public int count;
    /// <summary>Сколько из count штук уже приносили опыт за полку. Экземпляры одного типа
    /// взаимозаменяемы, поэтому хватает числа, а не флага на каждый.</summary>
    public int rewardedCount;
}

/// <summary>Прогресс активного квеста типа DeliverItem — единственный тип, для которого счётчик
/// нельзя пересчитать из состояния сцены (доставленный предмет уничтожается).</summary>
[Serializable]
public class DeliverQuestSave
{
    public string questId;
    public int currentCount;
}
