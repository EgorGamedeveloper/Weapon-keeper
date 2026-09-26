using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно «Достижения» (открывается из меню паузы): список всех достижений каталога с прогрессом по
/// статистике, скрытые — без названия до получения. Строки строятся по шаблону при первом открытии и
/// обновляются при получении достижения и смене языка. Esc — закрыть.
/// </summary>
public class AchievementsWindow : MonoBehaviour
{
    [Header("Окно")]
    [Tooltip("Корень окна (затемнение + панель) — включается при открытии.")]
    public CanvasGroup windowGroup;

    [Tooltip("Панель окна — «выпрыгивает» при открытии.")]
    public RectTransform panel;

    [Tooltip("Кнопка «Закрыть».")]
    public Button closeButton;

    [Header("Список")]
    [Tooltip("Контейнер строк (VerticalLayoutGroup внутри прокрутки).")]
    public RectTransform listRoot;

    [Tooltip("Шаблон строки (выключенный объект внутри окна).")]
    public AchievementRowUI rowTemplate;

    [Tooltip("Прокрутка списка.")]
    public ScrollRect scrollRect;

    [Tooltip("Итог «Получено: 3 из 20».")]
    public TMP_Text summaryText;

    /// <summary>Окно открыто.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Окно закрылось.</summary>
    public event Action OnClosed;

    private readonly List<(AchievementData data, AchievementRowUI row)> rows = new List<(AchievementData, AchievementRowUI)>();
    private StatsService stats;
    private LocalizationService localization;

    private void Awake()
    {
        UiWindowAnimation.HideImmediate(windowGroup);
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Open()
    {
        if (IsOpen) return;

        stats = StatsService.Instance;
        if (stats == null || stats.Catalog == null)
        {
            Debug.LogWarning("[Achievements] В игре нет StatsService с каталогом — окно достижений не откроется.", this);
            return;
        }

        IsOpen = true;
        if (rows.Count == 0) BuildRows();

        stats.OnAchievementUnlocked += HandleUnlocked;
        localization = LocalizationService.Instance;
        if (localization != null) localization.OnLanguageChanged += Refresh;

        Refresh();
        UiWindowAnimation.Show(windowGroup, panel);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Unsubscribe();
        UiWindowAnimation.Hide(windowGroup, panel);
        OnClosed?.Invoke();
    }

    private void Unsubscribe()
    {
        if (stats != null) stats.OnAchievementUnlocked -= HandleUnlocked;
        if (localization != null) localization.OnLanguageChanged -= Refresh;
        localization = null;
    }

    private void BuildRows()
    {
        if (rowTemplate == null || listRoot == null) return;

        var achievements = new List<AchievementData>();
        foreach (AchievementData achievement in stats.Catalog.achievements)
            if (achievement != null) achievements.Add(achievement);
        achievements.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.apiName, b.apiName));

        foreach (AchievementData achievement in achievements)
        {
            AchievementRowUI row = Instantiate(rowTemplate, listRoot);
            row.gameObject.SetActive(true);
            row.name = achievement.apiName;
            rows.Add((achievement, row));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
    }

    private void Refresh()
    {
        int unlocked = 0;
        foreach ((AchievementData data, AchievementRowUI row) in rows)
        {
            bool isUnlocked = stats.IsUnlocked(data);
            if (isUnlocked) unlocked++;
            row.Bind(data, isUnlocked, stats.GetValue(data.progressStat));
        }

        if (summaryText != null) summaryText.text = Loc.Get("achievements.summary", unlocked, rows.Count);
    }

    private void HandleUnlocked(AchievementData achievement) => Refresh();
}
