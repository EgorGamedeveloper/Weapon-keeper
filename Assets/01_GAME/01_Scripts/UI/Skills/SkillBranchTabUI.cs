using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Вкладка ветки навыков в окне прокачки: иконка и название. Активная вкладка светлее и
/// подчёркнута, при наведении фон подсвечивается. Бейдж показывает, сколько навыков ветки
/// сейчас можно купить.
/// </summary>
public class SkillBranchTabUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Ссылки")]
    public Image background;
    public Image icon;
    public Text label;

    [Tooltip("Полоска под активной вкладкой.")]
    public Image underline;

    [Tooltip("Бейдж «сколько можно купить в этой ветке». Скрыт, когда ноль.")]
    public GameObject badge;
    public Text badgeText;

    [Header("Цвета")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.04f);
    public Color hoverColor = new Color(1f, 1f, 1f, 0.1f);
    public Color activeColor = new Color(1f, 1f, 1f, 0.16f);
    public Color activeTextColor = Color.white;
    public Color inactiveTextColor = new Color(1f, 1f, 1f, 0.55f);

    [Tooltip("Длительность твинов вкладки.")]
    public float tweenDuration = 0.15f;

    public SkillBranch Branch { get; private set; }

    /// <summary>Клик по вкладке — окно переключает ветку.</summary>
    public event Action<SkillBranchTabUI> OnClicked;

    private bool active;
    private bool hovered;

    public void Bind(SkillBranch branch)
    {
        Branch = branch;
        if (label != null) label.text = branch.displayName;
        if (icon != null) { icon.sprite = branch.icon; icon.enabled = branch.icon != null; }
    }

    public void SetActive(bool isActive, bool animate)
    {
        active = isActive;
        Refresh(animate);

        if (underline == null) return;
        underline.rectTransform.DOKill();
        Vector3 target = new Vector3(active ? 1f : 0f, 1f, 1f);
        if (animate) underline.rectTransform.DOScale(target, 0.22f).SetEase(Ease.OutQuad);
        else underline.rectTransform.localScale = target;
    }

    public void SetAvailableCount(int count)
    {
        if (badge != null) badge.SetActive(count > 0);
        if (badgeText != null) badgeText.text = count.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(true); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Refresh(true); }
    public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);

    private void OnDisable()
    {
        if (background != null) background.DOKill();
        if (underline != null) underline.rectTransform.DOKill();
    }

    private void Refresh(bool animate)
    {
        Color bg = active ? activeColor : hovered ? hoverColor : normalColor;
        Color text = active || hovered ? activeTextColor : inactiveTextColor;

        if (background != null)
        {
            background.DOKill();
            if (animate) background.DOColor(bg, tweenDuration);
            else background.color = bg;
        }

        if (label != null) label.color = text;
        if (icon != null) icon.color = text;
    }
}
