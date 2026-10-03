using System.Collections.Generic;
using UnityEngine;

// Виды триггеров сюжетного графа (см. StoryTrigger). Поля ноды: target — id объекта из каталога сцены,
// category / item / zone / orderId / lootBoxId / enemyType / questId — id ассетов, value — порог, count — сколько раз.

/// <summary>Базовый вид «конкретный объект сцены по PersistentId»: починен, сломан, отмыт.</summary>
public abstract class SceneObjectTrigger<T> : StoryTrigger where T : Component
{
    protected T Target { get; private set; }

    protected override bool Subscribe()
    {
        Target = Scene.FindByPersistentId<T>(Node.target);
        if (Target == null)
        {
            Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет {typeof(T).Name} с id «{Node.target}» — он не сработает. " +
                                "Обновите каталог сцены и граф.");
            return false;
        }
        Hook(true);
        return true;
    }

    protected override void Unsubscribe()
    {
        if (Target != null) Hook(false);
    }

    protected abstract void Hook(bool subscribe);
}

public class RepairPointRepairedTrigger : SceneObjectTrigger<RepairPoint>
{
    protected override void Hook(bool subscribe)
    {
        if (subscribe) Target.OnRepaired += Handle; else Target.OnRepaired -= Handle;
    }
    private void Handle(RepairPoint point) => Fire();
    protected override bool IsSatisfied() => Target.IsRepaired;
}

public class BreakableBrokenTrigger : SceneObjectTrigger<Breakable>
{
    protected override void Hook(bool subscribe)
    {
        if (subscribe) Target.OnBroken += Handle; else Target.OnBroken -= Handle;
    }
    private void Handle(Breakable breakable) => Fire();
    protected override bool IsSatisfied() => Target.IsBroken;
}

public class StainCleanedTrigger : SceneObjectTrigger<CleanableStain>
{
    protected override void Hook(bool subscribe)
    {
        if (subscribe) Target.OnCleaned += Handle; else Target.OnCleaned -= Handle;
    }
    private void Handle(CleanableStain stain) => Fire();
    protected override bool IsSatisfied() => Target.IsClean;
}

/// <summary>На полках категории стоит не меньше count предметов (считается по состоянию полок).</summary>
public class ShelfItemsPlacedTrigger : StoryTrigger
{
    private readonly List<ShelfSlot> slots = new List<ShelfSlot>();

    protected override bool Subscribe()
    {
        foreach (Shelf shelf in Scene.All<Shelf>())
        {
            if (shelf == null || shelf.acceptedCategory == null || shelf.acceptedCategory.name != Node.category) continue;
            ShelfSlot[] shelfSlots = shelf.slots != null && shelf.slots.Length > 0 ? shelf.slots : shelf.GetComponentsInChildren<ShelfSlot>(true);
            foreach (ShelfSlot slot in shelfSlots)
                if (slot != null && !slots.Contains(slot)) slots.Add(slot);
        }

        if (slots.Count == 0)
        {
            Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): нет полок категории «{Node.category}».");
            return false;
        }
        foreach (ShelfSlot slot in slots) slot.OnItemPlaced += Handle;
        return true;
    }

    protected override void Unsubscribe()
    {
        foreach (ShelfSlot slot in slots) if (slot != null) slot.OnItemPlaced -= Handle;
    }

    private void Handle(ShelfSlot slot)
    {
        if (IsSatisfied()) Fire();
    }

    protected override bool IsSatisfied()
    {
        int total = 0;
        foreach (ShelfSlot slot in slots) if (slot != null) total += slot.StackCount;
        return total >= Mathf.Max(1, Node.count);
    }
}

/// <summary>В зону доставки (конкретную или любую) принесли предмет (конкретный или любой) count раз.</summary>
public class ItemDeliveredTrigger : StoryTrigger
{
    private readonly List<DeliveryZone> zones = new List<DeliveryZone>();
    private ItemData item;

