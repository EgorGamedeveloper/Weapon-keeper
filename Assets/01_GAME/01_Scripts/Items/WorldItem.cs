using UnityEngine;
using DG.Tweening;
using ITISKIRUHERE;

/// <summary>
/// Компонент, который вешается на предмет в мире (лежащий на полу, столе или на полке).
/// Хранит ссылку на ItemData и умеет подсвечиваться при наведении луча игрока.
/// </summary>
[RequireComponent(typeof(AdvancedOutline))]
public class WorldItem : MonoBehaviour
{
    public enum ItemState { InWorld, PickingUp, HeldVisible, CarriedHidden, PlacedOnShelf }

    [Header("Данные")]
    [Tooltip("Данные предмета (ScriptableObject).")]
    public ItemData itemData;

    [Header("Обводка при наведении")]
    [Tooltip("Цвет обводки при наведении луча игрока (Advanced Outline System).")]
    public Color highlightColor = Color.white;

    [Tooltip("Толщина обводки (шкала шейдера Advanced Outline System, 0-10; примерно пиксели на 1080p).")]
    public float outlineWidth = 10f;

    [Header("Эффект установки на полку")]
    [Tooltip("За сколько секунд светящаяся полоса проходит по предмету снизу вверх после приземления на полку.")]
    [Min(0.05f)] public float placementScanDuration = 0.6f;

    [Tooltip("Цвет полосы. HDR: значения больше 1 дают свечение через Bloom.")]
    [ColorUsage(false, true)] public Color placementScanColor = new Color(2.4f, 1.8f, 0.45f, 1f);

    private ShelfSlot sourceSlot;
    private Renderer[] renderers;
    private MeshFilter[] meshFilters;
    private Mesh[] originalMeshes;
    private bool isHighlighted;
    private Rigidbody itemRigidbody;
    private Collider[] colliders;
    private AdvancedOutline advancedOutline;

    private Tween placementTween;
    private Tween settleTween;
    private Tween scanTween;

    public ItemState State { get; private set; } = ItemState.InWorld;

    /// <summary>Опыт за установку этого экземпляра на полку уже выдан (PlayerProgression). Живёт на
    /// самом предмете и сохраняется вместе с ним — иначе после загрузки (предметы спавнятся заново)
    /// «снял с полки — поставил снова» опять приносил бы опыт.</summary>
    public bool PlacementRewarded { get; private set; }

    public void SetPlacementRewarded(bool rewarded) => PlacementRewarded = rewarded;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        // Исходные меши запоминаем сейчас, пока обводка выключена: на время наведения
        // AdvancedOutline подменяет sharedMesh своим клоном (см. PlacementScanEffect.Play).
        meshFilters = GetComponentsInChildren<MeshFilter>(true);
        originalMeshes = new Mesh[meshFilters.Length];
        for (int i = 0; i < meshFilters.Length; i++) originalMeshes[i] = meshFilters[i].sharedMesh;

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

