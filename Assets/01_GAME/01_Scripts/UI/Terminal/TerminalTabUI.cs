using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Вкладка терминала: активная залита мятным с тёмными иконкой и подписью, неактивная — только контур.</summary>
public class TerminalTabUI : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Кнопка вкладки.")]
    public Button button;

    [Tooltip("Заливка — видна у активной вкладки.")]
    public Image fill;

    [Tooltip("Контур.")]
    public Image outline;

    [Tooltip("Иконка.")]
    public Image icon;

    [Tooltip("Подпись.")]
    public Text label;

    [Header("Цвета")]
    [Tooltip("Акцентный цвет: заливка активной вкладки, контур и подпись неактивной.")]
    public Color accent = new Color(0.39f, 0.9f, 0.75f, 1f);

    [Tooltip("Цвет иконки и подписи на заливке.")]
    public Color onAccent = new Color(0.03f, 0.13f, 0.12f, 1f);

    public void SetActive(bool active, bool animate)
    {
        float duration = animate ? 0.15f : 0f;
        if (fill != null)
        {
            fill.DOKill();
            Color target = new Color(accent.r, accent.g, accent.b, active ? 1f : 0f);
            if (duration > 0f) fill.DOColor(target, duration).SetUpdate(true);
            else fill.color = target;
        }
        if (outline != null) outline.color = new Color(accent.r, accent.g, accent.b, active ? 1f : 0.5f);
        Color content = active ? onAccent : new Color(accent.r, accent.g, accent.b, 0.75f);
        if (icon != null) icon.color = content;
        if (label != null) label.color = content;
    }
}
