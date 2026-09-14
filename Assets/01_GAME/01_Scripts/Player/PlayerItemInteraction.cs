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

    [Header("Настройки луча")]
    [Tooltip("Слои, по которым бьёт луч (предметы и ячейки полок).")]
    public LayerMask interactableLayers = ~0;

    [Tooltip("Максимальная дистанция, на которой можно подобрать предмет.")]
    public float pickupRange = 2f;

    [Tooltip("Максимальная дистанция взаимодействия с полкой.")]
    public float shelfInteractRange = 3f;

    private WorldItem currentHighlighted;
    private ShelfSlot currentHoveredSlot;

    private void Update()
    {
        HandleRaycast();

        if (Input.GetMouseButtonDown(0))
            HandleClick();
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

        bool added = inventory.AddItem(worldItem.itemData);
        if (!added) return; // инвентарь полон

        ShelfSlot source = worldItem.GetSourceSlot();
        if (source != null)
            source.RemoveItem(); // предмет стоял на полке — освобождаем ячейку
        else
            Destroy(worldItem.gameObject); // предмет лежал в мире — убираем его

        currentHighlighted = null;
        if (infoUI != null) infoUI.Hide();
    }

    private void PlaceActiveItemOnShelf(ShelfSlot slot)
    {
        if (inventory == null) return;

        ItemData active = inventory.GetActiveItem();
        if (active == null || !slot.CanAccept(active)) return;

        slot.PlaceItem(active);
        inventory.RemoveActiveItem();

        currentHoveredSlot = null;
        if (infoUI != null) infoUI.Hide();
    }
}