    protected override bool Subscribe()
    {
        if (!string.IsNullOrEmpty(Node.item))
        {
            item = Scene.FindItem(Node.item);
            if (item == null)
            {
                Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): предмет «{Node.item}» не найден в ItemCatalog.");
                return false;
            }
        }

        foreach (DeliveryZone zone in Scene.All<DeliveryZone>())
            if (zone != null && Matches(Node.zone, zone.zoneId)) zones.Add(zone);
        if (zones.Count == 0)
        {
            Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): нет зоны доставки «{Node.zone}».");
            return false;
        }

        // Зона принимает предмет, только пока его кто-то ждёт (как у квеста DeliverItem).
        foreach (DeliveryZone zone in zones)
        {
            zone.OnItemDelivered += Handle;
            zone.AddRequest(item);
        }
        return true;
    }

    protected override void Unsubscribe()
    {
        foreach (DeliveryZone zone in zones)
        {
            if (zone == null) continue;
            zone.OnItemDelivered -= Handle;
            zone.RemoveRequest(item);
        }
    }

    private void Handle(DeliveryZone zone, WorldItem delivered)
    {
        if (item != null && (delivered == null || delivered.itemData != item)) return;
        Count();
    }
}

/// <summary>Предмет оказался в инвентаре уборки или экипировки.</summary>
public class ItemPickedUpTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.TidyUp == null && Scene.Equipment == null)
        {
            Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет инвентаря.");
            return false;
        }
        if (Scene.TidyUp != null) Scene.TidyUp.OnInventoryChanged += Handle;
        if (Scene.Equipment != null) Scene.Equipment.OnChanged += Handle;
        return true;
    }

    protected override void Unsubscribe()
    {
        if (Scene.TidyUp != null) Scene.TidyUp.OnInventoryChanged -= Handle;
        if (Scene.Equipment != null) Scene.Equipment.OnChanged -= Handle;
    }

    private void Handle()
    {
        if (IsSatisfied()) Fire();
    }

    protected override bool IsSatisfied()
    {
        if (Scene.TidyUp != null)
            foreach (InventoryEntry entry in Scene.TidyUp.entries)
                if (entry != null && entry.item != null && entry.item.itemId == Node.item && entry.Count > 0) return true;
        if (Scene.Equipment != null)
            foreach (WorldItem worldItem in Scene.Equipment.items)
                if (worldItem != null && worldItem.itemData != null && worldItem.itemData.itemId == Node.item) return true;
        return false;
    }
}

public class LevelReachedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Progression == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет PlayerProgression."); return false; }
        Scene.Progression.OnLevelUp += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Progression.OnLevelUp -= Handle;
    private void Handle(int level, int points) { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() => Scene.Progression.CurrentLevel >= Node.value;
}

public class RestorationPercentTrigger : StoryTrigger
{
    private BuildingRestorationTracker tracker;

