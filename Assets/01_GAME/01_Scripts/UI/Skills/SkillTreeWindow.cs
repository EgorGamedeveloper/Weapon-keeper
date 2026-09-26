using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Окно прокачки (по умолчанию — Tab): вкладки веток, дерево узлов со связями и панель деталей
/// выбранного навыка с кнопкой покупки. Дерево строится целиком из данных: узел ставится по
/// SkillData.row/column, линии — по prerequisites, поэтому новый навык — это только новый ассет.
///
/// Игра на паузу не ставится (время пока ни на что не влияет); пока окно открыто, игровой ввод
/// выключен через общий GameplayInputBlocker — иначе клик по кнопке окна заодно стрелял бы
/// или ставил предмет на полку.
/// </summary>
public class SkillTreeWindow : MonoBehaviour
{
    [Header("Данные")]
    public PlayerSkills playerSkills;
    public PlayerProgression progression;

    [Header("Управление")]
    [Tooltip("Клавиша открытия/закрытия окна. Escape тоже закрывает.")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("Общая блокировка ввода модальных окон (обзор, клики по миру, движение, стрельба, курсор).")]
    public GameplayInputBlocker inputBlocker;

    [Header("Окно")]
    [Tooltip("Корень окна (затемнение + панель) — плавно проявляется и гаснет.")]
    public CanvasGroup windowGroup;

    [Tooltip("Сама панель — «выпрыгивает» при открытии.")]
    public RectTransform panel;

    [Tooltip("Кнопка-крестик в углу панели.")]
    public Button closeButton;

    [Header("Шапка")]
    public Text levelText;
    public Image xpFill;
    public Text pointsText;

    [Tooltip("Плашка с очками — «щёлкает», когда очки меняются.")]
    public RectTransform pointsBadge;

    [Header("Вкладки")]
    public Transform tabsRoot;
    public SkillBranchTabUI tabPrefab;

    [Header("Дерево")]
    [Tooltip("Контейнер дерева — плавно проявляется при смене ветки.")]
    public CanvasGroup treeGroup;
    public RectTransform linesRoot;
    public RectTransform nodesRoot;
    public SkillNodeUI nodePrefab;

    [Tooltip("Шаг сетки узлов: X — между колонками, Y — между рядами (в пикселях канваса).")]
    public Vector2 cellSize = new Vector2(210f, 118f);

    [Tooltip("Толщина линий связей между узлами.")]
    public float lineThickness = 4f;

    [Tooltip("Спрайт линии (квадратный UI_Square).")]
    public Sprite lineSprite;

    [Header("Детали")]
    [Tooltip("Панель деталей — коротко проявляется при смене выбранного узла.")]
    public CanvasGroup detailGroup;
    public Image detailIcon;
    public Text detailTitle;
    public Text detailStatus;
    public Text detailDescription;
    public Text detailEffects;

    [Tooltip("Заголовок «Эффект» — скрывается у навыков без числовых эффектов (лицензии).")]
    public GameObject detailEffectsHeader;
    public Text detailRequirements;
    public Button buyButton;
    public Image buyBackground;
    public Text buyLabel;

    [Tooltip("Подсказка под кнопкой: чего не хватает для покупки.")]
    public Text buyHint;

    [Header("Цвета")]
    public Color accentColor = new Color(1f, 0.82f, 0.25f, 1f);
    public Color ownedColor = new Color(0.36f, 0.85f, 0.55f, 1f);
    public Color dangerColor = new Color(0.95f, 0.45f, 0.35f, 1f);
    public Color mutedColor = new Color(1f, 1f, 1f, 0.45f);
    public Color lineColor = new Color(1f, 1f, 1f, 0.12f);

    public bool IsOpen { get; private set; }

    private readonly List<SkillBranch> branches = new List<SkillBranch>();
    private readonly List<SkillBranchTabUI> tabs = new List<SkillBranchTabUI>();
    private readonly List<SkillNodeUI> nodes = new List<SkillNodeUI>();
    private readonly List<(SkillData from, SkillData to, Image image)> lines = new List<(SkillData, SkillData, Image)>();

    private SkillBranch currentBranch;
    private SkillData selected;
    private int shownPoints = -1;

    private void Awake()
    {
        if (windowGroup != null) windowGroup.gameObject.SetActive(false);
        if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Start() => BuildTabs();

    private void OnEnable()
    {
        if (playerSkills != null) playerSkills.OnSkillsChanged += RefreshIfOpen;
        if (progression == null) return;
        progression.OnXPChanged += HandleXPChanged;
        progression.OnLevelUp += HandleLevelUp;
        progression.OnUnlockPointsChanged += HandlePointsChanged;
    }

    private void OnDisable()
    {
        if (playerSkills != null) playerSkills.OnSkillsChanged -= RefreshIfOpen;
        if (progression == null) return;
        progression.OnXPChanged -= HandleXPChanged;
        progression.OnLevelUp -= HandleLevelUp;
        progression.OnUnlockPointsChanged -= HandlePointsChanged;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsOpen) Close();
            else Open();
        }
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    // ───────────────────────── Открытие / закрытие ─────────────────────────