        advancedOutline = GetComponent<AdvancedOutline>();
        if (advancedOutline != null)
        {
            advancedOutline.OutlineColor = highlightColor;
            advancedOutline.OutlineWidth = outlineWidth;
            // Обводка включается только на время наведения (SetHighlight). Выключенный компонент
            // рендереры не трогает (AdvancedOutline пропатчен, см. комментарий у его класса).
            advancedOutline.enabled = false;
        }
    }

    private void OnDisable()
    {
        KillTweens();
    }

    public ShelfSlot GetSourceSlot()
    {
        return sourceSlot;
    }

    public void BeginPickup()
    {
        // Глушим всё, что двигает или подсвечивает предмет на полке: полёт на полку, оседание стопки
        // (иначе оседание, начатое до подбора, продолжало бы тащить предмет уже в руке) и полосу.
        KillTweens();
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

    /// <summary>Мгновенная установка (восстановление из сейва, RepairPoint): позиция «этажа» стопки
    /// передаётся снаружи. Без эффекта установки.</summary>
    public void PlaceOnShelf(Transform slotTransform, ShelfSlot slot, Vector3 localPosition, Quaternion localRotation)
    {
        KillTweens();
        transform.SetParent(slotTransform, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = GetShelfLocalScale();
        sourceSlot = slot;
        SetPhysicsEnabled(false);
        EnablePlacedColliders();
        SetVisualEnabled(true);
        State = ItemState.PlacedOnShelf;
    }

    /// <summary>
    /// То же самое, что PlaceOnShelf, но предмет не телепортируется на место, а долетает туда
    /// плавной анимацией из своей текущей позы (обычно — из руки игрока). Состояние (State,
    /// sourceSlot) меняется сразу, синхронно: сейв это не ломает — ShelfSlot уже добавил предмет
    /// в placedItems до вызова этого метода, а точная поза для предметов на полке вообще не
    /// хранится (SaveLoadService.CaptureWorld), только id и порядок в стопке. Коллайдер
    /// включается только по прилёту, чтобы летящий предмет не мешал лучу игрока и не сталкивался
    /// с соседями.
    /// </summary>
    public void PlaceOnShelfAnimated(Transform slotTransform, ShelfSlot slot, Vector3 localPosition, Quaternion localRotation, float duration, Ease ease)
    {
        KillTweens();

        sourceSlot = slot;
        SetPhysicsEnabled(false);
        SetCollidersEnabled(false);
        SetVisualEnabled(true);
        State = ItemState.PlacedOnShelf;

        // worldPositionStays: true — полёт стартует из той точки в мире, где предмет был в руке.
        transform.SetParent(slotTransform, true);

        Vector3 shelfScale = GetShelfLocalScale();

        if (duration <= 0f)
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = shelfScale;
            EnablePlacedColliders();
            PlayPlacementFeedback();
            return;
        }

        var sequence = DOTween.Sequence();
        sequence.Join(transform.DOLocalMove(localPosition, duration).SetEase(ease));
        sequence.Join(transform.DOLocalRotateQuaternion(localRotation, duration).SetEase(ease));
        sequence.Join(transform.DOScale(shelfScale, duration).SetEase(ease));
        sequence.OnComplete(() =>
        {
            EnablePlacedColliders();
            PlayPlacementFeedback();
        });
        placementTween = sequence;
    }

    /// <summary>
    /// Масштаб предмета на полке/точке ремонта — масштаб корня его префаба относительно слота, ровно
    /// как у призрака-подсказки и у предмета, восстановленного из сейва. Иначе экземпляр, растянутый в
    /// сцене (у M16 там 1.2), вставал бы крупнее призрака, а после загрузки — уже другого размера.
    /// </summary>
    private Vector3 GetShelfLocalScale()
    {
        return itemData != null && itemData.worldPrefab != null
            ? itemData.worldPrefab.transform.localScale
            : transform.localScale;
    }

    /// <summary>Фидбек установки предмета на место (по приземлению): звук (ItemData.placementSound,
    /// если задан) и светящаяся полоса снизу вверх (PlacementScanEffect).</summary>
    private void PlayPlacementFeedback()
    {
        if (itemData != null && itemData.placementSound != null)
            AudioSource.PlayClipAtPoint(itemData.placementSound, transform.position);

        scanTween?.Kill();
        scanTween = PlacementScanEffect.Play(renderers, meshFilters, originalMeshes, gameObject.layer, placementScanColor, placementScanDuration);
    }

    /// <summary>Нарисовать поверх предмета дополнительный проход (например, подсветку «видения» сквозь
    /// стены, см. PlacementVision) — по всем сабмешам ИСХОДНЫХ мешей, на текущий кадр. Материалы
    /// рендереров не трогаются, поэтому с обводкой наведения не конфликтует.</summary>
    public void DrawOverlay(in RenderParams renderParams)
    {
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter filter = meshFilters[i];
            Mesh mesh = originalMeshes[i];
            if (filter == null || mesh == null || !filter.gameObject.activeInHierarchy) continue;

            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                Graphics.RenderMesh(renderParams, mesh, sub, matrix);
        }
    }

    /// <summary>Плавно опустить предмет на новый «этаж» стопки (ShelfSlot.RemoveItem, когда из-под
    /// него забрали предмет). Предыдущее оседание перебивается.</summary>
    public void SettleTo(Vector3 localPosition, float duration)
    {
        settleTween?.Kill();
        settleTween = transform.DOLocalMove(localPosition, duration);
    }

    private void KillTweens()
    {
        placementTween?.Kill();
        placementTween = null;
        settleTween?.Kill();
        settleTween = null;
        scanTween?.Kill();
        scanTween = null;
    }

    public void Drop(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        KillTweens();
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

    /// <summary>
    /// Предмет стоит на полке: физики нет (кинематический), но коллайдеры доступны лучу игрока —
    /// иначе стоящий предмет нельзя было бы навести и забрать. Именно это и ломало взятие с полок:
    /// SetPhysicsEnabled(false) выключает Rigidbody.detectCollisions, а коллайдеры такого тела не видит
    /// вообще ни один Raycast, даже если сами коллайдеры включены. Игрок сквозь предметы проходит
    /// по-прежнему (слой IgnoreBullets исключён в excludeLayers его CharacterController).
    /// </summary>
    private void EnablePlacedColliders()
    {
        if (itemRigidbody != null) itemRigidbody.detectCollisions = true;
        SetCollidersEnabled(true);
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
    /// Включает/выключает подсветку предмета при наведении — Advanced Outline System
    /// (стенсильная обводка по силуэту ВСЕХ дочерних рендереров сразу, единым контуром —
    /// в отличие от старого per-submesh хал-экструда, многочастные модели вроде M16
    /// обводятся одной линией, а не по каждой детали отдельно).
    /// </summary>
    public void SetHighlight(bool state)
    {
        if (isHighlighted == state) return;
        isHighlighted = state;

        if (advancedOutline == null) return; // компонент не найден — молча без подсветки
        advancedOutline.enabled = state;
    }
}