    protected override bool Subscribe()
    {
        tracker = BuildingRestorationTracker.Instance;
        if (tracker == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет BuildingRestorationTracker."); return false; }
        tracker.OnProgressChanged += Handle;
        return true;
    }
    protected override void Unsubscribe() { if (tracker != null) tracker.OnProgressChanged -= Handle; }
    private void Handle(float percent) { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() => tracker.ProgressPercent + 0.001f >= Node.value;
}

public class MoneyReachedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Wallet == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет PlayerWallet."); return false; }
        Scene.Wallet.OnBalanceChanged += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Wallet.OnBalanceChanged -= Handle;
    private void Handle(int balance) { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() => Scene.Wallet.Balance >= Node.value;
}

/// <summary>Игрок убил врага с точки спавна (конкретной — target, или любой) и типа (enemyType, или любого).
/// Враги, поставленные в сцену руками, а не точкой спавна, не считаются.</summary>
public class EnemyKilledTrigger : StoryTrigger
{
    private readonly List<EnemySpawnPoint> points = new List<EnemySpawnPoint>();

    protected override bool Subscribe()
    {
        if (!string.IsNullOrEmpty(Node.target))
        {
            EnemySpawnPoint point = Scene.FindByPersistentId<EnemySpawnPoint>(Node.target);
            if (point != null) points.Add(point);
        }
        else points.AddRange(Scene.All<EnemySpawnPoint>());

        // Без конкретной точки считаются и враги сюжетных волн вокруг игрока (событие «Спаун врагов»).
        bool waves = string.IsNullOrEmpty(Node.target) && Scene.Director != null;
        if (points.Count == 0 && !waves)
        {
            Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): нет точки спавна «{Node.target}».");
            return false;
        }
        foreach (EnemySpawnPoint point in points) point.OnEnemyKilled += Handle;
        if (waves) Scene.Director.OnWaveEnemyKilled += HandleWave;
        return true;
    }

    protected override void Unsubscribe()
    {
        foreach (EnemySpawnPoint point in points) if (point != null) point.OnEnemyKilled -= Handle;
        if (Scene.Director != null) Scene.Director.OnWaveEnemyKilled -= HandleWave;
    }

    private void Handle(EnemySpawnPoint point, Enemy enemy) => HandleWave(enemy);

    private void HandleWave(Enemy enemy)
    {
        string type = enemy != null && enemy.data != null ? enemy.data.enemyId : null;
        if (Matches(Node.enemyType, type)) Count();
    }
}

/// <summary>Коробка заказа (конкретного или любого) собрана и запечатана.</summary>
public class OrderPackedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Shipping == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет ShippingService."); return false; }
        Scene.Shipping.OnChanged += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Shipping.OnChanged -= Handle;
    private void Handle() { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() =>
        Scene.Shipping.IsPacked && Scene.Shipping.ActiveOrder != null && Matches(Node.orderId, Scene.Shipping.ActiveOrder.orderId);
}

public class OrderShippedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Shipping == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет ShippingService."); return false; }
        Scene.Shipping.OnShipmentCompleted += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Shipping.OnShipmentCompleted -= Handle;
    private void Handle(ShippingOrderData order) { if (Matches(Node.orderId, order != null ? order.orderId : null)) Count(); }
}

public class CrateArrivedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Supply == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет SupplyService."); return false; }
        Scene.Supply.OnCrateArrived += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Supply.OnCrateArrived -= Handle;
    private void Handle(LootBoxData box) { if (Matches(Node.lootBoxId, box != null ? box.lootBoxId : null)) Count(); }
}

public class CrateOpenedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Supply == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет SupplyService."); return false; }
        Scene.Supply.OnCrateOpened += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Supply.OnCrateOpened -= Handle;
    private void Handle(LootBoxData box) { if (Matches(Node.lootBoxId, box != null ? box.lootBoxId : null)) Count(); }
}

/// <summary>Выполнен квест, который живёт вне графа (autoStartQuests QuestManager).</summary>
public class QuestCompletedTrigger : StoryTrigger
{
    private QuestData quest;

    protected override bool Subscribe()
    {
        quest = Scene.Quests != null ? Scene.Quests.FindByQuestId(Node.questId) : null;
        if (quest == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): квест «{Node.questId}» не найден в QuestManager."); return false; }
        Scene.Quests.OnQuestCompleted += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Quests.OnQuestCompleted -= Handle;
    private void Handle(QuestProgress progress) { if (progress != null && progress.data == quest) Fire(); }
    protected override bool IsSatisfied() => Scene.Quests.IsCompleted(quest);
}

/// <summary>Ящик поставки оплачен в терминале (конкретный — lootBoxId, или любой), count раз.</summary>
public class CrateOrderedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Supply == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет SupplyService."); return false; }
        Scene.Supply.OnCrateOrdered += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Supply.OnCrateOrdered -= Handle;
    private void Handle(LootBoxData box) { if (Matches(Node.lootBoxId, box != null ? box.lootBoxId : null)) Count(); }
}