    public void Open()
    {
        if (IsOpen || windowGroup == null || playerSkills == null) return;
        // Открыто другое модальное окно (терминал) — навыки поверх него не открываем.
        if (inputBlocker != null && inputBlocker.IsBlocked) return;
        IsOpen = true;
        if (inputBlocker != null) inputBlocker.Acquire(this);

        windowGroup.gameObject.SetActive(true);
        windowGroup.DOKill();
        windowGroup.alpha = 0f;
        windowGroup.DOFade(1f, 0.2f);

        if (panel != null)
        {
            panel.DOKill();
            panel.localScale = Vector3.one * 0.94f;
            panel.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        }

        if (currentBranch == null && branches.Count > 0) currentBranch = branches[0];
        shownPoints = -1;
        if (currentBranch != null) ShowBranch(currentBranch, true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        windowGroup.DOKill();
        windowGroup.DOFade(0f, 0.15f).OnComplete(() => windowGroup.gameObject.SetActive(false));
        if (panel != null) { panel.DOKill(); panel.DOScale(0.96f, 0.15f); }

        if (inputBlocker != null) inputBlocker.Release(this);
    }

    // ───────────────────────── Построение ─────────────────────────

    private void BuildTabs()
    {
        if (playerSkills == null || playerSkills.catalog == null || tabsRoot == null || tabPrefab == null) return;

        foreach (var skill in playerSkills.catalog.skills)
            if (skill != null && skill.branch != null && !branches.Contains(skill.branch)) branches.Add(skill.branch);
        branches.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.name, b.name));

