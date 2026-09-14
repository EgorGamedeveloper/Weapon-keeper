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

    [Tooltip("Необязательно: CharacterController игрока, чтобы читать скорость движения для bobbing.")]
    public CharacterController playerController;

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

    private GameObject currentModel;
    private float bobTimer;
    private Vector3 targetLocalPos;
    private Quaternion targetLocalRot;

    private void OnEnable()
    {
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

    /// <summary>Пересоздаёт модель предмета в руке в соответствии с активным слотом инвентаря.</summary>
    private void RefreshCurrent()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }

        ItemData item = inventory != null ? inventory.GetActiveItem() : null;
        if (item != null && item.worldPrefab != null && handPoint != null)
        {
            currentModel = Instantiate(item.worldPrefab, handPoint);
            currentModel.transform.localPosition = item.handPositionOffset;
            currentModel.transform.localEulerAngles = item.handRotationOffset;

            // В руке предмету не нужны коллайдеры и логика подбора/полки.
            foreach (var col in currentModel.GetComponentsInChildren<Collider>())
                col.enabled = false;

            var worldItemComp = currentModel.GetComponent<WorldItem>();
            if (worldItemComp != null) Destroy(worldItemComp);
        }
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
        if (playerController != null)
        {
            Vector3 horizontalVelocity = playerController.velocity;
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

        handPoint.localPosition = Vector3.Lerp(handPoint.localPosition, targetLocalPos, Time.deltaTime * bobSmooth);
    }
}
