using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Рантайм-прогресс одного активного квеста. Шаблон условий и наград — в QuestData,
/// это состояние конкретного запуска (может быть несколько активных квестов на один QuestData).</summary>
public class QuestProgress
{
    public QuestData data;
    public int currentCount;
    public bool isCompleted;
}

/// <summary>
/// Держит список активных заданий игрока, сам находит на сцене подходящие объекты (пятна, точки
/// ремонта, полки, зоны доставки) и подписывается на их существующие события, продвигая прогресс
/// квеста. Синглтона нет — потребители (UI, PlayerProgression) получают ссылку через инспектор,
/// как и остальные новые компоненты проекта (см. GameBootstrap).
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("Квесты")]
    [Tooltip("Задания, которые стартуют автоматически при загрузке уровня.")]
    public QuestData[] autoStartQuests = Array.Empty<QuestData>();

    /// <summary>Все сейчас активные (незавершённые) квесты.</summary>
    public readonly List<QuestProgress> ActiveQuests = new List<QuestProgress>();

    /// <summary>Квест запущен — хук для UI (добавить строку в HUD).</summary>
    public event Action<QuestProgress> OnQuestStarted;

    /// <summary>Прогресс квеста изменился — хук для UI (обновить строку в HUD).</summary>
    public event Action<QuestProgress> OnQuestProgressChanged;

    /// <summary>Квест завершён — хук для UI (убрать строку) и PlayerProgression (начислить XP).</summary>
    public event Action<QuestProgress> OnQuestCompleted;

    /// <summary>Отписка от событий сцены, заведённых под конкретный квест при его старте.</summary>
    private readonly Dictionary<QuestProgress, Action> cleanupByQuest = new Dictionary<QuestProgress, Action>();

    private void Start()
    {
        foreach (var data in autoStartQuests)
            if (data != null) StartQuest(data);
    }

    private void OnDestroy()
    {
        foreach (var cleanup in cleanupByQuest.Values)
            cleanup?.Invoke();
        cleanupByQuest.Clear();
    }

    /// <summary>Запустить квест. Публичная точка расширения — позже сюда смогут стартовать квесты
    /// диалоги, триггеры уровня и т.п., не только autoStartQuests.</summary>
    public void StartQuest(QuestData data)
    {
        if (data == null) return;

        var progress = new QuestProgress { data = data };
        ActiveQuests.Add(progress);
        OnQuestStarted?.Invoke(progress);

        switch (data.type)
        {
            case QuestData.QuestType.CleanStains: StartCleanStains(progress); break;
            case QuestData.QuestType.RepairPoints: StartRepairPoints(progress); break;
            case QuestData.QuestType.ShelveItems: StartShelveItems(progress); break;
            case QuestData.QuestType.DeliverItem: StartDeliverItem(progress); break;
        }
    }

    private void StartCleanStains(QuestProgress progress)
    {
        var targets = new List<CleanableStain>();
        foreach (var stain in FindObjectsByType<CleanableStain>(FindObjectsSortMode.None))
        {
            if (!MatchesPersistentId(stain.GetComponent<PersistentId>(), progress.data.targetPersistentId)) continue;
            targets.Add(stain);
        }

        Action<CleanableStain> handler = _ => AdvanceQuest(progress);
        foreach (var stain in targets) stain.OnCleaned += handler;
        cleanupByQuest[progress] = () =>
        {
            foreach (var stain in targets)
                if (stain != null) stain.OnCleaned -= handler;
        };
    }

    private void StartRepairPoints(QuestProgress progress)
    {
        var targets = new List<RepairPoint>();
        foreach (var point in FindObjectsByType<RepairPoint>(FindObjectsSortMode.None))
        {
            if (!MatchesPersistentId(point.GetComponent<PersistentId>(), progress.data.targetPersistentId)) continue;
            targets.Add(point);
        }

        Action<RepairPoint> handler = _ => AdvanceQuest(progress);
        foreach (var point in targets) point.OnRepaired += handler;
        cleanupByQuest[progress] = () =>
        {
            foreach (var point in targets)
                if (point != null) point.OnRepaired -= handler;
        };
    }

    private void StartShelveItems(QuestProgress progress)
    {
        var slots = new List<ShelfSlot>();
        foreach (var shelf in FindObjectsByType<Shelf>(FindObjectsSortMode.None))
        {
            if (shelf.acceptedCategory != progress.data.targetCategory) continue;
            if (!MatchesPersistentId(shelf.GetComponent<PersistentId>(), progress.data.targetPersistentId)) continue;

            foreach (var slot in shelf.slots)
                if (slot != null) slots.Add(slot);
        }

        // Пересчёт по содержимому ячеек, а не инкремент по событию — устойчиво к
        // "поставил-снял-поставил" и учитывает то, что уже стояло на полке до старта квеста.
        Action<ShelfSlot> handler = _ => RecomputeShelveItems(progress, slots);
        foreach (var slot in slots)
        {
            slot.OnItemPlaced += handler;
            slot.OnItemRemoved += handler;
        }
        cleanupByQuest[progress] = () =>
        {
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                slot.OnItemPlaced -= handler;
                slot.OnItemRemoved -= handler;
            }
        };

        RecomputeShelveItems(progress, slots);
    }

    private void RecomputeShelveItems(QuestProgress progress, List<ShelfSlot> slots)
    {
        int count = 0;
        foreach (var slot in slots)
            if (slot != null) count += slot.StackCount;
        SetProgress(progress, count);
    }

    private void StartDeliverItem(QuestProgress progress)
    {
        var zones = new List<DeliveryZone>();
        foreach (var zone in FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None))
        {
            if (!string.IsNullOrEmpty(progress.data.targetZoneId) && zone.zoneId != progress.data.targetZoneId) continue;
            zones.Add(zone);
        }

        Action<DeliveryZone, WorldItem> handler = (_, item) =>
        {
            if (progress.data.targetItem != null && item.itemData != progress.data.targetItem) return;
            AdvanceQuest(progress);
        };
        foreach (var zone in zones) zone.OnItemDelivered += handler;
        cleanupByQuest[progress] = () =>
        {
            foreach (var zone in zones)
                if (zone != null) zone.OnItemDelivered -= handler;
        };
    }

    private static bool MatchesPersistentId(PersistentId id, string targetPersistentId)
    {
        if (string.IsNullOrEmpty(targetPersistentId)) return true;
        return id != null && id.Id == targetPersistentId;
    }

    private void AdvanceQuest(QuestProgress progress) => SetProgress(progress, progress.currentCount + 1);

    private void SetProgress(QuestProgress progress, int newCount)
    {
        if (progress.isCompleted) return;

        progress.currentCount = Mathf.Clamp(newCount, 0, progress.data.targetCount);
        OnQuestProgressChanged?.Invoke(progress);

        if (progress.currentCount >= progress.data.targetCount)
            CompleteQuest(progress);
    }

    private void CompleteQuest(QuestProgress progress)
    {
        progress.isCompleted = true;

        if (cleanupByQuest.TryGetValue(progress, out var cleanup))
        {
            cleanup?.Invoke();
            cleanupByQuest.Remove(progress);
        }

        ActiveQuests.Remove(progress);
        OnQuestCompleted?.Invoke(progress);
    }
}
