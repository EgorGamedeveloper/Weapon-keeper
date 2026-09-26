using DG.Tweening;
using UnityEngine;

/// <summary>
/// Общий фидбек приземления предмета на место (полка — WorldItem, место в кладке — RepairSlot):
/// короткая «пружинка» масштаба и облачко пыли у основания. Звук и светящаяся полоса — у вызывающих.
/// </summary>
public static class LandingFeedback
{
    private const float PunchDuration = 0.28f;
    private const int PunchVibrato = 5;
    private const float PunchElasticity = 0.5f;

    // Размер предмета, на котором облачко пыли играет в масштабе 1 (по большей горизонтальной стороне).
    private const float DustReferenceSize = 0.4f;

    /// <summary>«Пружинка»: предмет чуть вздувается и возвращается к текущему масштабу. amount — доля
    /// размера (0.08 — на 8%). Вызывающий глушит твин через Complete(), чтобы масштаб вернулся точно.</summary>
    public static Tween Punch(Transform target, float amount)
    {
        if (target == null || amount <= 0f) return null;
        return target.DOPunchScale(target.localScale * amount, PunchDuration, PunchVibrato, PunchElasticity);
    }

    /// <summary>Облачко пыли у основания предмета (низ общих границ рендереров), по размеру предмета.
    /// Префаб сам себя уничтожает (ParticleSystem, Stop Action = Destroy).</summary>
    public static void SpawnDust(GameObject dustPrefab, Renderer[] renderers)
    {
        if (dustPrefab == null || renderers == null) return;

        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!hasBounds) return;

        Vector3 position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        GameObject dust = Object.Instantiate(dustPrefab, position, Quaternion.identity);
        float size = Mathf.Max(bounds.size.x, bounds.size.z) / DustReferenceSize;
        dust.transform.localScale = Vector3.one * Mathf.Clamp(size, 0.5f, 2f);
    }
}
