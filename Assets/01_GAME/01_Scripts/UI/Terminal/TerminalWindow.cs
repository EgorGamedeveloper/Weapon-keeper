using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Окно терминала в стиле старого ЭЛТ-монитора: вкладки «Поставки» (заказ лутбоксов, список «в пути»)
/// и «Заказы» (входящие заказы на отправку, текущий заказ). Открывается кликом по TerminalStation,
/// закрывается Esc или крестиком. Пока открыто — игровой ввод заблокирован (GameplayInputBlocker).
/// </summary>
public class TerminalWindow : MonoBehaviour
{
    [Header("Данные")]
    public SupplyService supply;
    public ShippingService shipping;
    public PlayerWallet wallet;
    public PlayerProgression progression;
    public PlayerSkills skills;
    public GameplayInputBlocker inputBlocker;

    [Header("Окно")]
    [Tooltip("Корень окна (затемнение + монитор) — проявляется и гаснет.")]
    public CanvasGroup windowGroup;

    [Tooltip("Экран монитора — «включается» как ЭЛТ (полоска раскрывается в кадр).")]
    public RectTransform screen;

    [Tooltip("Содержимое экрана — проявляется после включения и слегка мерцает.")]
    public CanvasGroup screenContent;

    public Button closeButton;

    [Header("Шапка")]
    [Tooltip("Заголовок — печатается по буквам при открытии.")]
    public Text headerText;
    public string headerLine = "GUN KEEPER // ТЕРМИНАЛ СНАБЖЕНИЯ v1.3";
    public Text balanceText;

    [Header("Вкладки")]
    public Button suppliesTab;
    public Button ordersTab;
    public Text suppliesTabLabel;
    public Text ordersTabLabel;
    public CanvasGroup suppliesPage;
    public CanvasGroup ordersPage;

    [Header("Поставки")]
    public Transform cardsRoot;
    public SupplyCardUI cardPrefab;
    public Transform deliveriesRoot;
    public DeliveryRowUI deliveryRowPrefab;
    public Text deliveriesEmpty;

    [Header("Заказы")]
    public Transform inboxRoot;
    public OrderRowUI orderRowPrefab;
    public Text inboxEmpty;
    public Text activeTitle;
    public Text activeBody;
    public Text activeStatus;
    public Button cancelButton;

    [Header("Цвета")]
    public Color textColor = new Color(0.35f, 1f, 0.55f, 1f);
    public Color dimColor = new Color(0.35f, 1f, 0.55f, 0.45f);
    public Color warnColor = new Color(1f, 0.75f, 0.3f, 1f);

    public bool IsOpen { get; private set; }

    private readonly List<SupplyCardUI> cards = new List<SupplyCardUI>();
    private readonly List<DeliveryRowUI> deliveryRows = new List<DeliveryRowUI>();
    private readonly List<OrderRowUI> orderRows = new List<OrderRowUI>();
    private bool ordersTabActive;
    private Tween flicker;
    private Tween typing;

    private void Awake()
    {
        if (windowGroup != null) windowGroup.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (suppliesTab != null) suppliesTab.onClick.AddListener(() => ShowTab(false, true));
        if (ordersTab != null) ordersTab.onClick.AddListener(() => ShowTab(true, true));
        if (cancelButton != null) cancelButton.onClick.AddListener(() => { if (shipping != null) shipping.Cancel(); });
    }

    private void OnEnable()
    {
        if (supply != null) supply.OnChanged += RefreshIfOpen;
        if (shipping != null) shipping.OnChanged += RefreshIfOpen;
        if (wallet != null) wallet.OnBalanceChanged += HandleBalance;
        if (skills != null) skills.OnSkillsChanged += RefreshIfOpen;
    }

    private void OnDisable()
    {
        if (supply != null) supply.OnChanged -= RefreshIfOpen;
        if (shipping != null) shipping.OnChanged -= RefreshIfOpen;
        if (wallet != null) wallet.OnBalanceChanged -= HandleBalance;
        if (skills != null) skills.OnSkillsChanged -= RefreshIfOpen;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

        // Обратный отсчёт доставок — каждый кадр, без пересборки списка.
        if (supply != null)
            for (int i = 0; i < deliveryRows.Count && i < supply.Deliveries.Count; i++)
                deliveryRows[i].Bind(supply.Deliveries[i]);
    }

    // ───────── открытие / закрытие ─────────

