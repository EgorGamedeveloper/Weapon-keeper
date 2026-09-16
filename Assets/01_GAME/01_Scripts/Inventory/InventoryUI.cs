using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Простой UI панели инвентаря: иконки 5 слотов + подсветка активного слота.
/// Разместите на Canvas и заполните массивы slotIcons/slotHighlights в инспекторе
/// (по количеству слотов инвентаря).
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public InventorySystem inventory;
    public Image[] slotIcons;
    public GameObject[] slotHighlights;

    private void OnEnable()
    {
        if (inventory == null) return;
        inventory.OnInventoryChanged += Refresh;
        inventory.OnActiveSlotChanged += HandleActiveChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory == null) return;
        inventory.OnInventoryChanged -= Refresh;
        inventory.OnActiveSlotChanged -= HandleActiveChanged;
    }

    private void HandleActiveChanged(int index)
    {
        Refresh();
    }

    private void Refresh()
    {
        int visualSlotCount = Mathf.Max(slotIcons != null ? slotIcons.Length : 0, slotHighlights != null ? slotHighlights.Length : 0);
        for (int i = 0; i < visualSlotCount; i++)
        {
            ItemData item = i < inventory.entries.Count ? inventory.entries[i].item : null;

            if (slotIcons != null && i < slotIcons.Length && slotIcons[i] != null)
            {
                slotIcons[i].enabled = item != null;
                slotIcons[i].sprite = item != null ? item.icon : null;
            }

            if (slotHighlights != null && i < slotHighlights.Length && slotHighlights[i] != null)
                slotHighlights[i].SetActive(i == inventory.activeSlotIndex && i < inventory.entries.Count);
        }
    }
}
