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
    [Tooltip("Пул заданий, которые выдаются автоматически, как только выполнены их условия открытия " +
             "(prerequisiteQuests и requiredLevel в QuestData). Квест без условий стартует сразу при загрузке уровня.")]
    public QuestData[] autoStartQuests = Array.Empty<QuestData>();

    [Header("Условия открытия")]
    [Tooltip("Источник уровня игрока для QuestData.requiredLevel. Пусто — уровень считается равным 1.")]
    public PlayerProgression playerProgression;

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

    private readonly HashSet<QuestData> startedQuests = new HashSet<QuestData>();
    private readonly HashSet<QuestData> completedQuests = new HashSet<QuestData>();

    // Квесты сюжетного графа (StoryDirector): их запускает граф, а не условия открытия, но сейв должен
    // находить их по id так же, как квесты autoStartQuests.
    private readonly List<QuestData> storyQuests = new List<QuestData>();
    private readonly HashSet<QuestData> storyQuestSet = new HashSet<QuestData>();

    // Активные квесты из сейва: RestoreActive вызывается из SaveLoadService.Awake, когда полки ещё
    // не собрали свои ячейки (Shelf.Awake), а PlayerProgression/HUD ещё не подписались на события.
    // Подписка на цели и пересчёт прогресса для них откладываются до Start.
    private readonly List<QuestProgress> pendingRestoreDispatch = new List<QuestProgress>();

    // Старт квеста может тут же его завершить (ShelveItems учитывает уже стоящие предметы), а завершение
    // снова вызывает EvaluateUnlocks — без защиты получили бы вложенный обход пула посреди обхода.
    private bool isEvaluating;
    private bool needsReevaluate;

    /// <summary>Id всех завершённых квестов — для сохранения. ActiveQuests уже публичный и
    /// содержит только незавершённые (CompleteQuest вычищает их оттуда), поэтому отдельного
    /// свойства для активных квестов не нужно — их id сохраняющий код берёт прямо из ActiveQuests.</summary>
    public IEnumerable<string> CompletedQuestIds
    {
        get { foreach (var data in completedQuests) yield return data.questId; }
    }

    private int PlayerLevel => playerProgression != null ? playerProgression.CurrentLevel : 1;

    /// <summary>Найти квест по id из сейва: в autoStartQuests и среди квестов сюжетного графа.</summary>
    public QuestData FindByQuestId(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        foreach (var data in autoStartQuests)
            if (data != null && data.questId == questId) return data;
        foreach (var data in storyQuests)
            if (data != null && data.questId == questId) return data;
        return null;
    }

    /// <summary>
    /// Квесты сюжетного графа (зовёт StoryDirector в Awake — раньше загрузки сейва). Их запускает граф:
    /// автоматика условий открытия их пропускает, даже если они остались в autoStartQuests, а сейв находит
    /// их по id через FindByQuestId.
    /// </summary>
    public void RegisterStoryQuests(IEnumerable<QuestData> quests)
    {
        if (quests == null) return;
        foreach (var data in quests)
            if (data != null && storyQuestSet.Add(data)) storyQuests.Add(data);
    }

    /// <summary>Квест уже завершён (в этой сессии или восстановлен из сейва).</summary>
    public bool IsCompleted(QuestData data) => data != null && completedQuests.Contains(data);

    /// <summary>Квест уже запускался (активен или завершён).</summary>
    public bool IsStarted(QuestData data) => data != null && startedQuests.Contains(data);

    private void OnEnable()
    {
        if (playerProgression != null) playerProgression.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        if (playerProgression != null) playerProgression.OnLevelUp -= HandleLevelUp;
    }

    private void Start()
    {
        // Сначала восстановленные квесты: их пересчёт по уже восстановленному состоянию сцены может
        // их тут же завершить — это настоящее, ещё не вознаграждённое завершение (в сейв квест попал
        // незавершённым), поэтому событие и опыт здесь уместны. Затем — открытие новых квестов.
        foreach (var progress in pendingRestoreDispatch)
            DispatchStart(progress);
        pendingRestoreDispatch.Clear();

        EvaluateUnlocks();
    }

    private void OnDestroy()
    {
        foreach (var cleanup in cleanupByQuest.Values)
            cleanup?.Invoke();
        cleanupByQuest.Clear();
    }

    private void HandleLevelUp(int newLevel, int unlockPointsGranted) => EvaluateUnlocks();

    /// <summary>Запустить все ещё не запущенные квесты из пула, у которых выполнены условия открытия.</summary>
    private void EvaluateUnlocks()
    {
        if (isEvaluating)
        {
            needsReevaluate = true;
            return;
        }

        isEvaluating = true;
        do
        {
            needsReevaluate = false;
            foreach (var data in autoStartQuests)
                if (data != null && !storyQuestSet.Contains(data) && !startedQuests.Contains(data) && IsUnlocked(data))
                    StartQuest(data);
        }
        while (needsReevaluate);
        isEvaluating = false;
    }

    private bool IsUnlocked(QuestData data)
    {
        if (PlayerLevel < data.requiredLevel) return false;

        foreach (var prerequisite in data.prerequisiteQuests)
            if (prerequisite != null && !completedQuests.Contains(prerequisite)) return false;

        return true;
    }

    /// <summary>Запустить квест. Публичная точка расширения — сюда стартуют квесты сюжетного графа
    /// (StoryDirector). Ручной запуск обходит условия открытия (prerequisiteQuests/requiredLevel).
    /// Уже запущенный или завершённый квест повторно не стартует.</summary>
    public void StartQuest(QuestData data)
    {
        if (data == null || startedQuests.Contains(data)) return;

        startedQuests.Add(data);
        var progress = new QuestProgress { data = data };
        ActiveQuests.Add(progress);
        OnQuestStarted?.Invoke(progress);

        DispatchStart(progress);
    }

    /// <summary>
    /// Восстановление активного (начатого, но не завершённого) квеста из сейва. Тот же путь
    /// запуска, что и StartQuest, но без OnQuestStarted — общий принцип: восстановление не
    /// поднимает событий, иначе задваивало бы реакцию систем на уже случившийся прогресс.
    /// QuestHudUI строку всё равно не теряет — при своём OnEnable он сам обходит ActiveQuests
    /// (см. комментарий там), не полагаясь на то, что застанет именно момент события.
    ///
    /// initialCount нужен только DeliverItem: доставленный предмет уничтожается, пересчитать
    /// прогресс не из чего. Для CleanStains/RepairPoints/ShelveItems значение игнорируется —
    /// их StartXxx сам пересчитывает currentCount от текущего состояния целей.
    ///
    /// Сам запуск (подписка на цели и пересчёт) — в Start: см. pendingRestoreDispatch.
    /// </summary>
    public void RestoreActive(QuestData data, int initialCount)
    {
        if (data == null || startedQuests.Contains(data)) return;

        startedQuests.Add(data);
        var progress = new QuestProgress { data = data };
        ActiveQuests.Add(progress);
        progress.currentCount = Mathf.Clamp(initialCount, 0, data.targetCount);

        pendingRestoreDispatch.Add(progress);
    }

    /// <summary>Квест был завершён в прошлой сессии: учитывается в prerequisiteQuests и никогда
    /// не перезапустится, но никакой реакции (XP, HUD) не происходит — она уже случилась тогда.</summary>
    public void RestoreCompleted(QuestData data)
    {
        if (data == null || completedQuests.Contains(data)) return;
        startedQuests.Add(data);
        completedQuests.Add(data);
    }

    private void DispatchStart(QuestProgress progress)
    {
        switch (progress.data.type)
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
        // Include: уже очищенное пятно без отдельного stainVisual выключает сам себя
        // (CleanableStain.ApplyClean → gameObject.SetActive(false)) — Exclude потерял бы его
        // из подсчёта именно тогда, когда он и нужен: при восстановлении уже выполненного прогресса.
        foreach (var stain in FindObjectsByType<CleanableStain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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

        // Пересчёт от факта, а не от нуля: после восстановления сейва часть целей уже может
        // быть очищена (RestoreClean не поднимает событий, на которые опирается AdvanceQuest).
        int alreadyDone = 0;
        foreach (var stain in targets) if (stain != null && stain.IsClean) alreadyDone++;
        if (alreadyDone > 0) SetProgress(progress, alreadyDone);
    }

    private void StartRepairPoints(QuestProgress progress)
    {
        var targets = new List<RepairPoint>();
        foreach (var point in FindObjectsByType<RepairPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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

        int alreadyDone = 0;
        foreach (var point in targets) if (point != null && point.IsRepaired) alreadyDone++;
        if (alreadyDone > 0) SetProgress(progress, alreadyDone);
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
        // Сначала подписка, потом заявка: AddRequest сразу принимает предметы, которые уже лежат в
        // зоне (например, кирпич бросили туда до того, как квест открылся).
        foreach (var zone in zones) zone.OnItemDelivered += handler;
        foreach (var zone in zones) zone.AddRequest(progress.data.targetItem);
        cleanupByQuest[progress] = () =>
        {
            foreach (var zone in zones)
            {
                if (zone == null) continue;
                zone.RemoveRequest(progress.data.targetItem);
                zone.OnItemDelivered -= handler;
            }
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

        // Порядок важен: квест должен попасть в completedQuests ДО события — подписчик (PlayerProgression)
        // может начислить XP, получить левелап и через OnLevelUp снова вызвать EvaluateUnlocks.
        completedQuests.Add(progress.data);
        OnQuestCompleted?.Invoke(progress);
        EvaluateUnlocks();
    }
}
