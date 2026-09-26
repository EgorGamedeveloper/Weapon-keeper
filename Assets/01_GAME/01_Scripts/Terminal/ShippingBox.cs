using DG.Tweening;
using UnityEngine;

/// <summary>
/// Коробка для заказа на отправку — обычный предмет (WorldItem): её можно поднять, носить в
/// инвентаре и поставить в зону отправки. Пока она стоит на полу, в неё укладывается товар активного
/// заказа — ЛКМ с нужным предметом в руках, с голограммой-подсказкой, как на полке (IPlaceableSlot).
/// Всё уложено — коробка запечатана. У отменённого заказа коробка работает наоборот (IInteractable):
/// ЛКМ достаёт по одному предмету, пустая коробка исчезает.
/// </summary>
[RequireComponent(typeof(WorldItem))]
public class ShippingBox : MonoBehaviour, IPlaceableSlot, IInteractable
{
    [Header("Визуал")]
    [Tooltip("Открытая коробка (идёт упаковка).")]
    public GameObject openVisual;

    [Tooltip("Запечатанная коробка (всё уложено).")]
    public GameObject sealedVisual;

    [Tooltip("Где показывать голограмму укладываемого предмета.")]
    public Transform ghostPoint;

    private ShippingService service;
    private WorldItem worldItem;
    private GameObject ghost;
    private ItemData ghostItem;

    private bool OnFloor => worldItem != null && worldItem.State == WorldItem.ItemState.InWorld;

    private void Awake() => worldItem = GetComponent<WorldItem>();

    public void Init(ShippingService owner)
    {
        service = owner;
        Refresh();
    }

    /// <summary>Обновить вид: запечатана или открыта.</summary>
    public void Refresh()
    {
        bool sealedBox = service != null && service.IsPacked;
        if (sealedVisual != null) sealedVisual.SetActive(sealedBox);
        if (openVisual != null) openVisual.SetActive(!sealedBox);
    }

    // ───────── укладка (IPlaceableSlot) ─────────

    public bool CanAccept(ItemData item) => OnFloor && service != null && service.CanPack(item);

    public void ShowGhost(ItemData item, GhostMode mode)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;
        Transform at = ghostPoint != null ? ghostPoint : transform;

        if (ghost != null && ghostItem == item)
        {
            GhostPreviewUtility.SetMode(ghost, mode);
            ghost.SetActive(true);
            return;
        }

        if (ghost != null) Destroy(ghost);
        ghost = GhostPreviewUtility.Create(item, at, Vector3.zero, Quaternion.identity, mode);
        ghostItem = item;
    }

    public void HideGhost()
    {
        if (ghost != null) ghost.SetActive(false);
    }

    public void PlaceItem(WorldItem item)
    {
        HideGhost();
        if (service == null || !service.Pack(item)) return;

        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 8, 0.7f);
    }

    // ───────── разбор отменённого заказа (IInteractable) ─────────

    public string InteractTitle => "Коробка заказа";

    public string InteractHint
    {
        get
        {
            if (service == null) return "";
            if (service.BoxCancelled) return "ЛКМ — достать предмет (осталось " + service.BoxContents.Count + ")";
            return service.IsPacked ? "Запечатана — отнесите на крышу в зону отправки" : "Уложите товар по заказу (см. терминал)";
        }
    }

    public bool CanInteract => OnFloor && service != null && service.BoxCancelled && service.BoxContents.Count > 0;

    public void Interact()
    {
        if (!CanInteract) return;
        service.TakeOutOne();
        if (this != null) { transform.DOKill(true); transform.DOPunchScale(Vector3.one * 0.08f, 0.2f, 6, 0.7f); }
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (ghost != null) Destroy(ghost);
    }
}
