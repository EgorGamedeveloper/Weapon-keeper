using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Глобальный Volume настроек игрока поверх постобработки сцены: яркость (Color Adjustments → Post
/// Exposure) и выключатели Bloom / Motion Blur. Объект с компонентом создаёт меню
/// Tools/Weapon Keeper/Setup Settings &amp; Steam в игровой сцене.
///
/// Профиль создаётся в рантайме и принадлежит только этому Volume: ассет профиля в редакторе
/// изменения из Play Mode сохранил бы в файл, а свой ассет в проекте ни к чему.
///
/// Как это работает с профилем сцены: приоритет этого Volume выше базового, поэтому
/// - яркость 0 — компонент Color Adjustments выключен, экспозиция сцены как есть; другая яркость
///   ЗАМЕНЯЕТ Post Exposure сцены (Volume-система не складывает значения, а перекрывает);
/// - Bloom/Motion Blur «вкл» — переопределение выключено и эффект сцены работает как задуман,
///   «выкл» — переопределение с нулевой интенсивностью гасит его. Если эффекта нет в профиле сцены,
///   переключатель ничего не меняет.
/// Постобработка должна быть включена на камере (Rendering → Post Processing).
/// </summary>
[RequireComponent(typeof(Volume))]
[DisallowMultipleComponent]
public class PostProcessSettingsApplier : MonoBehaviour
{
    [Header("Volume")]
    [Tooltip("Приоритет Volume настроек — должен быть выше, чем у Volume сцены, иначе он её не перекроет.")]
    public float priority = 100f;

    private Volume volume;
    private VolumeProfile runtimeProfile;
    private ColorAdjustments colorAdjustments;
    private Bloom bloomOverride;
    private MotionBlur motionBlurOverride;
    private SettingsService service;

    private void Awake()
    {
        volume = GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 1f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "SettingsRuntimeProfile";

        // overrides = false: у компонентов переопределены только нужные параметры, остальные — от сцены.
        colorAdjustments = runtimeProfile.Add<ColorAdjustments>();
        bloomOverride = runtimeProfile.Add<Bloom>();
        bloomOverride.intensity.Override(0f);
        motionBlurOverride = runtimeProfile.Add<MotionBlur>();
        motionBlurOverride.intensity.Override(0f);

        volume.profile = runtimeProfile;
    }

    private void OnEnable()
    {
        service = SettingsService.Instance;
        if (service == null) return;
        service.OnSettingsApplied += Apply;
        Apply(service.Current);
    }

    private void OnDisable()
    {
        if (service != null) service.OnSettingsApplied -= Apply;
        service = null;
    }

    private void OnDestroy()
    {
        // Профиль и его компоненты созданы в рантайме — сами они не выгрузятся.
        if (runtimeProfile == null) return;
        foreach (VolumeComponent component in runtimeProfile.components)
            if (component != null) Destroy(component);
        Destroy(runtimeProfile);
    }

    private void Apply(GameSettingsData settings)
    {
        if (settings == null || runtimeProfile == null) return;

        colorAdjustments.postExposure.Override(settings.brightness);
        colorAdjustments.active = Mathf.Abs(settings.brightness) > 0.001f;
        bloomOverride.active = !settings.bloom;
        motionBlurOverride.active = !settings.motionBlur;
    }
}
