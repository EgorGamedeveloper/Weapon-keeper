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
        if (inventory == null) return;

        ShelfSlot source = worldItem.GetSourceSlot();
        if (source != null)
            source.RemoveItem();

        worldItem.BeginPickup();
        itemBeingPickedUp = worldItem;

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
        Transform target = itemHolder != null ? itemHolder.handPoint : transform;
        itemBeingPickedUp.transform.position = Vector3.Lerp(itemBeingPickedUp.transform.position, target.position, Time.deltaTime * pickupAnimationSpeed);
        itemBeingPickedUp.transform.rotation = Quaternion.Slerp(itemBeingPickedUp.transform.rotation, target.rotation, Time.deltaTime * pickupAnimationSpeed);
        if (Vector3.Distance(itemBeingPickedUp.transform.position, target.position) > 0.03f) return;

        inventory.AddWorldItem(itemBeingPickedUp);
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
