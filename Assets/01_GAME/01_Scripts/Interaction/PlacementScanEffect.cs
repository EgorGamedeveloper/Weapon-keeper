using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Эффект "предмет установлен": по уже стоящему предмету снизу вверх проходит светящаяся полоса
/// (шейдер Weapon Keeper/Item Hologram, режим Scan Band) и гаснет. Запускается из
/// WorldItem.PlaceOnShelfAnimated только по приземлению — пока предмет летит из руки, он выглядит как
/// обычно.
///
/// Полоса рисуется отдельным проходом через Graphics.RenderMesh по ВСЕМ сабмешам исходных мешей
/// предмета, каждый кадр, пока идёт твин. Массивы материалов рендереров не меняются вообще, поэтому
/// эффекту нечего делить с обводкой наведения (AdvancedOutline): наведение прицела на только что
/// поставленный предмет больше не обрывает эффект, а многосабмешевые модели покрываются целиком.
/// </summary>
public static class PlacementScanEffect
{
    private const string HologramShaderName = "Weapon Keeper/Item Hologram";

    // Доли от высоты предмета: так полоса одинаково читается и на ящике патронов, и на винтовке.
    private const float BandHalfWidthFraction = 0.05f;
    private const float MinBandHalfWidth = 0.01f;
    private const float TrailLengthFraction = 0.1f;
    private const float FadeOutDuration = 0.2f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ScanHeightId = Shader.PropertyToID("_ScanHeight");
    private static readonly int BandWidthId = Shader.PropertyToID("_BandWidth");
    private static readonly int TrailLengthId = Shader.PropertyToID("_TrailLength");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private static Material scanMaterialCache;
    private static bool scanMaterialLookedUp;

    /// <summary>
    /// Запускает полосу по предмету. filters/meshes — параллельные массивы: MeshFilter'ы предмета и их
    /// ИСХОДНЫЕ меши (на время наведения AdvancedOutline подменяет sharedMesh своим клоном с
    /// дополнительным сабмешем — рисовать по нему значило бы засветить предмет дважды).
    /// Возвращает твин (его нужно убить, если предмет забрали раньше) или null, если рисовать нечего.
    /// </summary>
    public static Tween Play(Renderer[] renderers, MeshFilter[] filters, Mesh[] meshes, int layer, Color color, float duration)
    {
        Material material = GetScanMaterial();
        if (material == null || filters == null || filters.Length == 0) return null;
        if (!TryGetWorldBounds(renderers, out Bounds bounds)) return null;

        float height = Mathf.Max(bounds.size.y, 0.01f);
        float bandHalfWidth = Mathf.Max(MinBandHalfWidth, height * BandHalfWidthFraction);
        float from = bounds.min.y - bandHalfWidth;
        float to = bounds.max.y + bandHalfWidth;

        var block = new MaterialPropertyBlock();
        block.SetColor(ColorId, color);
        block.SetFloat(BandWidthId, bandHalfWidth);
        block.SetFloat(TrailLengthId, height * TrailLengthFraction);

        var renderParams = new RenderParams(material)
        {
            layer = layer,
            matProps = block,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };

        float scanHeight = from;
        float intensity = 1f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(DOTween.To(() => scanHeight, v => scanHeight = v, to, Mathf.Max(0.05f, duration)).SetEase(Ease.InOutSine));
        sequence.Append(DOTween.To(() => intensity, v => intensity = v, 0f, FadeOutDuration));
        sequence.OnUpdate(() =>
        {
            block.SetFloat(ScanHeightId, scanHeight);
            block.SetFloat(IntensityId, intensity);
            Draw(renderParams, filters, meshes);
        });
        return sequence;
    }

    /// <summary>Полоса по произвольному объекту без WorldItem (например, кирпич, уложенный в кладку,
    /// см. RepairSlot). Меши берутся как есть: обводку наведения такие объекты не получают.</summary>
    public static Tween Play(GameObject target, Color color, float duration)
    {
        if (target == null) return null;

        MeshFilter[] filters = target.GetComponentsInChildren<MeshFilter>();
        var meshes = new Mesh[filters.Length];
        for (int i = 0; i < filters.Length; i++) meshes[i] = filters[i].sharedMesh;

        return Play(target.GetComponentsInChildren<Renderer>(), filters, meshes, target.layer, color, duration);
    }

    private static void Draw(RenderParams renderParams, MeshFilter[] filters, Mesh[] meshes)
    {
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            Mesh mesh = meshes[i];
            if (filter == null || mesh == null || !filter.gameObject.activeInHierarchy) continue;

            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                Graphics.RenderMesh(renderParams, mesh, sub, matrix);
        }
    }

    private static bool TryGetWorldBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (renderers == null) return false;

        foreach (var r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    private static Material GetScanMaterial()
    {
        if (scanMaterialLookedUp) return scanMaterialCache;
        scanMaterialLookedUp = true;

        Shader shader = Shader.Find(HologramShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"PlacementScanEffect: шейдер '{HologramShaderName}' не найден (проверьте Always Included Shaders) — эффект установки отключён.");
            return null;
        }

        scanMaterialCache = new Material(shader) { name = "Placement Scan (runtime)" };
        scanMaterialCache.SetFloat("_FillIntensity", 0f);
        scanMaterialCache.SetFloat("_RimIntensity", 0f);
        scanMaterialCache.SetFloat("_ScanIntensity", 1f);
        return scanMaterialCache;
    }
}
