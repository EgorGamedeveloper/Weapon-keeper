using System.Collections.Generic;
using UnityEngine;

/// <summary>Динамический UI tidy-up. Строки — фиксированная стопка по индексу слота инвентаря,
/// порядок никогда не меняется; при смене выбора анимируются только подсветка и масштаб строки
/// (см. TidyUpInventoryRowUI.BindCore), сама она никуда не едет.</summary>
public class TidyUpInventoryUI : MonoBehaviour
{
    public InventorySystem inventory;
    public Transform contentRoot;
    public TidyUpInventoryRowUI rowPrefab;
    private readonly List<TidyUpInventoryRowUI> rows = new List<TidyUpInventoryRowUI>();

    private void Awake()
    {
        // Если строку по ошибке оставили в сцене как шаблон, не показываем её как "лишнюю" запись.
        // Prefab asset не является дочерним объектом contentRoot, поэтому это не влияет на него.
        if (rowPrefab != null && contentRoot != null && rowPrefab.transform.parent == contentRoot)
            rowPrefab.gameObject.SetActive(false);
    }

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
        for (int i = 0; i < count; i++)
            rows[i].Bind(inventory.entries[i], i == selected);
    }
}
