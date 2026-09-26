using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Как показывать призрак предмета в точке установки.</summary>
public enum GhostMode
{
    /// <summary>Свободное место поблизости — тускло, мягко пульсирует.</summary>
    Hint,
    /// <summary>Место под прицелом — ярко.</summary>
    Focused,
    /// <summary>Дальнее место во время «видения» (PlacementVision) — видно сквозь стены, яркость
    /// общая для всех и гаснет в конце способности (SetVisionIntensity).</summary>
    Vision,
}

/// <summary>
/// Создание "призрака" предмета — общей подсказки для точек установки (ShelfSlot, RepairPoint,
/// RepairSlot).
/// Выглядит как ярко-жёлтая голограмма — шейдер Weapon Keeper/Item Hologram (аддитивная заливка +
/// френель-рим по краям силуэта). Текстура предмета не используется: раньше голограмма умножалась
/// на неё и на тёмных моделях оружия получалась почти чёрной.
///
/// Призрак — это ТОЛЬКО визуал: из копии префаба снимаются все скрипты (WorldItem, AdvancedOutline,
/// PersistentId, ShippingBox...), физика и коллайдеры. Раньше это был полноценный WorldItem в состоянии InWorld, и сейв
/// (SaveLoadService.CaptureWorld) записывал спрятанные призраки как лежащие предметы — после загрузки
/// они превращались в настоящие дубликаты.
/// </summary>
public static class GhostPreviewUtility
{
    private const string HologramShaderName = "Weapon Keeper/Item Hologram";

    // HDR: значения больше 1 дают свечение через Bloom (если он включён в пост-обработке).
    private static readonly Color GhostColor = new Color(2.2f, 1.6f, 0.25f, 1f);
    private const float GhostFillIntensity = 0.3f;
    private const float GhostRimIntensity = 1.5f;
    private const float GhostRimPower = 2f;

    // Подсказка свободного места поблизости — тот же цвет, заметно тусклее и с мягкой пульсацией,
    // чтобы место под прицелом явно выделялось среди остальных.
    private const float HintIntensity = 0.35f;
    private const float HintPulseAmount = 0.35f;
    private const float HintPulseSpeed = 3f;

    // «Видение»: сквозь стены (ZTest Always), плотная заливка и яркий контур — мелкие предметы
    // (ящик патронов на 7 м) иначе теряются на фоне стены.
    private const float VisionFillIntensity = 0.45f;
    private const float VisionRimIntensity = 2.5f;
    private const float VisionPulseAmount = 0.2f;

    private static Material focusedMaterialCache;
    private static Material hintMaterialCache;
    private static Material visionMaterialCache;
    private static bool materialsLookedUp;
    private static float visionIntensity = 1f;

