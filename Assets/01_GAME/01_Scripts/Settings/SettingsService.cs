using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Настройки игрока: чтение/запись settings.cfg, значения по умолчанию, применение ко всей игре и
/// событие OnSettingsApplied для компонентов, которые применяют настройки сами (AudioChannelVolume,
/// PostProcessSettingsApplier, будущие подписчики showHints).
///
/// Файл — Application.persistentDataPath/settings.cfg (JSON). Расширение намеренно не .sav: Steam
/// Auto-Cloud синхронизирует *.sav (см. SaveFileService), а разрешение экрана и качество графики
/// не должны переезжать между разными ПК. Битый или отсутствующий файл — значения по умолчанию,
/// без исключений.
///
/// Первый запуск: язык — язык Steam, иначе системы, иначе английский (LocalizationService); разрешение —
/// нативное; пресет качества — тот, что задан в проекте.
///
/// Статический Instance — осознанное исключение из правила «без синглтонов» (как у LocalizationService):
/// сервис живёт в PersistentServices через все сцены, а AudioChannelVolume висит на каждом источнике звука
/// любой сцены — искать сервис через FindFirstObjectByType из каждого было бы расточительно, а защита от
/// второго экземпляра всё равно требует статического поля.
/// </summary>
[DefaultExecutionOrder(-1800)]
[DisallowMultipleComponent]
public class SettingsService : MonoBehaviour
{
    public const string FileName = "settings.cfg";

    /// <summary>Живой экземпляр сервиса (null, если его нет в игре).</summary>
    public static SettingsService Instance { get; private set; }

    /// <summary>Полный путь к файлу настроек.</summary>
    public static string SettingsPath => Path.Combine(Application.persistentDataPath, FileName);

    [Header("Данные")]
    [Tooltip("Значения по умолчанию и диапазоны ползунков (04_Data/Settings/SettingsDefaults). " +
             "Пусто — значения, заданные в коде.")]
    public SettingsDefaults defaults;

    [Header("Звук")]
    [Tooltip("Дважды в секунду помечать каналом SFX источники звука без AudioChannelVolume. Оружие Easy Weapons " +
             "и AudioSource.PlayClipAtPoint создают AudioSource в рантайме — без этого громкость эффектов их не касалась бы.")]
    public bool autoBindAudioSources = true;

    /// <summary>Применённые сейчас настройки. Не меняйте поля напрямую — правьте копию и передавайте в Apply.</summary>
    public GameSettingsData Current { get; private set; }

    /// <summary>Значения по умолчанию и диапазоны (из ассета или встроенные).</summary>
    public SettingsDefaults Defaults
    {
        get
        {
            if (defaults != null) return defaults;
            if (builtInDefaults == null) builtInDefaults = ScriptableObject.CreateInstance<SettingsDefaults>();
            return builtInDefaults;
        }
    }

    /// <summary>Пресет качества, заданный в проекте (до применения настроек игрока).</summary>
    public int ProjectDefaultQualityLevel { get; private set; }

    /// <summary>Настройки применены (при старте, по кнопке «Применить», при сбросе). Передаёт Current.</summary>
    public event Action<GameSettingsData> OnSettingsApplied;

    private const float AudioBindInterval = 0.5f;

    private SettingsDefaults builtInDefaults;
    private bool hasFocus = true;
    private float nextAudioBindTime;

    // Без перезагрузки домена (Enter Play Mode Options) статика пережила бы прошлый запуск.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ProjectDefaultQualityLevel = QualitySettings.GetQualityLevel();

        bool firstRun = !File.Exists(SettingsPath);
        Current = Load();
        Apply();

