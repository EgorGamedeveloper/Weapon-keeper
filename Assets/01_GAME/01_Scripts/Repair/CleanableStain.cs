using System;
using UnityEngine;

/// <summary>
/// Пятно крови/грязи, которое игрок оттирает кликом — без предмета в руках и без инвентаря.
/// Намеренно НЕ реализует IPlaceableSlot: там ничего не устанавливается, "призрак" и предмет
/// не нужны, поэтому у пятна свой маленький контракт.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CleanableStain : MonoBehaviour
{
    [Tooltip("Визуал пятна — выключается после очистки. Если не задан, выключается весь объект.")]
    public GameObject stainVisual;

    [Tooltip("Цвет подсветки (emission) при наведении луча игрока — как у WorldItem.")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    /// <summary>Пятно уже очищено.</summary>
    public bool IsClean { get; private set; }

    /// <summary>Пятно очищено — хук для трекера восстановления.</summary>
    public event Action<CleanableStain> OnCleaned;

    private Renderer[] renderers;
    private bool isHighlighted;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>Включает/выключает подсветку — та же схема через emission, что и в WorldItem.SetHighlight.</summary>
    public void SetHighlight(bool state)
    {
        if (isHighlighted == state) return;
        isHighlighted = state;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (!mat.HasProperty("_EmissionColor")) continue;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", state ? highlightColor : Color.black);
            }
        }
    }

    /// <summary>Оттереть пятно.</summary>
    public void Clean()
    {
        if (IsClean) return;
        IsClean = true;

        SetHighlight(false);

        // Коллайдер выключаем всегда: иначе очищенное пятно продолжает ловить луч игрока
        // и перекрывает то, что за ним (пол, точка ремонта и т.п.).
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (stainVisual != null) stainVisual.SetActive(false);
        else gameObject.SetActive(false);

        OnCleaned?.Invoke(this);
    }
}
