using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Всплывающее уведомление «Достижение получено» в углу экрана — только когда достижения локальные
/// (билд без Steam, Play в редакторе): Steam показывает своё уведомление в оверлее сам. Несколько
/// достижений подряд показываются по очереди. Живёт в PersistentServices (свой канвас поверх всего),
/// анимации — в реальном времени: достижение может прийти и на паузе.
/// </summary>
public class AchievementToastUI : MonoBehaviour
{
    [Header("Тост")]
    [Tooltip("Группа тоста — проявляется и гаснет.")]
    public CanvasGroup group;

    [Tooltip("Панель тоста — выезжает из-за края экрана.")]
    public RectTransform panel;

    [Tooltip("Иконка достижения. Без иконки у достижения — скрывается.")]
    public Image icon;

    [Tooltip("Название достижения.")]
    public TMP_Text titleText;

    [Header("Время")]
    [Tooltip("Сколько секунд тост висит на экране.")]
    [Min(0.5f)] public float showSeconds = 3.5f;

    [Tooltip("На сколько пикселей вправо тост уезжает за край экрана.")]
    public float slideDistance = 520f;

    private readonly Queue<AchievementData> queue = new Queue<AchievementData>();
    private StatsService stats;
    private Vector2 shownPosition;
    private Sequence sequence;
    private bool showing;

    private void Awake()
    {
        if (panel != null) shownPosition = panel.anchoredPosition;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    private void OnEnable()
    {
        stats = StatsService.Instance;
        if (stats != null) stats.OnAchievementUnlocked += HandleUnlocked;
    }

    private void OnDisable()
    {
        if (stats != null) stats.OnAchievementUnlocked -= HandleUnlocked;
        stats = null;
        sequence?.Kill();
        sequence = null;
        showing = false;
        queue.Clear();
    }

    private void HandleUnlocked(AchievementData achievement)
    {
        if (stats == null || !stats.ShowToasts || achievement == null) return;
        queue.Enqueue(achievement);
        if (!showing) ShowNext();
    }

    private void ShowNext()
    {
        if (queue.Count == 0 || group == null || panel == null)
        {
            showing = false;
            return;
        }

        showing = true;
        AchievementData achievement = queue.Dequeue();
        if (titleText != null) titleText.text = Loc.Get(achievement.nameKey);
        if (icon != null)
        {
            icon.sprite = achievement.icon;
            icon.enabled = achievement.icon != null;
        }

        Vector2 hiddenPosition = shownPosition + new Vector2(slideDistance, 0f);
        panel.anchoredPosition = hiddenPosition;
        group.alpha = 0f;

        sequence = DOTween.Sequence()
            .Append(panel.DOAnchorPos(shownPosition, 0.35f).SetEase(Ease.OutCubic))
            .Join(group.DOFade(1f, 0.25f))
            .AppendInterval(showSeconds)
            .Append(group.DOFade(0f, 0.3f))
            .Join(panel.DOAnchorPos(hiddenPosition, 0.3f).SetEase(Ease.InCubic))
            .SetUpdate(true)
            .OnComplete(ShowNext);
    }
}
