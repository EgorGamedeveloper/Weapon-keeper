using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Заказы на отправку. Терминал показывает «входящие» (пополняются из шаблонов, на которые у игрока
/// есть лицензия и уровень), игрок берёт один заказ в работу — у упаковочного места появляется
/// коробка. В неё укладывается товар (ShippingBox), запечатанную коробку игрок несёт на крышу и
/// ставит в зону отправки (ShippingPad) — заказ закрыт, деньги и опыт начислены, дрон забирает груз.
///
/// Коробка одна на всю игру: пока она существует (в работе или отменена и не разобрана), новый
/// заказ не берётся. Её содержимое — данные (список ItemData), а не предметы в мире, поэтому
/// коробку можно носить в инвентаре, а сейв хранит просто список id.
///
/// Новые заказы приходят по игровым часам (refillHours — «утренняя и дневная почта»): в каждый такой час
/// входящие добиваются до лимита. Ночь, пропущенная сном, тоже приносит утреннюю почту — GameClock
/// поднимает смену часа для каждого пройденного часа. Без часов в сцене — по-старому, раз в refillInterval секунд.
/// </summary>
public class ShippingService : MonoBehaviour
{
    public enum AcceptBlock { None, BoxInUse, NoLicense, LowLevel }

    [Header("Ссылки")]
    [Tooltip("Реестр шаблонов заказов.")]
    public TerminalCatalog catalog;
    public PlayerWallet wallet;
    public PlayerProgression progression;
    public PlayerSkills skills;

    [Tooltip("Учёт процента расстановки: уложенный в коробку товар уходит из «всего».")]
    public ShelvingProgressTracker shelvingTracker;

    [Tooltip("Игровые часы: новые заказы приходят в refillHours. Пусто — раз в refillInterval секунд.")]
    public GameClock clock;

    [Header("Коробка")]
    [Tooltip("Предмет «Коробка для отправки» (у его worldPrefab должен быть ShippingBox).")]
    public ItemData boxItem;

    [Tooltip("Где появляется новая коробка.")]
    public Transform packingPoint;

    [Tooltip("Родитель для коробки и предметов, достанных из отменённой коробки. Пусто — корень сцены.")]
    public Transform itemsContainer;

    [Header("Входящие заказы")]
    [Tooltip("Сколько заказов одновременно висит во входящих.")]
    [Min(1)] public int inboxLimit = 3;

    [Tooltip("В какие игровые часы приходит почта: входящие добиваются до лимита.")]
    public int[] refillHours = { 6, 14 };

    [Tooltip("Без игровых часов в сцене: раз во сколько секунд приходит новый заказ (если есть место).")]
    [Min(1f)] public float refillInterval = 45f;

    /// <summary>Входящие, активный заказ или коробка изменились.</summary>
    public event Action OnChanged;

    /// <summary>Заказ отправлен (коробка ушла с дроном) — передаёт выполненный заказ. Для сюжетных триггеров.</summary>
    public event Action<ShippingOrderData> OnShipmentCompleted;

    public ShippingOrderData ActiveOrder { get; private set; }

    /// <summary>Заказ отменён, но в коробке остался товар — игрок должен его разобрать.</summary>
    public bool BoxCancelled { get; private set; }

    public IReadOnlyList<ShippingOrderData> Inbox => inbox;
    public IReadOnlyList<ItemData> BoxContents => boxContents;

    /// <summary>Коробка существует (в работе или ждёт разбора).</summary>
    public bool BoxInUse => ActiveOrder != null || BoxCancelled;

    /// <summary>Всё по заказу уложено — коробку можно отправлять.</summary>
    public bool IsPacked
    {
        get
        {
            if (ActiveOrder == null || BoxCancelled) return false;
            foreach (var line in ActiveOrder.lines)
                if (line != null && line.item != null && PackedCount(line.item) < line.count) return false;
            return true;
        }
    }

    private readonly List<ShippingOrderData> inbox = new List<ShippingOrderData>();
    private readonly List<ItemData> boxContents = new List<ItemData>();
    private ShippingBox box;
    private float refillTimer;

    // Start: после сейва коробка уже заспавнена SaveLoadService (Awake — пол, Start — инвентарь).
    private void Start()
    {
        box = FindAnyObjectByType<ShippingBox>();

        if (!BoxInUse && box != null)
        {
            // Коробка без заказа (например, сейв старее этой системы) — убираем, иначе она вечно висит.
            Destroy(box.gameObject);
            box = null;
        }
        else if (BoxInUse && box == null)
        {
            SpawnBox();
        }

        if (box != null) box.Init(this);
        while (inbox.Count < Mathf.Min(1, inboxLimit) && TryAddToInbox()) { }
    }

    private void OnEnable()
    {
        if (clock != null) clock.OnHourChanged += HandleHourChanged;
    }

    private void OnDisable()
    {
        if (clock != null) clock.OnHourChanged -= HandleHourChanged;
    }

    private void HandleHourChanged(int hour)
    {
        if (Array.IndexOf(refillHours, hour) < 0) return;
        while (inbox.Count < inboxLimit && TryAddToInbox()) { }
    }

    private void Update()
    {
        if (clock != null || inbox.Count >= inboxLimit) return;

        refillTimer += Time.deltaTime;
        if (refillTimer < refillInterval) return;

        refillTimer = 0f;
        TryAddToInbox();
    }

    private float Skill(SkillStat stat) => skills != null ? skills.GetValue(stat, 1f) : 1f;

    /// <summary>Награда с учётом навыков (множитель SkillStat.ShippingReward).</summary>
    public int GetReward(ShippingOrderData order) => Mathf.RoundToInt(order.moneyReward * Skill(SkillStat.ShippingReward));

    public AcceptBlock CheckAccept(ShippingOrderData order)
    {
        if (BoxInUse) return AcceptBlock.BoxInUse;
        if (!IsEligible(order, out AcceptBlock reason)) return reason;
        return AcceptBlock.None;
    }

    private bool IsEligible(ShippingOrderData order, out AcceptBlock reason)
    {
        reason = AcceptBlock.None;
        if (order.requiredLicense != null && (skills == null || !skills.IsOwned(order.requiredLicense))) reason = AcceptBlock.NoLicense;
        else if (progression != null && progression.CurrentLevel < order.requiredLevel) reason = AcceptBlock.LowLevel;
        return reason == AcceptBlock.None;
    }

    private bool TryAddToInbox()
    {
        if (catalog == null) return false;

        var candidates = new List<ShippingOrderData>();
        foreach (var order in catalog.orders)
            if (order != null && order != ActiveOrder && !inbox.Contains(order) && IsEligible(order, out _))
                candidates.Add(order);
        if (candidates.Count == 0) return false;

        inbox.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        OnChanged?.Invoke();
        return true;
    }

    public bool Accept(ShippingOrderData order)
    {
        if (order == null || !inbox.Contains(order) || CheckAccept(order) != AcceptBlock.None) return false;

        inbox.Remove(order);
        ActiveOrder = order;
        BoxCancelled = false;
        boxContents.Clear();
        SpawnBox();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Отменить заказ. Уложенное остаётся в коробке — игрок достаёт его сам (решение по дизайну).</summary>
    public void Cancel()
    {
        if (ActiveOrder == null) return;
        ActiveOrder = null;
        BoxCancelled = boxContents.Count > 0;
        if (!BoxCancelled) DestroyBox();
        else if (box != null) box.Refresh();
        OnChanged?.Invoke();
    }

    public int PackedCount(ItemData item)
    {
        int count = 0;
        foreach (var packed in boxContents) if (packed == item) count++;
        return count;
    }

    public bool CanPack(ItemData item) =>
        item != null && ActiveOrder != null && !BoxCancelled && PackedCount(item) < ActiveOrder.RequiredCount(item);

    /// <summary>Уложить предмет в коробку: предмет исчезает из мира и становится записью в заказе.</summary>
    public bool Pack(WorldItem item)
    {
        if (item == null || !CanPack(item.itemData)) return false;

        boxContents.Add(item.itemData);
        ChangeTrackedUnits(item.itemData, -1);
        Destroy(item.gameObject);

        if (box != null) box.Refresh();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Достать один предмет из коробки отменённого заказа — он появляется на коробке.</summary>
    public void TakeOutOne()
    {
        if (!BoxCancelled || boxContents.Count == 0 || box == null) return;

        ItemData itemData = boxContents[boxContents.Count - 1];
        boxContents.RemoveAt(boxContents.Count - 1);

        if (itemData.worldPrefab != null)
        {
            Vector3 position = box.transform.position + Vector3.up * 0.6f;
            var go = Instantiate(itemData.worldPrefab, position, Quaternion.identity, itemsContainer);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 2f, UnityEngine.Random.Range(-0.8f, 0.8f));
            ChangeTrackedUnits(itemData, +1);
        }

        if (boxContents.Count == 0)
        {
            BoxCancelled = false;
            DestroyBox();
        }
        else box.Refresh();

        OnChanged?.Invoke();
    }

    /// <summary>Запечатанная коробка поставлена в зону отправки — заказ выполнен.</summary>
    public void CompleteShipment()
    {
        if (!IsPacked) return;

        if (wallet != null) wallet.Add(GetReward(ActiveOrder));
        if (progression != null) progression.AddXP(ActiveOrder.xpReward);

        ShippingOrderData shipped = ActiveOrder;
        ActiveOrder = null;
        boxContents.Clear();
        box = null; // коробку уносит дрон — её уничтожит ShippingPad
        OnChanged?.Invoke();
        OnShipmentCompleted?.Invoke(shipped);
    }

    private void SpawnBox()
    {
        if (boxItem == null || boxItem.worldPrefab == null) return;
        Transform at = packingPoint != null ? packingPoint : transform;
        var go = Instantiate(boxItem.worldPrefab, at.position, at.rotation, itemsContainer);
        box = go.GetComponent<ShippingBox>();
        if (box != null) box.Init(this);
    }

    private void DestroyBox()
    {
        if (box != null) Destroy(box.gameObject);
        box = null;
    }

    private void ChangeTrackedUnits(ItemData item, int delta)
    {
        if (shelvingTracker == null || item.shelfType == null) return;
        if (shelvingTracker.trackedCategories.Contains(item.shelfType)) shelvingTracker.RegisterAdditionalUnits(delta);
    }

    // ───────── сейв ─────────

    public void Capture(SaveGameData data)
    {
        var inboxIds = new List<string>();
        foreach (var order in inbox) if (order != null) inboxIds.Add(order.orderId);
        data.shippingInboxIds = inboxIds.ToArray();
        data.shippingActiveOrderId = ActiveOrder != null ? ActiveOrder.orderId : "";
        var contentIds = new List<string>();
        foreach (var item in boxContents) if (item != null) contentIds.Add(item.itemId);
        data.shippingBoxContentIds = contentIds.ToArray();
        data.shippingBoxCancelled = BoxCancelled;
        data.shippingRefillTimer = refillTimer;
    }

    /// <summary>Восстановление из сейва (без событий). Коробку сервис найдёт сам в Start.</summary>
    public void RestoreState(SaveGameData data, ItemCatalog itemCatalog)
    {
        if (catalog == null) return;

        foreach (var id in data.shippingInboxIds)
        {
            var order = catalog.GetOrder(id);
            if (order != null && !inbox.Contains(order)) inbox.Add(order);
        }

        ActiveOrder = string.IsNullOrEmpty(data.shippingActiveOrderId) ? null : catalog.GetOrder(data.shippingActiveOrderId);

        if (itemCatalog != null)
            foreach (var id in data.shippingBoxContentIds)
            {
                var item = itemCatalog.GetById(id);
                if (item != null) boxContents.Add(item);
            }

        BoxCancelled = ActiveOrder == null && data.shippingBoxCancelled && boxContents.Count > 0;
        if (ActiveOrder == null && !BoxCancelled) boxContents.Clear();
        refillTimer = data.shippingRefillTimer;
    }
}
