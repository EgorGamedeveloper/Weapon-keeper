using UnityEngine;

/// <summary>
/// Точка в руке игрока, куда устанавливается модель активного предмета из инвентаря.
/// При смене активного слота обновляет 3D-модель, при ходьбе добавляет лёгкое покачивание
/// по инерции (sway) и покачивание от шагов (bobbing).
/// </summary>
public class EquippedItemHolder : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Точка в руке игрока (пустой Transform перед камерой), куда крепится модель предмета.")]
    public Transform handPoint;
    public InventorySystem inventory;

    [Tooltip("Rigidbody игрока: его скорость используется для bobbing.")]
    public Rigidbody playerBody;

    [Tooltip("Контейнер для подобранных, но сейчас не отображаемых физических объектов.")]
    public Transform carriedItemsStorage;

    [Header("Покачивание от мыши (sway)")]
    public float swayAmount = 4f;
    public float swaySmooth = 6f;
    public float maxSwayAngle = 8f;

    [Header("Покачивание при ходьбе (bobbing)")]
    public float bobFrequency = 6f;
    public float bobAmount = 0.03f;
    public float bobSmooth = 8f;
    [Tooltip("Минимальная скорость игрока, при которой начинается покачивание.")]
    public float moveThreshold = 0.1f;

    private float bobTimer;
    private Vector3 targetLocalPos;
    private Quaternion targetLocalRot;
    private bool presentationEnabled = true;
    private Vector3 handPointBaseLocalPosition;

    private void OnEnable()
    {
        if (handPoint != null)
            handPointBaseLocalPosition = handPoint.localPosition;
        if (inventory != null)
        {
            inventory.OnActiveSlotChanged += HandleActiveChanged;
            inventory.OnInventoryChanged += RefreshCurrent;
            RefreshCurrent();
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnActiveSlotChanged -= HandleActiveChanged;
            inventory.OnInventoryChanged -= RefreshCurrent;
        }
    }

    private void HandleActiveChanged(int index)
    {
        RefreshCurrent();
    }

    /// <summary>Перемещает существующие физические предметы между рукой и скрытым контейнером.</summary>
    public void RefreshCurrent()
    {
        if (inventory == null || handPoint == null) return;
        if (carriedItemsStorage == null)
        {
            var storage = new GameObject("CarriedItemsStorage");
            storage.transform.SetParent(transform, false);
            carriedItemsStorage = storage.transform;
        }

        foreach (var entry in inventory.entries)
        {
            foreach (var instance in entry.instances)
                if (instance != null) instance.SetCarriedHidden(carriedItemsStorage);
        }

        InventoryEntry active = inventory.GetActiveEntry();
        if (active == null || !presentationEnabled) return;

        int visibleCount = active.item.showAsVisualStack ? active.instances.Count : Mathf.Min(1, active.instances.Count);
        for (int i = 0; i < visibleCount; i++)
        {
            WorldItem instance = active.instances[i];
            if (instance == null) continue;
            Vector3 localPosition = active.item.handPositionOffset + Vector3.up * (active.item.heldStackSpacing * i);
            instance.SetHeldVisible(handPoint, localPosition, Quaternion.Euler(active.item.handRotationOffset));
        }
    }

    public void SetPresentationEnabled(bool enabled)
    {
        presentationEnabled = enabled;
        RefreshCurrent();
    }

    private void Update()
    {
        if (handPoint == null) return;
        ApplySway();
        ApplyBob();
    }

    /// <summary>Покачивание руки в сторону движения мыши, создающее ощущение инерции.</summary>
    private void ApplySway()
    {
        float mouseX = Input.GetAxis("Mouse X") * swayAmount;
        float mouseY = Input.GetAxis("Mouse Y") * swayAmount;

        mouseX = Mathf.Clamp(mouseX, -maxSwayAngle, maxSwayAngle);
        mouseY = Mathf.Clamp(mouseY, -maxSwayAngle, maxSwayAngle);

        targetLocalRot = Quaternion.Euler(-mouseY, mouseX, mouseX * 0.5f);
        handPoint.localRotation = Quaternion.Slerp(handPoint.localRotation, targetLocalRot, Time.deltaTime * swaySmooth);
    }

    /// <summary>Лёгкое покачивание позиции предмета в такт шагам игрока.</summary>
    private void ApplyBob()
    {
        float speed = 0f;
        if (playerBody != null)
        {
            Vector3 horizontalVelocity = playerBody.linearVelocity;
            horizontalVelocity.y = 0f;
            speed = horizontalVelocity.magnitude;
        }

        if (speed > moveThreshold)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            float x = Mathf.Cos(bobTimer) * bobAmount;
            float y = Mathf.Abs(Mathf.Sin(bobTimer)) * bobAmount;
            targetLocalPos = new Vector3(x, y, 0f);
        }
        else
        {
            bobTimer = 0f;
            targetLocalPos = Vector3.zero;
        }

        Vector3 desiredPosition = handPointBaseLocalPosition + targetLocalPos;
        handPoint.localPosition = Vector3.Lerp(handPoint.localPosition, desiredPosition, Time.deltaTime * bobSmooth);
    }
}
