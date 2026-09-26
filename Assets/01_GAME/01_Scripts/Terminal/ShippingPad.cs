using UnityEngine;

/// <summary>
/// Зона отправки на крыше. Принимает только запечатанную коробку активного заказа (с голограммой,
/// как полка). Заказ закрывается в момент установки — деньги и опыт начисляются сразу, а прилёт
/// дрона чисто визуальный: если игра сохранится посреди полёта, награда уже получена и ничего
/// не потеряется.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShippingPad : MonoBehaviour, IPlaceableSlot
{
    [Header("Ссылки")]
    public ShippingService service;

    [Tooltip("Дрон, который забирает коробку.")]
    public ShippingDrone drone;

    [Tooltip("Где стоит коробка на площадке.")]
    public Transform socket;

    private bool busy;
    private GameObject ghost;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public bool CanAccept(ItemData item) =>
        !busy && service != null && item != null && item == service.boxItem && service.IsPacked;

    public void ShowGhost(ItemData item, GhostMode mode)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;
        if (ghost == null) ghost = GhostPreviewUtility.Create(item, Socket, Vector3.zero, Quaternion.identity, mode);
        else GhostPreviewUtility.SetMode(ghost, mode);
        ghost.SetActive(true);
    }

    public void HideGhost()
    {
        if (ghost != null) ghost.SetActive(false);
    }

    public void PlaceItem(WorldItem item)
    {
        HideGhost();
        if (item == null || !CanAccept(item.itemData)) return;

        busy = true;
        item.PlaceOnShelf(Socket, null, Vector3.zero, Quaternion.identity);
        // Коробку на площадке нельзя снова поднять — её забирает дрон.
        foreach (var col in item.GetComponentsInChildren<Collider>()) col.enabled = false;

        service.CompleteShipment();

        if (drone != null) drone.PickUp(item.transform, () => { if (item != null) Destroy(item.gameObject); busy = false; });
        else { Destroy(item.gameObject); busy = false; }
    }

    private Transform Socket => socket != null ? socket : transform;
}
