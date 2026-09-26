using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Общая подсветка элемента терминала при наведении: рамка ярче, лёгкое увеличение.</summary>
public abstract class TerminalHoverItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Подсветка")]
    [Tooltip("Рамка элемента — ярче при наведении.")]
    public Image frame;

    public Color frameNormal = new Color(0.25f, 0.9f, 0.45f, 0.35f);
    public Color frameHover = new Color(0.35f, 1f, 0.55f, 0.9f);

    [Tooltip("Увеличение при наведении.")]
    public float hoverScale = 1.02f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(hoverScale, 0.12f);
        if (frame != null) { frame.DOKill(); frame.DOColor(frameHover, 0.12f); }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(1f, 0.12f);
        if (frame != null) { frame.DOKill(); frame.DOColor(frameNormal, 0.12f); }
    }

    /// <summary>«Щелчок» — подтверждение действия.</summary>
    public void Punch()
    {
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.05f, 0.25f, 8, 0.7f);
    }

    /// <summary>Появление при построении списка.</summary>
    public void PlayAppear(float delay)
    {
        var group = GetComponent<CanvasGroup>();
        if (group == null) return;
        group.DOKill();
        group.alpha = 0f;
        group.DOFade(1f, 0.25f).SetDelay(delay);
    }

    protected virtual void OnDisable()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        if (frame != null) { frame.DOKill(); frame.color = frameNormal; }
    }
}
