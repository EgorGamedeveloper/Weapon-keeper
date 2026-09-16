using UnityEngine;

/// <summary>
/// Компонент, который вешается на предмет в мире (лежащий на полу, столе или на полке).
/// Хранит ссылку на ItemData и умеет подсвечиваться при наведении луча игрока.
/// </summary>
public class WorldItem : MonoBehaviour
{
    public enum ItemState { InWorld, PickingUp, HeldVisible, CarriedHidden, PlacedOnShelf }
    [Tooltip("Данные предмета (ScriptableObject).")]
    public ItemData itemData;

    [Tooltip("Цвет подсветки (emission) при наведении луча игрока.")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    private ShelfSlot sourceSlot;
    private Renderer[] renderers;
    private bool isHighlighted;
    private Rigidbody itemRigidbody;
    private Collider[] colliders;

    public ItemState State { get; private set; } = ItemState.InWorld;
    public Rigidbody Rigidbody => itemRigidbody;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        itemRigidbody = GetComponent<Rigidbody>();
        if (itemRigidbody == null)
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);

        // Гарантируем, что предмет можно поймать лучом.
        if (GetComponentInChildren<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
            colliders = GetComponentsInChildren<Collider>(true);
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

    public void BeginPickup()
    {
        SetHighlight(false);
        SetPhysicsEnabled(false);
        State = ItemState.PickingUp;
    }

    public void SetHeldVisible(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        transform.SetParent(parent, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        SetPhysicsEnabled(false);
        SetVisualEnabled(true);
        State = ItemState.HeldVisible;
    }

    public void SetCarriedHidden(Transform storage)
    {
        transform.SetParent(storage, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetPhysicsEnabled(false);
        SetVisualEnabled(false);
        State = ItemState.CarriedHidden;
    }

    public void PlaceOnShelf(Transform slotTransform, ShelfSlot slot)
    {
        transform.SetParent(slotTransform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        sourceSlot = slot;
        SetPhysicsEnabled(false);
        SetCollidersEnabled(true);
        SetVisualEnabled(true);
        State = ItemState.PlacedOnShelf;
    }

    public void Drop(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        sourceSlot = null;
        SetVisualEnabled(true);
        SetPhysicsEnabled(true);
        if (itemRigidbody != null)
            itemRigidbody.linearVelocity = velocity;
        State = ItemState.InWorld;
    }

    public void SetVisualEnabled(bool enabled)
    {
        foreach (var renderer in renderers)
            if (renderer != null) renderer.enabled = enabled;
    }

    private void SetPhysicsEnabled(bool enabled)
    {
        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = !enabled;
            itemRigidbody.detectCollisions = enabled;
            if (!enabled) itemRigidbody.linearVelocity = Vector3.zero;
        }

        SetCollidersEnabled(enabled);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (var collider in colliders)
            if (collider != null) collider.enabled = enabled;
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
