using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка с полосой: иконка, подпись, тонкая полоса и значение. Шанс предмета в ящике («▮▮▮▯ 60%»),
/// упаковка заказа («2/3»), доставка в пути («17:40»). Без полосы — просто строка товара с количеством.
/// </summary>
public class TerminalBarRowUI : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Иконка предмета или ящика.")]
    public Image icon;

    [Tooltip("Подпись.")]
    public Text label;

    [Tooltip("Значение справа.")]
    public Text value;

    [Tooltip("Дорожка полосы (прячется вместе с заливкой, если полоса не нужна).")]
    public GameObject barTrack;

    [Tooltip("Заливка полосы (Image Type = Filled, Horizontal).")]
    public Image barFill;

    [Tooltip("Обычный цвет заливки.")]
    public Color barColor = new Color(0.39f, 0.9f, 0.75f, 1f);

    /// <summary>fraction &lt; 0 — без полосы. tint — свой цвет полосы и значения, null — обычный.</summary>
    public void Set(Sprite sprite, string labelText, float fraction, string valueText, Color? tint = null, bool animate = false)
    {
        if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
        if (label != null) label.text = labelText;
        if (value != null) value.text = valueText;

        bool showBar = fraction >= 0f;
        if (barTrack != null) barTrack.SetActive(showBar);
        if (barFill == null || !showBar) return;

        barFill.color = tint ?? barColor;
        float target = Mathf.Clamp01(fraction);
        barFill.DOKill();
        if (animate)
        {
            barFill.fillAmount = 0f;
            barFill.DOFillAmount(target, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
        else barFill.fillAmount = target;
    }

    private void OnDisable()
    {
        if (barFill != null) barFill.DOKill();
    }
}
