using System;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Точка повреждения, которую игрок чинит, принося нужный предмет: кирпич в стену,
/// доску на пролом, провод в повреждённую проводку. Работает так же, как ячейка полки
/// (тот же контракт IPlaceableSlot: призрак при наведении, установка по клику),
/// но вместо хранения предмета — чинит себя и поднимает OnRepaired.
/// Что именно даёт починка (свет, лифт и т.п.) — задача отдельного сценарного скрипта,
/// подписанного на OnRepaired (см. LightsActivator).
///
/// Два режима:
/// - одиночная точка — на этом же объекте триггер-коллайдер, в него целится игрок; нужно
///   requiredCount единиц предмета;
/// - кладка — у точки есть дочерние RepairSlot (места под кирпичи в проломе стены). Каждое место
///   принимает одну единицу; места собраны в ряды (RepairSlot.layer), и ряд открывается только
///   когда заполнен весь ряд под ним. Требуется столько единиц, сколько мест; коллайдер на самой
///   точке не нужен.
/// </summary>
public class RepairPoint : MonoBehaviour, IPlaceableSlot
{
    [Header("Что нужно для починки")]
    [Tooltip("Предмет, который требуется принести в эту точку.")]
    public ItemData requiredItem;

    [Tooltip("Сколько единиц предмета нужно (например, 3 кирпича на пролом в стене). В режиме кладки " +
             "(есть дочерние RepairSlot) игнорируется — нужно столько, сколько мест.")]
    [Min(1)] public int requiredCount = 1;

    [Header("Визуал")]
    [Tooltip("Повреждённый вид (выключается после починки).")]
    public GameObject brokenVisual;

    [Tooltip("Починенный вид (включается после починки).")]
    public GameObject fixedVisual;

    [Header("Звук")]
    [Tooltip("Точка полностью починена. Не задан — тихо.")]
    public SoundCue repairedSound;

    /// <summary>Починена ли точка.</summary>
    public bool IsRepaired { get; private set; }

    /// <summary>Сколько единиц уже принесено (0..RequiredCount).</summary>
    public int FilledCount { get; private set; }

    /// <summary>Сколько единиц нужно всего: число мест в режиме кладки, иначе requiredCount.</summary>
    public int RequiredCount => Slots.Length > 0 ? Slots.Length : requiredCount;

    /// <summary>Нижний ряд кладки, в котором ещё есть пустые места (только его места принимают кирпич).
    /// int.MaxValue — пустых мест нет или это не кладка.</summary>
    public int OpenLayer { get; private set; } = int.MaxValue;

    /// <summary>Точка полностью починена — хук для сценарных скриптов (свет, лифт, трекер прогресса).</summary>
    public event Action<RepairPoint> OnRepaired;

    private GameObject ghostInstance;
    private ItemData ghostItem;
    private RepairSlot[] slots;

    /// <summary>
    /// Места кладки в порядке рядов снизу вверх, внутри ряда — в порядке иерархии. Этот порядок —
    /// индекс в сейве (RepairPointSave.filledSlots), поэтому места не переставлять между рядами
    /// у вышедшей игры. Собираются лениво: SaveLoadService восстанавливает состояние из своего Awake,
    /// раньше Awake этой точки.
    /// </summary>
    private RepairSlot[] Slots
    {
        get
        {
            // OrderBy устойчив: внутри ряда сохраняется порядок иерархии.
            if (slots == null)
                slots = System.Linq.Enumerable.ToArray(
                    System.Linq.Enumerable.OrderBy(GetComponentsInChildren<RepairSlot>(true), slot => slot.layer));
            return slots;
        }
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider>() == null && GetComponentsInChildren<RepairSlot>(true).Length == 0)
            Debug.LogWarning($"RepairPoint '{name}': нет ни коллайдера (одиночная точка), ни дочерних RepairSlot (кладка) — в неё нельзя прицелиться.", this);
    }
