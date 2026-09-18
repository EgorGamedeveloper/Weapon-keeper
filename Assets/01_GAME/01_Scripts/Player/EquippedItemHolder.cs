using UnityEngine;

/// <summary>
/// Точка в руке игрока, куда устанавливается модель активного предмета из инвентаря.
/// При смене активного слота обновляет 3D-модель. Точка в руке намеренно остаётся
/// неподвижной: sway и bobbing временно отключены для стабильной отладки удержания.
/// </summary>
public class EquippedItemHolder : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Точка в руке игрока (пустой Transform перед камерой), куда крепится модель предмета.")]
    public Transform handPoint;
    public InventorySystem inventory;

    [Tooltip("Дочерний visual root HandPoint. Во время отладки он остаётся неподвижным.")]
    public Transform heldItemVisualRoot;

    [Tooltip("Контейнер для подобранных, но сейчас не отображаемых физических объектов.")]
    public Transform carriedItemsStorage;
    private bool presentationEnabled = true;

    /// <summary>Точка, под которой находятся видимые предметы в руках.</summary>
    public Transform HeldItemTransform
    {
        get
        {
            EnsureHeldItemVisualRoot();
            return heldItemVisualRoot;
        }
    }

    private void OnEnable()
    {
        EnsureHeldItemVisualRoot();
        if (inventory != null)
        {
            inventory.OnActiveSlotChanged += HandleActiveChanged;
            inventory.OnInventoryChanged += RefreshCurrent;
            RefreshCurrent();
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnActiveSlotChanged -= HandleActiveChanged;
            inventory.OnInventoryChanged -= RefreshCurrent;
        }
    }

    private void HandleActiveChanged(int index)
    {
        RefreshCurrent();
    }

    /// <summary>Перемещает существующие физические предметы между рукой и скрытым контейнером.</summary>
    public void RefreshCurrent()
    {
        if (inventory == null || !EnsureHeldItemVisualRoot()) return;
        if (carriedItemsStorage == null)
        {
            var storage = new GameObject("CarriedItemsStorage");
            storage.transform.SetParent(transform, false);
            carriedItemsStorage = storage.transform;
        }

        foreach (var entry in inventory.entries)
        {
            foreach (var instance in entry.instances)
                if (instance != null) instance.SetCarriedHidden(carriedItemsStorage);
        }

        InventoryEntry active = inventory.GetActiveEntry();
        if (active == null || !presentationEnabled) return;

        int visibleCount = active.item.showAsVisualStack ? active.instances.Count : Mathf.Min(1, active.instances.Count);
        for (int i = 0; i < visibleCount; i++)
        {
            WorldItem instance = active.instances[i];
            if (instance == null) continue;
            Vector3 localPosition = active.item.handPositionOffset + Vector3.up * (active.item.heldStackSpacing * i);
            instance.SetHeldVisible(heldItemVisualRoot, localPosition, Quaternion.Euler(active.item.handRotationOffset));
        }
        return true;
    }

    public void SetPresentationEnabled(bool enabled)
    {
        presentationEnabled = enabled;
        RefreshCurrent();
    }

    private bool EnsureHeldItemVisualRoot()
    {
        if (handPoint == null) return false;
        if (heldItemVisualRoot == null)
        {
            var root = new GameObject("HeldItemVisualRoot");
            root.transform.SetParent(handPoint, false);
            heldItemVisualRoot = root.transform;
        }
        return true;
    }
}