/// <summary>Из вскрытого ящика выпал предмет item — count штук (случайный лут: «первая M16 из поставки»).</summary>
public class ItemReceivedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Supply == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет SupplyService."); return false; }
        Scene.Supply.OnItemReceived += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Supply.OnItemReceived -= Handle;
    private void Handle(ItemData item) { if (Matches(Node.item, item != null ? item.itemId : null)) Count(); }
}

/// <summary>Предмет куплен в терминале напрямую (item или любой), count раз. Сработает, когда магазин
/// инструментов начнёт вызывать SupplyService.NotifyItemPurchased.</summary>
public class ItemPurchasedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Supply == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет SupplyService."); return false; }
        Scene.Supply.OnItemPurchased += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Supply.OnItemPurchased -= Handle;
    private void Handle(ItemData item) { if (Matches(Node.item, item != null ? item.itemId : null)) Count(); }
}

/// <summary>Открыт навык skillId. Уже открытый (в том числе из сейва или выданный на старте) засчитывается сразу.</summary>
public class SkillUnlockedTrigger : StoryTrigger
{
    private SkillData skill;

    protected override bool Subscribe()
    {
        if (Scene.Skills == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет PlayerSkills."); return false; }
        skill = Scene.Skills.FindBySkillId(Node.skillId);
        if (skill == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): навык «{Node.skillId}» не найден в SkillCatalog."); return false; }
        Scene.Skills.OnSkillsChanged += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Skills.OnSkillsChanged -= Handle;
    private void Handle() { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() => Scene.Skills.IsOwned(skill);
}

// ───────────────────────── Время суток и выживание ─────────────────────────

/// <summary>Общая часть триггеров времени: нужен GameClock.</summary>
public abstract class ClockTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Clock == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет GameClock."); return false; }
        SubscribeClock(Scene.Clock);
        return true;
    }
    protected override void Unsubscribe() { if (Scene.Clock != null) UnsubscribeClock(Scene.Clock); }
    protected abstract void SubscribeClock(GameClock clock);
    protected abstract void UnsubscribeClock(GameClock clock);
}

/// <summary>Наступила ночь (TimeSettings.nightHour). Если триггер включился уже ночью — срабатывает сразу.</summary>
public class NightStartedTrigger : ClockTrigger
{
    protected override void SubscribeClock(GameClock clock) => clock.OnNightStarted += Fire;
    protected override void UnsubscribeClock(GameClock clock) => clock.OnNightStarted -= Fire;
    protected override bool IsSatisfied() => Scene.Clock.IsNight;
}

/// <summary>Наступил вечер (TimeSettings.eveningHour). Уже вечер или ночь — срабатывает сразу.</summary>
public class EveningStartedTrigger : ClockTrigger
{
    protected override void SubscribeClock(GameClock clock) => clock.OnEveningStarted += Fire;
    protected override void UnsubscribeClock(GameClock clock) => clock.OnEveningStarted -= Fire;
    protected override bool IsSatisfied() => Scene.Clock.IsEvening;
}

/// <summary>Наступило утро нового дня — count раз (считается с момента, когда триггер начал слушать).</summary>
public class MorningStartedTrigger : ClockTrigger
{
    protected override void SubscribeClock(GameClock clock) => clock.OnDayStarted += Handle;
    protected override void UnsubscribeClock(GameClock clock) => clock.OnDayStarted -= Handle;
    private void Handle(int day) => Count();
}

/// <summary>Идёт день № value или позже.</summary>
public class DayReachedTrigger : ClockTrigger
{
    protected override void SubscribeClock(GameClock clock) => clock.OnDayStarted += Handle;
    protected override void UnsubscribeClock(GameClock clock) => clock.OnDayStarted -= Handle;
    private void Handle(int day) { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() => Scene.Clock.Day >= Mathf.RoundToInt(Node.value);
}

/// <summary>На часах наступил час value (0–23) — ближайший после того, как триггер начал слушать.</summary>
public class HourReachedTrigger : ClockTrigger
{
    protected override void SubscribeClock(GameClock clock) => clock.OnHourChanged += Handle;
    protected override void UnsubscribeClock(GameClock clock) => clock.OnHourChanged -= Handle;
    private void Handle(int hour) { if (hour == Mathf.RoundToInt(Node.value) % 24) Fire(); }
}

/// <summary>Сытость ≤ value (below) или ≥ value. Проверяется каждый игровой час, после еды и сна.</summary>
public class SatietyTrigger : StoryTrigger
{
    private readonly bool below;
    public SatietyTrigger(bool below) { this.below = below; }

