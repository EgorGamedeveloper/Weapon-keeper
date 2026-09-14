using UnityEngine;

/// <summary>
/// Одна ячейка на полке — точка (Transform), в которую можно поставить предмет.
/// Требует Collider (isTrigger = true) для того, чтобы луч игрока мог её обнаружить.
/// При наведении лучом с предметом в руках показывает полупрозрачный "призрак" предмета.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShelfSlot : MonoBehaviour
{
    [HideInInspector] public Shelf parentShelf;
    [HideInInspector] public ItemData currentItem;

    private GameObject ghostInstance;
    private ItemData ghostItem;
    private GameObject placedInstance;

    public bool IsEmpty => currentItem == null;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>Может ли эта ячейка принять данный предмет прямо сейчас.</summary>
    public bool CanAccept(ItemData item)
    {
        if (item == null || !IsEmpty || parentShelf == null) return false;
        return parentShelf.AcceptsItem(item);
    }

    /// <summary>Показать полупрозрачный "призрак" предмета в ячейке (подсказка игроку).</summary>
    public void ShowGhost(ItemData item)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;

        if (ghostInstance != null && ghostItem == item) return; // уже показан этот же предмет
        HideGhost();

        ghostInstance = Instantiate(item.worldPrefab, transform.position, transform.rotation, transform);
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

    /// <summary>Поставить предмет в ячейку (создаёт визуальную модель на полке).</summary>
    public void PlaceItem(ItemData item)
    {
        HideGhost();
        currentItem = item;

        if (item.worldPrefab != null)
        {
            placedInstance = Instantiate(item.worldPrefab, transform.position, transform.rotation, transform);

            var worldItem = placedInstance.GetComponent<WorldItem>();
            if (worldItem == null) worldItem = placedInstance.AddComponent<WorldItem>();
            worldItem.itemData = item;
            worldItem.SetSourceSlot(this);

            if (placedInstance.GetComponentInChildren<Collider>() == null)
                placedInstance.AddComponent<BoxCollider>();
        }
    }

    /// <summary>Убрать предмет из ячейки (например при подборе обратно в инвентарь). Возвращает данные предмета.</summary>
    public ItemData RemoveItem()
    {
        ItemData item = currentItem;
        currentItem = null;

        if (placedInstance != null)
        {
            Destroy(placedInstance);
            placedInstance = null;
        }
        return item;
    }
}
