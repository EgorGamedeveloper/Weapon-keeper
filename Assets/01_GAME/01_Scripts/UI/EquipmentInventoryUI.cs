using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Вертикальный список слота экипировки (оружие/лом/будущая швабра) — тот же rowPrefab и та же
/// подсветка выбора, что и в tidy-up (TidyUpInventoryRowUI), но, в отличие от него, строки здесь
/// позиционируются вручную (без Layout Group), потому что активный предмет всегда должен плавно
/// уезжать на нижнюю строку списка — LayoutGroup пересчитывал бы позиции мгновенно на каждый кадр
/// и "перебивал" бы DOTween-анимацию переезда.
/// </summary>
public class EquipmentInventoryUI : MonoBehaviour
{
    public EquipmentInventory equipmentInventory;
    public RectTransform contentRoot;
    public TidyUpInventoryRowUI rowPrefab;

    [Header("Ручная раскладка строк (без Layout Group)")]
    public float rowWidth = 172f;
    public float rowHeight = 48f;
    public float rowSpacing = 4f;
    public float moveTweenDuration = 0.25f;

    private readonly List<TidyUpInventoryRowUI> rows = new List<TidyUpInventoryRowUI>();

    private void Awake()
    {
        if (rowPrefab != null && contentRoot != null && rowPrefab.transform.parent == contentRoot)
            rowPrefab.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (equipmentInventory == null) return;
        equipmentInventory.OnChanged += HandleChanged;
        Refresh(instant: true);
    }

    private void OnDisable()
    {
        if (equipmentInventory == null) return;
        equipmentInventory.OnChanged -= HandleChanged;
    }

    private void HandleChanged() => Refresh(instant: false);

    private void Refresh(bool instant)
    {
        if (equipmentInventory == null || contentRoot == null || rowPrefab == null) return;
        int count = equipmentInventory.items.Count;
        while (rows.Count < count) rows.Add(Instantiate(rowPrefab, contentRoot));
        for (int i = 0; i < rows.Count; i++) rows[i].gameObject.SetActive(i < count);
        if (count == 0) return;

        int selected = equipmentInventory.activeIndex;

        // Активный предмет всегда последний в логическом порядке — то есть на самой нижней строке.
        int logicalIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == selected) continue;
            WorldItem item = equipmentInventory.items[i];
            if (item == null || item.itemData == null) continue;
            PlaceRow(rows[logicalIndex], item.itemData, false, logicalIndex, count, instant);
            logicalIndex++;
        }

        WorldItem active = selected >= 0 && selected < count ? equipmentInventory.items[selected] : null;
        if (active != null && active.itemData != null)
            PlaceRow(rows[logicalIndex], active.itemData, true, logicalIndex, count, instant);
    }

    private void PlaceRow(TidyUpInventoryRowUI row, ItemData item, bool selected, int logicalIndex, int total, bool instant)
    {
        row.Bind(item, selected);

        var rt = (RectTransform)row.transform;
        rt.sizeDelta = new Vector2(rowWidth, rowHeight);

        // logicalIndex считается сверху; переворачиваем — активный (последний логический) должен
        // оказаться на самой нижней строке, у Content (pivot 0.5,0) это позиция ближе к Y=0.
        float indexFromBottom = total - 1 - logicalIndex;
        float y = indexFromBottom * (rowHeight + rowSpacing) + rowHeight / 2f;
        Vector2 targetPos = new Vector2(0f, y);

        rt.DOKill();
        if (instant) rt.anchoredPosition = targetPos;
        else rt.DOAnchorPos(targetPos, moveTweenDuration).SetEase(Ease.OutCubic);
    }
}