        // Первый запуск: фиксируем определённые язык и разрешение, чтобы следующий старт их не угадывал заново.
        if (firstRun) Save();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (builtInDefaults != null) Destroy(builtInDefaults);
    }

    private void Update()
    {
        if (!autoBindAudioSources || Time.unscaledTime < nextAudioBindTime) return;
        nextAudioBindTime = Time.unscaledTime + AudioBindInterval;
        AudioChannelVolume.BindUnassigned();
    }

    private void OnApplicationFocus(bool focus)
    {
        hasFocus = focus;
        if (Current != null) ApplyMasterVolume();
    }

    // ───────────────────────── Применение ─────────────────────────

    /// <summary>Применить копию переданных настроек (значения приводятся к допустимым диапазонам).
    /// В файл не пишет — для этого Save (окно настроек сохраняет после подтверждения).</summary>
    public void Apply(GameSettingsData settings)
    {
        if (settings == null) return;
        Current = Normalize(settings.Clone());
        Apply();
    }

    /// <summary>Применить текущие настройки ко всей игре.</summary>
    public void Apply()
    {
        GraphicsSettingsApplier.ApplyDisplay(Current);
        GraphicsSettingsApplier.ApplyQuality(Current, ProjectDefaultQualityLevel);
        GraphicsSettingsApplier.ApplyPipeline(Current);
        GraphicsSettingsApplier.ApplyCameras(Current);
        ControlsSettingsApplier.Apply(Current);
        ApplyAudio();

        if (LocalizationService.Instance != null) LocalizationService.Instance.SetLanguage(Current.language);

        OnSettingsApplied?.Invoke(Current);
    }

    /// <summary>Сбросить на значения по умолчанию, применить и сохранить.</summary>
    public void ResetToDefaults()
    {
        Current = CreateDefaults();
        Apply();
        Save();
    }

    /// <summary>Настройки по умолчанию для этого компьютера: значения из SettingsDefaults, нативное
    /// разрешение, язык Steam/системы, пресет качества проекта.</summary>
    public GameSettingsData CreateDefaults()
    {
        GameSettingsData data = Defaults.defaults != null ? Defaults.defaults.Clone() : new GameSettingsData();
        data.version = GameSettingsData.CurrentVersion;
        data.resolutionWidth = 0;
        data.resolutionHeight = 0;
        data.qualityLevel = -1;
        data.language = "";
        return Normalize(data);
    }

    /// <summary>Привести значения к допустимым: диапазоны, нативное разрешение вместо нулей, язык.</summary>
    public GameSettingsData Normalize(GameSettingsData data)
    {
        Defaults.Clamp(data);

        if (data.resolutionWidth <= 0 || data.resolutionHeight <= 0)
        {
            Vector2Int native = GraphicsSettingsApplier.GetNativeResolution();
            data.resolutionWidth = native.x;
            data.resolutionHeight = native.y;
        }

        LocalizationService localization = LocalizationService.Instance;
        if (localization != null && !localization.IsSupported(data.language))
            data.language = localization.DetectDefaultLanguage();

        return data;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Камеры, поле зрения, мышь и покачивание живут в сценах — у новой сцены их надо настроить заново.
        GraphicsSettingsApplier.ApplyCameras(Current);
        ControlsSettingsApplier.Apply(Current);
        if (autoBindAudioSources) AudioChannelVolume.BindUnassigned();
    }

    private void ApplyAudio()
    {
        // В редакторе Run In Background — настройка проекта, её не трогаем.
        if (!Application.isEditor) Application.runInBackground = Current.audioInBackground;
        ApplyMasterVolume();
    }

    private void ApplyMasterVolume()
    {
        // Без «звука в фоне» потерявшее фокус окно молчит (AudioListener.pause занят меню паузы). В редакторе
        // фокус теряется от любого клика по инспектору — там звук не глушим.
        bool audible = hasFocus || Current.audioInBackground || Application.isEditor;
        AudioListener.volume = audible ? Current.masterVolume : 0f;
    }

    // ───────────────────────── Файл ─────────────────────────

    /// <summary>Записать текущие настройки. Атомарно: сначала во временный файл, потом замена — вылет
    /// посреди записи не оставит полуфайл.</summary>
    public void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(Current, true);
            string tempPath = SettingsPath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(SettingsPath)) File.Replace(tempPath, SettingsPath, null);
            else File.Move(tempPath, SettingsPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Settings] Не удалось сохранить настройки в {SettingsPath}: {e.Message}");
        }
    }

    private GameSettingsData Load()
    {
        GameSettingsData data = CreateDefaults();
        if (!File.Exists(SettingsPath)) return data;

        try
        {
            // Поверх значений по умолчанию: поля, которых нет в файле (новая версия игры), остаются дефолтными.
            JsonUtility.FromJsonOverwrite(File.ReadAllText(SettingsPath), data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Settings] settings.cfg повреждён ({e.Message}) — используются значения по умолчанию.");
            data = CreateDefaults();
        }

        data.version = GameSettingsData.CurrentVersion;
        return Normalize(data);
    }
}
