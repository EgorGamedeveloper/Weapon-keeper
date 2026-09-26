using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кольцо прогресса у прицела — сколько работы уже сделано в режиме работы с объектом (стёртая доля
/// пятна, отжатость доски; см. PlayerToolActions). Появляется и гаснет быстро и плавно.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HoldProgressUI : MonoBehaviour
{
    [Tooltip("Кольцо: Image с Image Type = Filled, Fill Method = Radial 360.")]
    public Image ring;

    [Tooltip("За сколько секунд кольцо появляется и гаснет.")]
    [Min(0f)] public float fadeDuration = 0.1f;

    [Tooltip("Как быстро кольцо догоняет новое значение (доля за секунду, 0 — сразу).")]
    [Min(0f)] public float fillSpeed = 3f;

    private CanvasGroup group;
    private Tween fadeTween;
    private bool visible;
    private float targetFill;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void Update()
    {
        if (ring == null) return;
        ring.fillAmount = fillSpeed > 0f
            ? Mathf.MoveTowards(ring.fillAmount, targetFill, fillSpeed * Time.unscaledDeltaTime)
            : targetFill;
    }

    /// <summary>Показать кольцо с прогрессом 0..1.</summary>
    public void SetProgress(float progress)
    {
        targetFill = Mathf.Clamp01(progress);
        if (!visible && ring != null) ring.fillAmount = targetFill;
        SetVisible(true);
    }

    /// <summary>Спрятать кольцо.</summary>
    public void Hide() => SetVisible(false);

    private void SetVisible(bool state)
    {
        if (visible == state || group == null) return;
        visible = state;
        fadeTween?.Kill();
        fadeTween = group.DOFade(state ? 1f : 0f, fadeDuration).SetUpdate(true);
    }
}
