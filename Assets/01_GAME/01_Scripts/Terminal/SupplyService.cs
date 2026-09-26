using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Одна поставка в пути: какой ящик и сколько осталось ехать.</summary>
public class SupplyDelivery
{
    public LootBoxData box;
    public float remaining;
    public float total;

    public float Progress => total > 0f ? 1f - Mathf.Clamp01(remaining / total) : 1f;
}

/// <summary>
/// Заказ лутбоксов через терминал: проверка лицензии/уровня/денег, списание, таймеры доставки и
/// появление ящика у точки доставки. Вскрытый ящик (LootCrate) сообщает сюда, сколько товара
/// высыпалось, — сервис добавляет его в учёт ShelvingProgressTracker.
/// Время — обычное Time.deltaTime: игровых суток пока нет.
/// </summary>
public class SupplyService : MonoBehaviour
{
    public enum Availability { Available, NoLicense, LowLevel, NoMoney }

    [Header("Ссылки")]
    [Tooltip("Реестр лутбоксов (для терминала и сейва).")]
    public TerminalCatalog catalog;
    public PlayerWallet wallet;
    public PlayerProgression progression;
    public PlayerSkills skills;

    [Tooltip("Учёт процента расстановки: привезённый товар увеличивает «всего».")]
    public ShelvingProgressTracker shelvingTracker;

    [Header("Доставка")]
    [Tooltip("Где появляются привезённые ящики.")]
    public Transform dropPoint;

    [Tooltip("Префаб ящика (Breakable + LootCrate).")]
    public LootCrate cratePrefab;

    [Tooltip("Разброс ящиков вокруг точки доставки, чтобы несколько штук не вставали друг в друга.")]
    [Min(0f)] public float dropScatter = 0.8f;

    [Tooltip("Родитель для высыпавшихся из ящика предметов. Пусто — корень сцены.")]
    public Transform itemsContainer;

    /// <summary>Что-то изменилось: заказ оформлен, ящик приехал или вскрыт.</summary>
    public event Action OnChanged;

    /// <summary>Заказанный ящик приехал (не при загрузке сейва). Для сюжетных триггеров.</summary>
    public event Action<LootBoxData> OnCrateArrived;

    /// <summary>Ящик вскрыт игроком. Для сюжетных триггеров.</summary>
    public event Action<LootBoxData> OnCrateOpened;

    private readonly List<SupplyDelivery> deliveries = new List<SupplyDelivery>();
    private readonly List<LootCrate> crates = new List<LootCrate>();

    public IReadOnlyList<SupplyDelivery> Deliveries => deliveries;

    /// <summary>Цена с учётом навыков (множитель SkillStat.OrderPrice).</summary>
    public int GetPrice(LootBoxData box) => Mathf.Max(0, Mathf.RoundToInt(box.price * Skill(SkillStat.OrderPrice)));

    /// <summary>Время доставки с учётом навыков (множитель SkillStat.DeliveryTime).</summary>
    public float GetDeliveryTime(LootBoxData box) => Mathf.Max(1f, box.deliverySeconds * Skill(SkillStat.DeliveryTime));

    public Availability Check(LootBoxData box)
    {
        if (box.requiredLicense != null && (skills == null || !skills.IsOwned(box.requiredLicense))) return Availability.NoLicense;
        if (progression != null && progression.CurrentLevel < box.requiredLevel) return Availability.LowLevel;
        if (wallet == null || !wallet.CanAfford(GetPrice(box))) return Availability.NoMoney;
        return Availability.Available;
    }

    public bool TryOrder(LootBoxData box)
    {
        if (box == null || Check(box) != Availability.Available) return false;
        if (!wallet.TrySpend(GetPrice(box))) return false;

        float time = GetDeliveryTime(box);
        deliveries.Add(new SupplyDelivery { box = box, remaining = time, total = time });
        OnChanged?.Invoke();
        return true;
    }

