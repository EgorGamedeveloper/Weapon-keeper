using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Одна строка динамического списка: иконка, имя, количество и выделение. Общий для tidy-up
/// (сгруппированные стопки, Bind(InventoryEntry,...)) и списка экипировки (одиночные предметы
/// без стопки, Bind(ItemData,...)) — оба используют этот же префаб, поэтому анимация выделения
/// у них идентична "бесплатно".
/// </summary>
public class TidyUpInventoryRowUI : MonoBehaviour
{
    public Image icon;
    public Text itemName;
    public Text quantity;
    public Image background;
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.82f, 0.25f, 1f);
    public float selectionTweenDuration = 0.15f;

    [Tooltip("Во сколько раз увеличивается выбранный элемент относительно остальных.")]
    public float selectedScale = 1.08f;

    private bool wasSelected;
    private bool initialized;

    public void Bind(InventoryEntry entry, bool selected)
    {
        if (entry == null || entry.item == null) return;
        BindCore(entry.item, entry.Count > 1 ? "× " + entry.Count : string.Empty, selected);
    }

    /// <summary>Для списков без стопки (экипировка): один предмет — одна строка, без счётчика.</summary>
    public void Bind(ItemData item, bool selected)
    {
        BindCore(item, string.Empty, selected);
    }

    private void BindCore(ItemData item, string quantityText, bool selected)
    {
        if (item == null) return;
        if (icon != null) { icon.enabled = item.icon != null; icon.sprite = item.icon; }
        if (itemName != null) itemName.text = item.itemName;
        if (quantity != null) quantity.text = quantityText;

        // Мгновенно при первой привязке/переиспользовании строки — плавно только при реальной смене выделения.
        bool selectionChanged = !initialized || selected != wasSelected;
        if (selectionChanged)
        {
            if (background != null)
            {
                Color target = selected ? selectedColor : normalColor;
                background.DOKill();
                if (initialized) background.DOColor(target, selectionTweenDuration);
                else background.color = target;
            }

            transform.DOKill();
            Vector3 targetScale = Vector3.one * (selected ? selectedScale : 1f);
            if (initialized) transform.DOScale(targetScale, selectionTweenDuration);
            else transform.localScale = targetScale;
        }

        wasSelected = selected;
        initialized = true;
    }
}
