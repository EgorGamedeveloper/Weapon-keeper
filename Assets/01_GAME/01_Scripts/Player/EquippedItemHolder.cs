using UnityEngine;

/// <summary>
/// Точка в руке игрока, куда устанавливается модель активного предмета из инвентаря.
/// При смене активного слота обновляет 3D-модель, при ходьбе добавляет лёгкое покачивание
/// по инерции (sway) и покачивание от шагов (bobbing), а когда в руку прилетает подобранный
/// предмет — короткую «просадку» (PlayCatchDip), будто рука его поймала.
/// </summary>
public class EquippedItemHolder : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Точка в руке игрока (пустой Transform перед камерой), куда крепится модель предмета.")]
    public Transform handPoint;
    [Tooltip("Tidy-up инвентарь: в руке показывается модель его активного предмета.")]
    public InventorySystem inventory;

    [Tooltip("Дочерний pivot HandPoint для sway/bobbing. HandPoint остаётся неподвижным якорем.")]
    public Transform heldItemVisualRoot;

    [Tooltip("Контроллер игрока: его горизонтальная скорость используется для покачивания предмета при ходьбе.")]
    public PlayerCharacterController playerController;

    [Tooltip("Контейнер для подобранных, но сейчас не отображаемых физических объектов.")]
    public Transform carriedItemsStorage;

    [Header("Конфиг")]
    [Tooltip("Если задан — значения покачивания ниже перекрываются из GameConfig при старте.")]
    public GameConfig config;

    [Header("Покачивание от мыши (sway)")]
    [Tooltip("Сила покачивания предмета от движения мыши.")]
    public float swayAmount = 4f;
    [Tooltip("Плавность возврата покачивания от мыши.")]
    public float swaySmooth = 6f;
    [Tooltip("Максимальный угол покачивания от мыши, градусы.")]
    public float maxSwayAngle = 8f;

    [Header("Покачивание при ходьбе (bobbing)")]
    [Tooltip("Частота покачивания при ходьбе.")]
    public float bobFrequency = 6f;
    [Tooltip("Амплитуда покачивания при ходьбе, м.")]
    public float bobAmount = 0.03f;
    [Tooltip("Плавность входа/выхода покачивания при ходьбе.")]
    public float bobSmooth = 8f;
    [Tooltip("Минимальная скорость игрока, при которой начинается покачивание.")]
    public float moveThreshold = 0.1f;

    [Header("Ловля предмета")]
    [Tooltip("Насколько рука проседает, поймав подобранный предмет, м.")]
    [Min(0f)] public float catchDip = 0.025f;

    [Tooltip("Насколько рука при этом наклоняется вперёд, градусы.")]
    [Min(0f)] public float catchTilt = 4f;

    [Tooltip("Жёсткость пружины возврата после просадки.")]
    [Min(1f)] public float catchSpring = 180f;

    [Tooltip("Затухание пружины возврата.")]
    [Min(0f)] public float catchDamping = 16f;

    private float bobTimer;
    private Vector3 targetLocalPos;
    private Quaternion targetLocalRot;
    private bool presentationEnabled = true;
    private Vector3 visualRootBaseLocalPosition;
    private Quaternion visualRootBaseLocalRotation;
    private bool visualRootBasePoseInitialized;
    private float catchOffset;
    private float catchVelocity;
    private float lastCatchOffset;
    private float lastCatchTilt;

    /// <summary>Точка, под которой находятся видимые предметы в руках.</summary>
    public Transform HeldItemTransform
    {
        get
        {
            EnsureHeldItemVisualRoot();
            return heldItemVisualRoot;
        }
    }

    private void Awake()
    {
        if (config == null) return;

        HeldItemSwaySettings s = config.heldItem;
        swayAmount = s.swayAmount;
        swaySmooth = s.swaySmooth;
        maxSwayAngle = s.maxSwayAngle;
        bobFrequency = s.bobFrequency;
        bobAmount = s.bobAmount;
        bobSmooth = s.bobSmooth;
        moveThreshold = s.moveThreshold;
    }

    private void OnEnable()
    {
        EnsureHeldItemVisualRoot();
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
        if (inventory == null || !EnsureHeldItemVisualRoot()) return;
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
            instance.SetHeldVisible(heldItemVisualRoot, localPosition, Quaternion.Euler(active.item.handRotationOffset));
        }
    }

    public void SetPresentationEnabled(bool enabled)
    {
        presentationEnabled = enabled;
        RefreshCurrent();
    }

    private bool EnsureHeldItemVisualRoot()
    {
        if (handPoint == null) return false;
        if (heldItemVisualRoot == null)
        {
            var root = new GameObject("HeldItemVisualRoot");
            root.transform.SetParent(handPoint, false);
            heldItemVisualRoot = root.transform;
            visualRootBasePoseInitialized = false;
        }

        if (!visualRootBasePoseInitialized)
        {
            visualRootBaseLocalPosition = heldItemVisualRoot.localPosition;
            visualRootBaseLocalRotation = heldItemVisualRoot.localRotation;
            visualRootBasePoseInitialized = true;
        }
        return true;
    }

    private void LateUpdate()
    {
        if (!EnsureHeldItemVisualRoot()) return;
        UpdateCatchSpring();
        ApplySway();
        ApplyBob();
    }

    /// <summary>Рука «ловит» прилетевший предмет: короткая просадка вниз с наклоном и возврат пружиной.</summary>
    public void PlayCatchDip()
    {
        catchVelocity -= catchDip * catchSpring * 0.12f;
    }

    private void UpdateCatchSpring()
    {
        float deltaTime = Time.deltaTime;
        catchVelocity -= catchOffset * catchSpring * deltaTime;
        catchVelocity -= catchVelocity * Mathf.Min(1f, catchDamping * deltaTime);
        catchOffset += catchVelocity * deltaTime;

        if (Mathf.Abs(catchOffset) < 0.0002f && Mathf.Abs(catchVelocity) < 0.0002f)
        {
            catchOffset = 0f;
            catchVelocity = 0f;
        }
    }

    /// <summary>Покачивание руки в сторону движения мыши, создающее ощущение инерции.</summary>
    private void ApplySway()
    {
        float mouseX = Input.GetAxis("Mouse X") * swayAmount;
        float mouseY = Input.GetAxis("Mouse Y") * swayAmount;

        mouseX = Mathf.Clamp(mouseX, -maxSwayAngle, maxSwayAngle);
        mouseY = Mathf.Clamp(mouseY, -maxSwayAngle, maxSwayAngle);

        Quaternion swayRotation = Quaternion.Euler(-mouseY, mouseX, mouseX * 0.5f);
        targetLocalRot = visualRootBaseLocalRotation * swayRotation;
        // Наклон от «ловли» — поверх покачивания, без сглаживания (пружина сама плавная); прошлый
        // наклон снимаем, чтобы он не копился в сглаживании.
        Quaternion current = heldItemVisualRoot.localRotation * Quaternion.Euler(-lastCatchTilt, 0f, 0f);
        Quaternion swayed = Quaternion.Slerp(current, targetLocalRot, Time.deltaTime * swaySmooth);
        float tilt = catchDip > 0f ? -catchOffset / catchDip * catchTilt : 0f;
        heldItemVisualRoot.localRotation = swayed * Quaternion.Euler(tilt, 0f, 0f);
        lastCatchTilt = tilt;
    }

    /// <summary>Лёгкое покачивание позиции предмета в такт шагам игрока.</summary>
    private void ApplyBob()
    {
        float speed = playerController != null ? playerController.HorizontalSpeed : 0f;

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

        Vector3 desiredPosition = visualRootBaseLocalPosition + targetLocalPos;
        Vector3 bobbed = Vector3.Lerp(heldItemVisualRoot.localPosition - Vector3.up * lastCatchOffset, desiredPosition, Time.deltaTime * bobSmooth);
        heldItemVisualRoot.localPosition = bobbed + Vector3.up * catchOffset;
        lastCatchOffset = catchOffset;
    }
}
