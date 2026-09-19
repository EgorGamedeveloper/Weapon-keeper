using UnityEngine;

/// <summary>
/// Пример сценарного скрипта на починку: игрок вставил провод в повреждённую проводку —
/// в помещении включается свет. Сам RepairPoint остаётся общим и не знает, что именно
/// он запускает; такой же скрипт позже включит лифт, ворота и т.п.
/// </summary>
public class LightsActivator : MonoBehaviour
{
    [Tooltip("Точка ремонта (проводка), после починки которой включается свет.")]
    public RepairPoint wiringPoint;

    [Tooltip("Источники света, которые должны загореться.")]
    public Light[] roomLights;

    [Tooltip("Дополнительные объекты, которые включатся вместе со светом (лампы, эмиссивные меши и т.п.).")]
    public GameObject[] activateOnRepair;

    private void OnEnable()
    {
        if (wiringPoint != null) wiringPoint.OnRepaired += HandleRepaired;
        ApplyState(wiringPoint != null && wiringPoint.IsRepaired);
    }

    private void OnDisable()
    {
        if (wiringPoint != null) wiringPoint.OnRepaired -= HandleRepaired;
    }

    private void HandleRepaired(RepairPoint point) => ApplyState(true);

    private void ApplyState(bool poweredOn)
    {
        foreach (var light in roomLights)
            if (light != null) light.enabled = poweredOn;

        foreach (var go in activateOnRepair)
            if (go != null) go.SetActive(poweredOn);
    }
}
