using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Карточка лутбокса во вкладке «Поставки».</summary>
public class SupplyCardUI : TerminalHoverItem
{
    [Header("Ссылки")]
    public Image icon;
    public Text title;
    public Text description;
    public Text price;

    [Tooltip("Старая цена (до скидки «Оптовика») — скрыта, если скидки нет.")]
    public Text oldPrice;
    public Text deliveryTime;

    [Tooltip("Причина, почему нельзя заказать (лицензия, уровень, деньги).")]
    public Text status;
    public Button buyButton;
    public Text buyLabel;
    public Image buyBackground;

    public LootBoxData Box { get; private set; }
    public event Action<SupplyCardUI> OnBuy;

    private void Awake()
    {
        if (buyButton != null) buyButton.onClick.AddListener(() => OnBuy?.Invoke(this));
    }

    public void Bind(LootBoxData box, string priceText, string oldPriceText, string timeText, bool canBuy, string reason, Color okColor, Color badColor)
    {
        Box = box;
        if (icon != null) { icon.sprite = box.icon; icon.enabled = box.icon != null; }
        if (title != null) title.text = box.title.ToUpperInvariant();
        if (description != null) description.text = box.description;
        if (price != null) price.text = priceText;
        if (oldPrice != null) { oldPrice.gameObject.SetActive(!string.IsNullOrEmpty(oldPriceText)); oldPrice.text = oldPriceText; }
        if (deliveryTime != null) deliveryTime.text = timeText;
        if (status != null) { status.text = reason; status.color = badColor; }
        if (buyLabel != null) buyLabel.text = canBuy ? "[ ЗАКАЗАТЬ ]" : "[ НЕДОСТУПНО ]";
        if (buyBackground != null) buyBackground.color = canBuy ? new Color(okColor.r, okColor.g, okColor.b, 0.25f) : new Color(1f, 1f, 1f, 0.05f);
        if (buyLabel != null) buyLabel.color = canBuy ? okColor : new Color(okColor.r, okColor.g, okColor.b, 0.35f);
    }

    /// <summary>Коротко показать сообщение в строке статуса (например, «ЗАКАЗАНО»).</summary>
    public void Flash(string message, Color color)
    {
        if (status == null) return;
        status.DOKill();
        status.text = message;
        status.color = color;
        status.DOFade(0.2f, 0.18f).SetLoops(4, LoopType.Yoyo);
    }
}
