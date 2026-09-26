using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Всплывающее «+N XP» у шкалы опыта: появляется при каждом начислении опыта (полка, квест, отправка —
/// всё, что идёт через PlayerProgression.AddXP), «подпрыгивает», держится и тает, уплывая вверх.
/// Начисления, пришедшие, пока надпись ещё видна, складываются в одну. Тут же звуки опыта и левелапа.
/// </summary>
public class XPGainPopupUI : MonoBehaviour
{
    [Tooltip("Источник опыта.")]
    public PlayerProgression playerProgression;

    [Tooltip("Надпись «+N XP».")]
    public Text label;

    [Header("Анимация")]
    [Tooltip("Сколько секунд надпись держится до начала затухания.")]
    [Min(0f)] public float holdDuration = 0.9f;

    [Tooltip("За сколько секунд надпись тает.")]
    [Min(0.01f)] public float fadeDuration = 0.4f;

    [Tooltip("На сколько пикселей надпись уплывает вверх, пока тает.")]
    public float riseDistance = 12f;

    [Tooltip("Насколько надпись «подпрыгивает» при появлении, доля размера.")]
    [Range(0f, 1f)] public float popScale = 0.25f;

    [Header("Звуки")]
    [Tooltip("Начислен опыт.")]
    public SoundCue xpSound;

    [Tooltip("Новый уровень.")]
    public SoundCue levelUpSound;

    private const float PopDuration = 0.25f;

    private RectTransform rect;
    private Vector2 basePosition;
    private Sequence sequence;
    private int shownAmount;

    private void Awake()
    {
        if (label == null) return;
        rect = label.rectTransform;
        basePosition = rect.anchoredPosition;
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        if (playerProgression == null) return;
        playerProgression.OnXPGained += HandleXPGained;
        playerProgression.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        sequence?.Kill();
        if (playerProgression == null) return;
        playerProgression.OnXPGained -= HandleXPGained;
        playerProgression.OnLevelUp -= HandleLevelUp;
    }

    private void HandleXPGained(int amount)
    {
        SoundPlayer.Play2D(xpSound);
        if (label == null) return;

        bool stillVisible = sequence != null && sequence.IsActive();
        shownAmount = stillVisible ? shownAmount + amount : amount;
        label.text = Loc.Get("hud.xp_gain", shownAmount);

        sequence?.Kill();
        rect.anchoredPosition = basePosition;
        rect.localScale = Vector3.one;
        SetAlpha(1f);

        sequence = DOTween.Sequence()
            .Append(rect.DOPunchScale(Vector3.one * popScale, PopDuration, 6, 0.6f))
            .AppendInterval(Mathf.Max(0f, holdDuration - PopDuration))
            .Append(rect.DOAnchorPos(basePosition + Vector2.up * riseDistance, fadeDuration).SetEase(Ease.OutQuad))
            .Join(label.DOFade(0f, fadeDuration));
    }

    private void HandleLevelUp(int newLevel, int unlockPointsGranted) => SoundPlayer.Play2D(levelUpSound);

    private void SetAlpha(float alpha)
    {
        Color color = label.color;
        color.a = alpha;
        label.color = color;
    }
}
