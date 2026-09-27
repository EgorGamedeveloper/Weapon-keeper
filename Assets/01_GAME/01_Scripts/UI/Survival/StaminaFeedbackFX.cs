using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Усталость «на экране», без цифр:
/// - одышка — тёмная виньетка по краям в ритме дыхания (и звук дыхания, если задан);
/// - чем выше действующая усталость, тем бледнее картинка (насыщенность вниз);
/// - вымотан — время от времени веки «слипаются» (короткое моргание чёрным).
///
/// Постобработка — свой глобальный Volume с профилем, созданным в рантайме (как у DamageVignetteUI), но с
/// приоритетом ниже: красная виньетка урона остаётся главной. Параметры, которые сейчас не нужны,
/// выключаются — тогда действуют настройки сцены и игрока (PostProcessSettingsApplier).
/// </summary>
public class StaminaFeedbackFX : MonoBehaviour
{
    [Header("Источник")]
    [Tooltip("Выносливость игрока.")]
    public PlayerStamina stamina;

    [Header("Одышка")]
    [Tooltip("Сила виньетки при одышке.")]
    [Range(0f, 1f)] public float windedVignette = 0.4f;

    [Tooltip("Цвет виньетки одышки.")]
    public Color vignetteColor = Color.black;

    [Tooltip("Частота «дыхания» виньетки, вдохов в секунду.")]
    [Min(0.1f)] public float breathRate = 0.9f;

    [Tooltip("Звук тяжёлого дыхания, играет раз в вдох, пока одышка. Не задан — тихо.")]
    public SoundCue breathSound;

    [Header("Усталость")]
    [Tooltip("С какой действующей усталости картинка начинает бледнеть.")]
    [Min(0f)] public float desaturateFrom = 40f;

    [Tooltip("Насыщенность, когда игрок вымотан (ColorAdjustments.saturation, −100…0).")]
    [Range(-100f, 0f)] public float exhaustedSaturation = -35f;

    [Header("Вымотан")]
    [Tooltip("Чёрный слой поверх HUD для моргания (CanvasGroup, alpha 0, без приёма кликов). Пусто — без моргания.")]
    public CanvasGroup blinkOverlay;

    [Tooltip("Пауза между морганиями, с (случайно в этих пределах).")]
    public Vector2 blinkInterval = new Vector2(6f, 11f);

    [Tooltip("Длительность одного моргания, с.")]
    [Min(0.05f)] public float blinkDuration = 0.4f;

    [Header("Volume")]
    [Tooltip("Приоритет Volume — выше, чем у сцены и настроек (100), ниже, чем у виньетки урона (110).")]
    public float priority = 105f;

    [Tooltip("Резкость, с которой эффекты догоняют состояние игрока.")]
    [Min(0.1f)] public float followSharpness = 4f;

    private Volume volume;
    private VolumeProfile runtimeProfile;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private float windedLevel;
    private float fatigueLevel;
    private float nextBlinkTime;
    private float nextBreathTime;

    private void Awake()
    {
        var volumeObject = new GameObject("StaminaFeedbackVolume");
        volumeObject.transform.SetParent(transform, false);
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 1f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "StaminaFeedbackRuntimeProfile";

        vignette = runtimeProfile.Add<Vignette>();
        vignette.color.Override(vignetteColor);
        vignette.intensity.Override(0f);
        vignette.smoothness.Override(0.9f);
        vignette.active = false;

        colorAdjustments = runtimeProfile.Add<ColorAdjustments>();
        colorAdjustments.saturation.Override(0f);
        colorAdjustments.active = false;

        volume.profile = runtimeProfile;

        if (blinkOverlay != null)
        {
            blinkOverlay.alpha = 0f;
            blinkOverlay.blocksRaycasts = false;
            blinkOverlay.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (blinkOverlay != null) blinkOverlay.DOKill();
        if (runtimeProfile == null) return;
        foreach (VolumeComponent component in runtimeProfile.components)
            if (component != null) Destroy(component);
        Destroy(runtimeProfile);
    }

    private void Update()
    {
        if (stamina == null) return;

        float deltaTime = Time.deltaTime;
        float k = 1f - Mathf.Exp(-followSharpness * deltaTime);

        windedLevel = Mathf.Lerp(windedLevel, stamina.IsWinded ? 1f : 0f, k);
        float breath = 0.75f + 0.25f * Mathf.Sin(Time.time * breathRate * Mathf.PI * 2f);
        float vignetteIntensity = windedVignette * windedLevel * breath;
        vignette.active = vignetteIntensity > 0.005f;
        vignette.intensity.value = vignetteIntensity;

        float target = Mathf.InverseLerp(desaturateFrom, stamina.Settings.exhaustedThreshold, stamina.EffectiveFatigue);
        fatigueLevel = Mathf.Lerp(fatigueLevel, target, k);
        colorAdjustments.active = fatigueLevel > 0.01f;
        colorAdjustments.saturation.value = exhaustedSaturation * fatigueLevel;

        UpdateBreathSound();
        UpdateBlink();
    }

    private void UpdateBreathSound()
    {
        if (breathSound == null || !stamina.IsWinded || Time.timeScale <= 0f) return;
        if (Time.time < nextBreathTime) return;
        nextBreathTime = Time.time + 1f / breathRate;
        SoundPlayer.Play2D(breathSound);
    }

    private void UpdateBlink()
    {
        if (blinkOverlay == null) return;

        if (!stamina.IsExhausted || Time.timeScale <= 0f)
        {
            nextBlinkTime = Time.time + Random.Range(blinkInterval.x, blinkInterval.y) * 0.5f;
            return;
        }
        if (Time.time < nextBlinkTime) return;

        nextBlinkTime = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
        blinkOverlay.DOKill();
        DOTween.Sequence()
            .Append(blinkOverlay.DOFade(0.92f, blinkDuration * 0.4f).SetEase(Ease.InQuad))
            .Append(blinkOverlay.DOFade(0f, blinkDuration * 0.6f).SetEase(Ease.OutQuad))
            .SetTarget(blinkOverlay);
    }
}
