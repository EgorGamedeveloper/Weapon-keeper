using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Пятно крови/грязи — декаль (URP DecalProjector), которое игрок стирает тряпкой: в режиме работы
/// (PlayerToolActions) мышь водит тряпку по пятну, и оно стирается именно там, где тряпка прошла.
/// Намеренно НЕ реализует IPlaceableSlot: там ничего не устанавливается.
///
/// Маска — альфа собственной копии текстуры пятна: при старте текстура копируется в Texture2D пятна,
/// а материал декали получает её вместо исходной (у каждого пятна свой экземпляр). Тряпка уменьшает
/// альфу штампами кисти вдоль пути (ScrubSegment); прогресс — доля стёртой альфы. Когда стёрто
/// cleanThreshold, остаток тает, пятно считается очищенным (OnCleaned).
///
/// Раскладка: объект стоит НА поверхности, декаль проецирует вдоль своей оси +Z (для пола — вниз), её
/// коробка — симметрично вокруг поверхности (pivot по Z = 0). Плоскость пятна — через позицию объекта
/// с нормалью −Z. Триггер-коллайдер нужен только чтобы в пятно можно было прицелиться лучом.
/// Частично стёртое пятно в сейв не пишется: после загрузки оно снова целое.
/// </summary>
[RequireComponent(typeof(DecalProjector))]
[RequireComponent(typeof(BoxCollider))]
public class CleanableStain : MonoBehaviour
{
    [Header("Пятно")]
    [Tooltip("Текстура пятна (форма — в альфе). Пусто — Base_Map материала декали.")]
    public Texture2D stainTexture;

    [Header("Тряпка")]
    [Tooltip("Радиус тряпки, м.")]
    [Min(0.01f)] public float brushRadius = 0.09f;

    [Tooltip("Сколько альфы снимает один штамп кисти в центре тряпки. Штампы идут каждые ¼ радиуса, " +
             "так что один проход снимает примерно половину пятна.")]
    [Range(0.01f, 1f)] public float brushStrength = 0.08f;

    [Tooltip("Какая доля пятна должна быть стёрта, чтобы остаток растаял сам и пятно засчиталось.")]
    [Range(0.5f, 1f)] public float cleanThreshold = 0.9f;

    [Header("Звуки")]
    [Tooltip("Шорох тряпки: раз на scrubSoundDistance пройденного по пятну пути.")]
    public SoundCue scrubSound;

    [Tooltip("Сколько метров тряпки по пятну между звуками шороха.")]
    [Min(0.01f)] public float scrubSoundDistance = 0.06f;

    [Tooltip("Пятно оттёрто.")]
    public SoundCue cleanedSound;

    /// <summary>Пятно уже очищено.</summary>
    public bool IsClean { get; private set; }

    /// <summary>Какая доля пятна стёрта, 0..1.</summary>
    public float Progress { get; private set; }

    /// <summary>Пятно очищено — хук для трекера восстановления.</summary>
    public event Action<CleanableStain> OnCleaned;

    /// <summary>Нормаль поверхности пятна (навстречу игроку).</summary>
    public Vector3 SurfaceNormal => -transform.forward;

    private const string BaseMapProperty = "Base_Map";
    private const float FinalFadeDuration = 0.35f;
    private const byte ClearAlpha = 3;

    private DecalProjector projector;
    private Texture2D maskTexture;
    private Material maskMaterial;
    private Color32[] pixels;
    private double initialAlphaSum;
    private double alphaSum;
    private bool dirty;
    private float scrubDistance;
    private Tween fadeTween;

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        SyncCollider();
    }

#if UNITY_EDITOR
    private void OnValidate() => SyncCollider();
