using UnityEngine;

/// <summary>
/// Основная логика взаимодействия игрока с предметами:
/// - луч из камеры подсвечивает предметы в радиусе pickupRange (по умолчанию 2 м);
/// - показывает панель информации о предмете на канвасе;
/// - ЛКМ на предмете — подбирает его в инвентарь;
/// - при наведении на пустую ячейку полки (держа подходящий предмет) — показывает "призрак";
/// - ЛКМ на ячейке полки — устанавливает активный предмет инвентаря на полку.
/// </summary>
public class PlayerItemInteraction : MonoBehaviour
{
    [Header("Ссылки")]
    public Camera playerCamera;
    public InventorySystem inventory;
    public ItemInfoUI infoUI;
    public EquippedItemHolder itemHolder;

    [Header("Настройки луча")]
    [Tooltip("Слои, по которым бьёт луч (предметы и ячейки полок).")]
    public LayerMask interactableLayers = ~0;

    [Tooltip("Максимальная дистанция, на которой можно подобрать предмет.")]
    public float pickupRange = 2f;

    [Tooltip("Максимальная дистанция взаимодействия с полкой.")]
    public float shelfInteractRange = 3f;

    [Header("Подбор и бросок")]
    public float pickupAnimationSpeed = 12f;
    public float dropDistance = 1.25f;
    public float dropSpeed = 4f;

    private WorldItem currentHighlighted;
    private ShelfSlot currentHoveredSlot;
    private WorldItem itemBeingPickedUp;
    private Vector3 pickupVelocity;

    private void Update()
    {
        HandleRaycast();

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

    Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    float maxDist = Mathf.Max(pickupRange, shelfInteractRange);

    if (!Physics.Raycast(ray, out RaycastHit hit, maxDist, interactableLayers, QueryTriggerInteraction.Collide))
    {
        if (infoUI != null) infoUI.Hide();
        return;
    }

    ItemData active = inventory != null ? inventory.GetActiveItem() : null;

    // ── Шаг 1. Резолвим, на ЧТО смотрим: предмет и/или слот (колонну) ──
    WorldItem hitItem = hit.collider.GetComponentInParent<WorldItem>();
    ShelfSlot slot = hit.collider.GetComponent<ShelfSlot>();

    // Луч попал в предмет, который уже стоит на полке (в стопке) —
    // нас интересует ЕГО колонна, а не он сам как точка установки.
    bool hitPlacedItem = hitItem != null && hitItem.State == WorldItem.ItemState.PlacedOnShelf;
    if (hitPlacedItem && slot == null)
        slot = hitItem.GetSourceSlot();

    // ── Шаг 2. РЕЖИМ УСТАНОВКИ (приоритет): в руке есть предмет и колонна готова его принять ──
    // Сюда попадаем в трёх случаях: пустая колонна, неполная стопка (даже если луч задел
    // стоящую пачку), одиночный пустой слот. CanAccept сам проверит всё: категорию,
    // заполненность, совпадение типа со стопкой.
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

    // ── Шаг 4. Ничего интересного под лучом ──
    if (infoUI != null) infoUI.Hide();

    }

    private void ClearHighlight()
    {
        if (currentHighlighted != null)
        {
            currentHighlighted.SetHighlight(false);
            currentHighlighted = null;
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
            PlaceActiveItemOnShelf(currentHoveredSlot);
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

    private void PlaceActiveItemOnShelf(ShelfSlot slot)
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
        WorldItem item = inventory.RemoveActiveWorldItem();
        if (item == null) return;
        Vector3 direction = playerCamera.transform.forward.normalized;
        item.Drop(playerCamera.transform.position + direction * dropDistance, playerCamera.transform.rotation, direction * dropSpeed);
        if (infoUI != null) infoUI.Hide();
    }
}
