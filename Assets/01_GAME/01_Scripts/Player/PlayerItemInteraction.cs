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

        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, interactableLayers, QueryTriggerInteraction.Collide))
        {
            // 1) Проверяем, не предмет ли это (лежащий, либо уже стоящий на полке).
            WorldItem worldItem = hit.collider.GetComponentInParent<WorldItem>();
            if (worldItem != null)
            {
                if (hit.distance <= pickupRange)
                {
                    currentHighlighted = worldItem;
                    worldItem.SetHighlight(true);
                    if (infoUI != null) infoUI.Show(worldItem.itemData);
                }
                return;
            }

            // 2) Проверяем, не пустая ли ячейка полки.
            ShelfSlot slot = hit.collider.GetComponent<ShelfSlot>();
            if (slot != null && hit.distance <= shelfInteractRange)
            {
                ItemData active = inventory != null ? inventory.GetActiveItem() : null;

                if (slot.IsEmpty && active != null && slot.CanAccept(active))
                {
                    slot.ShowGhost(active);
                    currentHoveredSlot = slot;
                    if (infoUI != null) infoUI.ShowPlacementHint(active, true);
                }
                else if (infoUI != null)
                {
                    infoUI.Hide();
                }
                return;
            }
        }

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
        if (inventory == null || itemHolder == null || itemHolder.handPoint == null) return;
        if (!inventory.CanAddWorldItem(worldItem)) return;

        ShelfSlot source = worldItem.GetSourceSlot();
        if (source != null)
            source.RemoveItem();

        worldItem.BeginPickup();
        // Интерполируем в локальных координатах руки: предмет следует за игроком во время анимации
        // и не отстаёт от движущейся камеры.
        worldItem.transform.SetParent(itemHolder.handPoint, true);
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
        Vector3 targetLocalPosition = itemBeingPickedUp.itemData.handPositionOffset;
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
