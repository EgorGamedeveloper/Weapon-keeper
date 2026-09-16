using UnityEngine;
using UnityEngine.UI;

/// <summary>Одна строка динамического списка tidy-up: иконка, имя, количество и выделение.</summary>
public class TidyUpInventoryRowUI : MonoBehaviour
{
    public Image icon;
    public Text itemName;
    public Text quantity;
    public Image background;
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.82f, 0.25f, 1f);

    public void Bind(InventoryEntry entry, bool selected)
    {
        if (entry == null || entry.item == null) return;
        if (icon != null) { icon.enabled = entry.item.icon != null; icon.sprite = entry.item.icon; }
        if (itemName != null) itemName.text = entry.item.itemName;
        if (quantity != null) quantity.text = entry.Count > 1 ? "× " + entry.Count : string.Empty;
        if (background != null) background.color = selected ? selectedColor : normalColor;
    }
}
