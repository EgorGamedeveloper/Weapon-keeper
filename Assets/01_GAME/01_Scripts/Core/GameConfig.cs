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
    public float swayAmount = 4f;
    public float swaySmooth = 6f;
    public float maxSwayAngle = 8f;

    [Header("При ходьбе")]
    public float bobFrequency = 6f;
    public float bobAmount = 0.03f;
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

    [Tooltip("Убрать/достать оружие (WeaponHolster).")]
    public KeyCode holsterKey = KeyCode.H;

    [Tooltip("Прыжок.")]
    public KeyCode jumpKey = KeyCode.Space;

    [Tooltip("Бег (удержание).")]
    public KeyCode sprintKey = KeyCode.LeftShift;
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

    [Tooltip("Сколько очков способностей (unlock points) даётся за каждый левелап.")]
    [Min(0)] public int unlockPointsPerLevel = 1;

    [Tooltip("Опыт за одну единицу товара, расставленную по полке (см. ShelvingProgressTracker).")]
    [Min(0)] public int xpPerShelvedUnit = 2;
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
    public InteractionSettings interaction = new InteractionSettings();

    [Header("Предмет в руке")]
    public HeldItemSwaySettings heldItem = new HeldItemSwaySettings();

    [Header("Инвентарь уборки")]
    public TidyUpInventorySettings tidyUpInventory = new TidyUpInventorySettings();

    [Header("Управление")]
    public PlayerInputKeys input = new PlayerInputKeys();

    [Header("Движение игрока")]
    public PlayerMovementSettings movement = new PlayerMovementSettings();

    [Header("Прогресс восстановления")]
    public RestorationProgressSettings restoration = new RestorationProgressSettings();

    [Header("Прогрессия игрока")]
    public PlayerProgressionSettings progression = new PlayerProgressionSettings();
}