#endif

    private void Awake()
    {
        if (brokenVisual != null) brokenVisual.SetActive(!IsRepaired);
        if (fixedVisual != null) fixedVisual.SetActive(IsRepaired);
        RefreshLayers();
    }

    // ───────────────────────── Одиночная точка ─────────────────────────

    public bool CanAccept(ItemData item)
    {
        // В режиме кладки принимают сами места (RepairSlot), а не точка целиком.
        if (Slots.Length > 0) return false;
        return !IsRepaired && item != null && item == requiredItem;
    }

    public void ShowGhost(ItemData item, GhostMode mode)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;

        // Как и в ShelfSlot: призрак переиспользуется, пока предмет тот же — иначе он
        // пересоздавался бы каждый кадр наведения (см. комментарий там).
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

    public void PlaceItem(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.itemData == null || !CanAccept(worldItem.itemData)) return;
        HideGhost();

        FilledCount++;

        // Принесённый предмет всегда расходуется. Вариант "оставить предмет в точке" был только
        // наполовину реализован: такой предмет не сохранялся (пропадал после загрузки), а подбор
        // его обратно не уменьшал FilledCount — одним предметом можно было починить всё.
        Destroy(worldItem.gameObject);

        if (FilledCount >= RequiredCount)
            CompleteRepair();
    }

    // ───────────────────────── Кладка ─────────────────────────

    /// <summary>Может ли место кладки принять предмет: нужный предмет, место пустое и его ряд открыт.</summary>
    public bool CanAcceptInSlot(RepairSlot slot, ItemData item)
    {
        return !IsRepaired && item != null && item == requiredItem
               && slot != null && !slot.IsFilled && slot.layer == OpenLayer;
    }

    /// <summary>Место кладки заполнено (зовёт RepairSlot.PlaceItem): счёт, открытие следующего ряда,
    /// завершение ремонта на последнем месте.</summary>
    public void NotifySlotFilled(RepairSlot slot)
    {
        FilledCount = CountFilledSlots();
        RefreshLayers();
        // Звук завершения — когда последний кирпич долетит до места, а не в момент клика.
        if (FilledCount >= RequiredCount && !IsRepaired)
            CompleteRepair(slot != null ? slot.placementDuration : 0f);
    }

    /// <summary>Какие места кладки заполнены — для сейва, по порядку Slots.</summary>
    public bool[] GetFilledSlots()
    {
        var result = new bool[Slots.Length];
        for (int i = 0; i < result.Length; i++) result[i] = Slots[i].IsFilled;
        return result;
    }

    private int CountFilledSlots()
    {
        int count = 0;
        foreach (var slot in Slots) if (slot.IsFilled) count++;
        return count;
    }

    /// <summary>Пересчитать открытый ряд и включить триггеры только у пустых мест этого ряда —
    /// сквозь закрытые ряды луч проходит, как сквозь пролом.</summary>
    private void RefreshLayers()
    {
        OpenLayer = int.MaxValue;
        if (!IsRepaired)
            foreach (var slot in Slots)
                if (!slot.IsFilled && slot.layer < OpenLayer) OpenLayer = slot.layer;

        foreach (var slot in Slots)
            slot.SetPlacementEnabled(!IsRepaired && !slot.IsFilled && slot.layer == OpenLayer);
    }

    // ───────────────────────── Сейв ─────────────────────────

    /// <summary>
    /// Восстановление из сейва: выставляет состояние и визуал напрямую, минуя PlaceItem/CompleteRepair,
    /// и намеренно НЕ поднимает OnRepaired — иначе при каждой загрузке сейва точка повторно
    /// зажигала бы свет/открывала лифт и т.п. (см. LightsActivator) и задваивала прогресс квестов.
    /// filledSlots — заполненные места кладки по порядку Slots (для одиночной точки не используется).
    /// </summary>
    public void RestoreState(int filledCount, bool repaired, bool[] filledSlots)
    {
        if (Slots.Length > 0)
        {
            for (int i = 0; i < Slots.Length; i++)
                Slots[i].RestoreFilled(filledSlots != null && i < filledSlots.Length && filledSlots[i]);
            FilledCount = CountFilledSlots();
            IsRepaired = repaired || FilledCount >= RequiredCount;
        }
        else
        {
            FilledCount = Mathf.Clamp(filledCount, 0, requiredCount);
            IsRepaired = repaired;
        }

        if (brokenVisual != null) brokenVisual.SetActive(!IsRepaired);
        if (fixedVisual != null) fixedVisual.SetActive(IsRepaired);
        RefreshLayers();
    }

    private void CompleteRepair(float soundDelay = 0f)
    {
        IsRepaired = true;
        RefreshLayers();
        if (soundDelay > 0f) DOVirtual.DelayedCall(soundDelay, () => SoundPlayer.Play(repairedSound, transform.position)).SetLink(gameObject);
        else SoundPlayer.Play(repairedSound, transform.position);

        if (brokenVisual != null) brokenVisual.SetActive(false);
        if (fixedVisual != null)
        {
            fixedVisual.SetActive(true);
            // Лёгкий "щелчок" вставшей на место детали — тем же DOTween, что и оседание стопки на полке.
            fixedVisual.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f);
        }

        OnRepaired?.Invoke(this);
    }
}
