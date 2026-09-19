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
/// </summary>
[RequireComponent(typeof(Collider))]
public class RepairPoint : MonoBehaviour, IPlaceableSlot
{
    [Header("Что нужно для починки")]
    [Tooltip("Предмет, который требуется принести в эту точку.")]
    public ItemData requiredItem;

    [Tooltip("Сколько единиц предмета нужно (например, 3 кирпича на пролом в стене).")]
    [Min(1)] public int requiredCount = 1;

    [Tooltip("Уничтожать предмет при установке. Если выключено — предмет остаётся здесь и его можно забрать обратно.")]
    public bool consumeItem = true;

    [Header("Визуал")]
    [Tooltip("Повреждённый вид (выключается после починки).")]
    public GameObject brokenVisual;

    [Tooltip("Починенный вид (включается после починки).")]
    public GameObject fixedVisual;

    /// <summary>Починена ли точка.</summary>
    public bool IsRepaired { get; private set; }

    /// <summary>Сколько единиц уже принесено (0..requiredCount).</summary>
    public int FilledCount { get; private set; }

    /// <summary>Точка полностью починена — хук для сценарных скриптов (свет, лифт, трекер прогресса).</summary>
    public event Action<RepairPoint> OnRepaired;

    private GameObject ghostInstance;
    private ItemData ghostItem;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        if (brokenVisual != null) brokenVisual.SetActive(!IsRepaired);
        if (fixedVisual != null) fixedVisual.SetActive(IsRepaired);
    }

    public bool CanAccept(ItemData item)
    {
        return !IsRepaired && item != null && item == requiredItem;
    }

    public void ShowGhost(ItemData item)
    {
        if (!CanAccept(item) || item.worldPrefab == null) return;
        if (ghostInstance != null && ghostItem == item) return;
        HideGhost();

        ghostInstance = GhostPreviewUtility.Create(item, transform, Vector3.zero, Quaternion.identity);
        ghostItem = item;
    }

    public void HideGhost()
    {
        if (ghostInstance != null)
        {
            Destroy(ghostInstance);
            ghostInstance = null;
            ghostItem = null;
        }
    }

    public void PlaceItem(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.itemData == null || !CanAccept(worldItem.itemData)) return;
        HideGhost();

        FilledCount++;

        if (consumeItem)
            Destroy(worldItem.gameObject);
        else
            worldItem.PlaceOnShelf(transform, null, Vector3.zero, Quaternion.identity);

        if (FilledCount >= requiredCount)
            CompleteRepair();
    }

    private void CompleteRepair()
    {
        IsRepaired = true;

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
