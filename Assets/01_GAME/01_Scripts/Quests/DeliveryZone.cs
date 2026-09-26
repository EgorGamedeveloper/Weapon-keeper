using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Зона доставки предмета — точка, куда игрок должен принести и бросить (ПКМ) нужный предмет,
/// чтобы выполнить квест типа DeliverItem. Не связана с WorldItem напрямую: у предмета, пока он
/// в руках игрока, коллайдеры выключены (см. WorldItem.SetPhysicsEnabled), поэтому триггер видит
/// только физически брошенный предмет (State == InWorld) — тот же цикл, что и обычный бросок.
///
/// Зона принимает предметы только по заявке: активный квест доставки регистрирует, какой предмет он
/// ждёт (AddRequest), и снимает заявку при завершении. Без заявок предмет просто лежит в зоне —
/// раньше зона съедала его в любом случае, и брошенный туда до открытия квеста единственный кирпич
/// делал квест невыполнимым. Предмет из нескольких коллайдеров засчитывается один раз.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeliveryZone : MonoBehaviour
{
    [Header("Доставка")]
    [Tooltip("Уникальный id зоны — с ним сверяется QuestData.targetZoneId.")]
    public string zoneId;

    [Tooltip("Какой предмет принимается. Пусто — любой.")]
    public ItemData requiredItem;

    [Tooltip("Уничтожать предмет при доставке. Если выключено — предмет остаётся лежать в зоне " +
             "и повторно засчитается только после того, как целиком выйдет из неё и зайдёт снова.")]
    public bool consumeOnDelivery = true;

    /// <summary>Предмет доставлен в зону — хук для QuestManager.</summary>
    public event Action<DeliveryZone, WorldItem> OnItemDelivered;

    // Уже засчитанные предметы: OnTriggerEnter приходит на КАЖДЫЙ коллайдер предмета, а Destroy
    // отложен до конца кадра — без этого предмет из нескольких коллайдеров засчитывался бы несколько раз.
    private readonly HashSet<WorldItem> delivered = new HashSet<WorldItem>();
    // Заявки активных квестов; null — «любой предмет».
    private readonly List<ItemData> requests = new List<ItemData>();
    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>Квест начал ждать предмет (null — любой). Предметы, которые уже лежат в зоне,
    /// проверяются сразу.</summary>
    public void AddRequest(ItemData item)
    {
        requests.Add(item);
        delivered.RemoveWhere(d => d == null);

        foreach (var candidate in FindItemsInside())
            if (!delivered.Contains(candidate)) TryDeliver(candidate);
    }

    /// <summary>Квест больше не ждёт предмет.</summary>
    public void RemoveRequest(ItemData item)
    {
        requests.Remove(item);
    }

    private void OnTriggerEnter(Collider other)
    {
        WorldItem item = other.GetComponentInParent<WorldItem>();
        if (item == null || delivered.Contains(item)) return;
        TryDeliver(item);
    }

    private void OnTriggerExit(Collider other)
    {
        // Незасчитанный (consumeOnDelivery выключен) предмет снова может быть засчитан, только когда
        // целиком покинет зону — выход одного из его коллайдеров не в счёт.
        WorldItem item = other.GetComponentInParent<WorldItem>();
        if (item != null && delivered.Contains(item) && !IsInside(item)) delivered.Remove(item);
    }

    private void TryDeliver(WorldItem item)
    {
        if (item.itemData == null || item.State != WorldItem.ItemState.InWorld) return;
        if (requiredItem != null && item.itemData != requiredItem) return;
        if (!HasRequestFor(item.itemData)) return;

        delivered.Add(item);
        OnItemDelivered?.Invoke(this, item);

        if (consumeOnDelivery) Destroy(item.gameObject);
    }

    /// <summary>Предметы, которые сейчас физически лежат в зоне. Спрашиваем физику, а не ведём
    /// учёт по OnTriggerEnter/Exit: при подборе у предмета выключаются коллайдеры, и OnTriggerExit
    /// Unity в этом случае не присылает — учёт "кто внутри" залипал бы.</summary>
    private List<WorldItem> FindItemsInside()
    {
        var result = new List<WorldItem>();
        if (zoneCollider == null) return result;

        Bounds bounds = zoneCollider.bounds;
        foreach (var col in Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            WorldItem item = col.GetComponentInParent<WorldItem>();
            if (item != null && !result.Contains(item)) result.Add(item);
        }
        return result;
    }

    private bool IsInside(WorldItem item)
    {
        if (zoneCollider == null) return false;
        Bounds bounds = zoneCollider.bounds;
        foreach (var col in item.GetComponentsInChildren<Collider>())
            if (col.enabled && bounds.Intersects(col.bounds)) return true;
        return false;
    }

    private bool HasRequestFor(ItemData item)
    {
        foreach (var request in requests)
            if (request == null || request == item) return true;
        return false;
    }
}
