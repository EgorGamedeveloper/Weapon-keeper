using UnityEngine;

/// <summary>Настройки луча взаимодействия и анимации подбора (PlayerItemInteraction).</summary>
[System.Serializable]
public class InteractionSettings
{
    [Tooltip("Максимальная дистанция, на которой можно подобрать предмет.")]
    public float pickupRange = 2f;

    [Tooltip("Максимальная дистанция взаимодействия с полкой/точкой ремонта.")]
    public float shelfInteractRange = 3f;

    [Tooltip("Максимальная дистанция разбора объектов ломом.")]
    public float toolInteractRange = 2.5f;

    [Tooltip("Радиус, в котором свободные места под предмет в руке подсвечиваются голограммой. 0 — без подсказок.")]
    [Min(0f)] public float placementHintRadius = 4f;

    [Tooltip("Скорость перелёта предмета в руку при подборе, м/с (время полёта ограничено 0.18–0.4 с).")]
    public float pickupAnimationSpeed = 12f;

    [Tooltip("Высота дуги полёта предмета в руку, м (на коротком пути — ниже). 0 — по прямой.")]
    [Min(0f)] public float pickupArcHeight = 0.1f;

    [Tooltip("На каком расстоянии от камеры появляется выброшенный предмет.")]
    public float dropDistance = 1.25f;

    [Tooltip("С какой скоростью выброшенный предмет улетает вперёд.")]
    public float dropSpeed = 4f;
}

/// <summary>Покачивание предмета в руке — от мыши (sway) и при ходьбе (bobbing). См. EquippedItemHolder.</summary>
[System.Serializable]
public class HeldItemSwaySettings
{
    [Header("От мыши")]
    [Tooltip("Сила покачивания предмета от движения мыши.")]
    public float swayAmount = 4f;
    [Tooltip("Плавность возврата покачивания от мыши.")]
    public float swaySmooth = 6f;
    [Tooltip("Максимальный угол покачивания от мыши, градусы.")]
    public float maxSwayAngle = 8f;

    [Header("При ходьбе")]
    [Tooltip("Частота покачивания при ходьбе.")]
    public float bobFrequency = 6f;
    [Tooltip("Амплитуда покачивания при ходьбе, м.")]
    public float bobAmount = 0.03f;
    [Tooltip("Плавность входа/выхода покачивания при ходьбе.")]
    public float bobSmooth = 8f;

    [Tooltip("Минимальная скорость игрока, при которой начинается покачивание.")]
    public float moveThreshold = 0.1f;
}

/// <summary>Инвентарь уборки (InventorySystem). Вместимость растёт по мере прогрессии игрока.</summary>
[System.Serializable]
public class TidyUpInventorySettings
{
    [Tooltip("Максимальное число предметов в инвентаре уборки.")]
    [Min(1)] public int maxItemCount = 5;

    [Tooltip("Инвертировать направление прокрутки колеса мыши.")]
    public bool invertScroll = false;
}

/// <summary>Раскладка клавиш. Проект сознательно остаётся на старом UnityEngine.Input, поэтому здесь KeyCode.</summary>
[System.Serializable]
public class PlayerInputKeys
{
    [Tooltip("Переключиться на инвентарь уборки.")]
    public KeyCode tidyUpKey = KeyCode.Alpha1;

    [Tooltip("Переключиться на слот экипировки (оружие/инструменты).")]
    public KeyCode equipmentKey = KeyCode.Alpha2;

    [Tooltip("Экипировать активный предмет / снять экипированное оружие.")]
    public KeyCode equipKey = KeyCode.Q;

    [Tooltip("Снять предмет с полки (ЛКМ только ставит; с пола подбирают ЛКМ).")]
    public KeyCode takeFromShelfKey = KeyCode.E;

    [Tooltip("«Видение»: подсветить все места и однотипные предметы сквозь стены (PlacementVision).")]
    public KeyCode visionKey = KeyCode.V;

    [Tooltip("Прыжок.")]
    public KeyCode jumpKey = KeyCode.Space;