    public void Open()
    {
        if (IsOpen || windowGroup == null) return;
        if (inputBlocker != null && inputBlocker.IsBlocked) return;
        IsOpen = true;
        if (inputBlocker != null) inputBlocker.Acquire(this);

        windowGroup.gameObject.SetActive(true);
        windowGroup.DOKill();
        windowGroup.alpha = 0f;
        windowGroup.DOFade(1f, 0.15f);

        // «Включение» ЭЛТ: тонкая яркая полоса раскрывается в полный кадр, потом проявляется текст.
        if (screen != null)
        {
            screen.DOKill();
            screen.localScale = new Vector3(1f, 0.02f, 1f);
            screen.DOScaleY(1f, 0.3f).SetEase(Ease.OutExpo).SetDelay(0.05f);
        }
        if (screenContent != null)
        {
            screenContent.DOKill();
            screenContent.alpha = 0f;
            screenContent.DOFade(1f, 0.25f).SetDelay(0.3f);
            flicker?.Kill();
            flicker = screenContent.DOFade(0.93f, 0.08f).SetDelay(0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutFlash);
        }

        TypeHeader();
        ShowTab(ordersTabActive, false);
        SetBalanceText();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        flicker?.Kill();
        typing?.Kill();

        if (screen != null) { screen.DOKill(); screen.DOScaleY(0.02f, 0.15f).SetEase(Ease.InQuad); }
        windowGroup.DOKill();
        windowGroup.DOFade(0f, 0.2f).SetDelay(0.05f).OnComplete(() => windowGroup.gameObject.SetActive(false));

        if (inputBlocker != null) inputBlocker.Release(this);
    }

    private void TypeHeader()
    {
        if (headerText == null) return;
        typing?.Kill();
        headerText.text = "";
        int length = 0;
        typing = DOTween.To(() => length, v => { length = v; headerText.text = headerLine.Substring(0, v) + "_"; }, headerLine.Length, 0.6f)
            .SetEase(Ease.Linear).SetDelay(0.3f)
            .OnComplete(() => headerText.text = headerLine);
    }

    private void ShowTab(bool orders, bool animate)
    {
        ordersTabActive = orders;
        SetTabLabel(suppliesTabLabel, !orders, "ПОСТАВКИ");
        SetTabLabel(ordersTabLabel, orders, "ЗАКАЗЫ");
        ShowPage(suppliesPage, !orders, animate);
        ShowPage(ordersPage, orders, animate);
        Refresh();
    }

    private void SetTabLabel(Text label, bool active, string name)
    {
        if (label == null) return;
        label.text = active ? "> " + name + " <" : "  " + name + "  ";
        label.color = active ? textColor : dimColor;
    }

    private static void ShowPage(CanvasGroup page, bool visible, bool animate)
    {
        if (page == null) return;
        page.gameObject.SetActive(visible);
        if (!visible || !animate) return;
        page.DOKill();
        page.alpha = 0f;
        page.DOFade(1f, 0.2f);
    }

    // ───────── обновление ─────────

