using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Общая подсветка элемента терминала: при наведении рамка ярче и элемент чуть крупнее, у выбранного —
/// своя рамка и мягко пульсирующее свечение. Плитки каталога и строки заказов наследуют её.
/// </summary>
public abstract class TerminalHoverItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Подсветка")]
    [Tooltip("Рамка элемента — ярче при наведении, своя у выбранного.")]
    public Image frame;

    [Tooltip("Свечение выбранного элемента (мягкое пятно позади), пульсирует. Пусто — без свечения.")]
    public Image glow;

    [Tooltip("Цвет рамки в покое.")]
    public Color frameNormal = new Color(0.39f, 0.9f, 0.75f, 0.45f);

    [Tooltip("Цвет рамки при наведении.")]
    public Color frameHover = new Color(0.39f, 0.9f, 0.75f, 0.85f);

    [Tooltip("Цвет рамки у выбранного элемента.")]
    public Color frameSelected = new Color(0.75f, 1f, 0.92f, 1f);

    [Tooltip("Увеличение при наведении.")]
    public float hoverScale = 1.04f;

    /// <summary>Элемент выбран (его подробности показаны в центре).</summary>
    public bool IsSelected { get; private set; }

    private bool hovered;
    private Tween glowTween;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        transform.DOKill();
        transform.DOScale(hoverScale, 0.12f);
        ApplyFrame(0.12f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        transform.DOKill();
        transform.DOScale(1f, 0.12f);
        ApplyFrame(0.12f);
    }

    /// <summary>Отметить элемент выбранным: своя рамка и пульсирующее свечение.</summary>
    public void SetSelected(bool selected)
    {
        if (IsSelected == selected) return;
        IsSelected = selected;
        ApplyFrame(0.15f);

        if (glow == null) return;
        glowTween?.Kill();
        glowTween = null;
        glow.DOKill();
        if (!selected)
        {
            glow.DOFade(0f, 0.15f);
            return;
        }
        Color c = glow.color;
        glow.color = new Color(c.r, c.g, c.b, 0.15f);
        glowTween = glow.DOFade(0.45f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    /// <summary>«Щелчок» — подтверждение действия.</summary>
    public void Punch()
    {
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.06f, 0.25f, 8, 0.7f);
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
        hovered = false;
        transform.DOKill();
        transform.localScale = Vector3.one;
        glowTween?.Kill();
        glowTween = null;
        if (glow != null) { glow.DOKill(); Color c = glow.color; glow.color = new Color(c.r, c.g, c.b, 0f); }
        if (frame != null) { frame.DOKill(); frame.color = IsSelected ? frameSelected : frameNormal; }
        IsSelected = false;
    }

    private void ApplyFrame(float duration)
    {
        if (frame == null) return;
        frame.DOKill();
        frame.DOColor(IsSelected ? frameSelected : hovered ? frameHover : frameNormal, duration);
    }
}
