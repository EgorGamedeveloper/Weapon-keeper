using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель информации о предмете на канвасе. Показывается, когда луч игрока
/// смотрит на предмет или на подходящую ячейку полки.
/// </summary>
public class ItemInfoUI : MonoBehaviour
{
    public GameObject panel;
    public Text nameText;
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
        if (nameText != null) nameText.text = item.itemName;
        if (descriptionText != null) descriptionText.text = item.description;
    }

    /// <summary>Показать подсказку об установке предмета на полку.</summary>
    public void ShowPlacementHint(ItemData item, bool canPlace)
    {
        if (item == null) { Hide(); return; }
        if (panel != null) panel.SetActive(true);
        if (nameText != null) nameText.text = item.itemName;
        if (descriptionText != null)
            descriptionText.text = canPlace ? "ЛКМ — установить \"" + item.itemName + "\" на полку" : item.description;
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}