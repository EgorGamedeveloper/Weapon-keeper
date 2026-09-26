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

    [Tooltip("Скорость перелёта предмета в руку при подборе.")]
    public float pickupAnimationSpeed = 12f;

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

    [Tooltip("За сколько секунд стопка оседает, когда из неё забрали предмет.")]
    [Min(0f)] public float settleDuration = 0.25f;
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

    [Header("Экономика")]
    [Tooltip("Стартовый баланс и символ валюты (PlayerWallet).")]
    public EconomySettings economy = new EconomySettings();
}
