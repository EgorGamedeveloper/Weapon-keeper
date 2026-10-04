using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка входящего заказа во вкладке «Заказы»: заказчик, иконки товара с количеством, награда; недоступный
/// заказ (лицензия, уровень) — тусклый с замком. Клик выбирает заказ.
/// </summary>
public class TerminalListRowUI : TerminalHoverItem
{
    [Header("Ссылки")]
    [Tooltip("Кнопка строки (клик — выбрать).")]
    public Button button;

    [Tooltip("Заказчик.")]
    public Text title;

    [Tooltip("Товар: «Кофе ×2, Патроны ×1».")]
    public Text items;

    [Tooltip("Награда.")]
    public Text reward;

    [Tooltip("Иконки товара (по порядку строк заказа; лишние прячутся).")]
    public Image[] itemIcons = Array.Empty<Image>();

    [Tooltip("Замок — заказ пока недоступен.")]
    public Image lockIcon;

    [Tooltip("Прозрачность всей строки: недоступный заказ тусклее.")]
    public CanvasGroup group;

    /// <summary>Заказ этой строки.</summary>
    public ShippingOrderData Order { get; private set; }

    /// <summary>Игрок кликнул по строке.</summary>
    public event Action<TerminalListRowUI> OnClicked;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(ShippingOrderData order, string itemsText, string rewardText, bool locked)
    {
        Order = order;
        if (title != null) title.text = order.DisplayCustomer.ToUpperInvariant();
        if (items != null) items.text = itemsText;
        if (reward != null) reward.text = rewardText;
        if (lockIcon != null) lockIcon.gameObject.SetActive(locked);
        if (group != null) group.alpha = locked ? 0.55f : 1f;

        int iconIndex = 0;
        foreach (var line in order.lines)
        {
            if (line == null || line.item == null || iconIndex >= itemIcons.Length) continue;
            itemIcons[iconIndex].sprite = line.item.icon;
            itemIcons[iconIndex].gameObject.SetActive(line.item.icon != null);
            iconIndex++;
        }
        for (; iconIndex < itemIcons.Length; iconIndex++) itemIcons[iconIndex].gameObject.SetActive(false);
    }
}
