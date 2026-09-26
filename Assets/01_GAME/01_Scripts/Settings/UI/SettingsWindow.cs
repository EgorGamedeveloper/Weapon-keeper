using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно настроек: вкладки Графика / Управление / Звук / Игра. Открывается из главного меню и из меню паузы.
///
/// Строки строятся из кода по шаблонам (Build): новая настройка — одна строка Add... с подписью и привязкой
/// к полю рабочей копии, без правки префаба. Правки идут в копию (pending) и применяются только кнопкой
/// «Применить»; «По умолчанию» сбрасывает копию; «Назад» с несохранёнными изменениями спрашивает.
///
/// Смена разрешения или режима окна после применения требует подтверждения: без него через
/// displayRevertSeconds (реальное время) экран откатывается — на случай, если монитор не показал картинку.
///
/// Компонент живёт на корне канваса, а само окно (windowGroup) — дочерний объект, который включается при
/// открытии. Esc: сначала закрывает диалог, потом окно (с вопросом о несохранённом).
/// </summary>
public class SettingsWindow : MonoBehaviour
{
    private static readonly FullScreenMode[] WindowModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

    private static readonly string[] WindowModeKeys =
        { "settings.window_mode.exclusive", "settings.window_mode.borderless", "settings.window_mode.windowed" };

    private static readonly string[] AntialiasingKeys =
        { "settings.aa.off", "settings.aa.fxaa", "settings.aa.smaa", "settings.aa.taa" };

    private static readonly string[] QualityLevelKeys =
        { "settings.level.low", "settings.level.medium", "settings.level.high" };

    [Header("Окно")]
    [Tooltip("Корень окна (затемнение + панель) — включается при открытии, проявляется и гаснет.")]
    public CanvasGroup windowGroup;

    [Tooltip("Панель окна — «выпрыгивает» при открытии.")]
    public RectTransform panel;

    [Header("Вкладки")]
    [Tooltip("Кнопки вкладок: Графика, Управление, Звук, Игра.")]
    public Button[] tabButtons = new Button[4];

    [Tooltip("Страницы вкладок в том же порядке: контейнеры строк с VerticalLayoutGroup внутри прокрутки.")]
    public RectTransform[] pages = new RectTransform[4];

    [Tooltip("Прокрутка содержимого вкладки: её content переключается на страницу активной вкладки.")]
    public ScrollRect scrollRect;

    [Header("Кнопки")]
    [Tooltip("Применить и сохранить изменения.")]
    public Button applyButton;

    [Tooltip("Сбросить все значения на значения по умолчанию (применяются кнопкой «Применить»).")]
    public Button resetButton;

    [Tooltip("Закрыть окно (с вопросом, если есть несохранённые изменения).")]
    public Button backButton;

    [Header("Шаблоны строк (выключенные объекты внутри окна)")]
    [Tooltip("Строка с ползунком.")]
    public SettingsSlider sliderTemplate;

    [Tooltip("Строка с выпадающим списком.")]
    public SettingsDropdown dropdownTemplate;

    [Tooltip("Строка с переключателем.")]
    public SettingsToggle toggleTemplate;

    [Tooltip("Строка «‹ значение ›».")]
    public SettingsStepper stepperTemplate;

    [Tooltip("Заголовок секции внутри вкладки.")]
    public LocalizedText sectionTemplate;

    [Header("Диалог")]
    [Tooltip("Диалог подтверждения (несохранённые изменения, новое разрешение).")]
    public ConfirmDialog dialog;

    [Header("Экран")]
    [Tooltip("Через сколько секунд (реального времени) без подтверждения новое разрешение/режим окна откатывается.")]
    [Min(3f)] public float displayRevertSeconds = 15f;

    [Header("Цвета вкладок")]
    [Tooltip("Фон активной вкладки.")]
    public Color tabActiveColor = new Color(1f, 0.82f, 0.25f, 1f);

    [Tooltip("Фон неактивной вкладки.")]
    public Color tabIdleColor = new Color(0.16f, 0.16f, 0.18f, 1f);

    [Tooltip("Текст активной вкладки.")]
    public Color tabActiveTextColor = new Color(0.1f, 0.08f, 0.02f, 1f);

    [Tooltip("Текст неактивной вкладки.")]
    public Color tabIdleTextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    /// <summary>Окно открыто.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Окно закрылось (меню паузы возвращает фокус на свои кнопки).</summary>
    public event Action OnClosed;

