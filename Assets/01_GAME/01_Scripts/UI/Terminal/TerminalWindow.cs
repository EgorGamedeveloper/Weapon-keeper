using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Окно терминала — широкий экран старого компьютера в бирюзовом «техническом» стиле. Сверху системная
/// строка (связь, питание, часы, кнопка «Выход»), снизу — сводка полоской и подсказки клавиш. Две вкладки,
/// три колонки:
/// - «Поставки»: слева каталог ящиков плитками по разделам (LootBoxData.category), в центре подробности
///   выбранного ящика (содержимое с шансами, параметры, кнопка заказа), справа «В пути»;
/// - «Заказы»: слева входящие заказы, в центре выбранный заказ, справа «Текущий заказ».
/// Открывается кликом по TerminalStation. Управление: клик, Q/E — вкладки, стрелки — выбор, Enter — заказать
/// или принять, Esc — выход (пока окно открыто, инвентари выключены блокировщиком — Q/E свободны).
/// Монитор вписывается в экран: канвас в ConstantPixelSize, иначе на большом разрешении окно было бы мелким.
/// </summary>
public class TerminalWindow : MonoBehaviour
{
    [Header("Данные")]
    [Tooltip("Поставки: ящики, цены, доставки.")]
    public SupplyService supply;

    [Tooltip("Заказы на отправку.")]
    public ShippingService shipping;

    [Tooltip("Деньги игрока.")]
    public PlayerWallet wallet;

    [Tooltip("Уровень игрока.")]
    public PlayerProgression progression;

    [Tooltip("Навыки: лицензии ветки «Каталог».")]
    public PlayerSkills skills;

    [Tooltip("Блокировка игрового ввода, пока окно открыто.")]
    public GameplayInputBlocker inputBlocker;

    [Tooltip("Игровые часы — для сводки и шапки.")]
    public GameClock clock;

    [Tooltip("Процент расставленного товара — для сводки.")]
    public ShelvingProgressTracker shelvingTracker;

    [Header("Окно")]
    [Tooltip("Корень окна (затемнение + монитор) — проявляется и гаснет.")]
    public CanvasGroup windowGroup;

    [Tooltip("Монитор целиком: масштабируется, чтобы влезть в экран.")]
    public RectTransform monitor;

    [Tooltip("Экран монитора — «включается» как ЭЛТ (полоска раскрывается в кадр).")]
    public RectTransform screen;

    [Tooltip("Содержимое экрана — проявляется после включения и слегка мерцает.")]
    public CanvasGroup screenContent;

    [Tooltip("Кнопка «Выход» в системной строке экрана.")]
    public Button exitButton;

    [Tooltip("Какую долю экрана может занять монитор.")]
    [Range(0.5f, 1f)] public float fitFraction = 0.96f;

    [Tooltip("Наибольший масштаб монитора (на очень больших экранах).")]
    [Min(0.5f)] public float maxScale = 1.6f;

    /// <summary>Строка статуса по умолчанию: пока statusLine совпадает с ней, показывается перевод terminal.header.</summary>
    private const string DefaultStatusLine = "GUN KEEPER // ТЕРМИНАЛ СНАБЖЕНИЯ v1.3";

    [Header("Шапка и подвал")]
    public TerminalTabUI suppliesTab;
    public TerminalTabUI ordersTab;

    [Tooltip("«ДЕНЬ 3 · 14:30» в шапке.")]
    public Text clockText;

    [Tooltip("Баланс в шапке.")]
    public Text balanceText;

    [Tooltip("Строка статуса в подвале — печатается по буквам при открытии.")]
    public Text statusText;

    [Tooltip("Текст строки статуса.")]
    public string statusLine = DefaultStatusLine;

    [Header("Сводка")]
    [Tooltip("Контейнер строк сводки (Vertical Layout Group).")]
    public Transform summaryRoot;

    [Tooltip("Префаб строки параметра (подпись, точки, значение).")]
    public TerminalStatRowUI statRowPrefab;

    [Tooltip("Префаб строки с полосой (иконка, подпись, полоса, значение).")]
    public TerminalBarRowUI barRowPrefab;

    [Header("Поставки")]
    public CanvasGroup suppliesPage;

    [Tooltip("Контейнер разделов каталога (Vertical Layout Group).")]
    public Transform catalogRoot;

    public TerminalCatalogGroupUI catalogGroupPrefab;
    public TerminalSlotUI slotPrefab;

