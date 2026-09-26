using DG.Tweening;
using UnityEngine;

/// <summary>
/// Одно место в кладке пролома (например, под один кирпич) — дочерний объект RepairPoint в режиме
/// кладки. Места собраны в ряды (layer): принимать предмет может только пустое место открытого ряда,
/// то есть ряд над ним откроется, когда будет заполнен весь ряд ниже (решает RepairPoint).
///
/// Принесённый предмет расходуется, а место показывает свой «уложенный» визуал (filledVisual): он
/// появляется в позе предмета из руки и долетает на место, по приземлению включает свой коллайдер и
/// проигрывает ту же светящуюся полосу, что и установка на полку. Так состояние места — один bool,
/// и сейв хранит только его (RepairPointSave.filledSlots), без восстановления предметов.
///
/// Триггер-коллайдер на этом объекте — зона прицеливания; RepairPoint включает его только у пустых
/// мест открытого ряда, сквозь остальные луч проходит, как сквозь пролом.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RepairSlot : MonoBehaviour, IPlaceableSlot
{
    [Header("Кладка")]
    [Tooltip("Ряд кладки: 0 — нижний. Место открывается, когда заполнены все места рядов ниже.")]
    [Min(0)] public int layer;

    [Tooltip("Уложенный предмет (например, кирпич-примитив) — включается, когда место заполнено. " +
             "Его поза в инспекторе — конечная поза после укладки.")]
    public GameObject filledVisual;

    [Header("Анимация установки")]
    [Tooltip("Время полёта предмета из руки на место, секунды. 0 — мгновенно.")]
    [Min(0f)] public float placementDuration = 0.3f;

    [Tooltip("Кривая анимации полёта.")]
    public Ease placementEase = Ease.OutCubic;

    /// <summary>Место заполнено.</summary>
    public bool IsFilled { get; private set; }

    private RepairPoint owner;
    private Collider placementTrigger;
    private Collider[] filledColliders;
    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private bool initialized;

    private GameObject ghostInstance;
    private ItemData ghostItem;
    private Tween flightTween;
    private Tween scanTween;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        EnsureInitialized();
        if (!IsFilled && filledVisual != null) filledVisual.SetActive(false);
    }

    private void OnDisable()
    {
        flightTween?.Kill();
        scanTween?.Kill();
    }

    /// <summary>Ленивая инициализация: SaveLoadService восстанавливает места из своего Awake, раньше
    /// Awake самого места.</summary>
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        owner = GetComponentInParent<RepairPoint>(true);
        placementTrigger = GetComponent<Collider>();
        if (filledVisual != null)
        {
            restLocalPosition = filledVisual.transform.localPosition;
            restLocalRotation = filledVisual.transform.localRotation;
            filledColliders = filledVisual.GetComponentsInChildren<Collider>(true);
        }
    }

    public bool CanAccept(ItemData item)
    {
        EnsureInitialized();
        return owner != null && owner.CanAcceptInSlot(this, item);
    }

    public void ShowGhost(ItemData item, GhostMode mode)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;

        // Как и в ShelfSlot: пока предмет тот же, призрак переиспользуется.
        if (ghostInstance != null && ghostItem == item)
        {
            ghostInstance.SetActive(true);
            GhostPreviewUtility.SetMode(ghostInstance, mode);
            return;
        }

        DestroyGhost();
        ghostInstance = GhostPreviewUtility.Create(item, transform, Vector3.zero, Quaternion.identity, mode);
        ghostItem = item;
    }

    public void HideGhost()
    {
        if (ghostInstance != null) ghostInstance.SetActive(false);
    }

    private void DestroyGhost()
    {
        if (ghostInstance == null) return;
        Destroy(ghostInstance);
        ghostInstance = null;
        ghostItem = null;
    }

    /// <summary>Включить/выключить зону прицеливания (управляет RepairPoint по открытому ряду).</summary>
    public void SetPlacementEnabled(bool placementEnabled)
    {
        EnsureInitialized();
        if (placementTrigger != null) placementTrigger.enabled = placementEnabled;
        if (!placementEnabled) HideGhost();
    }

    public void PlaceItem(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.itemData == null || !CanAccept(worldItem.itemData)) return;
        HideGhost();

        ItemData item = worldItem.itemData;
        Vector3 startPosition = worldItem.transform.position;
        Quaternion startRotation = worldItem.transform.rotation;
        Destroy(worldItem.gameObject);

        // Состояние меняется сразу, синхронно (как у полки): сохранение посреди полёта уже увидит
        // заполненное место.
        IsFilled = true;

        if (filledVisual != null)
        {
            Transform visual = filledVisual.transform;
            filledVisual.SetActive(true);
            SetFilledCollidersEnabled(false);

            if (placementDuration <= 0f)
            {
                visual.localPosition = restLocalPosition;
                visual.localRotation = restLocalRotation;
                Land(item);
            }
            else
            {
                visual.SetPositionAndRotation(startPosition, startRotation);
                flightTween?.Kill();
                flightTween = DOTween.Sequence()
                    .Join(visual.DOLocalMove(restLocalPosition, placementDuration).SetEase(placementEase))
                    .Join(visual.DOLocalRotateQuaternion(restLocalRotation, placementDuration).SetEase(placementEase))
                    .OnComplete(() => Land(item));
            }
        }

        if (owner != null) owner.NotifySlotFilled(this);
    }

    /// <summary>Предмет лёг на место: коллайдер, звук и светящаяся полоса — с настройками самого
    /// предмета (WorldItem на его префабе), чтобы укладка выглядела как установка на полку.</summary>
    private void Land(ItemData item)
    {
        SetFilledCollidersEnabled(true);

        if (item.placementSound != null)
            AudioSource.PlayClipAtPoint(item.placementSound, filledVisual.transform.position);

        WorldItem settings = item.worldPrefab != null ? item.worldPrefab.GetComponent<WorldItem>() : null;
        Color color = settings != null ? settings.placementScanColor : new Color(2.4f, 1.8f, 0.45f, 1f);
        float duration = settings != null ? settings.placementScanDuration : 0.6f;

        scanTween?.Kill();
        scanTween = PlacementScanEffect.Play(filledVisual, color, duration);
    }

    /// <summary>Восстановление из сейва: уложенный визуал сразу на месте, без анимации и событий.
    /// Зону прицеливания после этого выставляет RepairPoint.</summary>
    public void RestoreFilled(bool filled)
    {
        EnsureInitialized();
        flightTween?.Kill();
        IsFilled = filled;

        if (filledVisual == null) return;
        filledVisual.SetActive(filled);
        filledVisual.transform.localPosition = restLocalPosition;
        filledVisual.transform.localRotation = restLocalRotation;
        SetFilledCollidersEnabled(true);
    }

    private void SetFilledCollidersEnabled(bool collidersEnabled)
    {
        if (filledColliders == null) return;
        foreach (var col in filledColliders)
            if (col != null) col.enabled = collidersEnabled;
    }
}
