using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подсказка у прицела на канвасе: заголовок (предмет или объект) и действия одной строкой
/// («ЛКМ — взять», «ЛКМ — поставить, E — взять», «ЛКМ — оттереть»). Появляется и гаснет быстро и
/// плавно (CanvasGroup на панели); пока панель видна, смена текста — мгновенная. Hide можно звать
/// каждый кадр — повторный вызов ничего не перезапускает.
/// </summary>
public class ItemInfoUI : MonoBehaviour
{
    [Tooltip("Панель, которая показывается при наведении на предмет.")]
    public GameObject panel;
    [Tooltip("Текст с названием предмета.")]
    public Text nameText;
    [Tooltip("Текст с описанием предмета или подсказкой установки.")]
    public Text descriptionText;

    [Header("Появление")]
    [Tooltip("За сколько секунд подсказка проявляется.")]
    [Min(0f)] public float fadeInDuration = 0.08f;

    [Tooltip("За сколько секунд подсказка гаснет.")]
    [Min(0f)] public float fadeOutDuration = 0.1f;

    private CanvasGroup group;
    private Tween fadeTween;
    private bool visible;

    private void Awake()
    {
        if (panel == null) return;
        group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        panel.SetActive(false);
    }

    private void OnDisable()
    {
        fadeTween?.Kill();
    }

    /// <summary>Показать название и описание предмета.</summary>
    public void Show(ItemData item)
    {
        if (item == null) { Hide(); return; }
        SetTexts(item.DisplayName, item.DisplayDescription);
    }

    /// <summary>Показать название предмета и доступные действия одной строкой
    /// (например, «ЛКМ — поставить, E — взять»).</summary>
    public void ShowActions(ItemData item, string actions)
    {
        if (item == null) { Hide(); return; }
        SetTexts(item.DisplayName, actions);
    }

    /// <summary>Показать заголовок и произвольную подсказку — для объектов без ItemData
    /// (терминал: «ЛКМ — открыть терминал», пятно: «ЛКМ — оттереть»).</summary>
    public void ShowHint(string title, string hint)
    {
        SetTexts(title, hint);
    }

    public void Hide()
    {
        if (!visible || panel == null) return;
        visible = false;

        fadeTween?.Kill();
        if (group == null || fadeOutDuration <= 0f)
        {
            panel.SetActive(false);
            return;
        }
        fadeTween = group.DOFade(0f, fadeOutDuration).SetUpdate(true).OnComplete(() => panel.SetActive(false));
    }

    private void SetTexts(string title, string text)
    {
        if (nameText != null) nameText.text = title;
        if (descriptionText != null) descriptionText.text = text;
        if (visible || panel == null) return;
        visible = true;

        panel.SetActive(true);
        fadeTween?.Kill();
        if (group == null || fadeInDuration <= 0f)
        {
            if (group != null) group.alpha = 1f;
            return;
        }
        fadeTween = group.DOFade(1f, fadeInDuration).SetUpdate(true);
    }
}
