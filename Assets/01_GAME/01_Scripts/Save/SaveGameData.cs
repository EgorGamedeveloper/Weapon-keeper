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
/// пустой массив и есть правильное значение. 4 — игровое время и выживание: часы (clockTotalHours),
/// усталость, сытость, действующие эффекты; доставки терминала считаются в игровых часах
/// (SupplyDeliverySave.remainingHours). Сейв версии 3 мигрирует: первый день с утра, сил полно,
/// секунды доставок переводятся в часы.
/// </summary>
[Serializable]
public class SaveGameData
{
    /// <summary>Версия схемы, которую пишет текущая сборка.</summary>
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public string savedAtUtc;
    public string sceneName;

    public PlayerSave player;

    public LooseItemSave[] looseItems = Array.Empty<LooseItemSave>();
    public SlotSave[] shelfSlots = Array.Empty<SlotSave>();
    public RepairPointSave[] repairPoints = Array.Empty<RepairPointSave>();
    public string[] cleanedStainIds = Array.Empty<string>();
    public string[] brokenBreakableIds = Array.Empty<string>();
    /// <summary>Выбитые кирпичи ещё не обрушенных кладок (в старых сейвах поля нет → пусто: кладки целые).</summary>
    public BrickWallSave[] brickWalls = Array.Empty<BrickWallSave>();
    /// <summary>Протянутые провода (в старых сейвах поля нет → пусто: проводов нет).</summary>
    public WireSave[] wires = Array.Empty<WireSave>();

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
    /// <summary>Уже заказанные разовые ящики терминала (инструменты). В старых сейвах поля нет → пусто.</summary>
    public string[] purchasedOneTimeBoxIds = Array.Empty<string>();
    public string shippingActiveOrderId = "";
    public string[] shippingBoxContentIds = Array.Empty<string>();
    public bool shippingBoxCancelled;
    public float shippingRefillTimer;

    // — время и выживание — (версия 4+)
    /// <summary>Игровое время: часов с полуночи первого дня (GameClock.TotalHours). −1 — не сохранялось
    /// (сейв до версии 4): часы начинают новую игру.</summary>
    public double clockTotalHours = -1.0;

    /// <summary>Накопленная за день усталость (PlayerStamina). Бар не сохраняется — после загрузки он полный.</summary>
    public float staminaFatigue;

    /// <summary>Фонарик подобран (PlayerFlashlight).</summary>
    public bool flashlightOwned;

    /// <summary>Заряд батареи фонарика, секунды работы (PlayerFlashlight.ChargeSeconds).</summary>
    public float flashlightCharge;

    /// <summary>Сытость 0–100 (PlayerConsumption). −1 — не сохранялась: как в новой игре.</summary>
    public float satiety = -1f;

    /// <summary>Действующие временные эффекты (кофе, «Выспался»…).</summary>
    public StatusEffectSave[] statusEffects = Array.Empty<StatusEffectSave>();

    /// <summary>Сколько стимуляторов принято с последнего сна (толерантность).</summary>
    public int stimulantsToday;

    /// <summary>Предметы «не чаще раза в сутки», уже употреблённые с последнего сна (itemId).</summary>
    public string[] consumedTodayIds = Array.Empty<string>();
}

/// <summary>Действующий временный эффект: какой, до какого момента игрового времени и с какой силой.</summary>
[Serializable]
public class StatusEffectSave
{
    public string effectId;
    public double expiresAtHours;
    public float durationHours;
    public float strength;
}

/// <summary>Лутбокс в пути: сколько секунд осталось ехать.</summary>
[Serializable]
public class SupplyDeliverySave
{
    public string lootBoxId;

    /// <summary>Сколько игровых часов осталось ехать (версия 4+).</summary>
    public float remainingHours;
    public float totalHours;

    /// <summary>Сейвы до версии 4: реальные секунды. Только для миграции.</summary>
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

/// <summary>Провод между разъёмами (WireSocket): кто источник, кто потребитель и где лежит провод.</summary>
[Serializable]
public class WireSave
{
    public string sourceId;
    public string consumerId;
    /// <summary>Опорные точки провода (WireCable.ControlPoints), от источника к потребителю.</summary>
    public UnityEngine.Vector3[] points = Array.Empty<UnityEngine.Vector3>();
}

/// <summary>Кладка, которую разбивают по кирпичу (BrickWallSmash): какие кирпичи уже выбиты.</summary>
[Serializable]
public class BrickWallSave
{
    public string id;
    /// <summary>Индексы выбитых кирпичей по порядку иерархии кладки.</summary>
    public int[] knockedOut = Array.Empty<int>();
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
