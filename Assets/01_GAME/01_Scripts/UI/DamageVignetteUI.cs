using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Обратная связь по здоровью игрока без полоски (как в Call of Duty): чем меньше здоровья, тем гуще
/// красная виньетка по краям экрана, на каждый удар — короткая вспышка; при смерти экран плавно
/// уходит в чёрный.
///
/// Виньетка — URP Vignette на собственном глобальном Volume с профилем, созданным в рантайме (как
/// в PostProcessSettingsApplier). Управляется интенсивность, а не volume.weight: weight смешивал бы
/// с базой и цвет, и при частичном уроне виньетка выходила тёмной и еле заметной. Когда игрок цел,
/// компонент выключен — виньетка из профиля сцены остаётся как есть. Постобработка должна быть
/// включена на камере.
/// </summary>
public class DamageVignetteUI : MonoBehaviour
{
    [Header("Источник")]
    [Tooltip("Здоровье игрока.")]
    public PlayerHealth playerHealth;

    [Header("Виньетка")]
    [Tooltip("Цвет виньетки урона.")]
    public Color vignetteColor = new Color(0.55f, 0f, 0f);

    [Tooltip("Интенсивность виньетки при почти нулевом здоровье.")]
    [Range(0f, 1f)] public float maxIntensity = 0.55f;

    [Tooltip("Мягкость края виньетки.")]
    [Range(0.01f, 1f)] public float smoothness = 0.8f;

    [Tooltip("Добавка к силе эффекта на каждый удар — вспышка, заметная даже при почти полном здоровье.")]
    [Range(0f, 1f)] public float hitPulse = 0.35f;

    [Tooltip("Как быстро гаснет вспышка от удара, долей в секунду.")]
    [Min(0.01f)] public float pulseFadeSpeed = 1.5f;

    [Tooltip("Резкость, с которой сила эффекта догоняет текущее здоровье.")]
    [Min(0.1f)] public float followSharpness = 6f;

    [Tooltip("Приоритет Volume — выше, чем у Volume сцены и настроек (100).")]
    public float priority = 110f;

    [Header("Смерть")]
    [Tooltip("Полноэкранная чёрная картинка поверх HUD (CanvasGroup, alpha 0, без приёма кликов).")]
    public CanvasGroup deathOverlay;

    [Tooltip("За сколько секунд экран уходит в чёрный.")]
    [Min(0f)] public float deathFadeDuration = 2f;

    private Volume volume;
    private VolumeProfile runtimeProfile;
    private Vignette vignette;
    private float pulse;
    private float level;

    private void Awake()
    {
        var volumeObject = new GameObject("DamageVignetteVolume");
        volumeObject.transform.SetParent(transform, false);
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 1f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "DamageVignetteRuntimeProfile";
        vignette = runtimeProfile.Add<Vignette>();
        vignette.color.Override(vignetteColor);
        vignette.intensity.Override(0f);
        vignette.smoothness.Override(smoothness);
        vignette.active = false;
        volume.profile = runtimeProfile;

        if (deathOverlay != null)
        {
            deathOverlay.alpha = 0f;
            deathOverlay.blocksRaycasts = false;
            deathOverlay.interactable = false;
        }
    }

    private void OnEnable()
    {
        if (playerHealth == null) return;
        playerHealth.OnDamaged += HandleDamaged;
        playerHealth.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (playerHealth == null) return;
        playerHealth.OnDamaged -= HandleDamaged;
        playerHealth.OnDied -= HandleDied;
    }

    private void OnDestroy()
    {
        if (deathOverlay != null) deathOverlay.DOKill();
        // Профиль и его компоненты созданы в рантайме — сами они не выгрузятся.
        if (runtimeProfile == null) return;
        foreach (VolumeComponent component in runtimeProfile.components)
            if (component != null) Destroy(component);
        Destroy(runtimeProfile);
    }

    private void Update()
    {
        if (playerHealth == null || vignette == null) return;

        pulse = Mathf.MoveTowards(pulse, 0f, pulseFadeSpeed * Time.deltaTime);
        float target = Mathf.Clamp01(1f - playerHealth.Normalized + pulse);
        level = Mathf.Lerp(level, target, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

        vignette.active = level > 0.005f;
        vignette.intensity.value = maxIntensity * level;
    }

    private void HandleDamaged(float amount)
    {
        pulse = Mathf.Clamp01(pulse + hitPulse);
    }

    private void HandleDied()
    {
        if (deathOverlay == null) return;
        deathOverlay.DOKill();
        deathOverlay.blocksRaycasts = true;
        deathOverlay.DOFade(1f, deathFadeDuration).SetUpdate(true);
    }
}