    [Tooltip("Бег (удержание).")]
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Tooltip("Съесть, выпить или уколоть предмет в руке (еда, кофе, стимулятор — PlayerConsumption).")]
    public KeyCode useKey = KeyCode.F;

    [Tooltip("Включить/выключить налобный фонарик (PlayerFlashlight).")]
    public KeyCode flashlightKey = KeyCode.Alpha3;
}

/// <summary>«Видение» предмета в руке — будущая способность игрока. См. PlacementVision.</summary>
[System.Serializable]
public class VisionSettings
{
    [Tooltip("Сколько секунд действует видение.")]
    [Min(0.1f)] public float duration = 15f;

    [Tooltip("За сколько секунд до конца подсветка начинает плавно гаснуть.")]
    [Min(0f)] public float fadeDuration = 5f;

    [Tooltip("Перезарядка после окончания, секунды. 0 — можно включить сразу снова.")]
    [Min(0f)] public float cooldown = 0f;
}

/// <summary>Передвижение игрока. См. PlayerCharacterController.</summary>
[System.Serializable]
public class PlayerMovementSettings
{
    [Header("Скорости")]
    [Tooltip("Обычная скорость ходьбы, м/с.")]
    public float walkSpeed = 3.5f;

    [Tooltip("Скорость бега, м/с.")]
    public float sprintSpeed = 5.5f;

    [Tooltip("Время сглаживания разгона/торможения на земле, с.")]
    public float groundSmoothTime = 0.06f;

    [Tooltip("То же в воздухе — больше, чтобы управление в прыжке было ограниченным.")]
    public float airSmoothTime = 0.25f;

    [Header("Прыжок и гравитация")]
    [Tooltip("Высота прыжка в метрах (скорость считается из неё).")]
    public float jumpHeight = 1.1f;

    [Tooltip("Множитель гравитации: 1 — физически честно, но ощущается вяло.")]
    public float gravityMultiplier = 2f;

    [Tooltip("Сколько секунд после схода с края ещё засчитывается прыжок.")]
    public float coyoteTime = 0.12f;

    [Tooltip("Сколько секунд держится нажатие прыжка, сделанное чуть раньше приземления.")]
    public float jumpBuffer = 0.12f;

    [Tooltip("На какую глубину доводить игрока к опоре при потере контакта (спуск по ступеням).")]
    public float groundSnapDistance = 0.25f;

    [Tooltip("Предельная скорость соскальзывания с поверхностей круче slopeLimit, м/с.")]
    public float maxSlideSpeed = 10f;
}

/// <summary>Здоровье игрока с восстановлением, без полоски (как в Call of Duty). См. PlayerHealth.</summary>
[System.Serializable]
public class PlayerHealthSettings
{
    [Tooltip("Максимальное здоровье.")]
    [Min(1f)] public float maxHealth = 100f;

    [Tooltip("Через сколько секунд после последнего урона начинается восстановление.")]
    [Min(0f)] public float regenDelay = 4f;

    [Tooltip("Скорость восстановления, единиц здоровья в секунду.")]
    [Min(0f)] public float regenRate = 25f;
}

/// <summary>Пороги восстановления здания, на которых открываются новые возможности.</summary>
[System.Serializable]
public class RestorationProgressSettings
{
    [Tooltip("Проценты, на которых поднимается OnThresholdReached.")]
    public float[] thresholds = { 25f, 50f, 75f, 100f };
}

/// <summary>Опыт, уровни и очки способностей игрока. См. PlayerProgression.</summary>
[System.Serializable]
public class PlayerProgressionSettings
{
    [Tooltip("Опыт, необходимый для перехода с 1 на 2 уровень.")]
    [Min(1)] public int baseXPToLevel2 = 100;

    [Tooltip("Насколько растёт требуемый опыт с каждым следующим уровнем (линейно).")]
    [Min(0)] public int xpGrowthPerLevel = 50;

