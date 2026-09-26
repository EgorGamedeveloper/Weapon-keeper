using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель информации о предмете на канвасе. Показывается, когда луч игрока смотрит на предмет на
/// полу (название и описание) или на полку (название и действия: «ЛКМ — поставить», «E — взять»).
/// </summary>
public class ItemInfoUI : MonoBehaviour
{
    [Tooltip("Панель, которая показывается при наведении на предмет.")]
    public GameObject panel;
    [Tooltip("Текст с названием предмета.")]
    public Text nameText;
    [Tooltip("Текст с описанием предмета или подсказкой установки.")]
    public Text descriptionText;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Показать информацию о предмете (наведение на подбираемый предмет).</summary>
    public void Show(ItemData item)
    {
        if (item == null) { Hide(); return; }
        if (panel != null) panel.SetActive(true);
        if (nameText != null) nameText.text = item.DisplayName;
        if (descriptionText != null) descriptionText.text = item.DisplayDescription;
    }

    /// <summary>Показать название предмета и доступные действия одной строкой
    /// (например, «ЛКМ — поставить, E — взять»).</summary>
    public void ShowActions(ItemData item, string actions)
    {
        if (item == null) { Hide(); return; }
        if (panel != null) panel.SetActive(true);
        if (nameText != null) nameText.text = item.DisplayName;
        if (descriptionText != null) descriptionText.text = actions;
    }

    /// <summary>Показать заголовок и произвольную подсказку — для объектов без ItemData
    /// (терминал: «ЛКМ — открыть терминал», лифт: «Лифт не работает — нужен ремонт»).</summary>
    public void ShowHint(string title, string hint)
    {
        if (panel != null) panel.SetActive(true);
        if (nameText != null) nameText.text = title;
        if (descriptionText != null) descriptionText.text = hint;
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}