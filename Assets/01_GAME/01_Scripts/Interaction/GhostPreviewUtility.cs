using UnityEngine;

/// <summary>
/// Создание полупрозрачного "призрака" предмета — общей подсказки для точек установки
/// (ShelfSlot, RepairPoint). Раньше эта логика жила приватно внутри ShelfSlot.
/// </summary>
public static class GhostPreviewUtility
{
    private const float GhostAlpha = 0.35f;

    /// <summary>
    /// Создать призрак предмета под указанным родителем. Возвращает null, если у предмета нет модели.
    /// Коллайдеры призрака выключены, чтобы он не мешал лучу игрока и физике.
    /// </summary>
    public static GameObject Create(ItemData item, Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        if (item == null || item.worldPrefab == null) return null;

        GameObject ghost = Object.Instantiate(item.worldPrefab, parent);
        ghost.transform.localPosition = localPosition;
        ghost.transform.localRotation = localRotation;
        ghost.name = "Ghost_" + item.itemName;

        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            col.enabled = false;

        foreach (var rend in ghost.GetComponentsInChildren<Renderer>())
        {
            Material[] sourceMats = rend.materials;
            Material[] ghostMats = new Material[sourceMats.Length];
            for (int i = 0; i < sourceMats.Length; i++)
            {
                Material m = new Material(sourceMats[i]);
                Color c = m.HasProperty("_Color") ? m.color : Color.white;
                c.a = GhostAlpha;
                SetupTransparent(m, c);
                ghostMats[i] = m;
            }
            rend.materials = ghostMats;
        }

        return ghost;
    }

    private static void SetupTransparent(Material m, Color color)
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
}
