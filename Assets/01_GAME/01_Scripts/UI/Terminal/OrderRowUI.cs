using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Входящий заказ во вкладке «Заказы».</summary>
public class OrderRowUI : TerminalHoverItem
{
    [Header("Ссылки")]
    public Text customer;
    public Text items;
    public Text reward;
    public Text status;
    public Button acceptButton;
    public Text acceptLabel;
    public Image acceptBackground;

    public ShippingOrderData Order { get; private set; }
    public event Action<OrderRowUI> OnAccept;

    private void Awake()
    {
        if (acceptButton != null) acceptButton.onClick.AddListener(() => OnAccept?.Invoke(this));
    }

    public void Bind(ShippingOrderData order, string itemsText, string rewardText, bool canAccept, string reason, Color okColor, Color badColor)
    {
        Order = order;
        if (customer != null) customer.text = order.DisplayCustomer.ToUpperInvariant();
        if (items != null) items.text = itemsText;
        if (reward != null) reward.text = rewardText;
        if (status != null) { status.text = reason; status.color = badColor; }
        if (acceptLabel != null) { acceptLabel.text = canAccept ? Loc.Get("terminal.order.accept") : Loc.Get("terminal.order.busy"); acceptLabel.color = canAccept ? okColor : new Color(okColor.r, okColor.g, okColor.b, 0.35f); }
        if (acceptBackground != null) acceptBackground.color = canAccept ? new Color(okColor.r, okColor.g, okColor.b, 0.25f) : new Color(1f, 1f, 1f, 0.05f);
    }
}
