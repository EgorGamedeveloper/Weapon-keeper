using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Налобный фонарик — постоянный предмет игрока вне инвентарей. Сначала его подбирают
/// (ItemData.toolKind == Flashlight): PlayerItemInteraction зовёт Acquire, сам предмет исчезает, на HUD
/// появляется значок со шкалой заряда (UI/FlashlightHudUI). Дальше клавиша GameConfig.input.flashlightKey (3)
/// включает и выключает его со щелчком — независимо от инструмента в руках.
/// Батарея: пока свет включён, заряд тратится (batteryMinutes минут работы на полный заряд). Села — свет
/// гаснет, включить нельзя, пока заряд не пополнят (Recharge — под будущие батарейки).
/// Свет — Spot-источник рядом с камерой: он поворачивается за взглядом с лёгкой инерцией, а не приклеен к
/// экрану, — при резком повороте пятно света чуть догоняет взгляд. Сохраняется факт владения и заряд
/// (SaveGameData.flashlightOwned/flashlightCharge); включённость — нет: после загрузки фонарик выключен.
///
/// Работает в LateUpdate после покачивания камеры (DefaultExecutionOrder), чтобы свет не отставал на кадр.
/// </summary>
[DefaultExecutionOrder(100)]
public class PlayerFlashlight : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера игрока: свет смотрит туда же, куда она.")]
    public Camera playerCamera;

    [Tooltip("Spot-источник фонарика. Его яркость в инспекторе — яркость включённого фонарика. Не светит на " +
             "слой «в руках» (WorldItem.HeldLayer): вблизи он пересвечивал бы предметы в руках.")]
    public Light spotLight;

    [Tooltip("Слабый «ручной» свет: светит только на слой «в руках» (инструменты, предмет, оружие), включается и " +
             "гаснет вместе с фонариком. Его яркость в инспекторе — яркость на руках. Пусто — руки не освещаются.")]
    public Light handLight;


    [Header("Конфиг")]
    [Tooltip("Если задан — клавиша берётся из GameConfig.input.flashlightKey при старте.")]
    public GameConfig config;

    [Header("Управление")]
    [Tooltip("Клавиша фонарика. Перекрывается GameConfig.input.flashlightKey, если задан конфиг.")]
    public KeyCode toggleKey = KeyCode.G;

    [Tooltip("Подобран ли фонарик при старте сцены (для отладки; обычно выключено — его находят в мире).")]
    public bool startOwned;

    [Header("Батарея")]
    [Tooltip("Сколько минут светит полный заряд.")]
    [Min(0.1f)] public float batteryMinutes = 10f;

    [Tooltip("Заряд, ниже которого шкала на HUD краснеет (доля от полного).")]
    [Range(0f, 1f)] public float lowChargeFraction = 0.2f;

    [Header("Свет")]
    [Tooltip("Смещение света от камеры в её осях, м: чуть правее и ниже — как фонарик на голове.")]
    public Vector3 offset = new Vector3(0.12f, -0.08f, 0.05f);

    [Tooltip("Как быстро свет догоняет взгляд: больше — жёстче, 0 — без инерции.")]
    [Min(0f)] public float followSharpness = 16f;

    [Tooltip("За сколько секунд свет разгорается и гаснет.")]
    [Min(0f)] public float fadeDuration = 0.07f;

    [Header("Звуки")]
    [Tooltip("Щелчок включения.")]
    public SoundCue switchOnSound;

    [Tooltip("Щелчок выключения.")]
    public SoundCue switchOffSound;

    [Tooltip("Попытка включить фонарик с севшей батареей.")]
    public SoundCue emptySound;

    /// <summary>Фонарик включён.</summary>
    public bool IsOn { get; private set; }

    /// <summary>Фонарик подобран. Пока нет — клавиша ничего не делает, а значка на HUD нет.</summary>
    public bool IsOwned { get; private set; }

    /// <summary>Остаток заряда, секунды работы.</summary>
    public float ChargeSeconds { get; private set; }

    /// <summary>Полный заряд, секунды работы.</summary>
    public float MaxChargeSeconds => batteryMinutes * 60f;

    /// <summary>Заряд от 0 до 1.</summary>
    public float Charge01 => MaxChargeSeconds > 0f ? Mathf.Clamp01(ChargeSeconds / MaxChargeSeconds) : 0f;

    /// <summary>Заряд ниже порога «садится».</summary>
    public bool IsLowCharge => Charge01 <= lowChargeFraction;

    /// <summary>Фонарик подобран (HUD показывает значок).</summary>
    public event Action OnAcquired;

    private float onIntensity;
    private float handOnIntensity;
    private float brightness;
    private Tween fadeTween;

    private void Awake()
    {
        if (config != null) toggleKey = config.input.flashlightKey;
        if (startOwned) { IsOwned = true; ChargeSeconds = MaxChargeSeconds; }
        if (spotLight == null) return;

        onIntensity = spotLight.intensity;
        if (handLight != null) handOnIntensity = handLight.intensity;
        IsOn = false;
        ApplyBrightness(0f);
    }

    private void OnDisable()
    {
        fadeTween?.Kill();
    }

    private void Update()
    {
        if (!IsOwned) return;

        // Курсор свободен — открыто окно или пауза: клавиши игрока не работают.
        if (Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(toggleKey)) Toggle();

        if (!IsOn) return;
        ChargeSeconds = Mathf.Max(0f, ChargeSeconds - Time.deltaTime);
        if (ChargeSeconds <= 0f) SetOn(false);
    }

    /// <summary>Подобрать фонарик: полный заряд. Повторный подбор заряд не трогает.</summary>
    public void Acquire()
    {
        if (IsOwned) return;
        IsOwned = true;
        ChargeSeconds = MaxChargeSeconds;
        OnAcquired?.Invoke();
    }

    /// <summary>Пополнить батарею на seconds секунд работы (не больше полного заряда).</summary>
    public void Recharge(float seconds)
    {
        ChargeSeconds = Mathf.Clamp(ChargeSeconds + seconds, 0f, MaxChargeSeconds);
    }

    /// <summary>Восстановление из сейва: без событий и звуков, фонарик выключен.</summary>
    public void RestoreState(bool owned, float chargeSeconds)
    {
        IsOwned = owned || startOwned;
        ChargeSeconds = owned ? Mathf.Clamp(chargeSeconds, 0f, MaxChargeSeconds) : (startOwned ? MaxChargeSeconds : 0f);
    }

    private void LateUpdate()
    {
        if (spotLight == null || playerCamera == null || !spotLight.enabled) return;

        Transform cam = playerCamera.transform;
        Transform lightTransform = spotLight.transform;
        lightTransform.position = cam.TransformPoint(offset);
        lightTransform.rotation = followSharpness > 0f
            ? Quaternion.Slerp(lightTransform.rotation, cam.rotation, 1f - Mathf.Exp(-followSharpness * Time.deltaTime))
            : cam.rotation;
    }

    /// <summary>Переключить фонарик.</summary>
    public void Toggle() => SetOn(!IsOn);

    /// <summary>Включить или выключить фонарик (со щелчком и коротким разгоранием/угасанием).</summary>
    public void SetOn(bool on)
    {
        if (spotLight == null || IsOn == on) return;
        if (on && !IsOwned) return;
        if (on && ChargeSeconds <= 0f)
        {
            SoundPlayer.Play2D(emptySound != null ? emptySound : switchOffSound);
            return;
        }
        IsOn = on;
        SoundPlayer.Play2D(on ? switchOnSound : switchOffSound);

        fadeTween?.Kill();
        // Свет включается сразу туда, куда смотрит игрок, — без «доворота» от старой позы.
        if (on && playerCamera != null)
            spotLight.transform.SetPositionAndRotation(playerCamera.transform.TransformPoint(offset), playerCamera.transform.rotation);

        // Оба источника (основной и «ручной») разгораются и гаснут вместе — одна доля яркости 0..1.
        fadeTween = DOTween.To(() => brightness, ApplyBrightness, on ? 1f : 0f, fadeDuration);
    }

    /// <summary>Доля яркости 0..1 для обоих источников; при 0 они выключены.</summary>
    private void ApplyBrightness(float value)
    {
        brightness = value;
        bool lit = value > 0f;
        spotLight.enabled = lit;
        spotLight.intensity = onIntensity * value;
        if (handLight == null) return;
        handLight.enabled = lit;
        handLight.intensity = handOnIntensity * value;
    }
}
