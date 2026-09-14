using UnityEngine;

/// <summary>
/// Компонент, который вешается на предмет в мире (лежащий на полу, столе или на полке).
/// Хранит ссылку на ItemData и умеет подсвечиваться при наведении луча игрока.
/// </summary>
public class WorldItem : MonoBehaviour
{
    [Tooltip("Данные предмета (ScriptableObject).")]
    public ItemData itemData;

    [Tooltip("Цвет подсветки (emission) при наведении луча игрока.")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    private ShelfSlot sourceSlot;
    private Renderer[] renderers;
    private bool isHighlighted;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();

        // Гарантируем, что предмет можно поймать лучом.
        if (GetComponentInChildren<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
        }
    }

    public void SetSourceSlot(ShelfSlot slot)
    {
        sourceSlot = slot;
    }

    public ShelfSlot GetSourceSlot()
    {
        return sourceSlot;
    }

    /// <summary>
    /// Включает/выключает подсветку предмета через emission material'ов.
    /// Требует шейдер с поддержкой _EmissionColor (Standard, URP/Lit и т.п.).
    /// </summary>
    public void SetHighlight(bool state)
    {
        if (isHighlighted == state) return;
        isHighlighted = state;

        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                if (!mat.HasProperty("_EmissionColor")) continue;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", state ? highlightColor : Color.black);
            }
        }
    }
}