    private void Update()
    {
        List<LootBoxData> arrived = null;
        for (int i = deliveries.Count - 1; i >= 0; i--)
        {
            deliveries[i].remaining -= Time.deltaTime;
            if (deliveries[i].remaining > 0f) continue;

            LootBoxData box = deliveries[i].box;
            SpawnCrate(box, DropPosition(), Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            deliveries.RemoveAt(i);
            (arrived ??= new List<LootBoxData>()).Add(box);
        }
        if (arrived == null) return;
        OnChanged?.Invoke();
        foreach (LootBoxData box in arrived) OnCrateArrived?.Invoke(box);
    }

    private float Skill(SkillStat stat) => skills != null ? skills.GetValue(stat, 1f) : 1f;

    private Vector3 DropPosition()
    {
        Vector3 origin = dropPoint != null ? dropPoint.position : transform.position;
        Vector2 offset = UnityEngine.Random.insideUnitCircle * dropScatter;
        return origin + new Vector3(offset.x, 0f, offset.y);
    }

    private void SpawnCrate(LootBoxData box, Vector3 position, Quaternion rotation)
    {
        if (cratePrefab == null || box == null) return;
        var crate = Instantiate(cratePrefab, position, rotation);
        crate.Init(this, box);
        crates.Add(crate);
    }

    /// <summary>Ящик вскрыт — товар в мире, учитываем его в проценте расстановки.</summary>
    public void HandleCrateOpened(LootCrate crate, List<WorldItem> spawned)
    {
        crates.Remove(crate);
        LootBoxData openedBox = crate != null ? crate.Box : null;

        if (shelvingTracker != null)
        {
            int tracked = 0;
            foreach (var item in spawned)
                if (item != null && item.itemData != null && item.itemData.shelfType != null
                    && shelvingTracker.trackedCategories.Contains(item.itemData.shelfType)) tracked++;
            if (tracked > 0) shelvingTracker.RegisterAdditionalUnits(tracked);
        }

        OnChanged?.Invoke();
        if (openedBox != null) OnCrateOpened?.Invoke(openedBox);
    }

    // ───────── сейв ─────────

    public SupplyDeliverySave[] CaptureDeliveries()
    {
        var list = new List<SupplyDeliverySave>();
        foreach (var d in deliveries)
            if (d.box != null) list.Add(new SupplyDeliverySave { lootBoxId = d.box.lootBoxId, remainingSeconds = d.remaining, totalSeconds = d.total });
        return list.ToArray();
    }

    public DeliveredCrateSave[] CaptureCrates()
    {
        var list = new List<DeliveredCrateSave>();
        foreach (var crate in crates)
        {
            if (crate == null || crate.Box == null) continue;
            Vector3 p = crate.transform.position;
            Quaternion r = crate.transform.rotation;
            list.Add(new DeliveredCrateSave { lootBoxId = crate.Box.lootBoxId, posX = p.x, posY = p.y, posZ = p.z, rotX = r.x, rotY = r.y, rotZ = r.z, rotW = r.w });
        }
        return list.ToArray();
    }

    /// <summary>Восстановление из сейва (без событий): поставки в пути и невскрытые ящики.</summary>
    public void RestoreState(SupplyDeliverySave[] savedDeliveries, DeliveredCrateSave[] savedCrates)
    {
        if (catalog == null) return;

        foreach (var d in savedDeliveries)
        {
            var box = catalog.GetLootBox(d.lootBoxId);
            if (box != null) deliveries.Add(new SupplyDelivery { box = box, remaining = d.remainingSeconds, total = Mathf.Max(d.totalSeconds, d.remainingSeconds) });
        }

        foreach (var c in savedCrates)
        {
            var box = catalog.GetLootBox(c.lootBoxId);
            if (box != null) SpawnCrate(box, new Vector3(c.posX, c.posY, c.posZ), new Quaternion(c.rotX, c.rotY, c.rotZ, c.rotW));
        }
    }
}