    [Tooltip("Сколько очков навыков даётся за каждый левелап (навыки стоят 3/5/10 очков).")]
    [Min(0)] public int unlockPointsPerLevel = 5;
}

/// <summary>Анимации полок: полёт предмета из руки в ячейку и оседание стопки (Shelf → ShelfSlot).
/// Вместимость и шаг стопки — свойство конкретной ячейки, они остаются на ShelfSlot.</summary>
[System.Serializable]
public class ShelfSettings
{
    [Tooltip("Время полёта предмета из руки на полку, секунды. 0 — мгновенно.")]
    [Min(0f)] public float placementDuration = 0.3f;

    [Tooltip("Кривая анимации полёта.")]
    public DG.Tweening.Ease placementEase = DG.Tweening.Ease.OutCubic;

    [Tooltip("Высота дуги полёта на полку, м (на коротком пути — ниже). 0 — по прямой.")]
    [Min(0f)] public float placementArcHeight = 0.12f;

    [Tooltip("За сколько секунд стопка оседает, когда из неё забрали предмет.")]
    [Min(0f)] public float settleDuration = 0.25f;
}

/// <summary>Работа руками и инструментами: тряпка и швабра по пятну, лом-рычаг (PlayerToolActions), удар
/// кувалдой (SledgehammerSwing), струя мойки (PressureWasherSpray). Прочность конкретного объекта
/// (hitPoints) — на самом Breakable.</summary>
[System.Serializable]
public class ToolSettings
{
    [Header("Тряпка")]
    [Tooltip("Насколько тряпка (и швабра) сдвигается по пятну на единицу движения мыши (Input «Mouse X/Y»), м.")]
    [Min(0.0001f)] public float ragSensitivity = 0.012f;

    [Tooltip("Радиус тряпки, м.")]
    [Min(0.01f)] public float ragRadius = 0.11f;

    [Tooltip("Сколько альфы пятна снимает один штамп тряпки в центре (штампы — каждые ¼ радиуса).")]
    [Range(0.01f, 1f)] public float ragStrength = 0.14f;

    [Tooltip("Досягаемость тряпки и швабры: пятно оттирают только в этом радиусе вокруг игрока (по горизонтали " +
             "от его ног до точки на пятне), м. Дальше — подсказка «Подойдите ближе»; в работе инструмент " +
             "за этот радиус не выходит.")]
    [Min(0.3f)] public float scrubReach = 1.6f;

    [Header("Швабра")]
    [Tooltip("Радиус головки швабры, м.")]
    [Min(0.01f)] public float mopRadius = 0.2f;

    [Tooltip("Сколько альфы снимает один штамп швабры в центре.")]
    [Range(0.01f, 1f)] public float mopStrength = 0.09f;

    [Header("Мойка высокого давления")]
    [Tooltip("Дальность струи от камеры, м.")]
    [Min(0.5f)] public float washerRange = 6f;

    [Tooltip("Радиус пятна от струи, м.")]
    [Min(0.01f)] public float washerRadius = 0.2f;

    [Tooltip("Сколько альфы снимает штамп струи, когда прицел ведут по пятну.")]
    [Range(0.01f, 1f)] public float washerStrength = 0.12f;

    [Tooltip("Как быстро струя смывает пятно, если прицел стоит на месте (доля в секунду в центре струи).")]
    [Min(0f)] public float washerHoldRate = 2.5f;

    [Header("Лом")]
    [Tooltip("Насколько рычаг сдвигается на единицу движения мыши по вертикали (весь ход — от −1 до 1).")]
    [Min(0.001f)] public float leverSensitivity = 0.08f;

    [Tooltip("Угол качания лома вокруг лапки на краю хода рычага, градусы.")]
    [Range(1f, 45f)] public float leverAngle = 14f;

    [Tooltip("Суммарный ход рычага, чтобы снять доску. Полный качок вверх-вниз — 4, упор в край ход не даёт.")]
    [Min(0.5f)] public float pryTravelToBreak = 14f;

