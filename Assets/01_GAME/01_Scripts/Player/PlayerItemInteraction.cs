using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Основная логика взаимодействия игрока с предметами:
/// - луч из камеры подсвечивает предметы в радиусе pickupRange (по умолчанию 2 м);
/// - показывает панель информации о предмете на канвасе;
/// - ЛКМ на предмете на полу — подбирает его: он летит в руку по небольшой дуге и ложится в инвентарь
///   уборки, а инструмент (ItemData.IsTool) — сразу в экипировку;
/// - E (takeKey) на предмете на полке или на голограмме над стопкой — снимает этот (верхний) предмет;
/// - при наведении на свободное место (ячейка полки, место в кладке) или на стопку, куда подходит
///   предмет в руке, — яркий "призрак", ЛКМ — ставит туда активный предмет инвентаря. ЛКМ никогда
///   не забирает с полки, поэтому серия быстрых кликов по стопке только ставит;
/// - все остальные свободные места в радиусе placementHintRadius, куда подходит предмет в руке,
///   подсвечиваются приглушёнными голограммами-подсказками, а при активном «видении» (PlacementVision)
///   — вообще все такие места сцены, сквозь стены;
/// - ЛКМ по пятну (тряпка или швабра — по размеру пятна) или по доске с ломом в руках — режим работы с
///   объектом (PlayerToolActions); пока он идёт, этот компонент выключен блокировкой ввода.
/// С оружием, кувалдой, мойкой или катушкой провода в руках взаимодействия с миром нет: ЛКМ стреляет,
/// бьёт, моет или ведёт провод (у этих инструментов свои компоненты и свои подсказки).
/// Выполняется раньше EquipmentWeaponBridge/Weapon (см. DefaultExecutionOrder), чтобы
/// IsAimingAtInteractable этого кадра успевал долететь до проверки блокировки стрельбы.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PlayerItemInteraction : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера игрока: из центра её вьюпорта идёт луч взаимодействия.")]
    public Camera playerCamera;

    [Tooltip("Tidy-up инвентарь (слот 1): сюда кладутся подобранные предметы.")]
    public InventorySystem inventory;

    [Tooltip("Панель информации о предмете под прицелом. Пусто — без подсказок.")]
    public ItemInfoUI infoUI;

    [Tooltip("Точка в руке, куда летит подобранный предмет.")]
    public EquippedItemHolder itemHolder;

    [Tooltip("Опционально: вне режима TidyUp (оружие или лом в руках) выбросить предмет из tidy-up нельзя — его не видно.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("«Видение» (PlacementVision): пока оно действует, голограммы показываются у всех подходящих " +
             "мест сцены, а не только рядом. Пусто — без видения.")]
    public PlacementVision vision;

    [Tooltip("Режим работы с объектом (тряпка по пятну, лом-рычаг): его запускает ЛКМ по пятну или доске.")]
    public PlayerToolActions toolActions;

    [Tooltip("Выносливость: вымотанный игрок не подбирает, не ставит и не снимает предметы, не чинит и не " +
             "работает инструментами — только пользуется терминалом, лифтом и матрасом. Установка кирпича или " +
             "детали утомляет. Пусто — без ограничений.")]
    public PlayerStamina stamina;

    [Tooltip("Налобный фонарик: подобранный фонарик не попадает в инвентарь, а «надевается» на игрока.")]
    public PlayerFlashlight flashlight;

    [Header("Конфиг")]
    [Tooltip("Если задан — значения ниже перекрываются из GameConfig при старте. Пусто — работаем на значениях инспектора.")]
    public GameConfig config;

    [Header("Клавиши")]
    [Tooltip("Снять предмет с полки. ЛКМ только ставит и подбирает с пола. Перекрывается " +
             "GameConfig.input.takeFromShelfKey, если задан конфиг.")]
    public KeyCode takeKey = KeyCode.E;

    [Header("Настройки луча")]
    [Tooltip("Слои, по которым бьёт луч (предметы и ячейки полок). Не из конфига: зависит от разметки слоёв сцены.")]
    public LayerMask interactableLayers = ~0;

    [Tooltip("Максимальная дистанция, на которой можно подобрать предмет.")]
    public float pickupRange = 2f;

    [Tooltip("Максимальная дистанция взаимодействия с полкой.")]
    public float shelfInteractRange = 3f;

    [Tooltip("Максимальная дистанция разбора объектов ломом (режим Tool).")]
    public float toolInteractRange = 2.5f;

    [Tooltip("Радиус, в котором свободные места под предмет в руке подсвечиваются голограммой " +
             "(место под прицелом — ярче остальных). 0 — без подсказок.")]
    [Min(0f)] public float placementHintRadius = 4f;

    [Header("Подбор и бросок")]
    [Tooltip("Скорость полёта подобранного предмета в руку, м/с (время полёта ограничено 0.18–0.4 с).")]
    public float pickupAnimationSpeed = 12f;

    [Tooltip("Высота дуги полёта предмета в руку, м (на коротком пути — ниже). 0 — по прямой.")]
    [Min(0f)] public float pickupArcHeight = 0.1f;

    [Tooltip("На каком расстоянии перед камерой появляется брошенный предмет (ближе, если мешает стена).")]
    public float dropDistance = 1.25f;

    [Tooltip("Скорость, с которой брошенный предмет улетает вперёд.")]
    public float dropSpeed = 4f;

    [Tooltip("Что не даёт бросить предмет сквозь себя (стены, пол, полки). Игрока, инструмент в руке и " +
             "лежащие предметы не включать.")]
    public LayerMask dropObstacleLayers = ~((1 << 8) | (1 << 9) | (1 << 24) | (1 << 30));

    [Header("Звуки")]
    [Tooltip("Клик по объекту использования (терминал, кнопка лифта, коробка отменённого заказа).")]
    public SoundCue interactSound;

    private static readonly Collider[] HintHits = new Collider[64];
    private readonly HashSet<IPlaceableSlot> hintedSlots = new HashSet<IPlaceableSlot>();
    private readonly HashSet<IPlaceableSlot> nextHints = new HashSet<IPlaceableSlot>();
    private readonly HashSet<IPlaceableSlot> nearHints = new HashSet<IPlaceableSlot>();

    private const float MinPickupDuration = 0.18f;
    private const float MaxPickupDuration = 0.4f;
    private const float DropProbeRadius = 0.15f;
    private const float DropWallPadding = 0.1f;
    private static readonly RaycastHit[] RayHits = new RaycastHit[16];

    private WorldItem currentHighlighted;   // предмет на полу — ЛКМ подбирает
    private WorldItem currentTakeTarget;    // предмет на полке — E снимает
    private IPlaceableSlot currentHoveredSlot;
    private CleanableStain currentHoveredStain;
    private Breakable currentHoveredBreakable;
    private IInteractable currentInteractable; // терминал, кнопка лифта, коробка отменённого заказа — ЛКМ использует
    private Vector3 currentHoverPoint;      // точка под прицелом для пятна/доски/объекта использования
    private Ray currentAimRay;
    private WorldItem itemBeingPickedUp;
    private Vector3 pickupStartPosition;
    private Quaternion pickupStartRotation;
    private float pickupElapsed;
    private float pickupDuration;
    private float pickupArc;
    private bool aimingAtInteractableThisFrame;

    /// <summary>Прицел был наведён на предмет, точку установки (полка/ремонт) или пятно в момент
    /// луча этого кадра — по ним нельзя стрелять (см. EquipmentWeaponBridge). Снимок делается сразу
    /// после HandleRaycast, ДО обработки клика — сам подбор (PickUpWorldItem) обнуляет
    /// currentHighlighted при успехе, и если читать его позже, блокировка стрельбы снималась бы
    /// в тот же кадр, что и клик.</summary>
    public bool IsAimingAtInteractable => aimingAtInteractableThisFrame;

    /// <summary>
    /// Мгновенно досадить в инвентарь предмет, который сейчас летит в руку (UpdatePickupAnimation):
    /// в полёте он не принадлежит ни одному контейнеру — ни инвентарю, ни миру — и в сейв не попал бы.
    /// Зовётся перед сохранением.
    /// </summary>
    public void FinishPendingPickup()
    {
        if (itemBeingPickedUp == null) return;

        itemBeingPickedUp.transform.localPosition = GetPickupTargetLocalPosition(itemBeingPickedUp);
        itemBeingPickedUp.transform.localRotation = Quaternion.Euler(itemBeingPickedUp.itemData.handRotationOffset);
        CompletePickup();
    }

    /// <summary>
    /// Бросить предмет перед игроком. Точка появления — dropDistance перед камерой, но не дальше
    /// ближайшей стены: раньше предмет, брошенный лицом в стену, появлялся внутри неё или за ней.
    /// </summary>
    public void DropInFront(WorldItem item)
    {
        if (item == null) return;
        if (playerCamera == null)
        {
            item.Drop(item.transform.position, item.transform.rotation, Vector3.zero);
            return;
        }

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward.normalized;
        float distance = dropDistance;
        if (Physics.SphereCast(origin, DropProbeRadius, direction, out RaycastHit hit, dropDistance, dropObstacleLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - DropWallPadding);

        item.Drop(origin + direction * distance, playerCamera.transform.rotation, direction * dropSpeed);
    }

    private void Awake()
    {
        if (config == null) return;

        InteractionSettings s = config.interaction;
        pickupRange = s.pickupRange;
        shelfInteractRange = s.shelfInteractRange;
        toolInteractRange = s.toolInteractRange;
        takeKey = config.input.takeFromShelfKey;
        placementHintRadius = s.placementHintRadius;
        pickupAnimationSpeed = s.pickupAnimationSpeed;
        pickupArcHeight = s.pickupArcHeight;
        dropDistance = s.dropDistance;
        dropSpeed = s.dropSpeed;
    }

    private void Update()
    {
        HandleRaycast();
        aimingAtInteractableThisFrame = currentHighlighted != null || currentTakeTarget != null
                                        || currentHoveredSlot != null || currentHoveredStain != null
                                        || currentInteractable != null;

        UpdatePickupAnimation();

        // Клик, которым игрок возвращает захват курсора (CursorLockController, он выполняется позже
        // в кадре), не должен тут же подбирать или бросать предмет. ПКМ, которой игрок только что вышел
        // из режима работы с объектом, — тоже.
        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked
                            && (toolActions == null || (!toolActions.IsActive && toolActions.LastEndFrame != Time.frameCount));
        if (cursorLocked && itemBeingPickedUp == null && Input.GetMouseButtonDown(0))
            HandleClick();
        if (cursorLocked && itemBeingPickedUp == null && Input.GetMouseButtonDown(1))
            DropActiveItem();
        if (cursorLocked && itemBeingPickedUp == null && currentTakeTarget != null && Input.GetKeyDown(takeKey))
            PickUpWorldItem(currentTakeTarget);
    }

    /// <summary>Какой предмет сейчас обводится: подбираемый с пола или снимаемый с полки.</summary>
    private WorldItem HighlightedItem => currentHighlighted != null ? currentHighlighted : currentTakeTarget;

    /// <summary>
    /// Луч этого кадра. Цели наведения (предмет/точка установки/пятно/Breakable) сначала собираются
    /// заново в ResolveHoverTargets, и только потом подсветка/призрак выключаются у тех, кто перестал
    /// быть целью. Раньше всё гасилось и тут же включалось обратно КАЖДЫЙ кадр — у предмета это
    /// выключало/включало AdvancedOutline, а тот на каждое включение клонирует меши и считает
    /// сглаженные нормали (11 мешей в кадр на M16).
    /// </summary>
    private void HandleRaycast()
    {
        WorldItem previousItem = HighlightedItem;
        IPlaceableSlot previousSlot = currentHoveredSlot;
        Breakable previousBreakable = currentHoveredBreakable;

        currentHighlighted = null;
        currentTakeTarget = null;
        currentHoveredSlot = null;
        currentHoveredStain = null;
        currentHoveredBreakable = null;
        currentInteractable = null;

        ResolveHoverTargets();
        UpdatePlacementHints();

        WorldItem highlighted = HighlightedItem;
        if (previousItem != null && previousItem != highlighted) previousItem.SetHighlight(false);
        if (highlighted != null) highlighted.SetHighlight(true);
        if (previousSlot != null && previousSlot != currentHoveredSlot && !hintedSlots.Contains(previousSlot)) previousSlot.HideGhost();
        if (previousBreakable != null && previousBreakable != currentHoveredBreakable) previousBreakable.SetHighlight(false);
    }

    private void OnDisable()
    {
        if (HighlightedItem != null) HighlightedItem.SetHighlight(false);
        if (currentHoveredSlot != null) currentHoveredSlot.HideGhost();
        if (currentHoveredBreakable != null) currentHoveredBreakable.SetHighlight(false);
        foreach (var slot in hintedSlots)
            if (slot as Object != null) slot.HideGhost();
        hintedSlots.Clear();
        if (infoUI != null) infoUI.Hide();
        currentHighlighted = null;
        currentTakeTarget = null;
        currentHoveredSlot = null;
        currentHoveredStain = null;
        currentHoveredBreakable = null;
        currentInteractable = null;
    }

    /// <summary>Находит цели наведения этого кадра и включает им подсветку/призрак (повторное
    /// включение той же цели — no-op внутри SetHighlight/ShowGhost).</summary>
    private void ResolveHoverTargets()
    {
        if (playerCamera == null) return;

        // ── Экипировано оружие или инструмент со своей ЛКМ (кувалда, мойка, катушка): никакого
        // взаимодействия с миром — иначе прицел на предмете/полке блокировал бы выстрел (см.
        // IsAimingAtInteractable), а ЛКМ кувалды и подбирала бы, и била. Подбор/установка — только без
        // оружия (TidyUp) или с ломом/шваброй (см. ниже).
        if (IsExclusiveToolEquipped())
        {
            if (infoUI != null) infoUI.Hide();
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        currentAimRay = ray;

        // ── Вымотан: работать нельзя — только объекты использования (терминал, лифт, матрас) ──
        if (TooTiredToWork)
        {
            ResolveTiredHover(ray);
            return;
        }

        // ── Лом в руках: ищем, что можно поддеть, но НЕ прерываем обычную логику ниже — лом не
        // мешает подбирать предметы и расставлять их по полкам, это просто дополнительный инструмент.
        if (modeController != null && modeController.ActiveToolKind == ToolKind.Crowbar)
            HandleToolRaycast(ray);

        float maxDist = Mathf.Max(pickupRange, shelfInteractRange);

        if (!TryRaycastInteractable(ray, maxDist, out RaycastHit hit))
        {
            ShowIdlePrompt();
            return;
        }

        ItemData active = inventory != null ? inventory.GetActiveItem() : null;

        // ── Шаг 1. Резолвим, на ЧТО смотрим: предмет и/или точка установки (полка/ремонт) ──
        WorldItem hitItem = hit.collider.GetComponentInParent<WorldItem>();
        IPlaceableSlot slot = hit.collider.GetComponent<IPlaceableSlot>();
        bool inShelfRange = hit.distance <= shelfInteractRange;

        // ── Шаг 2. ПОЛКА: E снимает, ЛКМ ставит ──
        // Предмет на полке: E — снять именно его (остальные в стопке осядут); ЛКМ — поставить наверх его
        // стопки, если предмет в руке подходит. Голограмма над стопкой (триггер свободного места):
        // ЛКМ — поставить, E — снять верхний предмет под ней.
        if (inShelfRange && hitItem != null && hitItem.State == WorldItem.ItemState.PlacedOnShelf)
        {
            currentTakeTarget = hitItem;
            if (slot == null) slot = hitItem.GetSourceSlot();
        }
        else if (inShelfRange && slot is ShelfSlot shelfSlot)
        {
            currentTakeTarget = shelfSlot.TopItem;
        }

        // Свободное место — триггер ячейки полки (у стопки — над её верхом) или места в кладке открытого
        // ряда. CanAccept сам проверит категорию, заполненность, тип стопки, ряд кладки.
        if (slot != null && active != null && inShelfRange && slot.CanAccept(active))
        {
            slot.ShowGhost(active, GhostMode.Focused);
            currentHoveredSlot = slot;
        }

        if (currentHoveredSlot != null || currentTakeTarget != null)
        {
            ShowShelfActions(active);
            return;
        }

        // ── Шаг 2.5. ОБЪЕКТ ДЛЯ ИСПОЛЬЗОВАНИЯ: терминал, кнопка лифта, коробка отменённого заказа ──
        // Стоит до подбора: коробку отменённого заказа ЛКМ разбирает, а не поднимает. Если объект сейчас
        // нельзя использовать (нет питания, лифт сломан), подсказка объясняет почему, а клик уходит
        // дальше — у коробки в обычный подбор.
        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable != null && inShelfRange && (interactable.CanInteract || hitItem == null))
        {
            if (interactable.CanInteract) currentInteractable = interactable;
            currentHoverPoint = hit.point;
            if (infoUI != null) infoUI.ShowHint(interactable.InteractTitle, interactable.InteractHint);
            return;
        }

        // ── Шаг 3. ПОДБОР С ПОЛА: ЛКМ по лежащему предмету ──
        // Опыт за повторную установку не начисляется — флаг WorldItem.PlacementRewarded остаётся на предмете.
        if (hitItem != null && hitItem.State == WorldItem.ItemState.InWorld && hit.distance <= pickupRange)
        {
            currentHighlighted = hitItem;
            if (infoUI != null) infoUI.ShowActions(hitItem.itemData, Loc.Get("hud.action.pickup"));
            return;
        }

        // ── Шаг 3.5. ПЯТНО: ЛКМ — режим тряпки или швабры (PlayerToolActions); если пятно этим не отмыть
        // (размер), подсказка говорит, какой инструмент нужен, а клик ничего не делает ──
        CleanableStain stain = hit.collider.GetComponentInParent<CleanableStain>();
        if (stain != null && !stain.IsClean && hit.distance <= pickupRange)
        {
            ToolKind scrubTool = toolActions != null ? toolActions.CurrentScrubTool : ToolKind.None;
            string missingKey = stain.MissingToolKey(scrubTool);
            // Пятно далеко от игрока — сначала подойти (досягаемость — PlayerToolActions.scrubReach).
            if (missingKey == null && toolActions != null && stain.Raycast(ray, out Vector3 aimOnStain)
                && !toolActions.IsWithinReach(aimOnStain))
                missingKey = "hud.stain.too_far";
            if (missingKey == null) currentHoveredStain = stain;
            if (infoUI != null) infoUI.ShowHint(Loc.Get(stain.titleKey), Loc.Get(missingKey ?? "hud.action.scrub"));
            return;
        }

        // ── Шаг 4. Ничего интересного под лучом (кроме, может быть, доски для лома) ──
        ShowIdlePrompt();
    }

    /// <summary>Под лучом нет ни предмета, ни места, ни пятна: подсказка лома, если он смотрит на доску,
    /// иначе подсказка гаснет.</summary>
    private void ShowIdlePrompt()
    {
        if (infoUI == null) return;
        if (currentHoveredBreakable != null)
            infoUI.ShowHint(Loc.Get(currentHoveredBreakable.titleKey), Loc.Get("hud.action.pry"));
        else
            infoUI.Hide();
    }

    /// <summary>Игрок вымотан (PlayerStamina.IsExhausted): подбор, установка и работа недоступны.</summary>
    private bool TooTiredToWork => stamina != null && stamina.IsExhausted;

    /// <summary>
    /// Наведение, когда игрок вымотан: объект использования работает как обычно (терминал, лифт, матрас —
    /// спать-то и надо), а на предмете, полке, месте ремонта или пятне — только подсказка, что сил нет.
    /// </summary>
    private void ResolveTiredHover(Ray ray)
    {
        float maxDist = Mathf.Max(pickupRange, shelfInteractRange);
        if (!TryRaycastInteractable(ray, maxDist, out RaycastHit hit))
        {
            if (infoUI != null) infoUI.Hide();
            return;
        }

        bool inShelfRange = hit.distance <= shelfInteractRange;
        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable != null && inShelfRange)
        {
            if (interactable.CanInteract) currentInteractable = interactable;
            currentHoverPoint = hit.point;
            if (infoUI != null) infoUI.ShowHint(interactable.InteractTitle, interactable.InteractHint);
            return;
        }

        if (infoUI == null) return;
        WorldItem hitItem = hit.collider.GetComponentInParent<WorldItem>();
        CleanableStain stain = hit.collider.GetComponentInParent<CleanableStain>();
        bool workTarget = hitItem != null || hit.collider.GetComponent<IPlaceableSlot>() != null
                          || (stain != null && !stain.IsClean);
        if (!workTarget)
        {
            infoUI.Hide();
            return;
        }

        string tired = Loc.Get("hud.stamina.too_tired");
        if (hitItem != null && hitItem.itemData != null) infoUI.ShowActions(hitItem.itemData, tired);
        else if (stain != null) infoUI.ShowHint(Loc.Get(stain.titleKey), tired);
        else infoUI.ShowHint(Loc.Get("hud.stamina.exhausted"), tired);
    }

    /// <summary>В руках оружие или инструмент со своей ЛКМ (кувалда, мойка, катушка): мир не трогаем.</summary>
    private bool IsExclusiveToolEquipped()
    {
        if (modeController == null) return false;
        if (modeController.IsWeaponEquipped()) return true;
        ToolKind tool = modeController.ActiveToolKind;
        return tool == ToolKind.Sledgehammer || tool == ToolKind.PressureWasher || tool == ToolKind.WireSpool;
    }

    /// <summary>Подпись у прицела для полки: «ЛКМ — поставить», «E — взять» или обе сразу.</summary>
    private void ShowShelfActions(ItemData active)
    {
        if (infoUI == null) return;

        string actions = currentHoveredSlot != null ? Loc.Get("hud.action.place") : "";
        if (currentTakeTarget != null)
            actions += (actions.Length > 0 ? ", " : "") + Loc.Get("hud.action.take", takeKey);

        infoUI.ShowActions(currentTakeTarget != null ? currentTakeTarget.itemData : active, actions);
    }

    /// <summary>
    /// Голограммы-подсказки: все места в радиусе placementHintRadius, куда можно поставить предмет из
    /// руки прямо сейчас (свободные ячейки подходящих полок, пустые места открытого ряда кладки).
    /// Место под прицелом (currentHoveredSlot) остаётся ярким — его ведёт ResolveHoverTargets.
    /// Места, выпавшие из набора, гасятся; ShowGhost идемпотентен и дёшев — призрак переиспользуется.
    /// Пока действует «видение» (PlacementVision), к ним добавляются все подходящие места сцены: ближние
    /// остаются обычными подсказками (обычной яркости, не гаснут), дальние — GhostMode.Vision (сквозь
    /// стены, гаснут вместе с видением).
    /// </summary>
    private void UpdatePlacementHints()
    {
        nextHints.Clear();
        nearHints.Clear();

        ItemData active = inventory != null ? inventory.GetActiveItem() : null;
        bool canHint = active != null && playerCamera != null && placementHintRadius > 0f && !IsExclusiveToolEquipped()
                       && !TooTiredToWork;
        if (canHint)
        {
            int count = Physics.OverlapSphereNonAlloc(playerCamera.transform.position, placementHintRadius,
                                                      HintHits, interactableLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                IPlaceableSlot slot = HintHits[i].GetComponent<IPlaceableSlot>();
                if (slot != null && slot.CanAccept(active)) nearHints.Add(slot);
            }
            nextHints.UnionWith(nearHints);

            if (vision != null && vision.IsActive)
                foreach (var slot in vision.SceneSlots)
                    if (slot as Object != null && slot.CanAccept(active)) nextHints.Add(slot);
        }

        foreach (var slot in hintedSlots)
            if (!nextHints.Contains(slot) && slot != currentHoveredSlot && slot as Object != null)
                slot.HideGhost();

        foreach (var slot in nextHints)
            if (slot != currentHoveredSlot)
                slot.ShowGhost(active, nearHints.Contains(slot) ? GhostMode.Hint : GhostMode.Vision);

        hintedSlots.Clear();
        hintedSlots.UnionWith(nextHints);
    }

    /// <summary>Дополнительный луч, пока в руках лом: ищет Breakable, который можно поддеть. Вызывается
    /// перед обычной логикой подбора/установки (не вместо неё) — см. HandleRaycast.</summary>
    private void HandleToolRaycast(Ray ray)
    {
        if (!TryRaycastInteractable(ray, toolInteractRange, out RaycastHit hit))
            return;

        Breakable breakable = hit.collider.GetComponentInParent<Breakable>();
        if (breakable == null || breakable.IsBroken || !breakable.canPry) return;

        currentHoveredBreakable = breakable;
        currentHoverPoint = hit.point;
        breakable.SetHighlight(true);
    }

    /// <summary>
    /// Ближайшее попадание луча, которое имеет смысл для взаимодействия: сплошной коллайдер (он же
    /// загораживает то, что за ним) или триггер, который сам является целью (ячейка полки, точка
    /// ремонта, пятно, Breakable, предмет). Раньше одиночный Raycast по триггерам останавливался на
    /// первом попавшемся — например, на объёме DeliveryZone, — и предмет за ним нельзя было подобрать.
    /// </summary>
    private bool TryRaycastInteractable(Ray ray, float maxDistance, out RaycastHit result)
    {
        result = default;
        float best = float.MaxValue;
        bool found = false;

        int count = Physics.RaycastNonAlloc(ray, RayHits, maxDistance, interactableLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = RayHits[i];
            if (hit.distance >= best) continue;
            if (hit.collider.isTrigger && !IsInteractableTrigger(hit.collider)) continue;

            best = hit.distance;
            result = hit;
            found = true;
        }
        return found;
    }

    private static bool IsInteractableTrigger(Collider collider)
    {
        if (collider.GetComponent<IPlaceableSlot>() != null) return true;
        if (collider.GetComponentInParent<WorldItem>() != null) return true;
        if (collider.GetComponentInParent<IInteractable>() != null) return true;

        CleanableStain stain = collider.GetComponentInParent<CleanableStain>();
        if (stain != null && !stain.IsClean) return true;

        Breakable breakable = collider.GetComponentInParent<Breakable>();
        return breakable != null && !breakable.IsBroken;
    }

    private void HandleClick()
    {
        if (currentInteractable != null)
        {
            SoundPlayer.Play(interactSound, currentHoverPoint);
            currentInteractable.Interact();
            currentInteractable = null;
            return;
        }

        if (currentHighlighted != null)
        {
            PickUpWorldItem(currentHighlighted);
            return;
        }

        if (currentHoveredSlot != null)
        {
            PlaceActiveItem(currentHoveredSlot);
            return;
        }

        if (currentHoveredStain != null)
        {
            if (toolActions != null) toolActions.TryBeginScrub(currentHoveredStain, currentAimRay);
            return;
        }

        if (currentHoveredBreakable != null && toolActions != null)
            toolActions.TryBeginPry(currentHoveredBreakable, currentHoverPoint);
    }

    private void PickUpWorldItem(WorldItem worldItem)
    {
        if (inventory == null || itemHolder == null || itemHolder.HeldItemTransform == null) return;
        // Инструмент уходит в экипировку — вместимость инвентаря уборки на него не распространяется.
        bool isTool = worldItem.itemData != null && modeController != null && worldItem.itemData.IsTool;
        // Фонарик тоже мимо инвентаря уборки — вместимость на него не распространяется.
        if (worldItem.itemData != null && worldItem.itemData.IsFlashlight && flashlight != null) isTool = true;
        if (!isTool && !inventory.CanAddWorldItem(worldItem)) return;

        ShelfSlot source = worldItem.GetSourceSlot();
        if (source != null)
            source.RemoveItem(worldItem);

        worldItem.BeginPickup();
        // Интерполируем в локальных координатах руки: предмет следует за игроком во время анимации
        // и не отстаёт от движущейся камеры.
        worldItem.transform.SetParent(itemHolder.HeldItemTransform, true);
        itemBeingPickedUp = worldItem;
        pickupStartPosition = worldItem.transform.localPosition;
        pickupStartRotation = worldItem.transform.localRotation;
        pickupElapsed = 0f;

        float distance = Vector3.Distance(pickupStartPosition, GetPickupTargetLocalPosition(worldItem));
        pickupDuration = Mathf.Clamp(distance / Mathf.Max(0.01f, pickupAnimationSpeed), MinPickupDuration, MaxPickupDuration);
        pickupArc = pickupArcHeight * Mathf.Clamp01(distance);

        currentHighlighted = null;
        currentTakeTarget = null;
        if (infoUI != null) infoUI.Hide();
    }

    private void PlaceActiveItem(IPlaceableSlot slot)
    {
        if (inventory == null) return;

        ItemData active = inventory.GetActiveItem();
        if (active == null || !slot.CanAccept(active)) return;

        WorldItem item = inventory.RemoveActiveWorldItem();
        if (item == null) return;
        slot.PlaceItem(item);

        // Ремонт — тяжёлая работа: кирпич в кладку и деталь в точку ремонта утомляют (полки — нет).
        if (stamina != null)
        {
            if (slot is RepairSlot) stamina.WorkBrick();
            else if (slot is RepairPoint) stamina.WorkRepairPart();
        }

        currentHoveredSlot = null;
        if (infoUI != null) infoUI.Hide();
    }

    /// <summary>
    /// Полёт подобранного предмета в руку: по небольшой дуге (в осях руки — вверх), с замедлением в
    /// конце; цель — СРАЗУ финальная позиция (для стакающихся предметов — свой «этаж» стопки), она
    /// пересчитывается каждый кадр.
    /// </summary>
    private void UpdatePickupAnimation()
    {
        if (itemBeingPickedUp == null) return;

        pickupElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(pickupElapsed / pickupDuration);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);

        Vector3 targetLocalPosition = GetPickupTargetLocalPosition(itemBeingPickedUp);
        Quaternion targetLocalRotation = Quaternion.Euler(itemBeingPickedUp.itemData.handRotationOffset);

        itemBeingPickedUp.transform.localPosition = Vector3.LerpUnclamped(pickupStartPosition, targetLocalPosition, eased)
                                                    + Vector3.up * (pickupArc * Mathf.Sin(eased * Mathf.PI));
        itemBeingPickedUp.transform.localRotation = Quaternion.SlerpUnclamped(pickupStartRotation, targetLocalRotation, eased);
        if (t < 1f) return;

        itemBeingPickedUp.transform.localPosition = targetLocalPosition;
        CompletePickup();
    }

    /// <summary>Предмет долетел до руки: звук, рука чуть «ловит» его, предмет — в инвентарь, а если места
    /// уже нет — на пол перед игроком.</summary>
    private void CompletePickup()
    {
        WorldItem item = itemBeingPickedUp;
        itemBeingPickedUp = null;

        if (item.itemData != null) SoundPlayer.Play(item.itemData.pickupSound, item.transform.position);
        if (itemHolder != null) itemHolder.PlayCatchDip();

        // Фонарик — постоянный предмет вне инвентарей: игрок его «надевает», сам предмет исчезает.
        if (item.itemData != null && item.itemData.IsFlashlight && flashlight != null)
        {
            flashlight.Acquire();
            Destroy(item.gameObject);
            return;
        }

        // Инструмент — сразу в экипировку (слот 2), без Q.
        if (item.itemData != null && item.itemData.IsTool && modeController != null && modeController.AddToEquipment(item))
            return;
        if (!inventory.AddWorldItem(item))
            DropInFront(item);
    }
    
    /// <summary>
    /// Локальная цель полёта предмета в руку.
    /// Для визуально стакающихся (showAsVisualStack) — сразу «этаж» стопки,
    /// чтобы второй и последующие предметы летели наверх стопки, минуя базовую точку.
    /// Формула совпадает с EquippedItemHolder.RefreshCurrent — поэтому приземление происходит без щёлчка.
    /// </summary>
    private Vector3 GetPickupTargetLocalPosition(WorldItem item)
    {
        ItemData data = item.itemData;
        Vector3 baseOffset = data.handPositionOffset;

        if (!data.showAsVisualStack || inventory == null)
            return baseOffset;

        // При AddWorldItem предмет станет последним в entry.instances,
        // значит его будущий индекс = текущее число таких же предметов уже в руке.
        InventoryEntry entry = inventory.entries.Find(candidate => candidate.item == data);
        int stackIndex = entry != null ? entry.instances.Count : 0;

        return baseOffset + Vector3.up * (data.heldStackSpacing * stackIndex);
    }

    private void DropActiveItem()
    {
        if (inventory == null || playerCamera == null) return;
        // Вне TidyUp-режима предмет и так не виден в руке (оружие или лом) — бросать его вслепую нельзя.
        if (modeController != null && modeController.CurrentMode != PlayerInventoryModeController.InventoryMode.TidyUp) return;
        WorldItem item = inventory.RemoveActiveWorldItem();
        if (item == null) return;
        DropInFront(item);
        if (infoUI != null) infoUI.Hide();
    }
}