    [Tooltip("Иконки разделов каталога по порядку LootBoxCategory: Боеприпасы, Оружие, Инструменты, Снабжение.")]
    public Sprite[] categoryIcons = new Sprite[4];

    [Tooltip("Панель подробностей ящика — короткий переход при смене выбора.")]
    public CanvasGroup supplyDetail;
    public Text supplyTitle;
    public Image supplyIcon;
    public Text supplyLevelBadge;
    public Image supplyLicenseBadge;
    public Text supplyPrice;

    [Tooltip("Старая цена (до скидки «Оптовика»), зачёркнута; прячется, если скидки нет.")]
    public Text supplyOldPrice;
    public Text supplyDelivery;
    public Text supplyDescription;
    public Text lootHeader;
    public Transform lootRoot;
    public Transform supplyStatsRoot;
    public Button supplyAction;
    public Image supplyActionFill;
    public Text supplyActionLabel;

    [Tooltip("Почему ящик нельзя заказать, или «ЗАКАЗАНО».")]
    public Text supplyReason;

    [Tooltip("Строки «В пути».")]
    public Transform transitRoot;
    public Text transitEmpty;

    [Header("Заказы")]
    public CanvasGroup ordersPage;
    public Transform inboxRoot;
    public TerminalListRowUI listRowPrefab;
    public Text inboxEmpty;

    [Tooltip("Панель выбранного заказа — короткий переход при смене выбора.")]
    public CanvasGroup orderDetail;
    public Text orderTitle;
    public Text orderMessage;
    public Transform orderGoodsRoot;
    public Transform orderStatsRoot;
    public Button orderAction;
    public Image orderActionFill;
    public Text orderActionLabel;
    public Text orderReason;

    [Tooltip("Текст вместо подробностей, когда входящих нет.")]
    public Text orderEmpty;

    [Tooltip("Текущий заказ: заголовок, строки упаковки, статус, отмена.")]
    public Text activeTitle;
    public Transform activeLinesRoot;
    public Text activeStatus;
    public Button cancelButton;

    [Header("Иконки")]
    [Tooltip("Лицензия есть или не нужна.")]
    public Sprite checkSprite;

    [Tooltip("Лицензии нет.")]
    public Sprite lockSprite;

    [Header("Цвета")]
    [Tooltip("Акцент: контуры, полосы, заливка кнопок.")]
    public Color accent = new Color(0.39f, 0.9f, 0.75f, 1f);

    [Tooltip("Основной текст.")]
    public Color textColor = new Color(0.75f, 0.97f, 0.9f, 1f);

    [Tooltip("Второстепенный текст.")]
    public Color dimColor = new Color(0.39f, 0.9f, 0.75f, 0.6f);

    [Tooltip("Текст на залитом акцентом фоне.")]
    public Color onAccent = new Color(0.03f, 0.13f, 0.12f, 1f);

    [Tooltip("Предупреждение: нельзя заказать, не хватает денег.")]
    public Color warnColor = new Color(0.95f, 0.76f, 0.31f, 1f);

    public bool IsOpen { get; private set; }

    private readonly Dictionary<LootBoxCategory, TerminalCatalogGroupUI> groups = new Dictionary<LootBoxCategory, TerminalCatalogGroupUI>();
    private readonly Dictionary<LootBoxData, TerminalSlotUI> slots = new Dictionary<LootBoxData, TerminalSlotUI>();
    private readonly List<LootBoxData> orderedBoxes = new List<LootBoxData>();
    private readonly List<TerminalListRowUI> inboxRows = new List<TerminalListRowUI>();
    private readonly List<TerminalStatRowUI> summaryRows = new List<TerminalStatRowUI>();
    private readonly List<TerminalBarRowUI> lootRows = new List<TerminalBarRowUI>();
    private readonly List<TerminalStatRowUI> supplyStats = new List<TerminalStatRowUI>();
    private readonly List<TerminalBarRowUI> transitRows = new List<TerminalBarRowUI>();
    private readonly List<TerminalBarRowUI> goodsRows = new List<TerminalBarRowUI>();
    private readonly List<TerminalStatRowUI> orderStats = new List<TerminalStatRowUI>();
    private readonly List<TerminalBarRowUI> activeRows = new List<TerminalBarRowUI>();

