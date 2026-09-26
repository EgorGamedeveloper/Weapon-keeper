using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Появление и скрытие модальных окон (настройки, пауза, достижения, диалоги) — в одном стиле с окном
/// навыков: затемнение проявляется, панель «выпрыгивает». Все твины идут в реальном времени
/// (SetUpdate(true)): на паузе Time.timeScale = 0, и обычные твины замерли бы.
/// </summary>
public static class UiWindowAnimation
{
    public static void Show(CanvasGroup group, RectTransform panel)
    {
        if (group == null) return;

        group.gameObject.SetActive(true);
        group.DOKill();
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.DOFade(1f, 0.18f).SetUpdate(true);

        if (panel == null) return;
        panel.DOKill();
        panel.localScale = Vector3.one * 0.95f;
        panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public static void Hide(CanvasGroup group, RectTransform panel, Action onHidden = null)
    {
        if (group == null)
        {
            onHidden?.Invoke();
            return;
        }

        group.DOKill();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.DOFade(0f, 0.12f).SetUpdate(true).OnComplete(() =>
        {
            group.gameObject.SetActive(false);
            onHidden?.Invoke();
        });

        if (panel == null) return;
        panel.DOKill();
        panel.DOScale(0.97f, 0.12f).SetUpdate(true);
    }

    /// <summary>Спрятать без анимации (начальное состояние окна).</summary>
    public static void HideImmediate(CanvasGroup group)
    {
        if (group == null) return;
        group.DOKill();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }
}
