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
        if (inventory.slots == null) return;

        for (int i = 0; i < inventory.slots.Length; i++)
        {
            var item = inventory.slots[i].item;

            if (slotIcons != null && i < slotIcons.Length && slotIcons[i] != null)
            {
                slotIcons[i].enabled = item != null;
                slotIcons[i].sprite = item != null ? item.icon : null;
            }

            if (slotHighlights != null && i < slotHighlights.Length && slotHighlights[i] != null)
                slotHighlights[i].SetActive(i == inventory.activeSlotIndex);
        }
    }
}