    [Header("Кувалда")]
    [Tooltip("Урон по врагу за один удар (здоровье зомби — в EnemyData).")]
    [Min(0f)] public float sledgehammerDamage = 40f;

    [Tooltip("Дальность удара от камеры, м.")]
    [Min(0.5f)] public float sledgehammerRange = 2.2f;
}

/// <summary>Игровое время: длина часа, границы дня и ночи. См. GameClock.</summary>
[System.Serializable]
public class TimeSettings
{
    [Tooltip("Сколько реальных секунд длится игровой час. 75 — рабочий день 06:00–22:00 идёт 20 минут.")]
    [Min(1f)] public float realSecondsPerHour = 75f;

    [Tooltip("Во сколько начинается новая игра, часы (8 — 08:00 первого дня).")]
    [Range(0f, 23.9f)] public float newGameStartHour = 8f;

    [Tooltip("Утро: с этого часа идёт новый день (счётчик дней) и кончается ночь.")]
    [Range(0, 23)] public int morningHour = 6;

    [Tooltip("Вечер: с этого часа можно ложиться спать, часы в HUD показывают луну.")]
    [Range(0, 23)] public int eveningHour = 20;

    [Tooltip("Ночь: с этого часа до утра копится «сонливость» (StaminaSettings.nightFatiguePerHour).")]
    [Range(0, 23)] public int nightHour = 22;
}

/// <summary>
/// Выносливость в два слоя (PlayerStamina): бар тратится на рывки и сам восстанавливается, а усталость
/// копится за день от тяжёлой работы и от времени, запирает правую часть бара и снимается только сном
/// (еда возвращает немного, стимуляторы перекрывают временно). Числа — единицы бара при максимуме 100.
/// </summary>
[System.Serializable]
public class StaminaSettings
{
    [Header("Бар")]
    [Tooltip("Максимум выносливости без навыков.")]
    [Min(1f)] public float maxStamina = 100f;

    [Tooltip("Восстановление бара в секунду.")]
    [Min(0f)] public float regenPerSecond = 25f;

    [Tooltip("Через сколько секунд после последней траты начинается восстановление.")]
    [Min(0f)] public float regenDelay = 1f;

    [Tooltip("Одышка: бар опустел — бег, прыжок, удары и работа инструментом недоступны, пока он не " +
             "восстановится до этой доли потолка.")]
    [Range(0f, 1f)] public float windedRecoverFraction = 0.3f;

    [Tooltip("Как бы игрок ни устал, потолок бара не опускается ниже этой доли максимума.")]
    [Range(0.05f, 1f)] public float minCapFraction = 0.15f;

    [Header("Расход бара")]
    [Tooltip("Бег, в секунду (с полного бара — примерно 7 с бега).")]
    [Min(0f)] public float sprintPerSecond = 14f;

    [Tooltip("Прыжок.")]
    [Min(0f)] public float jumpCost = 12f;

    [Tooltip("Удар кувалдой (любой, в том числе мимо).")]
    [Min(0f)] public float swingCost = 18f;

    [Tooltip("Лом: за доску целиком, списывается по ходу рычага.")]
    [Min(0f)] public float pryCostPerBoard = 40f;

    [Tooltip("Тряпка и швабра: за метр пути по пятну.")]
    [Min(0f)] public float scrubCostPerMeter = 2.5f;

    [Tooltip("Удар зомби по игроку сбивает дыхание.")]
    [Min(0f)] public float hitCost = 15f;

    [Header("Усталость от работы")]
    [Tooltip("Лом: за доску целиком, по прогрессу.")]
    [Min(0f)] public float pryFatiguePerBoard = 3f;

    [Tooltip("Удар кувалдой.")]
    [Min(0f)] public float swingFatigue = 0.6f;

    [Tooltip("Пятно целиком, по прогрессу.")]
    [Min(0f)] public float scrubFatiguePerStain = 0.8f;

    [Tooltip("Кирпич, уложенный в кладку.")]
    [Min(0f)] public float brickFatigue = 1f;

