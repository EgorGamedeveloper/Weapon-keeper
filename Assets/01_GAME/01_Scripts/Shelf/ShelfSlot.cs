using UnityEngine;
using System.Collections.Generic;
using DG;
using DG.Tweening;

/// <summary>
/// Одна ячейка на полке — точка (Transform), в которую можно поставить предмет.
/// Требует Collider (isTrigger = true) для того, чтобы луч игрока мог её обнаружить.
/// При наведении лучом с предметом в руках показывает полупрозрачный "призрак" предмета.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShelfSlot : MonoBehaviour
{
    
    [Header("Режим стопки (колонна)")]
    public bool isStackSlot = false;
    [Min(1)] public int maxStack = 4;        // максимум пачек в колонне
    public float stackSpacing = 0.08f;       // высота одной пачки

    public readonly List<WorldItem> placedItems = new(); // вместо одиночного placedWorldItem
    public int StackCount => placedItems.Count;
    
    [HideInInspector] public Shelf parentShelf;
    [HideInInspector] public ItemData currentItem;

    private GameObject ghostInstance;
    private ItemData ghostItem;
    private GameObject placedInstance;
    private WorldItem placedWorldItem;

    public bool IsEmpty => currentItem == null;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// Позиция, куда встанет СЛЕДУЮЩИЙ предмет = верх стопки. Пустая колонна → база (0).
    public Vector3 GetNextPlacementLocalPosition()
        => isStackSlot ? Vector3.up * (stackSpacing * StackCount) : Vector3.zero;

    public bool CanAccept(ItemData item)
    {
        if (item == null || parentShelf == null || !parentShelf.AcceptsItem(item)) return false;
        if (!isStackSlot) return IsEmpty;                    // старое поведение — одиночный слот
        if (StackCount >= maxStack) return false;
        return StackCount == 0 || placedItems[StackCount - 1].itemData == item; // стопка одного типа
    }

    /// <summary>Показать полупрозрачный "призрак" предмета в ячейке (подсказка игроку).</summary>
    public void ShowGhost(ItemData item)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;

        if (ghostInstance != null && ghostItem == item) return; // уже показан этот же предмет
        HideGhost();

        ghostInstance = Instantiate(item.worldPrefab, transform.position, transform.rotation, transform);
        ghostInstance.transform.localPosition = GetNextPlacementLocalPosition();
        ghostInstance.name = "Ghost_" + item.itemName;
        ghostItem = item;
        MakeGhostVisual(ghostInstance);
    }

    /// <summary>Скрыть призрак предмета.</summary>
    public void HideGhost()
    {
        if (ghostInstance != null)
        {
            Destroy(ghostInstance);
            ghostInstance = null;
            ghostItem = null;
        }
    }

    private void MakeGhostVisual(GameObject go)
    {
        // Отключаем коллайдеры у призрака, чтобы он не мешал лучу/физике.
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Делаем полупрозрачный материал.
        foreach (var rend in go.GetComponentsInChildren<Renderer>())
        {
            Material[] sourceMats = rend.materials;
            Material[] ghostMats = new Material[sourceMats.Length];
            for (int i = 0; i < sourceMats.Length; i++)
            {
                Material m = new Material(sourceMats[i]);
                Color c = m.HasProperty("_Color") ? m.color : Color.white;
                c.a = 0.35f;
                SetupTransparent(m, c);
                ghostMats[i] = m;
            }
            rend.materials = ghostMats;
        }
    }

    private void SetupTransparent(Material m, Color color)
    {
        m.color = color;
        // Standard shader transparent режим (для URP/Lit используйте Surface Type = Transparent в материале-первоисточнике).
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
    }

    /// <summary>Поставить существующий физический предмет в ячейку.</summary>
    public void PlaceItem(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.itemData == null || !CanAccept(worldItem.itemData)) return;
        HideGhost();
        currentItem = worldItem.itemData;
        placedWorldItem = worldItem;
        placedInstance = worldItem.gameObject;
        worldItem.PlaceOnShelf(transform, this);
    }

    /// <summary>Освободить ячейку и вернуть существующий предмет для подбора.</summary>
    public WorldItem RemoveItem(WorldItem item)
    {
        int idx = placedItems.IndexOf(item);
        if (idx < 0) return null;

        placedItems.RemoveAt(idx);
        currentItem = StackCount > 0 ? placedItems[StackCount - 1].itemData : null;

        // Оседание: все, кто стоял выше вынятого, опускаем на «этаж» вниз
        for (int i = idx; i < StackCount; i++)
            placedItems[i].transform.DOLocalMove(Vector3.up * (stackSpacing * i), 0.25f); // DOTween уже есть

        return item;
    }
}
