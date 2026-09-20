using System;
using UnityEngine;

/// <summary>
/// Зона доставки предмета — точка, куда игрок должен принести и бросить (ПКМ) нужный предмет,
/// чтобы выполнить квест типа DeliverItem. Не связана с WorldItem напрямую: у предмета, пока он
/// в руках игрока, коллайдеры выключены (см. WorldItem.SetPhysicsEnabled), поэтому триггер видит
/// только физически брошенный предмет (State == InWorld) — тот же цикл, что и обычный бросок.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeliveryZone : MonoBehaviour
{
    [Tooltip("Уникальный id зоны — с ним сверяется QuestData.targetZoneId.")]
    public string zoneId;

    [Tooltip("Какой предмет принимается. Пусто — любой.")]
    public ItemData requiredItem;

    [Tooltip("Уничтожать предмет при доставке. Если выключено — предмет остаётся лежать в зоне " +
             "и повторно засчитается только после нового захода/выхода коллайдера.")]
    public bool consumeOnDelivery = true;

    /// <summary>Сколько предметов уже доставлено в эту зону.</summary>
    public int DeliveredCount { get; private set; }

    /// <summary>Предмет доставлен в зону — хук для QuestManager.</summary>
    public event Action<DeliveryZone, WorldItem> OnItemDelivered;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        WorldItem item = other.GetComponentInParent<WorldItem>();
        if (item == null || item.itemData == null) return;
        if (item.State != WorldItem.ItemState.InWorld) return;
        if (requiredItem != null && item.itemData != requiredItem) return;

        DeliveredCount++;
        OnItemDelivered?.Invoke(this, item);

        if (consumeOnDelivery) Destroy(item.gameObject);
    }
}
