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

    [Tooltip("«Пружинка» по приземлении на место: насколько предмет вздувается, доля размера. 0 — без неё.")]
    [Range(0f, 0.3f)] public float landingPunch = 0.08f;

    [Tooltip("Облачко пыли по приземлении на место (префаб с ParticleSystem, сам себя уничтожает). Пусто — без пыли.")]
    public GameObject landingDust;

    [Header("Удар о поверхность")]
    [Tooltip("Удары медленнее этого молчат (скатывание, оседание), м/с. Звук — ItemData.impactSound.")]
    [Min(0f)] public float impactMinSpeed = 1.2f;

    [Tooltip("Скорость удара, при которой звук играет на полной громкости, м/с.")]
    [Min(0.1f)] public float impactFullSpeed = 6f;

    // Первые мгновения после загрузки сцены предметы оседают на полу — это не «падение», звук не нужен.
    private const float ImpactSilenceAfterLoad = 0.5f;
    private const float ImpactCooldown = 0.1f;

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
    private Tween punchTween;
    private float lastImpactTime = -1f;

    public ItemState State { get; private set; } = ItemState.InWorld;

    /// <summary>
    /// Слой «в руках» (Tools): всё, что игрок держит, — модели инструментов под HandPoint, предмет в руке,
    /// оружие. Основной свет фонарика его не освещает (вблизи пересвечивал бы), а освещает слабый
    /// «ручной» свет (PlayerFlashlight.handLight).
    /// </summary>
    public const int HeldLayer = 8;

    /// <summary>Rendering layer для того, что в руках: только NoDecals (бит 1) — пятна-декали на него не ложатся.</summary>
    public const uint HeldRenderingLayers = 1u << 1;

    // Исходные слой и rendering layers объектов-рендереров (без коллайдеров), пока предмет в руках.
    private (Renderer renderer, int layer, uint renderingLayers)[] heldRestore;
    private bool heldLayerApplied;

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
        SetHeldLayer(true);
        State = ItemState.PickingUp;
    }

    public void SetHeldVisible(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        transform.SetParent(parent, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        SetPhysicsEnabled(false);
        SetVisualEnabled(true);
        SetHeldLayer(true);
        State = ItemState.HeldVisible;
    }

    public void SetCarriedHidden(Transform storage)
    {
        transform.SetParent(storage, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetPhysicsEnabled(false);
        SetVisualEnabled(false);
        SetHeldLayer(true);
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
        SetHeldLayer(false);
        EnablePlacedColliders();
        SetVisualEnabled(true);
        State = ItemState.PlacedOnShelf;
    }

    /// <summary>
    /// То же самое, что PlaceOnShelf, но предмет не телепортируется на место, а долетает туда
    /// плавной анимацией из своей текущей позы (обычно — из руки игрока) по небольшой дуге высотой
    /// arcHeight (на коротком пути — ниже). По приземлении — звук, «пружинка», пыль и полоса. Состояние (State,
    /// sourceSlot) меняется сразу, синхронно: сейв это не ломает — ShelfSlot уже добавил предмет
    /// в placedItems до вызова этого метода, а точная поза для предметов на полке вообще не
    /// хранится (SaveLoadService.CaptureWorld), только id и порядок в стопке. Коллайдер
    /// включается только по прилёту, чтобы летящий предмет не мешал лучу игрока и не сталкивался
    /// с соседями.
    /// </summary>
    public void PlaceOnShelfAnimated(Transform slotTransform, ShelfSlot slot, Vector3 localPosition, Quaternion localRotation, float duration, Ease ease, float arcHeight = 0f)
    {
        KillTweens();

        sourceSlot = slot;
        SetPhysicsEnabled(false);
        SetCollidersEnabled(false);
        SetVisualEnabled(true);
        SetHeldLayer(false);
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

        Vector3 startPosition = transform.localPosition;
        Quaternion startRotation = transform.localRotation;
        Vector3 startScale = transform.localScale;
        // Дуга в пространстве ячейки, по кривой пути (а не по времени): предмет приподнимается и
        // опускается на место, вершина — на середине пути. Короткий путь — дуга ниже.
        float arc = arcHeight * Mathf.Clamp01(Vector3.Distance(transform.position, slotTransform.TransformPoint(localPosition)));

        placementTween = DOVirtual.Float(0f, 1f, duration, t =>
            {
                float eased = DOVirtual.EasedValue(0f, 1f, t, ease);
                transform.localPosition = Vector3.LerpUnclamped(startPosition, localPosition, eased)
                                          + Vector3.up * (arc * Mathf.Sin(Mathf.Clamp01(eased) * Mathf.PI));
                transform.localRotation = Quaternion.SlerpUnclamped(startRotation, localRotation, eased);
                transform.localScale = Vector3.LerpUnclamped(startScale, shelfScale, eased);
            })
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = shelfScale;
                EnablePlacedColliders();
                PlayPlacementFeedback();
            });
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

    /// <summary>Фидбек установки предмета на место (по приземлению): звук (ItemData.placeSound),
    /// «пружинка», облачко пыли и светящаяся полоса снизу вверх (PlacementScanEffect).</summary>
    private void PlayPlacementFeedback()
    {
        if (itemData != null) SoundPlayer.Play(itemData.placeSound, transform.position);

        CompletePunch();
        punchTween = LandingFeedback.Punch(transform, landingPunch);
        LandingFeedback.SpawnDust(landingDust, renderers);

        scanTween?.Kill();
        scanTween = PlacementScanEffect.Play(renderers, meshFilters, originalMeshes, gameObject.layer, placementScanColor, placementScanDuration);
    }

    /// <summary>Досрочно закончить «пружинку»: Complete возвращает масштаб точно к исходному, Kill
    /// оставил бы предмет раздутым.</summary>
    private void CompletePunch()
    {
        if (punchTween != null && punchTween.IsActive()) punchTween.Complete();
        punchTween = null;
    }

    /// <summary>Звук удара о поверхность: только у лежащего в мире предмета (упал, брошен, выпал
    /// из ящика), громкость — от скорости удара.</summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (State != ItemState.InWorld || itemData == null || itemData.impactSound == null) return;
        if (Time.timeSinceLevelLoad < ImpactSilenceAfterLoad || Time.time - lastImpactTime < ImpactCooldown) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < impactMinSpeed) return;
        lastImpactTime = Time.time;

        float volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(impactMinSpeed, impactFullSpeed, speed));
        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        SoundPlayer.Play(itemData.impactSound, point, volume);
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
        CompletePunch();
    }

    public void Drop(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        KillTweens();
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        sourceSlot = null;
        SetHeldLayer(false);
        SetVisualEnabled(true);
        SetPhysicsEnabled(true);
        if (itemRigidbody != null)
            itemRigidbody.linearVelocity = velocity;
        State = ItemState.InWorld;
    }

    /// <summary>
    /// Предмет в руках — его рендереры на слое HeldLayer и без декалей; положили или бросили — исходные
    /// значения. Меняются объекты с рендерерами (и коллайдер на том же объекте, если есть): в руках коллайдеры
    /// выключены (SetPhysicsEnabled), а исходный слой возвращается раньше, чем они снова включаются.
    /// </summary>
    public void SetHeldLayer(bool held)
    {
        if (held == heldLayerApplied) return;
        if (heldRestore == null)
        {
            var list = new System.Collections.Generic.List<(Renderer, int, uint)>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r != null)
                    list.Add((r, r.gameObject.layer, r.renderingLayerMask));
            heldRestore = list.ToArray();
        }

        heldLayerApplied = held;
        foreach (var (r, layer, mask) in heldRestore)
        {
            if (r == null) continue;
            r.gameObject.layer = held ? HeldLayer : layer;
            r.renderingLayerMask = held ? HeldRenderingLayers : mask;
        }
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