    private bool ordersTabActive;
    private LootBoxData selectedBox;
    private ShippingOrderData selectedOrder;
    private Tween flicker;
    private Tween typing;
    private Tween cursorBlink;
    private float nextLiveRefresh;
    private Vector2Int lastScreenSize;
    private Vector2 supplyDetailBase;
    private Vector2 orderDetailBase;
    private Vector2 suppliesPageBase;
    private Vector2 ordersPageBase;

    private void Awake()
    {
        if (windowGroup != null) windowGroup.gameObject.SetActive(false);
        if (suppliesTab != null && suppliesTab.button != null) suppliesTab.button.onClick.AddListener(() => ShowTab(false, true));
        if (ordersTab != null && ordersTab.button != null) ordersTab.button.onClick.AddListener(() => ShowTab(true, true));
        if (supplyAction != null) supplyAction.onClick.AddListener(BuySelected);
        if (orderAction != null) orderAction.onClick.AddListener(AcceptSelected);
        if (cancelButton != null) cancelButton.onClick.AddListener(() => { if (shipping != null) shipping.Cancel(); });
        if (exitButton != null) exitButton.onClick.AddListener(Close);

        supplyDetailBase = AnchoredOf(supplyDetail);
        orderDetailBase = AnchoredOf(orderDetail);
        suppliesPageBase = AnchoredOf(suppliesPage);
        ordersPageBase = AnchoredOf(ordersPage);
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
        if (Input.GetKeyDown(KeyCode.Q)) SwitchTab(-1);
        if (Input.GetKeyDown(KeyCode.E)) SwitchTab(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)) Step(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) Step(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmSelected();

        if (lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height) FitToScreen();

        // Время, доставки в пути и сводка меняются сами — обновляем без пересборки списков.
        if (Time.unscaledTime >= nextLiveRefresh)
        {
            nextLiveRefresh = Time.unscaledTime + 0.25f;
            RefreshLive();
        }
    }

    // ───────── открытие / закрытие ─────────

    public void Open()
    {
        if (IsOpen || windowGroup == null) return;
        if (inputBlocker != null && inputBlocker.IsBlocked) return;
        IsOpen = true;
        if (inputBlocker != null) inputBlocker.Acquire(this);

        windowGroup.gameObject.SetActive(true);
        FitToScreen();
        UISoundFeedback.PlayWindowOpen();
        windowGroup.DOKill();
        windowGroup.alpha = 0f;
        windowGroup.DOFade(1f, 0.15f);

        // «Включение» ЭЛТ: тонкая яркая полоса раскрывается в полный кадр, потом проявляется содержимое.
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
            flicker = screenContent.DOFade(0.96f, 0.09f).SetDelay(0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutFlash);
        }

        TypeStatus();
        ShowTab(ordersTabActive, false);
        RefreshLive();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        UISoundFeedback.PlayWindowClose();
        flicker?.Kill();
        typing?.Kill();
        cursorBlink?.Kill();

        if (screen != null) { screen.DOKill(); screen.DOScaleY(0.02f, 0.15f).SetEase(Ease.InQuad); }
        windowGroup.DOKill();
        windowGroup.DOFade(0f, 0.2f).SetDelay(0.05f).OnComplete(() => windowGroup.gameObject.SetActive(false));

