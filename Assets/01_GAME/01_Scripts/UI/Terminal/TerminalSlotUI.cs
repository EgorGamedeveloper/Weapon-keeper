using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Плитка каталога во вкладке «Поставки»: иконка ящика, бейдж уровня, замок, если ящик недоступен
/// (лицензия, уровень, уже куплен). Клик выбирает ящик — подробности показываются в центре.
/// </summary>
public class TerminalSlotUI : TerminalHoverItem
{
    [Header("Ссылки")]
    [Tooltip("Кнопка плитки (клик — выбрать).")]
    public Button button;

    [Tooltip("Иконка ящика.")]
    public Image icon;

    [Tooltip("Бейдж в углу плитки: минимальный уровень.")]
    public Text badge;

    [Tooltip("Замок поверх иконки — ящик недоступен.")]
    public Image lockIcon;

    [Header("Цвета")]
    [Tooltip("Цвет иконки доступного ящика.")]
    public Color iconColor = new Color(0.75f, 1f, 0.92f, 1f);

    [Tooltip("Цвет иконки недоступного ящика.")]
    public Color lockedIconColor = new Color(0.39f, 0.9f, 0.75f, 0.3f);

    /// <summary>Ящик этой плитки.</summary>
    public LootBoxData Box { get; private set; }

    /// <summary>Игрок кликнул по плитке.</summary>
    public event Action<TerminalSlotUI> OnClicked;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(LootBoxData box, bool locked)
    {
        Box = box;
        if (icon != null)
        {
            icon.sprite = box.icon;
            icon.enabled = box.icon != null;
            icon.color = locked ? lockedIconColor : iconColor;
        }
        if (badge != null) badge.text = box.requiredLevel.ToString();
        if (lockIcon != null) lockIcon.gameObject.SetActive(locked);
    }
}
