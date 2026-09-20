using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Одна строка HUD активных квестов: заголовок, счётчик "X/Y" и прогресс-бар. Стиль — как
/// TidyUpInventoryRowUI: полоса плавно тянется к новому значению через DOTween, а не прыгает.
/// </summary>
public class QuestRowUI : MonoBehaviour
{
    public Text title;
    public Text progressText;
    public Image progressFill;

    [Tooltip("Длительность анимации подтягивания прогресс-бара к новому значению.")]
    public float fillTweenDuration = 0.25f;

    private bool initialized;

    public void Bind(QuestProgress progress)
    {
        if (progress == null || progress.data == null) return;

        if (title != null) title.text = progress.data.title;
        if (progressText != null) progressText.text = $"{progress.currentCount}/{progress.data.targetCount}";

        if (progressFill != null)
        {
            float target = progress.data.targetCount > 0
                ? (float)progress.currentCount / progress.data.targetCount
                : 0f;

            progressFill.DOKill();
            if (initialized) progressFill.DOFillAmount(target, fillTweenDuration);
            else progressFill.fillAmount = target;
        }

        initialized = true;
    }
}
