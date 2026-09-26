using UnityEngine;

/// <summary>
/// Значения настроек по умолчанию и диапазоны ползунков. Ассет — 04_Data/Settings/SettingsDefaults
/// (создаёт меню Tools/Weapon Keeper/Setup Settings &amp; Steam). Без ассета SettingsService работает
/// на значениях, заданных прямо в коде полей.
///
/// Разрешение и язык по умолчанию здесь не задаются: при первом запуске это нативное разрешение
/// монитора и язык Steam/системы (см. SettingsService.CreateDefaults).
/// </summary>
[CreateAssetMenu(fileName = "SettingsDefaults", menuName = "Game/Settings Defaults", order = 10)]
public class SettingsDefaults : ScriptableObject
{
    [Header("Значения по умолчанию")]
    [Tooltip("Настройки нового игрока и кнопки «По умолчанию». Разрешение и язык подставляются автоматически.")]
    public GameSettingsData defaults = new GameSettingsData();

    [Header("Диапазоны ползунков")]
    [Tooltip("Поле зрения, градусы: минимум (x) и максимум (y).")]
    public Vector2 fieldOfViewRange = new Vector2(60f, 110f);

    [Tooltip("Чувствительность мыши (множитель): минимум и максимум.")]
    public Vector2 sensitivityRange = new Vector2(0.1f, 5f);

    [Tooltip("Максимальное сглаживание мыши, с.")]
    [Min(0f)] public float maxMouseSmoothing = 0.3f;

    [Tooltip("Масштаб рендеринга: минимум и максимум.")]
    public Vector2 renderScaleRange = new Vector2(0.5f, 1f);

    [Tooltip("Дальность теней, м: минимум и максимум.")]
    public Vector2 shadowDistanceRange = new Vector2(10f, 150f);

    [Tooltip("Яркость (добавка к экспозиции, EV): минимум и максимум.")]
    public Vector2 brightnessRange = new Vector2(-1f, 1f);

    [Header("Варианты выбора")]
    [Tooltip("Лимиты FPS в списке. 0 — без ограничения.")]
    public int[] frameRateOptions = { 30, 60, 120, 144, 0 };

    [Tooltip("Варианты MSAA — число выборок (1 — выключено).")]
    public int[] msaaOptions = { 1, 2, 4, 8 };

    /// <summary>Привести значения к допустимым диапазонам (битый или устаревший файл настроек,
    /// значения из другой версии игры).</summary>
    public void Clamp(GameSettingsData data)
    {
        data.frameRateLimit = Mathf.Max(0, data.frameRateLimit);
        data.renderScale = Mathf.Clamp(data.renderScale, renderScaleRange.x, renderScaleRange.y);
        data.antialiasingQuality = Mathf.Clamp(data.antialiasingQuality, 0, 2);
        data.msaaSamples = System.Array.IndexOf(msaaOptions, data.msaaSamples) >= 0 ? data.msaaSamples : 1;
        data.shadowDistance = Mathf.Clamp(data.shadowDistance, shadowDistanceRange.x, shadowDistanceRange.y);
        data.fieldOfView = Mathf.Clamp(data.fieldOfView, fieldOfViewRange.x, fieldOfViewRange.y);
        data.brightness = Mathf.Clamp(data.brightness, brightnessRange.x, brightnessRange.y);
        data.mouseSensitivity = Mathf.Clamp(data.mouseSensitivity, sensitivityRange.x, sensitivityRange.y);
        data.mouseSensitivityY = Mathf.Clamp(data.mouseSensitivityY, sensitivityRange.x, sensitivityRange.y);
        data.mouseSmoothing = Mathf.Clamp(data.mouseSmoothing, 0f, maxMouseSmoothing);
        data.masterVolume = Mathf.Clamp01(data.masterVolume);
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        data.uiVolume = Mathf.Clamp01(data.uiVolume);
        data.ambienceVolume = Mathf.Clamp01(data.ambienceVolume);

        if (data.windowMode != FullScreenMode.ExclusiveFullScreen && data.windowMode != FullScreenMode.FullScreenWindow
            && data.windowMode != FullScreenMode.Windowed)
            data.windowMode = FullScreenMode.FullScreenWindow;

        if (!System.Enum.IsDefined(typeof(PostAntialiasingMode), data.postAntialiasing))
            data.postAntialiasing = PostAntialiasingMode.SMAA;

        data.qualityLevel = Mathf.Clamp(data.qualityLevel, -1, QualitySettings.names.Length - 1);
    }
}