#endif

    /// <summary>Триггер по размеру декали: тонкая пластина вокруг поверхности.</summary>
    private void SyncCollider()
    {
        var box = GetComponent<BoxCollider>();
        var decal = GetComponent<DecalProjector>();
        if (box == null || decal == null) return;
        box.center = new Vector3(decal.pivot.x, decal.pivot.y, 0f);
        box.size = new Vector3(decal.size.x, decal.size.y, 0.1f);
    }

    private void Awake()
    {
        projector = GetComponent<DecalProjector>();
        // Пятно уже восстановлено из сейва очищенным (SaveLoadService работает раньше) — маска не нужна.
        if (!IsClean) BuildMask();
    }

    private void OnDestroy()
    {
        fadeTween?.Kill();
        if (maskTexture != null) Destroy(maskTexture);
        if (maskMaterial != null) Destroy(maskMaterial);
    }

    private void LateUpdate()
    {
        if (!dirty || maskTexture == null) return;
        dirty = false;
        maskTexture.SetPixels32(pixels);
        maskTexture.Apply(false);
    }

    /// <summary>Копия текстуры пятна — её альфа становится маской стирания.</summary>
    private void BuildMask()
    {
        Material source = projector.material;
        Texture2D texture = stainTexture;
        if (texture == null && source != null && source.HasProperty(BaseMapProperty))
            texture = source.GetTexture(BaseMapProperty) as Texture2D;
        if (texture == null || source == null)
        {
            Debug.LogWarning($"CleanableStain '{name}': нет текстуры пятна или материала декали — стирать нечего.", this);
            return;
        }

        maskTexture = CopyReadable(texture);
        maskTexture.wrapMode = TextureWrapMode.Clamp;
        pixels = maskTexture.GetPixels32();
        foreach (Color32 pixel in pixels) initialAlphaSum += pixel.a;
        alphaSum = initialAlphaSum;

        maskMaterial = new Material(source) { name = source.name + " (mask)" };
        maskMaterial.SetTexture(BaseMapProperty, maskTexture);
        projector.material = maskMaterial;
    }

    /// <summary>Читаемая копия текстуры: напрямую, если у неё включён Read/Write, иначе через рендер-текстуру.</summary>
    private static Texture2D CopyReadable(Texture2D texture)
    {
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        if (texture.isReadable)
        {
            copy.SetPixels32(texture.GetPixels32());
            copy.Apply(false);
            return copy;
        }

        RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, temporary);
        RenderTexture.active = temporary;
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        copy.Apply(false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return copy;
    }

    // ───────────────────────── Геометрия пятна ─────────────────────────

    /// <summary>Где луч встречает плоскость пятна; false — мимо прямоугольника декали.</summary>
    public bool Raycast(Ray ray, out Vector3 point)
    {
        point = default;
        var plane = new Plane(SurfaceNormal, transform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        point = ray.GetPoint(distance);
        Vector2 uv = WorldToUV(point);
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    /// <summary>Точка на плоскости пятна, не дальше margin за краем декали.</summary>
    public Vector3 ClampToStain(Vector3 world, float margin)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (world - transform.position) - projector.pivot;
        Vector2 half = new Vector2(projector.size.x, projector.size.y) * 0.5f + Vector2.one * margin;
        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.y = Mathf.Clamp(local.y, -half.y, half.y);
        local.z = 0f;
        return transform.position + transform.rotation * (local + new Vector3(projector.pivot.x, projector.pivot.y, 0f));
    }

    // Декаль масштабо-независима (DecalScaleMode.ScaleInvariant): размер — только projector.size.
    private Vector2 WorldToUV(Vector3 world)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (world - transform.position) - projector.pivot;
        return new Vector2(local.x / projector.size.x + 0.5f, local.y / projector.size.y + 0.5f);
    }

    // ───────────────────────── Стирание ─────────────────────────

    /// <summary>
    /// Тряпка прошла от from до to (мировые точки на плоскости пятна): штампы кисти каждые ¼ радиуса.
    /// Неподвижная тряпка не стирает — пятно нужно именно тереть. Возвращает true, когда пятно очищено.
    /// </summary>
    public bool ScrubSegment(Vector3 from, Vector3 to)
    {
        if (IsClean || pixels == null) return IsClean;

        float distance = Vector3.Distance(from, to);
        if (distance < 0.0005f) return false;

        float step = brushRadius * 0.25f;
        int stamps = Mathf.Max(1, Mathf.CeilToInt(distance / step));
        for (int i = 1; i <= stamps; i++)
            Stamp(WorldToUV(Vector3.Lerp(from, to, (float)i / stamps)));

        scrubDistance += distance;
        if (scrubDistance >= scrubSoundDistance)
        {
            scrubDistance = 0f;
            SoundPlayer.Play(scrubSound, to);
        }

        Progress = initialAlphaSum > 0 ? Mathf.Clamp01((float)(1.0 - alphaSum / initialAlphaSum)) : 1f;
        if (Progress >= cleanThreshold) Clean();
        return IsClean;
    }

    private void Stamp(Vector2 uv)
    {
        int width = maskTexture.width, height = maskTexture.height;
        float radiusX = brushRadius / projector.size.x * width;
        float radiusY = brushRadius / projector.size.y * height;
        float centerX = uv.x * width, centerY = uv.y * height;

        int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radiusX)), maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centerX + radiusX));
        int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radiusY)), maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centerY + radiusY));

        for (int y = minY; y <= maxY; y++)
        {
            float dy = (y + 0.5f - centerY) / radiusY;
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x + 0.5f - centerX) / radiusX;
                float d2 = dx * dx + dy * dy;
                if (d2 >= 1f) continue;

                int index = y * width + x;
                byte alpha = pixels[index].a;
                if (alpha == 0) continue;

                // Мягкий край тряпки: в центре — полная сила, к краю — ноль.
                float falloff = (1f - d2) * (1f - d2);
                float next = alpha * (1f - brushStrength * falloff);
                byte result = next <= ClearAlpha ? (byte)0 : (byte)next;
                if (result == alpha) continue;

                alphaSum -= alpha - result;
                pixels[index].a = result;
                dirty = true;
            }
        }
    }

    /// <summary>Оттереть пятно сразу целиком: остаток тает, звук, событие.</summary>
    public void Clean()
    {
        if (IsClean) return;
        Progress = 1f;
        SoundPlayer.Play(cleanedSound, transform.position);
        ApplyClean(true);
        OnCleaned?.Invoke(this);
    }

    /// <summary>Восстановление из сейва: тот же визуальный итог, что и Clean(), но без события и
    /// звука — иначе при каждой загрузке трекер восстановления и квесты задваивали бы прогресс.</summary>
    public void RestoreClean()
    {
        if (IsClean) return;
        Progress = 1f;
        ApplyClean(false);
    }

    private void ApplyClean(bool animated)
    {
        IsClean = true;

        // Коллайдер выключаем всегда: иначе очищенное пятно продолжает ловить луч игрока
        // и перекрывает то, что за ним (пол, точка ремонта и т.п.).
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (projector == null) projector = GetComponent<DecalProjector>();
        fadeTween?.Kill();
        if (!animated)
        {
            projector.enabled = false;
            return;
        }

        fadeTween = DOTween.To(() => projector.fadeFactor, value => projector.fadeFactor = value, 0f, FinalFadeDuration)
            .OnComplete(() => projector.enabled = false);
    }
}