        foreach (var branch in branches)
        {
            var tab = Instantiate(tabPrefab, tabsRoot);
            tab.gameObject.SetActive(true);
            tab.Bind(branch);
            tab.SetActive(false, false);
            tab.OnClicked += t => { if (t.Branch != currentBranch) ShowBranch(t.Branch, true); };
            tabs.Add(tab);
        }
    }

    private void ShowBranch(SkillBranch branch, bool animate)
    {
        currentBranch = branch;
        foreach (var tab in tabs) tab.SetActive(tab.Branch == branch, animate);

        foreach (var node in nodes) if (node != null) Destroy(node.gameObject);
        foreach (var line in lines) if (line.image != null) Destroy(line.image.gameObject);
        nodes.Clear();
        lines.Clear();

        var skills = new List<SkillData>();
        int maxColumn = 0, maxRow = 0;
        foreach (var skill in playerSkills.catalog.skills)
        {
            if (skill == null || skill.branch != branch) continue;
            skills.Add(skill);
            maxColumn = Mathf.Max(maxColumn, skill.column);
            maxRow = Mathf.Max(maxRow, skill.row);
        }

        // Длинная ветка (много рядов) не должна вылезать за область дерева — шаг сжимается под её
        // размер, но не больше заданного cellSize.
        Vector2 nodeSize = ((RectTransform)nodePrefab.transform).sizeDelta;
        Vector2 step = cellSize;
        Vector2 room = nodesRoot.rect.size - nodeSize - new Vector2(24f, 24f);
        if (maxColumn > 0 && room.x > 0f) step.x = Mathf.Min(step.x, room.x / maxColumn);
        if (maxRow > 0 && room.y > 0f) step.y = Mathf.Min(step.y, room.y / maxRow);

        // Сетка центрируется в области дерева, ряд 0 — сверху.
        Vector2 origin = new Vector2(-maxColumn * step.x * 0.5f, maxRow * step.y * 0.5f);
        var positions = new Dictionary<SkillData, Vector2>();
        foreach (var skill in skills)
            positions[skill] = origin + new Vector2(skill.column * step.x, -skill.row * step.y);

        foreach (var skill in skills)
            foreach (var prerequisite in skill.prerequisites)
                if (prerequisite != null && positions.ContainsKey(prerequisite))
                    lines.Add((prerequisite, skill, CreateLine(positions[prerequisite], positions[skill])));

        foreach (var skill in skills)
        {
            var node = Instantiate(nodePrefab, nodesRoot);
            node.gameObject.SetActive(true);
            ((RectTransform)node.transform).anchoredPosition = positions[skill];
            node.Bind(skill, playerSkills.GetState(skill), false);
            node.OnClicked += n => Select(n.Skill);
            nodes.Add(node);
            if (animate) node.PlayAppear(skill.row * 0.06f + skill.column * 0.03f);
        }

        if (selected == null || selected.branch != branch) selected = PickDefaultSelection(skills);

        if (animate && treeGroup != null)
        {
            treeGroup.DOKill();
            treeGroup.alpha = 0f;
            treeGroup.DOFade(1f, 0.2f);
        }

        RefreshAll(true);
    }

    private SkillData PickDefaultSelection(List<SkillData> skills)
    {
        foreach (var skill in skills) if (playerSkills.GetState(skill) == SkillState.Available) return skill;
        foreach (var skill in skills) if (playerSkills.GetState(skill) != SkillState.Owned) return skill;
        return skills.Count > 0 ? skills[0] : null;
    }

    private Image CreateLine(Vector2 from, Vector2 to)
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(linesRoot, false);

        Vector2 delta = to - from;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = from;
        rect.sizeDelta = new Vector2(delta.magnitude, lineThickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var image = go.GetComponent<Image>();
        image.sprite = lineSprite;
        image.raycastTarget = false;
        image.color = lineColor;
        return image;
    }

    // ───────────────────────── Обновление ─────────────────────────

    private void Select(SkillData skill)
    {
        if (skill == null || skill == selected) return;
        selected = skill;
        RefreshAll(true);
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) RefreshAll(false);
    }

    private void RefreshAll(bool animateDetails)
    {
        RefreshHeader();

        foreach (var node in nodes)
            node.Bind(node.Skill, playerSkills.GetState(node.Skill), node.Skill == selected);

        foreach (var line in lines)
        {
            bool fromOwned = playerSkills.IsOwned(line.from);
            bool toOwned = playerSkills.IsOwned(line.to);
            Color target = fromOwned && toOwned ? ownedColor
                : fromOwned ? new Color(accentColor.r, accentColor.g, accentColor.b, 0.55f)
                : lineColor;
            line.image.DOKill();
            line.image.DOColor(target, 0.25f);
        }

        foreach (var tab in tabs)
        {
            int available = 0;
            foreach (var skill in playerSkills.catalog.skills)
                if (skill != null && skill.branch == tab.Branch && playerSkills.GetState(skill) == SkillState.Available) available++;
            tab.SetAvailableCount(available);
        }

        RefreshDetails(animateDetails);
    }

    private void RefreshHeader()
    {
        if (progression == null) return;

        if (levelText != null) levelText.text = "Уровень " + progression.CurrentLevel;
        if (xpFill != null)
        {
            float fill = progression.XPToNextLevel > 0 ? (float)progression.CurrentXP / progression.XPToNextLevel : 0f;
            xpFill.DOKill();
            xpFill.DOFillAmount(fill, 0.25f);
        }

        int points = progression.UnlockPoints;
        if (pointsText != null) pointsText.text = points.ToString();
        if (pointsBadge != null && shownPoints >= 0 && points != shownPoints)
        {
            pointsBadge.DOKill(true);
            pointsBadge.DOPunchScale(Vector3.one * 0.2f, 0.3f, 8, 0.7f);
        }
        shownPoints = points;
    }

    private void RefreshDetails(bool animate)
    {
        if (selected == null) return;

        SkillState state = playerSkills.GetState(selected);

        if (detailIcon != null) { detailIcon.sprite = selected.icon; detailIcon.enabled = selected.icon != null; }
        if (detailTitle != null) detailTitle.text = selected.title;
        if (detailDescription != null) detailDescription.text = selected.description;
        string effects = FormatEffects(selected);
        if (detailEffects != null) detailEffects.text = effects;
        if (detailEffectsHeader != null) detailEffectsHeader.SetActive(effects.Length > 0);
        if (detailRequirements != null) detailRequirements.text = FormatRequirements(selected, state);

        if (detailStatus != null)
        {
            switch (state)
            {
                case SkillState.Owned: detailStatus.text = "Изучено"; detailStatus.color = ownedColor; break;
                case SkillState.Available: detailStatus.text = "Можно изучить"; detailStatus.color = accentColor; break;
                case SkillState.NotEnoughPoints: detailStatus.text = "Не хватает очков"; detailStatus.color = dangerColor; break;
                default: detailStatus.text = "Закрыто"; detailStatus.color = mutedColor; break;
            }
        }

        if (buyButton != null) buyButton.gameObject.SetActive(state != SkillState.Owned);
        if (buyLabel != null) buyLabel.text = state == SkillState.Locked ? "Недоступно" : "Изучить  —  " + selected.cost + " оч.";
        if (buyBackground != null) buyBackground.color = state == SkillState.Available ? accentColor : new Color(1f, 1f, 1f, 0.12f);
        if (buyLabel != null) buyLabel.color = state == SkillState.Available ? new Color(0.1f, 0.08f, 0.02f, 1f) : mutedColor;
        if (buyHint != null) buyHint.text = "";

        if (animate && detailGroup != null)
        {
            detailGroup.DOKill();
            detailGroup.alpha = 0.3f;
            detailGroup.DOFade(1f, 0.2f);
        }
    }

    private void OnBuyClicked()
    {
        if (selected == null) return;

        SkillState state = playerSkills.GetState(selected);
        if (state == SkillState.Available && playerSkills.TryPurchase(selected))
        {
            foreach (var node in nodes)
                if (node.Skill == selected) node.PlayPurchased();
            return;
        }

        if (buyHint != null)
        {
            buyHint.color = dangerColor;
            buyHint.text = state == SkillState.NotEnoughPoints && progression != null
                ? "Нужно ещё " + (selected.cost - progression.UnlockPoints) + " оч. — получите следующий уровень."
                : "Сначала выполните требования выше.";
        }

        if (buyButton != null)
        {
            var rect = (RectTransform)buyButton.transform;
            rect.DOKill(true);
            rect.DOShakeAnchorPos(0.3f, new Vector2(8f, 0f), 20, 0f);
        }
    }

    private void HandleXPChanged(int currentXP, int xpToNextLevel) => RefreshIfOpen();
    private void HandleLevelUp(int newLevel, int pointsGranted) => RefreshIfOpen();
    private void HandlePointsChanged(int points) => RefreshIfOpen();

    // ───────────────────────── Тексты ─────────────────────────

    private string FormatEffects(SkillData skill)
    {
        if (skill.modifiers == null || skill.modifiers.Length == 0) return "";

        var sb = new StringBuilder();
        foreach (var modifier in skill.modifiers)
        {
            if (modifier == null) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("• ").Append(StatName(modifier.stat)).Append(": ");

            if (modifier.op == SkillModifierOp.Add)
                sb.Append(modifier.value >= 0f ? "+" : "−").Append(Mathf.Abs(modifier.value).ToString("0.##")).Append(StatUnit(modifier.stat));
            else
            {
                int percent = Mathf.RoundToInt((modifier.value - 1f) * 100f);
                sb.Append(percent >= 0 ? "+" : "−").Append(Mathf.Abs(percent)).Append('%');
            }
        }
        return sb.ToString();
    }

    private string FormatRequirements(SkillData skill, SkillState state)
    {
        if (state == SkillState.Owned) return "";

        var sb = new StringBuilder();
        int level = progression != null ? progression.CurrentLevel : 1;
        AppendRequirement(sb, level >= skill.requiredLevel, "Уровень " + skill.requiredLevel);

        foreach (var prerequisite in skill.prerequisites)
            if (prerequisite != null) AppendRequirement(sb, playerSkills.IsOwned(prerequisite), prerequisite.title);

        int points = progression != null ? progression.UnlockPoints : 0;
        AppendRequirement(sb, points >= skill.cost, "Очки навыков: " + skill.cost + " (есть " + points + ")");
        return sb.ToString();
    }

    private void AppendRequirement(StringBuilder sb, bool ok, string text)
    {
        if (sb.Length > 0) sb.Append('\n');
        string color = ColorUtility.ToHtmlStringRGB(ok ? ownedColor : dangerColor);
        sb.Append("<color=#").Append(color).Append('>').Append(ok ? "✔ " : "✖ ").Append(text).Append("</color>");
    }

    private static string StatName(SkillStat stat)
    {
        switch (stat)
        {
            case SkillStat.TidyUpCapacity: return "Вместимость инвентаря";
            case SkillStat.PickupRange: return "Дальность подбора";
            case SkillStat.WalkSpeed: return "Скорость ходьбы";
            case SkillStat.SprintSpeed: return "Скорость бега";
            case SkillStat.JumpHeight: return "Высота прыжка";
            case SkillStat.MaxHealth: return "Здоровье";
            case SkillStat.XPGain: return "Получаемый опыт";
            case SkillStat.ReloadTime: return "Время перезарядки";
            case SkillStat.Recoil: return "Отдача и разброс";
            case SkillStat.AmmoCapacity: return "Ёмкость магазина";
            case SkillStat.OrderPrice: return "Цена лутбоксов";
            case SkillStat.DeliveryTime: return "Время доставки";
            case SkillStat.ShippingReward: return "Награда за отправку";
            default: return stat.ToString();
        }
    }

    private static string StatUnit(SkillStat stat) => stat == SkillStat.PickupRange ? " м" : "";
}