    [Tooltip("Деталь, установленная в точку ремонта (запчасти генератора, мотор лифта).")]
    [Min(0f)] public float repairPartFatigue = 4f;

    [Tooltip("Тяжёлый предмет в руках (ItemData.isHeavy): усталость за каждые 20 м пути.")]
    [Min(0f)] public float heavyCarryFatiguePer20m = 1f;

    [Tooltip("Скорость ходьбы с тяжёлым предметом (множитель). Бег и прыжок с ним недоступны.")]
    [Range(0.1f, 1f)] public float heavyCarrySpeedMultiplier = 0.8f;

    [Header("Усталость от времени")]
    [Tooltip("Днём (с утра до ночи), в игровой час.")]
    [Min(0f)] public float dayFatiguePerHour = 1.5f;

    [Tooltip("Ночью — «сонливость», в игровой час. Из-за неё к полуночи вымотан любой игрок.")]
    [Min(0f)] public float nightFatiguePerHour = 25f;

    [Tooltip("Больше этого усталость не копится (по умолчанию — весь бар).")]
    [Min(1f)] public float maxFatigue = 100f;

    [Header("Пороги (по действующей усталости — за вычетом стимуляторов)")]
    [Tooltip("Сообщение «Вы устали».")]
    [Min(0f)] public float tiredThreshold = 50f;

    [Tooltip("«Вымотан»: подбирать, ставить, чинить и работать инструментами нельзя — только ходить, " +
             "стрелять, бить кувалдой врагов, есть, пить кофе и спать.")]
    [Min(0f)] public float exhaustedThreshold = 70f;

    [Header("Еда и стимуляторы")]
    [Tooltip("Сытость (0–100) убывает на столько в игровой час, в том числе во сне. Сытость — только " +
             "ограничитель: порция, которая не помещается, не съедается.")]
    [Min(0f)] public float satietyDecayPerHour = 8f;

    [Tooltip("Сытость в начале новой игры.")]
    [Range(0f, 100f)] public float newGameSatiety = 50f;

    [Tooltip("Каждый следующий стимулятор до сна слабее на эту долю.")]
    [Range(0f, 1f)] public float stimulantTolerancePerDose = 0.25f;

    [Tooltip("Слабее этой доли стимулятор не становится.")]
    [Range(0f, 1f)] public float stimulantMinStrength = 0.25f;
}

/// <summary>Сон и отключка. См. SleepService.</summary>
[System.Serializable]
public class SleepSettings
{
    [Tooltip("С какого часа можно лечь спать. Раньше — только если игрок уже вымотан.")]
    [Range(0, 23)] public int sleepFromHour = 20;

    [Tooltip("Во сколько игрок просыпается.")]
    [Range(0, 23)] public int wakeHour = 6;

    [Tooltip("Сколько часов сна снимают всю усталость (меньше — пропорционально). Проспал столько — утром " +
             "эффект «Выспался» (при подъёме в 06:00 это отбой до 22:00).")]
    [Min(0.5f)] public float hoursForFullRest = 8f;

    [Tooltip("До какого часа держатся утренние эффекты («Выспался», «Разбитость»).")]
    [Range(0, 23)] public int morningEffectsUntilHour = 12;

    [Header("Отключка")]
    [Tooltip("Во сколько игрок, так и не легший спать, отключается от усталости.")]
    [Range(0, 23)] public int passOutHour = 2;

    [Tooltip("Во сколько он приходит в себя — на матрасе.")]
    [Range(0, 23)] public int passOutWakeHour = 8;

    [Tooltip("Какая доля обычного восстановления засчитывается после отключки.")]
    [Range(0f, 1f)] public float passOutRestEfficiency = 0.5f;

    [Header("Экран сна")]
    [Tooltip("Затемнение и проявление экрана, с.")]
    [Min(0f)] public float fadeDuration = 0.8f;

    [Tooltip("Сколько секунд на экране крутятся часы до утра.")]
    [Min(0f)] public float clockSpinDuration = 2.5f;
}

