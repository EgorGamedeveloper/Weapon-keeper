using System;
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Одна ячейка на полке — точка (Transform), в которую можно поставить предмет.
/// Требует Collider (isTrigger = true) для того, чтобы луч игрока мог её обнаружить.
/// При наведении лучом с предметом в руках показывает голограмму-"призрак" предмета.
///
/// Триггер — это «свободное место», а не вся ячейка: у одиночной ячейки он включён, только пока она
/// пуста; у стопки он стоит на следующем «этаже» над верхним предметом и выключается, когда стопка
/// полна. Поэтому ЛКМ по стоящему предмету всегда попадает в сам предмет и забирает его, а ЛКМ по
/// свободному месту ставит предмет из руки. Раньше триггер охватывал стоящий предмет (у стойки M16 —
/// целиком), луч упирался в ячейку, и забрать предмет с полки было нельзя.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShelfSlot : MonoBehaviour, IPlaceableSlot
{

    [Header("Режим стопки (колонна)")]
    [Tooltip("Ячейка-колонна: предметы одного типа ставятся друг на друга. Выключено — один предмет.")]
    public bool isStackSlot = false;

    [Tooltip("Максимум предметов в колонне.")]
    [Min(1)] public int maxStack = 4;

    [Tooltip("Высота одного «этажа» колонны, м.")]
    public float stackSpacing = 0.08f;

    [Header("Анимация установки")]
    [Tooltip("Время полёта предмета из руки на полку, секунды. 0 — мгновенно, как раньше.")]
    [Min(0f)] public float placementDuration = 0.3f;
    [Tooltip("Кривая анимации полёта.")]
    public Ease placementEase = Ease.OutCubic;

    [Tooltip("За сколько секунд стопка оседает, когда из неё забрали предмет.")]
    [Min(0f)] public float settleDuration = 0.25f;

    /// <summary>Предметы в ячейке, снизу вверх.</summary>
    public readonly List<WorldItem> placedItems = new();
    public int StackCount => placedItems.Count;

    /// <summary>Верхний предмет стопки (для одиночной ячейки — единственный) или null.</summary>
    public WorldItem TopItem => StackCount > 0 ? placedItems[StackCount - 1] : null;

    [HideInInspector] public Shelf parentShelf;
    [HideInInspector] public ItemData currentItem;

    /// <summary>Предмет поставлен в эту ячейку (слушает ShelvingProgressTracker).</summary>
    public event Action<ShelfSlot> OnItemPlaced;

    /// <summary>Предмет забран из этой ячейки (слушает ShelvingProgressTracker).</summary>
    public event Action<ShelfSlot> OnItemRemoved;

    private GameObject ghostInstance;
    private ItemData ghostItem;

    private BoxCollider placementTrigger;
    private Vector3 triggerBaseCenter;
    private Vector3 triggerBaseSize;

    public bool IsEmpty => currentItem == null;

    private void Awake()
    {
        placementTrigger = GetComponent<BoxCollider>();
        if (placementTrigger != null)
        {
            triggerBaseCenter = placementTrigger.center;
            triggerBaseSize = placementTrigger.size;
        }
        RefreshPlacementTrigger();
    }

    /// <summary>Поставить триггер туда, где сейчас есть свободное место (см. комментарий у класса).</summary>
    private void RefreshPlacementTrigger()
    {
        if (placementTrigger == null) return;

        if (!isStackSlot)
        {
            placementTrigger.enabled = IsEmpty;
            return;
        }

        bool hasRoom = StackCount < maxStack;
        placementTrigger.enabled = hasRoom;
        if (!hasRoom) return;

        // Следующий «этаж»: предметы стопки стоят опорой снизу, шаг — stackSpacing.
        Vector3 next = GetNextPlacementLocalPosition();
        placementTrigger.center = new Vector3(triggerBaseCenter.x, next.y + stackSpacing * 0.5f, triggerBaseCenter.z);
        placementTrigger.size = new Vector3(triggerBaseSize.x, stackSpacing, triggerBaseSize.z);
    }

    /// <summary>Применить общие настройки анимаций из GameConfig (вызывает Shelf.Awake).</summary>
    public void ApplyShelfSettings(ShelfSettings settings)
    {
        if (settings == null) return;
        placementDuration = settings.placementDuration;
        placementEase = settings.placementEase;
        settleDuration = settings.settleDuration;
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// Позиция, куда встанет СЛЕДУЮЩИЙ предмет = верх стопки. Пустая колонна → база (0).
    public Vector3 GetNextPlacementLocalPosition()
        => isStackSlot ? Vector3.up * (stackSpacing * StackCount) : Vector3.zero;

    public bool CanAccept(ItemData item)
    {
        if (item == null || parentShelf == null || !parentShelf.AcceptsItem(item)) return false;
        if (!isStackSlot) return IsEmpty;                    // старое поведение — одиночный слот
        if (StackCount >= maxStack) return false;
        return StackCount == 0 || placedItems[StackCount - 1].itemData == item; // стопка одного типа
    }

    /// <summary>Показать голограмму-"призрак" предмета в ячейке в нужном режиме (см. GhostMode).</summary>
    public void ShowGhost(ItemData item, GhostMode mode)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;

        // Пока предмет тот же — переиспользуем готовый призрак: PlayerItemInteraction зовёт Show
        // каждый кадр наведения, пересоздавать его каждый раз — мусор в GC на ровном месте.
        // Позицию обновляем: стопка могла подрасти.
        if (ghostInstance != null && ghostItem == item)
        {
            ghostInstance.transform.localPosition = GetNextPlacementLocalPosition();
            ghostInstance.SetActive(true);
            GhostPreviewUtility.SetMode(ghostInstance, mode);
            return;
        }

        DestroyGhost();
        ghostInstance = GhostPreviewUtility.Create(item, transform, GetNextPlacementLocalPosition(), Quaternion.identity, mode);
        ghostItem = item;
    }

    /// <summary>Скрыть призрак: экземпляр остаётся, чтобы не пересоздавать его каждый кадр.</summary>
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

    /// <summary>Поставить существующий физический предмет в ячейку (наверх стопки для isStackSlot).</summary>
    public void PlaceItem(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.itemData == null || !CanAccept(worldItem.itemData)) return;
        HideGhost();

        // Позу вычисляем ДО добавления: это верх текущей стопки (для пустой колонны — база)
        Vector3 localPos = GetNextPlacementLocalPosition();

        placedItems.Add(worldItem);              // ← стопка теперь реально растёт
        currentItem = worldItem.itemData;

        worldItem.PlaceOnShelfAnimated(transform, this, localPos, Quaternion.identity, placementDuration, placementEase);
        RefreshPlacementTrigger();

        OnItemPlaced?.Invoke(this);
    }

    /// <summary>
    /// Восстановление из сейва: ставит предмет в ячейку в обход CanAccept (сейв авторитетен —
    /// раз предмет там лежал, значит подходил) и без события OnItemPlaced — иначе при каждой
    /// загрузке ShelvingProgressTracker, квесты (ShelveItems) и PlayerProgression (XP за полку)
    /// среагировали бы на уже когда-то сделанную расстановку заново.
    ///
    /// Вызывать для одной ячейки строго по порядку снизу вверх (как хранится в сейве) — позиция
    /// каждого следующего предмета считается от текущей высоты стопки, как и в PlaceItem.
    /// </summary>
    public void RestorePlacement(WorldItem item)
    {
        if (item == null || item.itemData == null) return;

        Vector3 localPos = GetNextPlacementLocalPosition();
        placedItems.Add(item);
        currentItem = item.itemData;

        item.PlaceOnShelf(transform, this, localPos, Quaternion.identity);
        RefreshPlacementTrigger();
    }

    /// <summary>Освободить ячейку и вернуть существующий предмет для подбора.</summary>
    public WorldItem RemoveItem(WorldItem item)
    {
        int idx = placedItems.IndexOf(item);
        if (idx < 0) return null;

        placedItems.RemoveAt(idx);
        currentItem = StackCount > 0 ? placedItems[StackCount - 1].itemData : null;

        // Оседание: все, кто стоял выше вынятого, опускаем на «этаж» вниз
        for (int i = idx; i < StackCount; i++)
            placedItems[i].SettleTo(Vector3.up * (stackSpacing * i), settleDuration);
        RefreshPlacementTrigger();

        OnItemRemoved?.Invoke(this);

        return item;
    }
}
