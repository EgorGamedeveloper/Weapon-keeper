using System.Collections.Generic;
using UnityEngine;

/// <summary>Динамический UI tidy-up. Выбранный тип предмета всегда рисуется последней строкой.</summary>
public class TidyUpInventoryUI : MonoBehaviour
{
    public InventorySystem inventory;
    public Transform contentRoot;
    public TidyUpInventoryRowUI rowPrefab;
    private readonly List<TidyUpInventoryRowUI> rows = new List<TidyUpInventoryRowUI>();

    private void OnEnable()
    {
        if (inventory == null) return;
        inventory.OnInventoryChanged += Refresh;
        inventory.OnActiveSlotChanged += HandleSelectionChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory == null) return;
        inventory.OnInventoryChanged -= Refresh;
        inventory.OnActiveSlotChanged -= HandleSelectionChanged;
    }

    private void HandleSelectionChanged(int _) => Refresh();

    public void Refresh()
    {
        if (inventory == null || contentRoot == null || rowPrefab == null) return;
        int count = inventory.entries.Count;
        while (rows.Count < count) rows.Add(Instantiate(rowPrefab, contentRoot));
        for (int i = 0; i < rows.Count; i++) rows[i].gameObject.SetActive(i < count);
        if (count == 0) return;

        int selected = inventory.activeSlotIndex;
        int rowIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == selected) continue;
            rows[rowIndex].Bind(inventory.entries[i], false);
            rows[rowIndex++].transform.SetSiblingIndex(rowIndex - 1);
        }
        rows[rowIndex].Bind(inventory.entries[selected], true);
        rows[rowIndex].transform.SetSiblingIndex(rowIndex);
    }
}