        if (inputBlocker != null) inputBlocker.Release(this);
    }

    /// <summary>Вписать монитор в экран целиком (с запасом fitFraction).</summary>
    private void FitToScreen()
    {
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (monitor == null) return;

        Canvas canvas = monitor.GetComponentInParent<Canvas>();
        float canvasScale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        Vector2 size = monitor.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;

        float scale = Mathf.Min(Screen.width * fitFraction / size.x, Screen.height * fitFraction / size.y) / canvasScale;
        scale = Mathf.Min(scale, maxScale);
        monitor.localScale = new Vector3(scale, scale, 1f);
    }

    private void TypeStatus()
    {
        if (statusText == null) return;
        typing?.Kill();
        cursorBlink?.Kill();
        statusText.text = "";
        int length = 0;
        // Строка по умолчанию переводится (terminal.header); свой текст из инспектора показывается как есть.
        string line = statusLine == DefaultStatusLine ? Loc.Get("terminal.header") : statusLine;
        typing = DOTween.To(() => length, v => { length = v; statusText.text = line.Substring(0, v) + "_"; }, line.Length, 0.7f)
            .SetEase(Ease.Linear).SetDelay(0.35f)
            .OnComplete(() =>
            {
                bool shown = true;
                cursorBlink = DOVirtual.DelayedCall(0.5f, () =>
                {
                    shown = !shown;
                    if (statusText != null) statusText.text = line + (shown ? "_" : " ");
                }).SetLoops(-1);
            });
    }

    // ───────── вкладки и выбор ─────────

    private void SwitchTab(int direction)
    {
        bool orders = direction > 0;
        if (orders != ordersTabActive) ShowTab(orders, true);
    }

    private void ShowTab(bool orders, bool animate)
    {
        ordersTabActive = orders;
        if (suppliesTab != null) suppliesTab.SetActive(!orders, animate);
        if (ordersTab != null) ordersTab.SetActive(orders, animate);
        ShowPage(suppliesPage, suppliesPageBase, !orders, animate, orders ? -1f : 1f);
        ShowPage(ordersPage, ordersPageBase, orders, animate, orders ? 1f : -1f);
        Refresh();
    }

    private static void ShowPage(CanvasGroup page, Vector2 basePosition, bool visible, bool animate, float side)
    {
        if (page == null) return;
        page.gameObject.SetActive(visible);
        if (!visible) return;

        var rect = (RectTransform)page.transform;
        page.DOKill();
        rect.DOKill();
        if (!animate)
        {
            page.alpha = 1f;
            rect.anchoredPosition = basePosition;
            return;
        }
        page.alpha = 0f;
        page.DOFade(1f, 0.2f).SetUpdate(true);
        rect.anchoredPosition = basePosition + new Vector2(24f * side, 0f);
        rect.DOAnchorPos(basePosition, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    /// <summary>Выбрать предыдущий (−1) или следующий (+1) элемент списка текущей вкладки.</summary>
    private void Step(int direction)
    {
        if (ordersTabActive)
        {
            if (shipping == null || shipping.Inbox.Count == 0) return;
            int index = IndexOf(shipping.Inbox, selectedOrder);
            SelectOrder(shipping.Inbox[Wrap(index + direction, shipping.Inbox.Count)]);
        }
        else
        {
            if (orderedBoxes.Count == 0) return;
            int index = orderedBoxes.IndexOf(selectedBox);
            SelectBox(orderedBoxes[Wrap(index + direction, orderedBoxes.Count)]);
        }
    }

    private void ConfirmSelected()
    {
        if (ordersTabActive) AcceptSelected();
        else BuySelected();
    }

    private void SelectBox(LootBoxData box)
    {
        if (box == null || box == selectedBox) return;
        selectedBox = box;
        foreach (var pair in slots) pair.Value.SetSelected(pair.Key == box);
        BindSupplyDetail(true);
    }

    private void SelectOrder(ShippingOrderData order)
    {
        if (order == null || order == selectedOrder) return;
        selectedOrder = order;
        foreach (var row in inboxRows) if (row.gameObject.activeSelf) row.SetSelected(row.Order == order);
        BindOrderDetail(true);
    }

    // ───────── обновление ─────────

    private void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }

    private void HandleBalance(int _)
    {
        if (!IsOpen) return;
        RefreshLive();
        if (balanceText != null) { balanceText.transform.DOKill(true); balanceText.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 8, 0.7f); }
        Refresh();
    }

    private void Refresh()
    {
        if (ordersTabActive) RefreshOrders();
        else RefreshSupplies();
        RefreshLive();
    }

    /// <summary>То, что меняется само со временем: шапка, сводка, доставки в пути, упаковка текущего заказа.</summary>
    private void RefreshLive()
    {
        if (balanceText != null && wallet != null) balanceText.text = wallet.Format(wallet.Balance);
        if (clockText != null && clock != null)
            clockText.text = Loc.Get("terminal.top.clock", clock.Day, GameClock.FormatTime(clock.Hour, 10));

        RefreshSummary();
        if (!ordersTabActive) RefreshTransit();
    }

    private void RefreshSummary()
    {
        if (summaryRoot == null || statRowPrefab == null) return;
        EnsureCount(summaryRows, 5, statRowPrefab, summaryRoot);

        summaryRows[0].Set(Loc.Get("terminal.summary.day"), clock != null
            ? Loc.Get("terminal.summary.day_value", clock.Day, GameClock.FormatTime(clock.Hour, 10)) : "—");
        summaryRows[1].Set(Loc.Get("terminal.summary.balance"), wallet != null ? wallet.Format(wallet.Balance) : "—");
        summaryRows[2].Set(Loc.Get("terminal.summary.level"), progression != null ? progression.CurrentLevel.ToString() : "—");

        BuildingRestorationTracker building = BuildingRestorationTracker.Instance;
        summaryRows[3].Set(Loc.Get("terminal.summary.building"), building != null ? Mathf.RoundToInt(building.ProgressPercent) + "%" : "—");
        summaryRows[4].Set(Loc.Get("terminal.summary.shelves"), shelvingTracker != null ? Mathf.RoundToInt(shelvingTracker.ProgressPercent) + "%" : "—");
    }

    private void RefreshSupplies()
    {
        if (supply == null || supply.catalog == null) return;

        orderedBoxes.Clear();
        foreach (var box in supply.catalog.lootBoxes) if (box != null) orderedBoxes.Add(box);
        orderedBoxes.Sort((a, b) =>
        {
            if (a.category != b.category) return a.category.CompareTo(b.category);
            if (a.requiredLevel != b.requiredLevel) return a.requiredLevel.CompareTo(b.requiredLevel);
            return a.price.CompareTo(b.price);
        });

        foreach (var box in orderedBoxes)
        {
            TerminalSlotUI slot = GetSlot(box);
            if (slot == null) continue;
            slot.Bind(box, IsLocked(supply.Check(box)));
        }

        if (selectedBox == null || !orderedBoxes.Contains(selectedBox)) selectedBox = FirstAvailableBox();
        foreach (var pair in slots) pair.Value.SetSelected(pair.Key == selectedBox);
        BindSupplyDetail(false);
        RefreshTransit();
    }

    private LootBoxData FirstAvailableBox()
    {
        foreach (var box in orderedBoxes) if (!IsLocked(supply.Check(box))) return box;
        return orderedBoxes.Count > 0 ? orderedBoxes[0] : null;
    }

    private TerminalSlotUI GetSlot(LootBoxData box)
    {
        if (slots.TryGetValue(box, out TerminalSlotUI existing)) return existing;
        TerminalCatalogGroupUI group = GetGroup(box.category);
        if (group == null || slotPrefab == null) return null;

        TerminalSlotUI slot = Instantiate(slotPrefab, group.grid);
        slot.OnClicked += s => SelectBox(s.Box);
        slots[box] = slot;
        slot.PlayAppear(0.03f * slots.Count);
        return slot;
    }

    private TerminalCatalogGroupUI GetGroup(LootBoxCategory category)
    {
        if (groups.TryGetValue(category, out TerminalCatalogGroupUI existing)) return existing;
        if (catalogGroupPrefab == null || catalogRoot == null) return null;

        TerminalCatalogGroupUI group = Instantiate(catalogGroupPrefab, catalogRoot);
        int index = (int)category;
        if (group.icon != null && categoryIcons != null && index < categoryIcons.Length) group.icon.sprite = categoryIcons[index];
        if (group.label != null) group.label.text = Loc.Get("terminal.category." + category.ToString().ToLowerInvariant());
        groups[category] = group;

        // Разделы — по порядку enum, а не по порядку появления.
        var ordered = new List<LootBoxCategory>(groups.Keys);
        ordered.Sort();
        for (int i = 0; i < ordered.Count; i++) groups[ordered[i]].transform.SetSiblingIndex(i);
        return group;
    }

    private void BindSupplyDetail(bool animate)
    {
        LootBoxData box = selectedBox;
        if (supplyDetail != null) supplyDetail.gameObject.SetActive(box != null);
        if (box == null) return;
        if (animate) PlayDetailTransition(supplyDetail, supplyDetailBase);

        int price = supply.GetPrice(box);
        var availability = supply.Check(box);
        bool licensed = box.requiredLicense == null || (skills != null && skills.IsOwned(box.requiredLicense));

        SetText(supplyTitle, box.DisplayTitle.ToUpperInvariant());
        if (supplyIcon != null) { supplyIcon.sprite = box.icon; supplyIcon.enabled = box.icon != null; }
        SetText(supplyLevelBadge, box.requiredLevel.ToString());
        if (supplyLicenseBadge != null)
        {
            supplyLicenseBadge.sprite = licensed ? checkSprite : lockSprite;
            supplyLicenseBadge.color = licensed ? accent : warnColor;
        }
        SetText(supplyPrice, wallet != null ? wallet.Format(price) : price.ToString());
        if (supplyOldPrice != null)
        {
            bool discounted = price < box.price;
            supplyOldPrice.gameObject.SetActive(discounted);
            if (discounted && wallet != null) supplyOldPrice.text = wallet.Format(box.price);
        }
        SetText(supplyDelivery, Loc.Get("terminal.delivery_time", FormatTime(supply.GetDeliveryTime(box))));
        SetText(supplyDescription, box.DisplayDescription);
        SetText(lootHeader, Loc.Get("terminal.section.contents", box.rolls));

        // Содержимое: шанс каждой строки — её доля в сумме весов.
        float totalWeight = 0f;
        foreach (var entry in box.loot) if (entry != null && entry.item != null) totalWeight += entry.weight;
        var entries = new List<LootEntry>();
        foreach (var entry in box.loot) if (entry != null && entry.item != null) entries.Add(entry);
        EnsureCount(lootRows, entries.Count, barRowPrefab, lootRoot);
        for (int i = 0; i < entries.Count; i++)
        {
            LootEntry entry = entries[i];
            float chance = totalWeight > 0f ? entry.weight / totalWeight : 0f;
            string count = entry.maxCount > entry.minCount ? "×" + entry.minCount + "–" + entry.maxCount : "×" + entry.minCount;
            lootRows[i].Set(entry.item.icon, entry.item.DisplayName, chance, Mathf.RoundToInt(chance * 100f) + "%  " + count, null, animate);
        }

        // Цена и доставка уже крупно в шапке подробностей — в параметрах только то, чего там нет.
        EnsureCount(supplyStats, 2, statRowPrefab, supplyStatsRoot);
        supplyStats[0].Set(Loc.Get("terminal.stat.license"),
            box.requiredLicense != null ? box.requiredLicense.DisplayTitle : Loc.Get("terminal.value.none"), licensed ? (Color?)null : warnColor);
        supplyStats[1].Set(Loc.Get("terminal.stat.purchase"), Loc.Get(box.oneTimePurchase ? "terminal.value.onetime" : "terminal.value.repeat"));
        if (supplyPrice != null) supplyPrice.color = availability == SupplyService.Availability.NoMoney ? warnColor : textColor;

        bool canBuy = availability == SupplyService.Availability.Available;
        SetActionButton(supplyAction, supplyActionFill, supplyActionLabel, canBuy,
            canBuy ? Loc.Get("terminal.supply.buy_price", wallet != null ? wallet.Format(price) : price.ToString()) : Loc.Get("terminal.supply.unavailable"));
        if (supplyReason != null)
        {
            supplyReason.DOKill();
            supplyReason.text = SupplyReason(box, availability);
            supplyReason.color = warnColor;
        }
    }

    private void RefreshTransit()
    {
        if (supply == null || transitRoot == null) return;
        EnsureCount(transitRows, supply.Deliveries.Count, barRowPrefab, transitRoot);
        for (int i = 0; i < supply.Deliveries.Count; i++)
        {
            SupplyDelivery delivery = supply.Deliveries[i];
            double arrival = supply.GetArrivalTime(delivery);
            string eta = arrival >= 0.0 ? GameClock.FormatTime(GameClock.HourOf(arrival), 10) : FormatTime(delivery.remaining);
            transitRows[i].Set(delivery.box.icon, delivery.box.DisplayTitle, delivery.Progress, eta);
        }
        if (transitEmpty != null) transitEmpty.gameObject.SetActive(supply.Deliveries.Count == 0);
    }

    private void RefreshOrders()
    {
        if (shipping == null) return;

        EnsureCount(inboxRows, shipping.Inbox.Count, listRowPrefab, inboxRoot, row => row.OnClicked += r => SelectOrder(r.Order));
        for (int i = 0; i < shipping.Inbox.Count; i++)
        {
            ShippingOrderData order = shipping.Inbox[i];
            var block = shipping.CheckAccept(order);
            inboxRows[i].Bind(order, DescribeLines(order, false),
                Loc.Get("terminal.order.reward_short", wallet != null ? wallet.Format(shipping.GetReward(order)) : "", order.xpReward),
                block == ShippingService.AcceptBlock.NoLicense || block == ShippingService.AcceptBlock.LowLevel);
        }
        if (inboxEmpty != null) inboxEmpty.gameObject.SetActive(shipping.Inbox.Count == 0);

        if (selectedOrder == null || IndexOf(shipping.Inbox, selectedOrder) < 0)
            selectedOrder = shipping.Inbox.Count > 0 ? shipping.Inbox[0] : null;
        foreach (var row in inboxRows) if (row.gameObject.activeSelf) row.SetSelected(row.Order == selectedOrder);

        BindOrderDetail(false);
        BindActiveOrder();
    }

    private void BindOrderDetail(bool animate)
    {
        ShippingOrderData order = selectedOrder;
        if (orderDetail != null) orderDetail.gameObject.SetActive(order != null);
        if (orderEmpty != null) orderEmpty.gameObject.SetActive(order == null);
        if (order == null) return;
        if (animate) PlayDetailTransition(orderDetail, orderDetailBase);

        SetText(orderTitle, order.DisplayCustomer.ToUpperInvariant());
        SetText(orderMessage, "«" + order.DisplayMessage + "»");

        var lines = new List<OrderLine>();
        foreach (var line in order.lines) if (line != null && line.item != null) lines.Add(line);
        EnsureCount(goodsRows, lines.Count, barRowPrefab, orderGoodsRoot);
        for (int i = 0; i < lines.Count; i++) goodsRows[i].Set(lines[i].item.icon, lines[i].item.DisplayName, -1f, "×" + lines[i].count);

        var block = shipping.CheckAccept(order);
        bool licensed = order.requiredLicense == null || (skills != null && skills.IsOwned(order.requiredLicense));
        bool levelOk = progression == null || progression.CurrentLevel >= order.requiredLevel;
        EnsureCount(orderStats, 4, statRowPrefab, orderStatsRoot);
        orderStats[0].Set(Loc.Get("terminal.stat.money"), wallet != null ? wallet.Format(shipping.GetReward(order)) : shipping.GetReward(order).ToString());
        orderStats[1].Set(Loc.Get("terminal.stat.xp"), "+" + order.xpReward);
        orderStats[2].Set(Loc.Get("terminal.stat.license"),
            order.requiredLicense != null ? order.requiredLicense.DisplayTitle : Loc.Get("terminal.value.none"), licensed ? (Color?)null : warnColor);
        orderStats[3].Set(Loc.Get("terminal.stat.level"), order.requiredLevel.ToString(), levelOk ? (Color?)null : warnColor);

        bool canAccept = block == ShippingService.AcceptBlock.None;
        SetActionButton(orderAction, orderActionFill, orderActionLabel, canAccept,
            Loc.Get(canAccept ? "terminal.order.accept_label" : "terminal.order.unavailable"));
        if (orderReason != null)
        {
            orderReason.text = OrderReason(block);
            orderReason.color = warnColor;
        }
    }

    private void BindActiveOrder()
    {
        ShippingOrderData active = shipping.ActiveOrder;
        if (cancelButton != null) cancelButton.gameObject.SetActive(active != null);

        if (active != null)
        {
            SetText(activeTitle, active.DisplayCustomer.ToUpperInvariant());
            var lines = new List<OrderLine>();
            foreach (var line in active.lines) if (line != null && line.item != null) lines.Add(line);
            EnsureCount(activeRows, lines.Count, barRowPrefab, activeLinesRoot);
            for (int i = 0; i < lines.Count; i++)
            {
                int packed = shipping.PackedCount(lines[i].item);
                activeRows[i].Set(lines[i].item.icon, lines[i].item.DisplayName, (float)packed / lines[i].count, packed + "/" + lines[i].count);
            }
            bool packedAll = shipping.IsPacked;
            SetText(activeStatus, Loc.Get(packedAll ? "terminal.active.packed" : "terminal.active.pack_items"));
            if (activeStatus != null) activeStatus.color = packedAll ? accent : warnColor;
            return;
        }

        EnsureCount(activeRows, 0, barRowPrefab, activeLinesRoot);
        if (shipping.BoxCancelled)
        {
            SetText(activeTitle, Loc.Get("terminal.cancelled.title"));
            SetText(activeStatus, Loc.Get("terminal.cancelled.body", shipping.BoxContents.Count) + "\n" + Loc.Get("terminal.cancelled.status"));
            if (activeStatus != null) activeStatus.color = warnColor;
        }
        else
        {
            SetText(activeTitle, Loc.Get("terminal.none.title"));
            SetText(activeStatus, Loc.Get("terminal.none.body"));
            if (activeStatus != null) activeStatus.color = dimColor;
        }
    }

    // ───────── действия ─────────

    private void BuySelected()
    {
        if (supply == null || selectedBox == null) return;
        LootBoxData box = selectedBox;
        if (supply.TryOrder(box))
        {
            if (slots.TryGetValue(box, out TerminalSlotUI slot)) slot.Punch();
            Flash(supplyReason, Loc.Get("terminal.supply.ordered"), accent);
        }
        else Flash(supplyReason, SupplyReason(box, supply.Check(box)), warnColor);
    }

    private void AcceptSelected()
    {
        if (shipping == null || selectedOrder == null) return;
        if (shipping.Accept(selectedOrder)) return;
        foreach (var row in inboxRows) if (row.Order == selectedOrder) row.Punch();
        Flash(orderReason, OrderReason(shipping.CheckAccept(selectedOrder)), warnColor);
    }

    private void Flash(Text text, string message, Color color)
    {
        if (text == null) return;
        text.DOKill();
        text.text = message;
        text.color = color;
        text.DOFade(0.2f, 0.16f).SetLoops(4, LoopType.Yoyo).SetUpdate(true);
    }

    private void SetActionButton(Button button, Image fill, Text label, bool enabled, string text)
    {
        if (button != null) button.interactable = enabled;
        if (fill != null) fill.color = enabled ? accent : new Color(accent.r, accent.g, accent.b, 0.12f);
        if (label != null)
        {
            label.text = text;
            label.color = enabled ? onAccent : dimColor;
        }
    }

    private static void PlayDetailTransition(CanvasGroup group, Vector2 basePosition)
    {
        if (group == null) return;
        var rect = (RectTransform)group.transform;
        group.DOKill();
        rect.DOKill();
        group.alpha = 0.35f;
        group.DOFade(1f, 0.18f).SetUpdate(true);
        rect.anchoredPosition = basePosition + new Vector2(10f, 0f);
        rect.DOAnchorPos(basePosition, 0.2f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    // ───────── тексты ─────────

    private static bool IsLocked(SupplyService.Availability availability) =>
        availability == SupplyService.Availability.NoLicense || availability == SupplyService.Availability.LowLevel
        || availability == SupplyService.Availability.AlreadyOwned;

    private string SupplyReason(LootBoxData box, SupplyService.Availability availability)
    {
        switch (availability)
        {
            case SupplyService.Availability.NoLicense: return Loc.Get("terminal.block.license", box.requiredLicense.DisplayTitle.ToUpperInvariant());
            case SupplyService.Availability.LowLevel: return Loc.Get("terminal.block.level", box.requiredLevel);
            case SupplyService.Availability.NoMoney: return Loc.Get("terminal.block.money");
            case SupplyService.Availability.AlreadyOwned: return Loc.Get("terminal.block.owned");
            default: return "";
        }
    }

    private string OrderReason(ShippingService.AcceptBlock block)
    {
        switch (block)
        {
            case ShippingService.AcceptBlock.BoxInUse:
                return shipping.BoxCancelled ? Loc.Get("terminal.block.unpack_cancelled") : Loc.Get("terminal.block.finish_current");
            case ShippingService.AcceptBlock.NoLicense: return Loc.Get("terminal.block.license_generic");
            case ShippingService.AcceptBlock.LowLevel: return Loc.Get("terminal.block.higher_level");
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
            sb.Append(line.item.DisplayName).Append(" ×").Append(line.count);
            if (withProgress) sb.Append("   [").Append(shipping.PackedCount(line.item)).Append('/').Append(line.count).Append(']');
        }
        return sb.ToString();
    }

    /// <summary>Длительность в игровых часах: «3 Ч», «1 Ч 30 МИН».</summary>
    private static string FormatTime(float hours)
    {
        int minutes = Mathf.Max(1, Mathf.RoundToInt(hours * 60f));
        int h = minutes / 60;
        int m = minutes % 60;
        if (m == 0) return Loc.Get("terminal.duration_hours", h);
        return h == 0 ? Loc.Get("terminal.duration_minutes", m) : Loc.Get("terminal.duration_hours_minutes", h, m);
    }

    private static void SetText(Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private static Vector2 AnchoredOf(Component component) =>
        component != null ? ((RectTransform)component.transform).anchoredPosition : Vector2.zero;

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    private static int IndexOf(IReadOnlyList<ShippingOrderData> list, ShippingOrderData order)
    {
        for (int i = 0; i < list.Count; i++) if (list[i] == order) return i;
        return -1;
    }

    private static void EnsureCount<T>(List<T> list, int count, T prefab, Transform root, System.Action<T> onCreate = null) where T : Component
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