    protected override bool Subscribe()
    {
        if (Scene.Consumption == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет PlayerConsumption."); return false; }
        Scene.Consumption.OnConsumed += HandleItem;
        if (Scene.Clock != null) { Scene.Clock.OnHourChanged += HandleHour; Scene.Clock.OnTimeSkipped += HandleSkip; }
        return true;
    }
    protected override void Unsubscribe()
    {
        Scene.Consumption.OnConsumed -= HandleItem;
        if (Scene.Clock != null) { Scene.Clock.OnHourChanged -= HandleHour; Scene.Clock.OnTimeSkipped -= HandleSkip; }
    }
    private void HandleItem(ItemData item) => Check();
    private void HandleHour(int hour) => Check();
    private void HandleSkip(double from, double to) => Check();
    private void Check() { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() => below ? Scene.Consumption.Satiety <= Node.value : Scene.Consumption.Satiety >= Node.value;
}

/// <summary>Усталость ≥ value (0–100). Проверяется каждый игровой час и при смене «устал»/«измотан».</summary>
public class FatigueAboveTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Stamina == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет PlayerStamina."); return false; }
        Scene.Stamina.OnTiredChanged += HandleFlag;
        Scene.Stamina.OnExhaustedChanged += HandleFlag;
        if (Scene.Clock != null) Scene.Clock.OnHourChanged += HandleHour;
        return true;
    }
    protected override void Unsubscribe()
    {
        Scene.Stamina.OnTiredChanged -= HandleFlag;
        Scene.Stamina.OnExhaustedChanged -= HandleFlag;
        if (Scene.Clock != null) Scene.Clock.OnHourChanged -= HandleHour;
    }
    private void HandleFlag(bool on) { if (IsSatisfied()) Fire(); }
    private void HandleHour(int hour) { if (IsSatisfied()) Fire(); }
    protected override bool IsSatisfied() => Scene.Stamina.Fatigue >= Node.value;
}

/// <summary>Игрок выдохся (бар выносливости на нуле) — count раз.</summary>
public class PlayerWindedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Stamina == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет PlayerStamina."); return false; }
        Scene.Stamina.OnWindedChanged += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Stamina.OnWindedChanged -= Handle;
    private void Handle(bool winded) { if (winded) Count(); }
}

/// <summary>Игрок поспал и проснулся (в том числе упал без сил) — count раз.</summary>
public class PlayerSleptTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Sleep == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет SleepService."); return false; }
        Scene.Sleep.OnWokeUp += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Sleep.OnWokeUp -= Handle;
    private void Handle(DayReport report) => Count();
}

/// <summary>Съеден или выпит предмет (item, пусто — любой) — count раз.</summary>
public class ItemConsumedTrigger : StoryTrigger
{
    protected override bool Subscribe()
    {
        if (Scene.Consumption == null) { Scene.Warn(Node.id, $"Триггер «{Node.trigger}» ({Node.id}): в сцене нет PlayerConsumption."); return false; }
        Scene.Consumption.OnConsumed += Handle;
        return true;
    }
    protected override void Unsubscribe() => Scene.Consumption.OnConsumed -= Handle;
    private void Handle(ItemData item) { if (Matches(Node.item, item != null ? item.itemId : null)) Count(); }
}