/// <summary>Валюта и терминал. См. PlayerWallet.</summary>
[System.Serializable]
public class EconomySettings
{
    [Tooltip("Баланс в начале новой игры.")]
    [Min(0)] public int startingBalance = 100;

    [Tooltip("Символ валюты в интерфейсе.")]
    public string currencySymbol = "$";
}

/// <summary>
/// Единый конфиг игры: всё, что хочется крутить как баланс, лежит в одном ассете, а не
/// размазано по инспектору десятка объектов сцены.
///
/// Доставка — обычная ссылка в инспекторе у каждого потребителя (а не Resources.Load):
/// зависимость видно в инспекторе, компонент можно проверить в изоляции, и в проекте не
/// заводится папка Resources, которая целиком уезжает в билд. Каждый потребитель применяет
/// конфиг в Awake и, если ссылка не проставлена, продолжает работать на своих значениях
/// из инспектора — поэтому переход на конфиг можно делать по одному компоненту за раз.
///
/// Сюда НЕ выносится то, что осмысленно настраивать индивидуально: вместимость конкретной
/// ячейки полки, разброс обломков конкретной доски и т.п.
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Config", order = 0)]
public class GameConfig : ScriptableObject
{
    [Header("Взаимодействие")]
    [Tooltip("Дальности луча взаимодействия, подбор и бросок (PlayerItemInteraction).")]
    public InteractionSettings interaction = new InteractionSettings();

    [Header("Предмет в руке")]
    [Tooltip("Покачивание предмета в руке (EquippedItemHolder).")]
    public HeldItemSwaySettings heldItem = new HeldItemSwaySettings();

    [Header("Инвентарь уборки")]
    [Tooltip("Вместимость и прокрутка tidy-up инвентаря (InventorySystem).")]
    public TidyUpInventorySettings tidyUpInventory = new TidyUpInventorySettings();

    [Header("Управление")]
    [Tooltip("Клавиши управления.")]
    public PlayerInputKeys input = new PlayerInputKeys();

    [Header("Движение игрока")]
    [Tooltip("Скорости, прыжок и гравитация (PlayerCharacterController).")]
    public PlayerMovementSettings movement = new PlayerMovementSettings();

    [Header("Здоровье игрока")]
    [Tooltip("Здоровье и восстановление (PlayerHealth).")]
    public PlayerHealthSettings playerHealth = new PlayerHealthSettings();

    [Header("Прогресс восстановления")]
    [Tooltip("Пороги прогресса восстановления здания (BuildingRestorationTracker).")]
    public RestorationProgressSettings restoration = new RestorationProgressSettings();

    [Header("Прогрессия игрока")]
    [Tooltip("Опыт, уровни и очки способностей (PlayerProgression).")]
    public PlayerProgressionSettings progression = new PlayerProgressionSettings();

    [Header("Полки")]
    [Tooltip("Анимации полок (Shelf/ShelfSlot).")]
    public ShelfSettings shelf = new ShelfSettings();

    [Header("Видение")]
    [Tooltip("Длительность, затухание и перезарядка «видения» (PlacementVision).")]
    public VisionSettings vision = new VisionSettings();

    [Header("Инструменты")]
    [Tooltip("Тряпка, лом-рычаг и кувалда (PlayerToolActions, SledgehammerSwing).")]
    public ToolSettings tools = new ToolSettings();

    [Header("Экономика")]
    [Tooltip("Стартовый баланс и символ валюты (PlayerWallet).")]
    public EconomySettings economy = new EconomySettings();

    [Header("Время")]
    [Tooltip("Длина игрового часа, границы дня и ночи (GameClock).")]
    public TimeSettings time = new TimeSettings();

    [Header("Выносливость")]
    [Tooltip("Бар, расход, усталость, пороги, еда и стимуляторы (PlayerStamina, PlayerConsumption).")]
    public StaminaSettings stamina = new StaminaSettings();

    [Header("Сон")]
    [Tooltip("Когда можно лечь, сколько сна нужно, отключка (SleepService).")]
    public SleepSettings sleep = new SleepSettings();
}
