using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Список активных заданий вверху слева экрана. Динамически создаёт/убирает строки по событиям
/// QuestManager — по образцу TidyUpInventoryUI, но со строками, привязанными не к индексу слота,
/// а к конкретному QuestProgress (квесты появляются и пропадают в произвольном порядке).
/// </summary>
public class QuestHudUI : MonoBehaviour
{
    public QuestManager questManager;
    public Transform contentRoot;
    public QuestRowUI rowPrefab;

    [Tooltip("Длительность анимации исчезновения строки завершённого квеста.")]
    public float completeFadeDuration = 0.3f;

    private readonly Dictionary<QuestProgress, QuestRowUI> rows = new Dictionary<QuestProgress, QuestRowUI>();

    private void Awake()
    {
        // Если строку по ошибке оставили в сцене как шаблон — не показываем её как "лишнюю" запись.
        if (rowPrefab != null && contentRoot != null && rowPrefab.transform.parent == contentRoot)
            rowPrefab.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (questManager == null) return;

        questManager.OnQuestStarted += HandleQuestStarted;
        questManager.OnQuestProgressChanged += HandleQuestProgressChanged;
        questManager.OnQuestCompleted += HandleQuestCompleted;

        // Догнать квесты, уже запущенные до включения этого UI (autoStartQuests стартуют в Start()
        // менеджера, который может выполниться раньше, чем включится этот компонент).
        foreach (var progress in questManager.ActiveQuests)
            HandleQuestStarted(progress);
    }

    private void OnDisable()
    {
        if (questManager == null) return;

        questManager.OnQuestStarted -= HandleQuestStarted;
        questManager.OnQuestProgressChanged -= HandleQuestProgressChanged;
        questManager.OnQuestCompleted -= HandleQuestCompleted;
    }

    private void HandleQuestStarted(QuestProgress progress)
    {
        if (contentRoot == null || rowPrefab == null || rows.ContainsKey(progress)) return;

        var row = Instantiate(rowPrefab, contentRoot);
        row.gameObject.SetActive(true);
        row.Bind(progress);
        rows[progress] = row;
    }

    private void HandleQuestProgressChanged(QuestProgress progress)
    {
        if (rows.TryGetValue(progress, out var row)) row.Bind(progress);
    }

    private void HandleQuestCompleted(QuestProgress progress)
    {
        if (!rows.TryGetValue(progress, out var row)) return;
        rows.Remove(progress);

        row.Bind(progress);
        row.transform.DOKill();
        row.transform.DOScale(Vector3.zero, completeFadeDuration)
            .OnComplete(() => { if (row != null) Destroy(row.gameObject); });
    }
}