    /// <summary>
    /// Создать призрак предмета под указанным родителем в нужном режиме (см. GhostMode).
    /// Возвращает null, если у предмета нет модели.
    /// </summary>
    public static GameObject Create(ItemData item, Transform parent, Vector3 localPosition, Quaternion localRotation, GhostMode mode)
    {
        if (item == null || item.worldPrefab == null) return null;

        // Копию собираем под выключенным временным родителем: у неё не отрабатывают Awake игровых
        // компонентов (WorldItem добавил бы Rigidbody, AdvancedOutline полез бы в меши), и мы успеваем
        // снять их до того, как призрак впервые окажется в активной иерархии.
        var builder = new GameObject("GhostBuilder");
        builder.SetActive(false);
        GameObject ghost = Object.Instantiate(item.worldPrefab, builder.transform);
        StripToVisual(ghost);

        ghost.name = "Ghost_" + item.itemName;
        ghost.transform.SetParent(parent, false);
        ghost.transform.localPosition = localPosition;
        ghost.transform.localRotation = localRotation;
        Object.Destroy(builder);

        foreach (var rend in ghost.GetComponentsInChildren<Renderer>(true))
        {
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
        SetMode(ghost, mode);

        return ghost;
    }

    /// <summary>Переключить режим призрака. Дёшево: только подмена общих материалов, ничего не
    /// пересоздаётся.</summary>
    public static void SetMode(GameObject ghost, GhostMode mode)
    {
        if (ghost == null) return;
        EnsureMaterials();
        Material material = mode == GhostMode.Focused ? focusedMaterialCache
                          : mode == GhostMode.Vision ? visionMaterialCache
                          : hintMaterialCache;
        if (material == null) return; // шейдер не найден — остаётся обычная копия предмета

        foreach (var rend in ghost.GetComponentsInChildren<Renderer>(true))
        {
            Material[] current = rend.sharedMaterials;
            if (current.Length > 0 && current[0] == material) continue;

            var materials = new Material[current.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            rend.sharedMaterials = materials;
        }
    }

    /// <summary>
    /// Снимает с копии префаба всё, кроме Transform и рендеринга: скрипты, физику, коллайдеры.
    /// Компонент нельзя снять, пока его требует ([RequireComponent]) другой компонент того же объекта
    /// (ShippingBox требует WorldItem, WorldItem — AdvancedOutline), поэтому снимаем проходами: каждый
    /// раз то, что уже никому не нужно. Раньше порядок был зашит (сначала WorldItem), и на коробке
    /// отправки снятие WorldItem падало — призрак оставался полноценным предметом.
    /// </summary>
    private static void StripToVisual(GameObject ghost)
    {
        var pending = new List<Component>();
        foreach (var component in ghost.GetComponentsInChildren<Component>(true))
            if (component is MonoBehaviour || component is Rigidbody || component is Collider)
                pending.Add(component);

        bool removedAny = true;
        while (pending.Count > 0 && removedAny)
        {
            removedAny = false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Component component = pending[i];
                if (component != null && IsRequiredByOthers(component)) continue;

                if (component != null) Object.DestroyImmediate(component);
                pending.RemoveAt(i);
                removedAny = true;
            }
        }

        if (pending.Count > 0)
            Debug.LogWarning($"GhostPreviewUtility: у призрака '{ghost.name}' не удалось снять {pending.Count} компонент(ов) — циклический RequireComponent?", ghost);
    }

    /// <summary>Требует ли этот компонент кто-то ещё на том же объекте, и нет ли ему замены того же типа.</summary>
    private static bool IsRequiredByOthers(Component component)
    {
        Type type = component.GetType();
        foreach (var other in component.GetComponents<Component>())
        {
            if (other == null || other == component) continue;
            foreach (RequireComponent require in Attribute.GetCustomAttributes(other.GetType(), typeof(RequireComponent), true))
            {
                if (IsBlocking(require.m_Type0, type, component) || IsBlocking(require.m_Type1, type, component)
                    || IsBlocking(require.m_Type2, type, component))
                    return true;
            }
        }
        return false;
    }

    private static bool IsBlocking(Type required, Type type, Component component)
    {
        if (required == null || !required.IsAssignableFrom(type)) return false;
        // Второй компонент того же требуемого типа (например, ещё один коллайдер) требование закрывает.
        foreach (var candidate in component.GetComponents(required))
            if (candidate != component) return false;
        return true;
    }

    /// <summary>Два общих материала на все призраки (яркий и подсказка): цвет у них одинаковый, а
    /// инстанс на каждый рендерер раньше просто утекал при каждом пересоздании призрака.</summary>
    private static void EnsureMaterials()
    {
        if (materialsLookedUp) return;
        materialsLookedUp = true;

        Shader shader = Shader.Find(HologramShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"GhostPreviewUtility: шейдер '{HologramShaderName}' не найден (проверьте Always Included Shaders) — призрак будет обычной копией предмета.");
            return;
        }

        focusedMaterialCache = BuildMaterial(shader, "Ghost Hologram (runtime)", 1f, 0f);
        hintMaterialCache = BuildMaterial(shader, "Ghost Hint (runtime)", HintIntensity, HintPulseAmount);

        visionMaterialCache = BuildMaterial(shader, "Ghost Vision (runtime)", visionIntensity, VisionPulseAmount);
        visionMaterialCache.SetFloat("_FillIntensity", VisionFillIntensity);
        visionMaterialCache.SetFloat("_RimIntensity", VisionRimIntensity);
        visionMaterialCache.SetFloat("_ZTest", (float)CompareFunction.Always);
    }

    /// <summary>Общий материал «видения» — им же PlacementVision рисует однотипные предметы сквозь
    /// стены. null, если шейдер не найден.</summary>
    public static Material GetVisionMaterial()
    {
        EnsureMaterials();
        return visionMaterialCache;
    }

    /// <summary>Яркость «видения» 0..1 (PlacementVision гасит её в конце способности) — одна на все
    /// дальние призраки и подсвеченные предметы.</summary>
    public static void SetVisionIntensity(float intensity)
    {
        visionIntensity = Mathf.Clamp01(intensity);
        EnsureMaterials();
        if (visionMaterialCache != null) visionMaterialCache.SetFloat("_Intensity", visionIntensity);
    }

    private static Material BuildMaterial(Shader shader, string name, float intensity, float pulseAmount)
    {
        var material = new Material(shader) { name = name };
        material.SetColor("_Color", GhostColor);
        material.SetFloat("_FillIntensity", GhostFillIntensity);
        material.SetFloat("_RimIntensity", GhostRimIntensity);
        material.SetFloat("_RimPower", GhostRimPower);
        material.SetFloat("_ScanIntensity", 0f);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_PulseAmount", pulseAmount);
        material.SetFloat("_PulseSpeed", HintPulseSpeed);
        return material;
    }
}
