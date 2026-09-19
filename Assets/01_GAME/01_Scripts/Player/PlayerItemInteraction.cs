using UnityEngine;

/// <summary>
/// Основная логика взаимодействия игрока с предметами:
/// - луч из камеры подсвечивает предметы в радиусе pickupRange (по умолчанию 2 м);
/// - показывает панель информации о предмете на канвасе;
/// - ЛКМ на предмете — подбирает его в инвентарь;
/// - при наведении на пустую ячейку полки (держа подходящий предмет) — показывает "призрак";
/// - ЛКМ на ячейке полки — устанавливает активный предмет инвентаря на полку.
/// Выполняется раньше EquipmentWeaponBridge/Weapon (см. DefaultExecutionOrder), чтобы
/// IsAimingAtInteractable этого кадра успевал долететь до проверки блокировки стрельбы.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PlayerItemInteraction : MonoBehaviour
{
    [Header("Ссылки")]
    public Camera playerCamera;
    public InventorySystem inventory;
    public ItemInfoUI infoUI;
    public EquippedItemHolder itemHolder;

    [Tooltip("Опционально: вне режима TidyUp (оружие или лом в руках) выбросить предмет из tidy-up нельзя — его не видно.")]
    public PlayerInventoryModeController modeController;

    [Header("Настройки луча")]
    [Tooltip("Слои, по которым бьёт луч (предметы и ячейки полок).")]
    public LayerMask interactableLayers = ~0;

    [Tooltip("Максимальная дистанция, на которой можно подобрать предмет.")]
    public float pickupRange = 2f;

    [Tooltip("Максимальная дистанция взаимодействия с полкой.")]
    public float shelfInteractRange = 3f;

    [Tooltip("Максимальная дистанция разбора объектов ломом (режим Tool).")]
    public float toolInteractRange = 2.5f;

    [Header("Подбор и бросок")]
    public float pickupAnimationSpeed = 12f;
    public float dropDistance = 1.25f;
    public float dropSpeed = 4f;

    private WorldItem currentHighlighted;
    private IPlaceableSlot currentHoveredSlot;
    private CleanableStain currentHoveredStain;
    private Breakable currentHoveredBreakable;
    private WorldItem itemBeingPickedUp;
    private Vector3 pickupVelocity;
    private bool aimingAtInteractableThisFrame;

    /// <summary>Прицел был наведён на предмет, точку установки (полка/ремонт) или пятно в момент
    /// луча этого кадра — по ним нельзя стрелять (см. EquipmentWeaponBridge). Снимок делается сразу
    /// после HandleRaycast, ДО обработки клика — сам подбор (PickUpWorldItem) обнуляет
    /// currentHighlighted при успехе, и если читать его позже, блокировка стрельбы снималась бы
    /// в тот же кадр, что и клик.</summary>
    public bool IsAimingAtInteractable => aimingAtInteractableThisFrame;

    private void Update()
    {
        HandleRaycast();
        aimingAtInteractableThisFrame = currentHighlighted != null || currentHoveredSlot != null || currentHoveredStain != null;

        UpdatePickupAnimation();

        if (itemBeingPickedUp == null && Input.GetMouseButtonDown(0))
            HandleClick();
        if (itemBeingPickedUp == null && Input.GetMouseButtonDown(1))
            DropActiveItem();
    }
    
        
       private void HandleRaycast()
{
    ClearHighlight();
    ClearGhost();

    if (playerCamera == null) return;

    // ── Экипировано оружие: только бой, никакого взаимодействия с миром — иначе прицел на
    // предмете/полке блокировал бы выстрел (см. IsAimingAtInteractable). С оружием в руках
    // игрок либо стреляет, либо ничего не делает; подбор/установка — только без оружия (TidyUp)
    // или с ломом (см. ниже).
    if (modeController != null && modeController.IsWeaponEquipped()) return;

    Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

    // ── Лом в руках: ищем, что можно разобрать, но НЕ прерываем обычную логику ниже — лом не
    // мешает подбирать предметы и расставлять их по полкам, это просто дополнительный инструмент.
    if (modeController != null && modeController.IsBreakToolEquipped())
        HandleToolRaycast(ray);

    float maxDist = Mathf.Max(pickupRange, shelfInteractRange);

    if (!Physics.Raycast(ray, out RaycastHit hit, maxDist, interactableLayers, QueryTriggerInteraction.Collide))
    {
        if (infoUI != null) infoUI.Hide();
        return;
    }

    ItemData active = inventory != null ? inventory.GetActiveItem() : null;

    // ── Шаг 1. Резолвим, на ЧТО смотрим: предмет и/или точка установки (полка/ремонт) ──
    WorldItem hitItem = hit.collider.GetComponentInParent<WorldItem>();
    IPlaceableSlot slot = hit.collider.GetComponent<IPlaceableSlot>();

    // Луч попал в предмет, который уже стоит на полке (в стопке) —
    // нас интересует ЕГО колонна, а не он сам как точка установки.
    bool hitPlacedItem = hitItem != null && hitItem.State == WorldItem.ItemState.PlacedOnShelf;
    if (hitPlacedItem && slot == null)
        slot = hitItem.GetSourceSlot();

    // ── Шаг 2. РЕЖИМ УСТАНОВКИ (приоритет): в руке есть предмет и точка готова его принять ──
    // Сюда попадаем в трёх случаях: пустая колонна, неполная стопка (даже если луч задел
    // стоящую пачку), одиночный пустой слот. CanAccept сам проверит всё: категорию,
    // заполненность, совпадение типа со стопкой. Для RepairPoint — совпадение с requiredItem.
    if (slot != null && active != null
        && hit.distance <= shelfInteractRange
        && slot.CanAccept(active))
    {
        slot.ShowGhost(active); // призрак рисуется наверху стопки через GetNextPlacementLocalPosition()
        currentHoveredSlot = slot;
        if (infoUI != null) infoUI.ShowPlacementHint(active, true);
        return;
    }

    // ── Шаг 3. РЕЖИМ ПОДБОРА: луч попал в предмет — в мире ИЛИ в стопке на полке ──
    // Срабатывает, когда руки пустые (active == null) или активный предмет этой колонне
    // не подходит. Подсвечиваем именно тот предмет, в который смотрим: клик заберёт его
    // из стопки (верхний, средний — любой), остальные «оседают».
    if (hitItem != null && hit.distance <= pickupRange)
    {
        currentHighlighted = hitItem;
        hitItem.SetHighlight(true);
        if (infoUI != null) infoUI.Show(hitItem.itemData);
        return;
    }

    // ── Шаг 3.5. ПЯТНО: оттирается кликом, предмет в руках не нужен ──
    CleanableStain stain = hit.collider.GetComponentInParent<CleanableStain>();
    if (stain != null && !stain.IsClean && hit.distance <= pickupRange)
    {
        currentHoveredStain = stain;
        stain.SetHighlight(true);
        return;
    }

    // ── Шаг 4. Ничего интересного под лучом ──
    if (infoUI != null) infoUI.Hide();

    }

    /// <summary>Дополнительный луч, пока в руках лом: ищет Breakable. Вызывается перед обычной
    /// логикой подбора/установки (не вместо неё) — см. HandleRaycast.</summary>
    private void HandleToolRaycast(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, toolInteractRange, interactableLayers, QueryTriggerInteraction.Collide))
            return;

        Breakable breakable = hit.collider.GetComponentInParent<Breakable>();
        if (breakable == null || breakable.IsBroken) return;

        currentHoveredBreakable = breakable;
        breakable.SetHighlight(true);
    }

    private void ClearHighlight()
    {
        if (currentHighlighted != null)
        {
            currentHighlighted.SetHighlight(false);
            currentHighlighted = null;
        }

        if (currentHoveredStain != null)
        {
            currentHoveredStain.SetHighlight(false);
            currentHoveredStain = null;
        }

        if (currentHoveredBreakable != null)
        {
            currentHoveredBreakable.SetHighlight(false);
            currentHoveredBreakable = null;
        }
    }

    private void ClearGhost()
    {
        if (currentHoveredSlot != null)
        {
            currentHoveredSlot.HideGhost();
            currentHoveredSlot = null;
        }
    }

    private void HandleClick()
    {
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
            currentHoveredStain.Clean();
            currentHoveredStain = null;
            return;
        }

        if (currentHoveredBreakable != null)
        {
            currentHoveredBreakable.Break();
            currentHoveredBreakable = null;
        }
    }

    private void PickUpWorldItem(WorldItem worldItem)
    {
        if (itemBeingPickedUp != null)
        {
            var stuck = itemBeingPickedUp;
            itemBeingPickedUp = null;
            if (inventory.AddWorldItem(stuck)) { /* ок */ }
            else stuck.Drop(stuck.transform.position, stuck.transform.rotation, Vector3.zero);
        }
        
        if (inventory == null || itemHolder == null || itemHolder.HeldItemTransform == null) return;
        if (!inventory.CanAddWorldItem(worldItem)) return;

        ShelfSlot source = worldItem.GetSourceSlot();
        if (source != null)
            source.RemoveItem(worldItem);

        worldItem.BeginPickup();
        // Интерполируем в локальных координатах руки: предмет следует за игроком во время анимации
        // и не отстаёт от движущейся камеры.
        worldItem.transform.SetParent(itemHolder.HeldItemTransform, true);
        itemBeingPickedUp = worldItem;
        pickupVelocity = Vector3.zero;

        currentHighlighted = null;
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

        currentHoveredSlot = null;
        if (infoUI != null) infoUI.Hide();
    }

    private void UpdatePickupAnimation()
    {
        if (itemBeingPickedUp == null) return;

        // Цель полёта — СРАЗУ финальная позиция: для стакающихся предметов — свой «этаж» стопки
        Vector3 targetLocalPosition = GetPickupTargetLocalPosition(itemBeingPickedUp);
        Quaternion targetLocalRotation = Quaternion.Euler(itemBeingPickedUp.itemData.handRotationOffset);

        itemBeingPickedUp.transform.localPosition = Vector3.SmoothDamp(
            itemBeingPickedUp.transform.localPosition,
            targetLocalPosition,
            ref pickupVelocity,
            1f / Mathf.Max(0.01f, pickupAnimationSpeed),
            Mathf.Infinity,
            Time.deltaTime);
        itemBeingPickedUp.transform.localRotation = Quaternion.Slerp(
            itemBeingPickedUp.transform.localRotation,
            targetLocalRotation,
            1f - Mathf.Exp(-pickupAnimationSpeed * Time.deltaTime));
        if (Vector3.Distance(itemBeingPickedUp.transform.localPosition, targetLocalPosition) > 0.01f) return;

        if (!inventory.AddWorldItem(itemBeingPickedUp))
            itemBeingPickedUp.Drop(itemBeingPickedUp.transform.position, itemBeingPickedUp.transform.rotation, Vector3.zero);
        itemBeingPickedUp = null;
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
        Vector3 direction = playerCamera.transform.forward.normalized;
        item.Drop(playerCamera.transform.position + direction * dropDistance, playerCamera.transform.rotation, direction * dropSpeed);
        if (infoUI != null) infoUI.Hide();
    }
}
