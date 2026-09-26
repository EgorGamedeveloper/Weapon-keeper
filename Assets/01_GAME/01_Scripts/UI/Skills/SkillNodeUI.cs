using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Узел дерева навыков: иконка, название, цена и рамка цвета состояния (куплен / можно купить /
/// не хватает очков / закрыт). Наведение слегка увеличивает узел и зажигает подсветку, выбранный
/// узел держит подсветку постоянно, доступный к покупке — мягко «дышит» рамкой.
/// Расстановкой и данными управляет SkillTreeWindow.
/// </summary>
public class SkillNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Ссылки")]
    [Tooltip("Подсветка вокруг узла (наведение/выбор).")]
    public Image glow;

    [Tooltip("Рамка — красится в цвет состояния.")]
    public Image frame;

    public Image icon;
    public Text title;

    [Tooltip("Строка под названием: цена и уровень или «Изучено».")]
    public Text subtitle;

    [Tooltip("Значок замка — узел закрыт уровнем или предыдущими навыками.")]
    public GameObject lockMark;

    [Tooltip("Значок галочки — навык куплен.")]
    public GameObject checkMark;

    [Header("Цвета состояний")]
    public Color ownedColor = new Color(0.36f, 0.85f, 0.55f, 1f);
    public Color availableColor = new Color(1f, 0.82f, 0.25f, 1f);
    public Color notEnoughPointsColor = new Color(0.95f, 0.5f, 0.3f, 1f);
    public Color lockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Анимация")]
    [Tooltip("Во сколько раз увеличивается узел при наведении.")]
    public float hoverScale = 1.06f;

    [Tooltip("Длительность твинов наведения/выбора.")]
    public float tweenDuration = 0.15f;

    public SkillData Skill { get; private set; }

    /// <summary>Клик по узлу — окно выбирает его и показывает детали.</summary>
    public event Action<SkillNodeUI> OnClicked;

    private SkillState state;
    private bool selected;
    private bool hovered;
    private Tween pulseTween;

    public void Bind(SkillData skill, SkillState newState, bool isSelected)
    {
        Skill = skill;
        state = newState;
        selected = isSelected;

        if (icon != null) { icon.sprite = skill.icon; icon.enabled = skill.icon != null; }
        if (title != null) title.text = skill.DisplayTitle;
        if (subtitle != null)
            subtitle.text = state == SkillState.Owned
                ? Loc.Get("skills.state.owned")
                : Loc.Get("skills.node.subtitle",
                    skill.cost > 0 ? Loc.Get("skills.node.cost", skill.cost) : Loc.Get("skills.node.free"), skill.requiredLevel);

        Color stateColor = StateColor(state);
        bool locked = state == SkillState.Locked;

        if (frame != null) frame.color = stateColor;
        if (lockMark != null) lockMark.SetActive(locked);
        if (checkMark != null) checkMark.SetActive(state == SkillState.Owned);
        if (icon != null) icon.color = locked ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        if (title != null) title.color = locked ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        if (subtitle != null) subtitle.color = state == SkillState.Owned ? ownedColor : new Color(stateColor.r, stateColor.g, stateColor.b, locked ? 0.6f : 1f);

        UpdateGlow(false);
        UpdatePulse();
    }

    /// <summary>Анимация покупки: «щелчок» узла и вспышка подсветки цветом «куплено».</summary>
    public void PlayPurchased()
    {
        transform.DOKill(true);
        transform.localScale = Vector3.one * (hovered ? hoverScale : 1f);
        transform.DOPunchScale(Vector3.one * 0.18f, 0.35f, 8, 0.7f);

        if (glow == null) return;
        glow.DOKill();
        glow.color = new Color(ownedColor.r, ownedColor.g, ownedColor.b, 1f);
        glow.DOFade(selected ? 0.8f : 0f, 0.6f).SetEase(Ease.OutQuad);
    }

    /// <summary>Появление при построении ветки — с задержкой по порядку, «волной».</summary>
    public void PlayAppear(float delay)
    {
        transform.DOKill();
        transform.localScale = Vector3.one * 0.6f;
        transform.DOScale(1f, 0.28f).SetDelay(delay).SetEase(Ease.OutBack);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        transform.DOKill();
        transform.DOScale(hoverScale, tweenDuration).SetEase(Ease.OutQuad);
        UpdateGlow(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        transform.DOKill();
        transform.DOScale(1f, tweenDuration).SetEase(Ease.OutQuad);
        UpdateGlow(true);
    }

    public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);

    private void OnDisable()
    {
        pulseTween?.Kill();
        transform.DOKill();
        if (glow != null) glow.DOKill();
        if (frame != null) frame.DOKill();
    }

    private Color StateColor(SkillState s)
    {
        switch (s)
        {
            case SkillState.Owned: return ownedColor;
            case SkillState.Available: return availableColor;
            case SkillState.NotEnoughPoints: return notEnoughPointsColor;
            default: return lockedColor;
        }
    }

    private void UpdateGlow(bool animate)
    {
        if (glow == null) return;

        Color c = selected ? availableColor : StateColor(state);
        float alpha = selected ? 0.8f : hovered ? 0.45f : 0f;
        Color target = new Color(c.r, c.g, c.b, alpha);

        glow.DOKill();
        if (animate) glow.DOColor(target, tweenDuration);
        else glow.color = target;
    }

    // Доступный к покупке узел мягко «дышит» рамкой — глаз сразу находит, что можно изучить.
    private void UpdatePulse()
    {
        pulseTween?.Kill();
        if (frame == null) return;

        Color c = frame.color;
        frame.color = new Color(c.r, c.g, c.b, 1f);
        if (state != SkillState.Available) return;

        pulseTween = frame.DOFade(0.45f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }
}
