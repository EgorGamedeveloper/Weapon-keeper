using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Считает прогресс восстановления здания: сколько точек ремонта починено и пятен оттёрто
/// из общего числа на сцене. Сами точки о трекере не знают — он находит их сам и подписывается,
/// чтобы не таскать ссылку в инспекторе на десятки объектов.
/// UI здесь не строится: прогресс-бар позже подпишется на OnProgressChanged.
/// </summary>
public class BuildingRestorationTracker : MonoBehaviour
{
    /// <summary>Единственный экземпляр на сцену. Осознанное исключение из "в проекте нет синглтонов":
    /// будущим несвязанным системам (UI, магазин, терминал) нужно читать текущий % без цепочки ссылок.</summary>
    public static BuildingRestorationTracker Instance { get; private set; }

    [Tooltip("Пороги в процентах, на которых поднимается OnThresholdReached (например 25, 50, 75, 100).")]
    [SerializeField] private float[] thresholds = { 25f, 50f, 75f, 100f };

    /// <summary>Всего задач восстановления на сцене (точки ремонта + пятна).</summary>
    public int TotalCount { get; private set; }

    /// <summary>Сколько из них уже выполнено.</summary>
    public int RepairedCount { get; private set; }

    public float ProgressPercent => TotalCount == 0 ? 0f : (float)RepairedCount / TotalCount * 100f;

    /// <summary>Прогресс изменился (передаёт текущий процент).</summary>
    public event Action<float> OnProgressChanged;

    /// <summary>Пройден очередной порог (передаёт значение порога). На каждый порог — ровно один раз.</summary>
    public event Action<float> OnThresholdReached;

    private readonly List<RepairPoint> repairPoints = new List<RepairPoint>();
    private readonly List<CleanableStain> stains = new List<CleanableStain>();
    private readonly HashSet<float> firedThresholds = new HashSet<float>();

    private void Awake()
    {
        Instance = this;

        repairPoints.AddRange(FindObjectsByType<RepairPoint>(FindObjectsSortMode.None));
        stains.AddRange(FindObjectsByType<CleanableStain>(FindObjectsSortMode.None));

        TotalCount = repairPoints.Count + stains.Count;

        foreach (var point in repairPoints)
        {
            point.OnRepaired += HandleRepairPointDone;
            if (point.IsRepaired) RepairedCount++;  // точка могла быть починена изначально
        }

        foreach (var stain in stains)
        {
            stain.OnCleaned += HandleStainDone;
            if (stain.IsClean) RepairedCount++;
        }
    }

    private void OnDestroy()
    {
        foreach (var point in repairPoints)
            if (point != null) point.OnRepaired -= HandleRepairPointDone;

        foreach (var stain in stains)
            if (stain != null) stain.OnCleaned -= HandleStainDone;

        if (Instance == this) Instance = null;
    }

    private void HandleRepairPointDone(RepairPoint point) => RegisterCompleted();

    private void HandleStainDone(CleanableStain stain) => RegisterCompleted();

    private void RegisterCompleted()
    {
        RepairedCount = Mathf.Min(RepairedCount + 1, TotalCount);

        float percent = ProgressPercent;
        OnProgressChanged?.Invoke(percent);

        foreach (float threshold in thresholds)
        {
            if (percent >= threshold && firedThresholds.Add(threshold))
                OnThresholdReached?.Invoke(threshold);
        }
    }
}
