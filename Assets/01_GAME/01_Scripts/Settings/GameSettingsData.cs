using System;
using UnityEngine;

/// <summary>Сглаживание постобработкой — тип AA на камере URP.</summary>
public enum PostAntialiasingMode
{
    Off = 0,
    FXAA = 1,
    SMAA = 2,
    TAA = 3
}

/// <summary>
/// Канал громкости. AudioMixer в проекте нет (а .mixer из кода не создать), поэтому громкость канала
/// умножается на базовую громкость источника компонентом AudioChannelVolume.
/// </summary>
public enum AudioChannel
{
    SFX = 0,
    Music = 1,
    UI = 2,
    Ambience = 3
}

/// <summary>
/// Все настройки игрока одним сериализуемым классом. Хранит их SettingsService в settings.cfg (JSON),
/// значения по умолчанию лежат в SettingsDefaults. Поля, которых не было в старом файле, при чтении
/// получают значения по умолчанию (JsonUtility.FromJsonOverwrite поверх дефолтов), поэтому новая
/// настройка — это просто новое поле; version нужен только на случай переименований.
/// </summary>
[Serializable]
public class GameSettingsData
{
    public const int CurrentVersion = 1;

    [Tooltip("Версия схемы файла настроек.")]
    public int version = CurrentVersion;

    [Header("Экран")]
    [Tooltip("Ширина разрешения, пиксели. 0 — нативное разрешение монитора.")]
    public int resolutionWidth;

    [Tooltip("Высота разрешения, пиксели. 0 — нативное разрешение монитора.")]
    public int resolutionHeight;

    [Tooltip("Режим окна: эксклюзивный полноэкранный, окно без рамки на весь экран или обычное окно.")]
    public FullScreenMode windowMode = FullScreenMode.FullScreenWindow;

    [Tooltip("Вертикальная синхронизация. Пока включена, лимит FPS не действует.")]
    public bool vSync = true;

    [Tooltip("Лимит кадров в секунду без вертикальной синхронизации. 0 — без ограничения.")]
    [Min(0)] public int frameRateLimit = 0;

    [Header("Качество")]
    [Tooltip("Пресет качества (Project Settings → Quality), индекс. -1 — пресет проекта по умолчанию.")]
    public int qualityLevel = -1;

    [Tooltip("Масштаб рендеринга URP: 1 — полное разрешение, 0.5 — вдвое меньше по каждой оси.")]
    [Range(0.5f, 1f)] public float renderScale = 1f;

    [Tooltip("Сглаживание постобработкой на камере.")]
    public PostAntialiasingMode postAntialiasing = PostAntialiasingMode.SMAA;

    [Tooltip("Качество SMAA/TAA: 0 — низкое, 1 — среднее, 2 — высокое.")]
    [Range(0, 2)] public int antialiasingQuality = 2;

    [Tooltip("MSAA — число выборок: 1 (выключено), 2, 4 или 8. Вместе с TAA не включается.")]
    public int msaaSamples = 1;

    [Tooltip("Тени от источников света.")]
    public bool shadows = true;

    [Tooltip("Дальность прорисовки теней, м.")]
    public float shadowDistance = 50f;

    [Header("Изображение")]
    [Tooltip("Поле зрения камеры игрока по вертикали, градусы.")]
    public float fieldOfView = 60f;

    [Tooltip("Яркость — добавка к экспозиции (EV). 0 — как задумано в сцене.")]
    public float brightness = 0f;

    [Tooltip("Свечение ярких областей (Bloom) — действует, если оно есть в профиле постобработки сцены.")]
    public bool bloom = true;

    [Tooltip("Размытие в движении — действует, если оно есть в профиле постобработки сцены.")]
    public bool motionBlur = true;

    [Header("Управление")]
    [Tooltip("Чувствительность мыши — множитель к скорости поворота камеры из инспектора MouseRotator.")]
    public float mouseSensitivity = 1f;

    [Tooltip("Задавать чувствительность по вертикали отдельно.")]
    public bool separateVerticalSensitivity = false;

    [Tooltip("Чувствительность по вертикали (если включена отдельная).")]
    public float mouseSensitivityY = 1f;

    [Tooltip("Инвертировать вертикальную ось мыши.")]
    public bool invertY = false;

    [Tooltip("Сглаживание мыши, с. 0 — камера следует за мышью без задержки.")]
    public float mouseSmoothing = 0.05f;

    [Tooltip("Покачивание камеры при ходьбе и просадка при приземлении.")]
    public bool headBob = true;

    [Header("Звук")]
    [Tooltip("Общая громкость (0..1).")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Tooltip("Громкость музыки (0..1).")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    [Tooltip("Громкость эффектов: выстрелы, шаги, предметы (0..1).")]
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Tooltip("Громкость звуков интерфейса (0..1).")]
    [Range(0f, 1f)] public float uiVolume = 1f;

    [Tooltip("Громкость окружения (0..1).")]
    [Range(0f, 1f)] public float ambienceVolume = 1f;

    [Tooltip("Звук и игра продолжаются, когда окно игры не в фокусе.")]
    public bool audioInBackground = false;

    [Header("Игра")]
    [Tooltip("Язык интерфейса — код Steam (russian, english). Пусто — определить при первом запуске.")]
    public string language = "";

    [Tooltip("Показывать подсказки. Читается подписчиками SettingsService.OnSettingsApplied.")]
    public bool showHints = true;

    [Tooltip("Субтитры — резерв на будущее: озвучки в игре пока нет, в окне настроек пункта нет.")]
    public bool subtitles = false;

    /// <summary>Копия (все поля — значения и строка, поверхностной копии достаточно).</summary>
    public GameSettingsData Clone() => (GameSettingsData)MemberwiseClone();

    /// <summary>Совпадают ли все значения.</summary>
    public bool ContentEquals(GameSettingsData other) =>
        other != null && JsonUtility.ToJson(this) == JsonUtility.ToJson(other);

    /// <summary>Совпадают ли настройки экрана — их смена требует подтверждения с откатом по таймеру.</summary>
    public bool DisplayEquals(GameSettingsData other) =>
        other != null && resolutionWidth == other.resolutionWidth && resolutionHeight == other.resolutionHeight
        && windowMode == other.windowMode;

    /// <summary>Скопировать настройки экрана из другого набора (откат после неподтверждённой смены).</summary>
    public void CopyDisplayFrom(GameSettingsData other)
    {
        resolutionWidth = other.resolutionWidth;
        resolutionHeight = other.resolutionHeight;
        windowMode = other.windowMode;
    }

    /// <summary>Громкость канала (без учёта общей — её применяет AudioListener.volume).</summary>
    public float GetChannelVolume(AudioChannel channel)
    {
        switch (channel)
        {
            case AudioChannel.Music: return musicVolume;
            case AudioChannel.UI: return uiVolume;
            case AudioChannel.Ambience: return ambienceVolume;
            default: return sfxVolume;
        }
    }

    /// <summary>MSAA, который реально включается: с TAA — всегда выключен.</summary>
    public int EffectiveMsaaSamples => postAntialiasing == PostAntialiasingMode.TAA ? 1 : Mathf.Max(1, msaaSamples);

    /// <summary>Чувствительность по вертикали с учётом переключателя «отдельно».</summary>
    public float EffectiveVerticalSensitivity => separateVerticalSensitivity ? mouseSensitivityY : mouseSensitivity;
}
