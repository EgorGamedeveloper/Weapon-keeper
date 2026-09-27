using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Иконка одного действующего эффекта рядом с полосой выносливости: картинка эффекта, кольцо оставшегося
/// времени и короткий остаток («1:20» — часы:минуты игрового времени). Вредные эффекты красятся
/// предупреждающим цветом.
/// </summary>
public class StatusEffectIconUI : MonoBehaviour
{
    [Tooltip("Картинка эффекта.")]
    public Image icon;

    [Tooltip("Кольцо оставшегося времени (Image Type = Filled, Radial 360).")]
    public Image timerFill;

    [Tooltip("Остаток времени текстом (необязательно).")]
    public Text timeText;

    [Tooltip("Цвет полезного эффекта.")]
    public Color positiveColor = Color.white;

    [Tooltip("Цвет вредного эффекта («Отходняк», «Разбитость»).")]
    public Color negativeColor = new Color(1f, 0.45f, 0.4f);

    /// <summary>Показать эффект effect на момент now (GameClock.TotalHours).</summary>
    public void Bind(ActiveStatusEffect effect, double now)
    {
        if (effect == null || effect.data == null) return;

        Color color = effect.data.isNegative ? negativeColor : positiveColor;
        if (icon != null)
        {
            icon.sprite = effect.data.icon;
            icon.enabled = effect.data.icon != null;
            icon.color = color;
        }

        float remaining = effect.RemainingHours(now);
        if (timerFill != null)
        {
            timerFill.fillAmount = effect.durationHours > 0f ? Mathf.Clamp01(remaining / effect.durationHours) : 0f;
            timerFill.color = new Color(color.r, color.g, color.b, timerFill.color.a);
        }
        if (timeText != null)
        {
            int minutes = Mathf.CeilToInt(remaining * 60f);
            timeText.text = (minutes / 60) + ":" + (minutes % 60).ToString("00");
        }
    }
}