    private void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }

    private void HandleBalance(int _)
    {
        if (!IsOpen) return;
        SetBalanceText();
        if (balanceText != null) { balanceText.transform.DOKill(true); balanceText.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 8, 0.7f); }
        Refresh();
    }

    private void SetBalanceText()
    {
        if (balanceText != null && wallet != null) balanceText.text = "БАЛАНС: " + wallet.Format(wallet.Balance);
    }

    private void Refresh()
    {
        if (ordersTabActive) RefreshOrders();
        else RefreshSupplies();
    }

    private void RefreshSupplies()
    {
        if (supply == null || supply.catalog == null) return;

        var boxes = new List<LootBoxData>();
        foreach (var box in supply.catalog.lootBoxes) if (box != null) boxes.Add(box);
        boxes.Sort((a, b) => a.requiredLevel != b.requiredLevel ? a.requiredLevel.CompareTo(b.requiredLevel) : a.price.CompareTo(b.price));

        EnsureCount(cards, boxes.Count, cardPrefab, cardsRoot, card => card.OnBuy += HandleBuy);
        for (int i = 0; i < boxes.Count; i++)
        {
            LootBoxData box = boxes[i];
            int price = supply.GetPrice(box);
            string oldPrice = price < box.price ? wallet.Format(box.price) : "";
            float time = supply.GetDeliveryTime(box);
            var availability = supply.Check(box);
            cards[i].Bind(box, wallet.Format(price), oldPrice, "ДОСТАВКА: " + FormatTime(time),
                availability == SupplyService.Availability.Available, SupplyReason(box, availability), textColor, warnColor);
        }

        EnsureCount(deliveryRows, supply.Deliveries.Count, deliveryRowPrefab, deliveriesRoot, null);
        for (int i = 0; i < supply.Deliveries.Count; i++) deliveryRows[i].Bind(supply.Deliveries[i]);
        if (deliveriesEmpty != null) deliveriesEmpty.gameObject.SetActive(supply.Deliveries.Count == 0);
    }

    private void RefreshOrders()
    {
        if (shipping == null) return;

        EnsureCount(orderRows, shipping.Inbox.Count, orderRowPrefab, inboxRoot, row => row.OnAccept += HandleAccept);
        for (int i = 0; i < shipping.Inbox.Count; i++)
        {
            var order = shipping.Inbox[i];
            var block = shipping.CheckAccept(order);
            orderRows[i].Bind(order, DescribeLines(order, false), "НАГРАДА: " + wallet.Format(shipping.GetReward(order)) + "  +" + order.xpReward + " XP",
                block == ShippingService.AcceptBlock.None, OrderReason(block), textColor, warnColor);
        }
        if (inboxEmpty != null) inboxEmpty.gameObject.SetActive(shipping.Inbox.Count == 0);

        var active = shipping.ActiveOrder;
        if (cancelButton != null) cancelButton.gameObject.SetActive(active != null);

        if (active != null)
        {
            SetText(activeTitle, "ТЕКУЩИЙ ЗАКАЗ: " + active.customer.ToUpperInvariant());
            SetText(activeBody, active.message + "\n\n" + DescribeLines(active, true));
            bool packed = shipping.IsPacked;
            SetText(activeStatus, packed ? "КОРОБКА ЗАПЕЧАТАНА — ОТНЕСИТЕ НА КРЫШУ В ЗОНУ ОТПРАВКИ" : "УЛОЖИТЕ ТОВАР В КОРОБКУ У ТЕРМИНАЛА");
            if (activeStatus != null) activeStatus.color = packed ? textColor : warnColor;
        }
        else if (shipping.BoxCancelled)
        {
            SetText(activeTitle, "ЗАКАЗ ОТМЕНЁН");
            SetText(activeBody, "В коробке остался товар: " + shipping.BoxContents.Count + " шт.");
            SetText(activeStatus, "РАЗБЕРИТЕ КОРОБКУ И ВЕРНИТЕ ТОВАР НА ПОЛКИ");
            if (activeStatus != null) activeStatus.color = warnColor;
        }
        else
        {
            SetText(activeTitle, "НЕТ АКТИВНОГО ЗАКАЗА");
            SetText(activeBody, "Примите заказ из входящих — у терминала появится коробка для упаковки.");
            SetText(activeStatus, "");
        }
    }

    private void HandleBuy(SupplyCardUI card)
    {
        if (supply != null && supply.TryOrder(card.Box))
        {
            card.Punch();
            card.Flash("ЗАКАЗАНО — ЖДИТЕ ДОСТАВКУ", textColor);
        }
        else card.Flash(SupplyReason(card.Box, supply != null ? supply.Check(card.Box) : SupplyService.Availability.NoMoney), warnColor);
    }

    private void HandleAccept(OrderRowUI row)
    {
        if (shipping != null && shipping.Accept(row.Order)) return;
        row.Punch();
    }

    // ───────── тексты ─────────

    private string SupplyReason(LootBoxData box, SupplyService.Availability availability)
    {
        switch (availability)
        {
            case SupplyService.Availability.NoLicense: return "НУЖНА ЛИЦЕНЗИЯ: " + box.requiredLicense.title.ToUpperInvariant();
            case SupplyService.Availability.LowLevel: return "НУЖЕН УРОВЕНЬ " + box.requiredLevel;
            case SupplyService.Availability.NoMoney: return "НЕ ХВАТАЕТ ДЕНЕГ";
            default: return "";
        }
    }

    private string OrderReason(ShippingService.AcceptBlock block)
    {
        switch (block)
        {
            case ShippingService.AcceptBlock.BoxInUse:
                return shipping.BoxCancelled ? "СНАЧАЛА РАЗБЕРИТЕ КОРОБКУ ОТМЕНЁННОГО ЗАКАЗА" : "СНАЧАЛА ЗАВЕРШИТЕ ТЕКУЩИЙ ЗАКАЗ";
            case ShippingService.AcceptBlock.NoLicense: return "НУЖНА ЛИЦЕНЗИЯ";
            case ShippingService.AcceptBlock.LowLevel: return "НУЖЕН БОЛЕЕ ВЫСОКИЙ УРОВЕНЬ";
            default: return "";
        }
    }

    private string DescribeLines(ShippingOrderData order, bool withProgress)
    {
        var sb = new StringBuilder();
        foreach (var line in order.lines)
        {
            if (line == null || line.item == null) continue;
            if (sb.Length > 0) sb.Append(withProgress ? "\n" : ",  ");
            sb.Append(line.item.itemName).Append(" ×").Append(line.count);
            if (withProgress) sb.Append("   [").Append(shipping.PackedCount(line.item)).Append('/').Append(line.count).Append(']');
        }
        return sb.ToString();
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }

    private static void SetText(Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private static void EnsureCount<T>(List<T> list, int count, T prefab, Transform root, System.Action<T> onCreate) where T : Component
    {
        if (prefab == null || root == null) return;
        while (list.Count < count)
        {
            var item = Instantiate(prefab, root);
            onCreate?.Invoke(item);
            list.Add(item);
            if (item is TerminalHoverItem hover) hover.PlayAppear(0.04f * list.Count);
        }
        for (int i = 0; i < list.Count; i++) list[i].gameObject.SetActive(i < count);
    }
}