    private readonly List<SettingsControl> controls = new List<SettingsControl>();
    private readonly List<(SettingsControl control, Func<bool> isAvailable)> dependencies =
        new List<(SettingsControl, Func<bool>)>();
    private readonly List<string> languageCodes = new List<string>();

    private SettingsService service;
    private LocalizationService localization;
    private GameSettingsData pending;
    private List<Vector2Int> resolutions = new List<Vector2Int>();
    private bool built;
    private int currentTab;

    private bool HasUnsavedChanges => service != null && pending != null && !pending.ContentEquals(service.Current);

    private void Awake()
    {
        UiWindowAnimation.HideImmediate(windowGroup);

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tab = i;
            if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => ShowTab(tab));
        }

        if (applyButton != null) applyButton.onClick.AddListener(() => ApplyChanges(false));
        if (resetButton != null) resetButton.onClick.AddListener(ResetToDefaults);
        if (backButton != null) backButton.onClick.AddListener(RequestClose);

        HideTemplate(sliderTemplate);
        HideTemplate(dropdownTemplate);
        HideTemplate(toggleTemplate);
        HideTemplate(stepperTemplate);
        HideTemplate(sectionTemplate);
    }

    private void OnDisable()
    {
        if (localization != null) localization.OnLanguageChanged -= HandleLanguageChanged;
        localization = null;
    }

    private void Update()
    {
        if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;

        if (dialog != null && dialog.IsOpen) dialog.Cancel();
        else RequestClose();
    }

    // ───────────────────────── Открытие / закрытие ─────────────────────────

    public void Open()
    {
        if (IsOpen) return;

        service = SettingsService.Instance;
        if (service == null)
        {
            Debug.LogWarning("[Settings] В игре нет SettingsService — добавьте PersistentServices (Tools/Weapon Keeper/Setup Settings & Steam).", this);
            return;
        }

        IsOpen = true;
        pending = service.Current.Clone();
        if (!built) Build();

        localization = LocalizationService.Instance;
        if (localization != null) localization.OnLanguageChanged += HandleLanguageChanged;

        RefreshAll();
        ShowTab(currentTab);
        UiWindowAnimation.Show(windowGroup, panel);
    }

    /// <summary>«Назад»/Esc: с несохранёнными изменениями — вопрос, иначе закрыть.</summary>
    public void RequestClose()
    {
        if (!IsOpen) return;
        if (!HasUnsavedChanges || dialog == null)
        {
            Close();
            return;
        }

        dialog.Show(Loc.Get("settings.unsaved.title"), Loc.Get("settings.unsaved.message"),
            Loc.Get("common.apply"), () => ApplyChanges(true),
            Loc.Get("common.cancel"), null,
            Loc.Get("settings.unsaved.discard"), Close);
    }

    /// <summary>Закрыть без вопросов (несохранённые правки теряются).</summary>
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        if (localization != null) localization.OnLanguageChanged -= HandleLanguageChanged;
        localization = null;
        UiWindowAnimation.Hide(windowGroup, panel);
        OnClosed?.Invoke();
    }

    // ───────────────────────── Кнопки ─────────────────────────

    private void ApplyChanges(bool closeAfterwards)
    {
        if (service == null) return;

        GameSettingsData previous = service.Current.Clone();
        bool displayChanged = !pending.DisplayEquals(previous);

        service.Apply(pending);
        pending = service.Current.Clone();
        RefreshAll();

        if (!displayChanged || dialog == null)
        {
            service.Save();
            if (closeAfterwards) Close();
            return;
        }

        // Новое разрешение — только после подтверждения: вдруг монитор его не показывает.
        dialog.Show(Loc.Get("settings.display.title"), Loc.Get("settings.display.message"),
            Loc.Get("settings.display.keep"), () =>
            {
                service.Save();
                if (closeAfterwards) Close();
            },
            Loc.Get("settings.display.revert"), () =>
            {
                RevertDisplay(previous);
                if (closeAfterwards) Close();
            },
            timeoutSeconds: displayRevertSeconds);
    }

    private void RevertDisplay(GameSettingsData previous)
    {
        GameSettingsData reverted = service.Current.Clone();
        reverted.CopyDisplayFrom(previous);
        service.Apply(reverted);
        service.Save();
        pending = service.Current.Clone();
        RefreshAll();
    }

    private void ResetToDefaults()
    {
        if (service == null) return;
        pending = service.CreateDefaults();
        RefreshAll();
    }

    // ───────────────────────── Вкладки ─────────────────────────

    private void ShowTab(int index)
    {
        currentTab = Mathf.Clamp(index, 0, pages.Length - 1);

        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].gameObject.SetActive(i == currentTab);

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            bool active = i == currentTab;
            if (tabButtons[i].image != null) tabButtons[i].image.color = active ? tabActiveColor : tabIdleColor;
            TMP_Text label = tabButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = active ? tabActiveTextColor : tabIdleTextColor;
        }

        if (scrollRect != null && pages[currentTab] != null)
        {
            scrollRect.content = pages[currentTab];
            LayoutRebuilder.ForceRebuildLayoutImmediate(pages[currentTab]);
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    // ───────────────────────── Строки ─────────────────────────

    private void Build()
    {
        built = true;
        SettingsDefaults d = service.Defaults;
        resolutions = GraphicsSettingsApplier.GetResolutionOptions();

        // Графика
        RectTransform graphics = pages[0];
        AddSection(graphics, "settings.section.display");
        AddDropdown(graphics, "settings.graphics.resolution", ResolutionOptions, FindResolutionIndex, i =>
        {
            pending.resolutionWidth = resolutions[i].x;
            pending.resolutionHeight = resolutions[i].y;
        });
        AddStepper(graphics, "settings.graphics.window_mode", () => Localize(WindowModeKeys),
            () => Mathf.Max(0, Array.IndexOf(WindowModes, pending.windowMode)), i => pending.windowMode = WindowModes[i]);
        AddToggle(graphics, "settings.graphics.vsync", () => pending.vSync, v => pending.vSync = v);
        SettingsControl fps = AddStepper(graphics, "settings.graphics.fps_limit", FrameRateOptions,
            () => IndexOrLast(d.frameRateOptions, pending.frameRateLimit), i => pending.frameRateLimit = d.frameRateOptions[i]);
        DependsOn(fps, () => !pending.vSync);

        AddSection(graphics, "settings.section.quality");
        AddDropdown(graphics, "settings.graphics.quality", QualityOptions,
            () => pending.qualityLevel >= 0 ? pending.qualityLevel : service.ProjectDefaultQualityLevel,
            i => pending.qualityLevel = i);
        AddSlider(graphics, "settings.graphics.render_scale", d.renderScaleRange.x, d.renderScaleRange.y, false,
            () => pending.renderScale, v => pending.renderScale = Mathf.Round(v * 20f) / 20f, Percent);
        AddDropdown(graphics, "settings.graphics.antialiasing", () => Localize(AntialiasingKeys),
            () => (int)pending.postAntialiasing, i => pending.postAntialiasing = (PostAntialiasingMode)i);
        SettingsControl aaQuality = AddStepper(graphics, "settings.graphics.aa_quality", () => Localize(QualityLevelKeys),
            () => pending.antialiasingQuality, i => pending.antialiasingQuality = i);
        DependsOn(aaQuality, () => pending.postAntialiasing == PostAntialiasingMode.SMAA
                                   || pending.postAntialiasing == PostAntialiasingMode.TAA);
        SettingsControl msaa = AddStepper(graphics, "settings.graphics.msaa", MsaaOptions,
            () => Mathf.Max(0, Array.IndexOf(d.msaaOptions, pending.msaaSamples)), i => pending.msaaSamples = d.msaaOptions[i]);
        DependsOn(msaa, () => pending.postAntialiasing != PostAntialiasingMode.TAA);
        AddToggle(graphics, "settings.graphics.shadows", () => pending.shadows, v => pending.shadows = v);
        SettingsControl shadowDistance = AddSlider(graphics, "settings.graphics.shadow_distance",
            d.shadowDistanceRange.x, d.shadowDistanceRange.y, true,
            () => pending.shadowDistance, v => pending.shadowDistance = v, v => Loc.Get("settings.unit.meters", v));
        DependsOn(shadowDistance, () => pending.shadows);

        AddSection(graphics, "settings.section.image");
        AddSlider(graphics, "settings.graphics.fov", d.fieldOfViewRange.x, d.fieldOfViewRange.y, true,
            () => pending.fieldOfView, v => pending.fieldOfView = v, v => Loc.Get("settings.unit.degrees", v));
        AddSlider(graphics, "settings.graphics.brightness", d.brightnessRange.x, d.brightnessRange.y, false,
            () => pending.brightness, v => pending.brightness = Mathf.Round(v * 20f) / 20f,
            v => (v > 0f ? "+" : "") + v.ToString("0.00"));
        AddToggle(graphics, "settings.graphics.bloom", () => pending.bloom, v => pending.bloom = v);
        AddToggle(graphics, "settings.graphics.motion_blur", () => pending.motionBlur, v => pending.motionBlur = v);

        // Управление
        RectTransform controlsPage = pages[1];
        AddSection(controlsPage, "settings.section.mouse");
        AddSlider(controlsPage, "settings.controls.sensitivity", d.sensitivityRange.x, d.sensitivityRange.y, false,
            () => pending.mouseSensitivity, v => pending.mouseSensitivity = Mathf.Round(v * 20f) / 20f, v => v.ToString("0.00"));
        AddToggle(controlsPage, "settings.controls.separate_y",
            () => pending.separateVerticalSensitivity, v => pending.separateVerticalSensitivity = v);
        SettingsControl sensitivityY = AddSlider(controlsPage, "settings.controls.sensitivity_y",
            d.sensitivityRange.x, d.sensitivityRange.y, false,
            () => pending.mouseSensitivityY, v => pending.mouseSensitivityY = Mathf.Round(v * 20f) / 20f, v => v.ToString("0.00"));
        DependsOn(sensitivityY, () => pending.separateVerticalSensitivity);
        AddToggle(controlsPage, "settings.controls.invert_y", () => pending.invertY, v => pending.invertY = v);
        AddSlider(controlsPage, "settings.controls.smoothing", 0f, d.maxMouseSmoothing, false,
            () => pending.mouseSmoothing, v => pending.mouseSmoothing = Mathf.Round(v * 100f) / 100f,
            v => Loc.Get("settings.unit.seconds", v));
        AddSection(controlsPage, "settings.section.camera");
        AddToggle(controlsPage, "settings.controls.head_bob", () => pending.headBob, v => pending.headBob = v);

        // Звук
        RectTransform audioPage = pages[2];
        AddSlider(audioPage, "settings.audio.master", 0f, 1f, false, () => pending.masterVolume, v => pending.masterVolume = RoundVolume(v), Percent);
        AddSlider(audioPage, "settings.audio.music", 0f, 1f, false, () => pending.musicVolume, v => pending.musicVolume = RoundVolume(v), Percent);
        AddSlider(audioPage, "settings.audio.sfx", 0f, 1f, false, () => pending.sfxVolume, v => pending.sfxVolume = RoundVolume(v), Percent);
        AddSlider(audioPage, "settings.audio.ui", 0f, 1f, false, () => pending.uiVolume, v => pending.uiVolume = RoundVolume(v), Percent);
        AddSlider(audioPage, "settings.audio.ambience", 0f, 1f, false, () => pending.ambienceVolume, v => pending.ambienceVolume = RoundVolume(v), Percent);
        AddToggle(audioPage, "settings.audio.background", () => pending.audioInBackground, v => pending.audioInBackground = v);

        // Игра
        RectTransform gamePage = pages[3];
        AddDropdown(gamePage, "settings.game.language", LanguageOptions, FindLanguageIndex,
            i => pending.language = languageCodes[i]);
        AddToggle(gamePage, "settings.game.hints", () => pending.showHints, v => pending.showHints = v);
    }

    private void RefreshAll()
    {
        foreach (SettingsControl control in controls) control.Refresh();
        RefreshDependencies();
    }

    private void RefreshDependencies()
    {
        foreach ((SettingsControl control, Func<bool> isAvailable) in dependencies)
            control.SetInteractable(isAvailable());
        if (applyButton != null) applyButton.interactable = HasUnsavedChanges;
    }

    private void HandleControlChanged() => RefreshDependencies();

    private void HandleLanguageChanged()
    {
        // Подписи строк (LocalizedText) обновятся сами; варианты списков и единицы — здесь.
        if (IsOpen) RefreshAll();
    }

    private void DependsOn(SettingsControl control, Func<bool> isAvailable)
    {
        if (control != null) dependencies.Add((control, isAvailable));
    }

    private SettingsSlider AddSlider(RectTransform page, string labelKey, float min, float max, bool wholeNumbers,
                                     Func<float> get, Action<float> set, Func<float, string> format)
    {
        SettingsSlider row = CreateRow(sliderTemplate, page, labelKey);
        if (row != null) row.Bind(min, max, wholeNumbers, get, set, format);
        return row;
    }

    private SettingsToggle AddToggle(RectTransform page, string labelKey, Func<bool> get, Action<bool> set)
    {
        SettingsToggle row = CreateRow(toggleTemplate, page, labelKey);
        if (row != null) row.Bind(get, set);
        return row;
    }

    private SettingsDropdown AddDropdown(RectTransform page, string labelKey, Func<IList<string>> options, Func<int> get, Action<int> set)
    {
        SettingsDropdown row = CreateRow(dropdownTemplate, page, labelKey);
        if (row != null) row.Bind(options, get, set);
        return row;
    }

    private SettingsStepper AddStepper(RectTransform page, string labelKey, Func<IList<string>> options, Func<int> get, Action<int> set)
    {
        SettingsStepper row = CreateRow(stepperTemplate, page, labelKey);
        if (row != null) row.Bind(options, get, set);
        return row;
    }

    private void AddSection(RectTransform page, string titleKey)
    {
        if (sectionTemplate == null || page == null) return;
        LocalizedText section = Instantiate(sectionTemplate, page);
        section.gameObject.SetActive(true);
        section.SetKey(titleKey);
    }

    private T CreateRow<T>(T template, RectTransform page, string labelKey) where T : SettingsControl
    {
        if (template == null || page == null)
        {
            Debug.LogWarning($"[Settings] Нет шаблона строки {typeof(T).Name} или страницы — «{labelKey}» пропущена.", this);
            return null;
        }

        T row = Instantiate(template, page);
        row.gameObject.SetActive(true);
        row.name = labelKey;
        row.SetLabel(labelKey);
        row.Changed += HandleControlChanged;
        controls.Add(row);
        return row;
    }

    private static void HideTemplate(Component template)
    {
        if (template != null) template.gameObject.SetActive(false);
    }

    // ───────────────────────── Варианты ─────────────────────────

    private IList<string> ResolutionOptions()
    {
        var options = new List<string>(resolutions.Count);
        foreach (Vector2Int size in resolutions) options.Add(size.x + " × " + size.y);
        return options;
    }

    private int FindResolutionIndex()
    {
        int index = resolutions.IndexOf(new Vector2Int(pending.resolutionWidth, pending.resolutionHeight));
        if (index >= 0) return index;

        // Сохранённого разрешения нет у этого монитора — показываем нативное.
        index = resolutions.IndexOf(GraphicsSettingsApplier.GetNativeResolution());
        return Mathf.Max(0, index);
    }

    private IList<string> FrameRateOptions()
    {
        var options = new List<string>();
        foreach (int limit in service.Defaults.frameRateOptions)
            options.Add(limit > 0 ? limit.ToString() : Loc.Get("settings.fps.unlimited"));
        return options;
    }

    private IList<string> MsaaOptions()
    {
        var options = new List<string>();
        foreach (int samples in service.Defaults.msaaOptions)
            options.Add(samples > 1 ? samples + "x" : Loc.Get("common.off"));
        return options;
    }

    private static IList<string> QualityOptions()
    {
        var options = new List<string>();
        LocalizationService localizationService = LocalizationService.Instance;
        foreach (string name in QualitySettings.names)
        {
            // Стандартные имена пресетов переводятся, свои (добавленные в проект) показываются как есть.
            string key = "settings.quality." + name.ToLowerInvariant().Replace(' ', '_');
            options.Add(localizationService != null && localizationService.HasKey(key) ? localizationService.Get(key) : name);
        }
        return options;
    }

    private IList<string> LanguageOptions()
    {
        languageCodes.Clear();
        var options = new List<string>();
        LocalizationService localizationService = LocalizationService.Instance;
        if (localizationService != null)
        {
            foreach (string code in localizationService.AvailableLanguages)
            {
                languageCodes.Add(code);
                options.Add(GameLanguages.GetNativeName(code));
            }
        }

        if (languageCodes.Count == 0)
        {
            languageCodes.Add(pending.language);
            options.Add(GameLanguages.GetNativeName(pending.language));
        }
        return options;
    }

    private int FindLanguageIndex() => Mathf.Max(0, languageCodes.IndexOf(pending.language));

    private static IList<string> Localize(string[] keys)
    {
        var options = new List<string>(keys.Length);
        foreach (string key in keys) options.Add(Loc.Get(key));
        return options;
    }

    private static int IndexOrLast(int[] values, int value)
    {
        int index = Array.IndexOf(values, value);
        return index >= 0 ? index : values.Length - 1;
    }

    private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

    private static float RoundVolume(float value) => Mathf.Round(value * 100f) / 100f;
}
