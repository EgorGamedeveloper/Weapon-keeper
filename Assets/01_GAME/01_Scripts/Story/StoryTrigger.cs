using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Нода «Триггер» сюжетного графа: ждёт действия игрока, которое не было квестом (починил лифт, сломал
/// дверь, отправил заказ...). Каждый вид — маленький наследник: как подписаться на существующее событие
/// игры и как проверить уже сложившееся состояние. Если условие выполнено к моменту, когда триггер начал
/// слушать (в том числе после загрузки сейва — восстановление событий не поднимает), он срабатывает сразу.
///
/// Добавить вид: наследник в StoryTriggerKinds.cs + строка в Registry ниже + строка в списке TRIGGERS
/// веб-редактора (Tools/StoryEditor/story_editor.html). Остальная система не меняется.
///
/// Абстрактный класс, а не интерфейс: общая часть (однократное срабатывание, отписка, счётчик) одна на
/// всех, наследнику остаются только подписка и проверка.
/// </summary>
public abstract class StoryTrigger
{
    private static readonly Dictionary<string, Func<StoryTrigger>> Registry = new Dictionary<string, Func<StoryTrigger>>
    {
        { "repairPointRepaired", () => new RepairPointRepairedTrigger() },
        { "breakableBroken", () => new BreakableBrokenTrigger() },
        { "stainCleaned", () => new StainCleanedTrigger() },
        { "shelfItemsPlaced", () => new ShelfItemsPlacedTrigger() },
        { "itemDelivered", () => new ItemDeliveredTrigger() },
        { "itemPickedUp", () => new ItemPickedUpTrigger() },
        { "levelReached", () => new LevelReachedTrigger() },
        { "restorationPercent", () => new RestorationPercentTrigger() },
        { "moneyReached", () => new MoneyReachedTrigger() },
        { "enemyKilled", () => new EnemyKilledTrigger() },
        { "orderPacked", () => new OrderPackedTrigger() },
        { "orderShipped", () => new OrderShippedTrigger() },
        { "crateArrived", () => new CrateArrivedTrigger() },
        { "crateOpened", () => new CrateOpenedTrigger() },
        { "questCompleted", () => new QuestCompletedTrigger() },
    };

    /// <summary>Создать триггер вида kind (null — неизвестный вид).</summary>
    public static StoryTrigger Create(string kind) =>
        kind != null && Registry.TryGetValue(kind, out Func<StoryTrigger> factory) ? factory() : null;

    /// <summary>Известные виды — для проверки графа при импорте.</summary>
    public static IEnumerable<string> Kinds => Registry.Keys;

    protected StoryNodeData Node { get; private set; }
    protected StoryScene Scene { get; private set; }

    private Action onFired;
    private bool listening;
    private bool fired;
    private int counter;

    /// <summary>Начать слушать. onFired вызывается один раз.</summary>
    public void Begin(StoryNodeData node, StoryScene scene, Action fired)
    {
        Node = node;
        Scene = scene;
        onFired = fired;

        if (!Subscribe()) return;
        listening = true;
        if (IsSatisfied()) Fire();
    }

    /// <summary>Перестать слушать (нода завершилась или сюжет выгружается).</summary>
    public void End()
    {
        if (!listening) return;
        listening = false;
        Unsubscribe();
    }

    /// <summary>Подписаться на события игры. false — цели нет в сцене (триггер никогда не сработает;
    /// наследник сам пишет предупреждение через Scene.Warn).</summary>
    protected abstract bool Subscribe();

    protected abstract void Unsubscribe();

    /// <summary>Условие уже выполнено (состояние сцены). По умолчанию — нет: триггер ждёт события.</summary>
    protected virtual bool IsSatisfied() => false;

    /// <summary>Сработать (один раз).</summary>
    protected void Fire()
    {
        if (fired) return;
        fired = true;
        End();
        onFired?.Invoke();
    }

    /// <summary>Для событий, которые надо набрать count раз (убито врагов, отправлено заказов): счёт идёт
    /// с момента, когда триггер начал слушать, и после загрузки сейва начинается заново.</summary>
    protected void Count()
    {
        if (++counter >= Mathf.Max(1, Node.count)) Fire();
    }

    protected bool Matches(string wanted, string actual) => string.IsNullOrEmpty(wanted) || wanted == actual;
}
