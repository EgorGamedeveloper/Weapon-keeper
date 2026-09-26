using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Список активных заданий вверху слева экрана. Динамически создаёт/убирает строки по событиям
/// QuestManager — по образцу TidyUpInventoryUI, но со строками, привязанными не к индексу слота,
/// а к конкретному QuestProgress (квесты появляются и пропадают в произвольном порядке).
/// </summary>
public class QuestHudUI : MonoBehaviour
{
    [Tooltip("Источник активных квестов.")]
    public QuestManager questManager;
    [Tooltip("Контейнер для строк квестов.")]
    public Transform contentRoot;
    [Tooltip("Префаб строки квеста.")]
    public QuestRowUI rowPrefab;

    [Tooltip("Длительность анимации исчезновения строки завершённого квеста.")]
    public float completeFadeDuration = 0.3f;

    private readonly Dictionary<QuestProgress, QuestRowUI> rows = new Dictionary<QuestProgress, QuestRowUI>();

    private Image background;

    private void Awake()
    {
        background = GetComponent<Image>();

        // Если строку по ошибке оставили в сцене как шаблон — не показываем её как "лишнюю" запись.
        if (rowPrefab != null && contentRoot != null && rowPrefab.transform.parent == contentRoot)
            rowPrefab.gameObject.SetActive(false);
    }

    // Фон панели сам растёт по высоте строк (Layout Group), но без квестов остался бы пустой полоской.
    private void RefreshBackground()
    {
        if (background != null) background.enabled = rows.Count > 0;
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

        RefreshBackground();
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
        RefreshBackground();
    }

    private void HandleQuestProgressChanged(QuestProgress progress)
    {
        if (rows.TryGetValue(progress, out var row)) row.Bind(progress);
    }

    private void HandleQuestCompleted(QuestProgress progress)
    {
        if (!rows.TryGetValue(progress, out var row)) return;
        rows.Remove(progress);
        RefreshBackground();

        row.Bind(progress);
        row.transform.DOKill();
        row.transform.DOScale(Vector3.zero, completeFadeDuration)
            .OnComplete(() => { if (row != null) Destroy(row.gameObject); });
    }
}
