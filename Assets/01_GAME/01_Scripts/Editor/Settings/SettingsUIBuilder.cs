using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строит из кода префабы окна настроек и меню паузы (03_Prefabs/UI/Settings/). Префаб перезаписывается
/// по тому же пути — GUID сохраняется, экземпляры в сценах подхватывают новую версию.
///
/// Разметка рассчитана на 1920×1080 (CanvasScaler масштабирует под любой экран). Строки настроек в префабе —
/// только шаблоны: сами строки окно строит в рантайме (SettingsWindow.Build).
/// </summary>
public static class SettingsUIBuilder
{
    public const string PrefabFolder = "Assets/01_GAME/03_Prefabs/UI/Settings";
    public const string SettingsWindowPath = PrefabFolder + "/SettingsWindow.prefab";
    public const string PauseMenuPath = PrefabFolder + "/PauseMenu.prefab";

    private const float RowHeight = 58f;
    private const float LabelFontSize = 24f;
    private const float ValueFontSize = 22f;

    // ───────────────────────── Окно настроек ─────────────────────────

    public static GameObject BuildSettingsWindow()
    {
        UIBuilderKit.ResetStringCache();
        GameObject root = UIBuilderKit.CreateCanvasRoot("SettingsWindow", 110);
        var window = root.AddComponent<SettingsWindow>();

        RectTransform windowRect = UIBuilderKit.CreateRect("Window", root.transform);
        UIBuilderKit.Stretch(windowRect);
        window.windowGroup = windowRect.gameObject.AddComponent<CanvasGroup>();
        UIBuilderKit.CreateDim(windowRect, UIBuilderKit.DimColor);

        RectTransform panel = UIBuilderKit.CreatePanel(windowRect, new Vector2(1240f, 900f));
        window.panel = panel;

        // Шапка.
        TextMeshProUGUI title = UIBuilderKit.CreateText("Title", panel, "settings.title", 42f, UIBuilderKit.TextColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        UIBuilderKit.Anchor(title.rectTransform, 0f, 1f, 1f, 1f, 44f, -92f, 44f, 14f);

        // Вкладки.
        RectTransform tabs = UIBuilderKit.CreateRect("Tabs", panel);
        UIBuilderKit.Anchor(tabs, 0f, 1f, 1f, 1f, 44f, -170f, 44f, 104f);
        HorizontalLayoutGroup tabsLayout = UIBuilderKit.AddHorizontalLayout(tabs.gameObject, 10f, TextAnchor.MiddleLeft);
        tabsLayout.childForceExpandWidth = true;

        string[] tabKeys = { "settings.tab.graphics", "settings.tab.controls", "settings.tab.audio", "settings.tab.game" };
        string[] tabNames = { "Tab_Graphics", "Tab_Controls", "Tab_Audio", "Tab_Game" };
        window.tabButtons = new Button[tabKeys.Length];
        for (int i = 0; i < tabKeys.Length; i++)
        {
            Button tab = UIBuilderKit.CreateButton(tabNames[i], tabs, tabKeys[i], UIBuilderKit.ControlColor,
                UIBuilderKit.TextColor, 24f, out _);
            window.tabButtons[i] = tab;
        }

        // Содержимое вкладок.
        RectTransform content = UIBuilderKit.CreateRect("Content", panel);
        UIBuilderKit.Stretch(content, 44f, 190f, 44f, 118f);
        window.scrollRect = UIBuilderKit.CreateScrollView(content, out RectTransform viewport);
        UIBuilderKit.Stretch((RectTransform)window.scrollRect.transform);

        string[] pageNames = { "Page_Graphics", "Page_Controls", "Page_Audio", "Page_Game" };
        window.pages = new RectTransform[pageNames.Length];
        for (int i = 0; i < pageNames.Length; i++)
        {
            window.pages[i] = UIBuilderKit.CreatePage(pageNames[i], viewport, 6f);
            window.pages[i].gameObject.SetActive(i == 0);
        }
        window.scrollRect.content = window.pages[0];

        // Кнопки внизу.
        RectTransform footer = UIBuilderKit.CreateRect("Footer", panel);
        UIBuilderKit.Anchor(footer, 0f, 0f, 1f, 0f, 44f, 30f, 44f, -94f);

        window.resetButton = UIBuilderKit.CreateButton("ResetButton", footer, "settings.reset", UIBuilderKit.ControlColor,
            UIBuilderKit.TextColor, 24f, out _);
        UIBuilderKit.Anchor((RectTransform)window.resetButton.transform, 0f, 0f, 0f, 1f, 0f, 0f, -300f, 0f);

        window.applyButton = UIBuilderKit.CreateButton("ApplyButton", footer, "common.apply", UIBuilderKit.AccentColor,
            UIBuilderKit.DarkTextColor, 26f, out _);
        UIBuilderKit.Anchor((RectTransform)window.applyButton.transform, 1f, 0f, 1f, 1f, -270f, 0f, 0f, 0f);

        window.backButton = UIBuilderKit.CreateButton("BackButton", footer, "common.back", UIBuilderKit.ControlColor,
            UIBuilderKit.TextColor, 24f, out _);
        UIBuilderKit.Anchor((RectTransform)window.backButton.transform, 1f, 0f, 1f, 1f, -500f, 0f, 290f, 0f);

        // Шаблоны строк — выключенные, окно клонирует их в страницы.
        RectTransform templates = UIBuilderKit.CreateRect("Templates", panel);
        UIBuilderKit.Stretch(templates);
        window.sliderTemplate = BuildSliderRow(templates);
        window.dropdownTemplate = BuildDropdownRow(templates);
        window.toggleTemplate = BuildToggleRow(templates);
        window.stepperTemplate = BuildStepperRow(templates);
        window.sectionTemplate = BuildSectionRow(templates);
        templates.gameObject.SetActive(false);

        window.dialog = BuildConfirmDialog(root.transform);
        return UIBuilderKit.SavePrefab(root, SettingsWindowPath);
    }

    /// <summary>Общая основа строки: фон, подпись слева, группа для «недоступно».</summary>
    private static T CreateRowBase<T>(Transform parent, string name) where T : SettingsControl
    {
        Image background = UIBuilderKit.CreateImage(name, parent, UIBuilderKit.RowColor);
        UIBuilderKit.AddLayoutElement(background.gameObject, RowHeight);
        var control = background.gameObject.AddComponent<T>();
        control.rowGroup = background.gameObject.AddComponent<CanvasGroup>();

        TextMeshProUGUI label = UIBuilderKit.CreateText("Label", background.transform, null, LabelFontSize,
            UIBuilderKit.TextColor, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Настройка");
        UIBuilderKit.Anchor(label.rectTransform, 0f, 0f, 0.46f, 1f, 18f, 0f, 8f, 0f);
        control.label = label;
        control.labelLocalization = label.gameObject.AddComponent<LocalizedText>();
        return control;
    }

    private static SettingsSlider BuildSliderRow(Transform parent)
    {
        var row = CreateRowBase<SettingsSlider>(parent, "SliderRow");
        row.slider = UIBuilderKit.CreateSlider(row.transform);
        UIBuilderKit.Anchor((RectTransform)row.slider.transform, 0.48f, 0.2f, 0.84f, 0.8f);

        row.valueText = UIBuilderKit.CreateText("Value", row.transform, null, ValueFontSize, UIBuilderKit.AccentColor,
            TextAlignmentOptions.MidlineRight, FontStyles.Bold, "100%");
        UIBuilderKit.Anchor(row.valueText.rectTransform, 0.85f, 0f, 1f, 1f, 0f, 0f, 18f, 0f);
        return row;
    }

    private static SettingsDropdown BuildDropdownRow(Transform parent)
    {
        var row = CreateRowBase<SettingsDropdown>(parent, "DropdownRow");
        row.dropdown = UIBuilderKit.CreateDropdown(row.transform, ValueFontSize);
        UIBuilderKit.Anchor((RectTransform)row.dropdown.transform, 0.48f, 0.12f, 1f, 0.88f, 0f, 0f, 18f, 0f);
        return row;
    }

    private static SettingsToggle BuildToggleRow(Transform parent)
    {
        var row = CreateRowBase<SettingsToggle>(parent, "ToggleRow");
        row.toggle = UIBuilderKit.CreateToggle(row.transform);
        var toggleRect = (RectTransform)row.toggle.transform;
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0.48f, 0.5f);
        toggleRect.pivot = new Vector2(0f, 0.5f);
        toggleRect.sizeDelta = new Vector2(36f, 36f);
        toggleRect.anchoredPosition = Vector2.zero;

        row.stateText = UIBuilderKit.CreateText("State", row.transform, null, ValueFontSize, UIBuilderKit.MutedTextColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Вкл");
        UIBuilderKit.Anchor(row.stateText.rectTransform, 0.48f, 0f, 1f, 1f, 52f, 0f, 18f, 0f);
        return row;
    }

    private static SettingsStepper BuildStepperRow(Transform parent)
    {
        var row = CreateRowBase<SettingsStepper>(parent, "StepperRow");

        row.previousButton = UIBuilderKit.CreateButton("Previous", row.transform, null, UIBuilderKit.ControlColor,
            UIBuilderKit.TextColor, 28f, out TextMeshProUGUI previousLabel);
        previousLabel.text = "‹";
        PlaceSquare((RectTransform)row.previousButton.transform, 0.48f, 0f);

        row.nextButton = UIBuilderKit.CreateButton("Next", row.transform, null, UIBuilderKit.ControlColor,
            UIBuilderKit.TextColor, 28f, out TextMeshProUGUI nextLabel);
        nextLabel.text = "›";
        PlaceSquare((RectTransform)row.nextButton.transform, 1f, -18f);

        row.valueText = UIBuilderKit.CreateText("Value", row.transform, null, ValueFontSize, UIBuilderKit.TextColor,
            TextAlignmentOptions.Center, FontStyles.Bold, "Значение");
        UIBuilderKit.Anchor(row.valueText.rectTransform, 0.48f, 0f, 1f, 1f, 52f, 0f, 70f, 0f);
        return row;
    }

    /// <summary>Квадратная кнопка 42×42 у доли ширины anchorX (0.48 — слева от значения, 1 — у правого края).</summary>
    private static void PlaceSquare(RectTransform rect, float anchorX, float offsetX)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0.5f);
        rect.pivot = new Vector2(anchorX >= 1f ? 1f : 0f, 0.5f);
        rect.sizeDelta = new Vector2(42f, 42f);
        rect.anchoredPosition = new Vector2(offsetX, 0f);
    }

    private static LocalizedText BuildSectionRow(Transform parent)
    {
        TextMeshProUGUI title = UIBuilderKit.CreateText("SectionRow", parent, null, 22f, UIBuilderKit.AccentColor,
            TextAlignmentOptions.BottomLeft, FontStyles.Bold | FontStyles.UpperCase, "Секция");
        title.margin = new Vector4(6f, 0f, 0f, 6f);
        UIBuilderKit.AddLayoutElement(title.gameObject, 50f);
        return title.gameObject.AddComponent<LocalizedText>();
    }

    // ───────────────────────── Меню паузы ─────────────────────────

    public static GameObject BuildPauseMenu()
    {
        UIBuilderKit.ResetStringCache();
        GameObject root = UIBuilderKit.CreateCanvasRoot("PauseMenu", 100);
        var pause = root.AddComponent<PauseMenu>();

        RectTransform windowRect = UIBuilderKit.CreateRect("Window", root.transform);
        UIBuilderKit.Stretch(windowRect);
        pause.windowGroup = windowRect.gameObject.AddComponent<CanvasGroup>();
        UIBuilderKit.CreateDim(windowRect, new Color(0f, 0f, 0f, 0.6f));

        RectTransform panel = UIBuilderKit.CreatePanel(windowRect, new Vector2(600f, 660f));
        pause.panel = panel;

        TextMeshProUGUI title = UIBuilderKit.CreateText("Title", panel, "pause.title", 46f, UIBuilderKit.TextColor,
            TextAlignmentOptions.Center, FontStyles.Bold);
        UIBuilderKit.Anchor(title.rectTransform, 0f, 1f, 1f, 1f, 30f, -120f, 30f, 30f);

        RectTransform buttons = UIBuilderKit.CreateRect("Buttons", panel);
        UIBuilderKit.Stretch(buttons, 70f, 150f, 70f, 50f);
        VerticalLayoutGroup layout = UIBuilderKit.AddVerticalLayout(buttons.gameObject, 16f);
        layout.childAlignment = TextAnchor.UpperCenter;

        pause.resumeButton = CreateMenuButton(buttons, "ResumeButton", "pause.resume", true);
        pause.settingsButton = CreateMenuButton(buttons, "SettingsButton", "pause.settings", false);
        pause.achievementsButton = CreateMenuButton(buttons, "AchievementsButton", "pause.achievements", false);
        pause.mainMenuButton = CreateMenuButton(buttons, "MainMenuButton", "pause.main_menu", false);
        pause.quitButton = CreateMenuButton(buttons, "QuitButton", "pause.quit", false);

        pause.dialog = BuildConfirmDialog(root.transform);
        return UIBuilderKit.SavePrefab(root, PauseMenuPath);
    }

    private static Button CreateMenuButton(Transform parent, string name, string key, bool accent)
    {
        Button button = UIBuilderKit.CreateButton(name, parent, key,
            accent ? UIBuilderKit.AccentColor : UIBuilderKit.ControlColor,
            accent ? UIBuilderKit.DarkTextColor : UIBuilderKit.TextColor, 28f, out _);
        UIBuilderKit.AddLayoutElement(button.gameObject, 72f);
        return button;
    }

    // ───────────────────────── Диалог ─────────────────────────

    /// <summary>Диалог подтверждения — последним ребёнком канваса, поверх окна.</summary>
    public static ConfirmDialog BuildConfirmDialog(Transform canvasRoot)
    {
        RectTransform root = UIBuilderKit.CreateRect("ConfirmDialog", canvasRoot);
        UIBuilderKit.Stretch(root);
        var dialog = root.gameObject.AddComponent<ConfirmDialog>();
        dialog.group = root.gameObject.AddComponent<CanvasGroup>();
        UIBuilderKit.CreateDim(root, new Color(0f, 0f, 0f, 0.55f));

        RectTransform panel = UIBuilderKit.CreatePanel(root, new Vector2(760f, 360f));
        dialog.panel = panel;

        dialog.titleText = UIBuilderKit.CreateText("Title", panel, null, 32f, UIBuilderKit.AccentColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Заголовок");
        UIBuilderKit.Anchor(dialog.titleText.rectTransform, 0f, 1f, 1f, 1f, 36f, -86f, 36f, 26f);

        dialog.messageText = UIBuilderKit.CreateText("Message", panel, null, 24f, UIBuilderKit.TextColor,
            TextAlignmentOptions.TopLeft, FontStyles.Normal, "Текст вопроса");
        dialog.messageText.textWrappingMode = TextWrappingModes.Normal;
        dialog.messageText.overflowMode = TextOverflowModes.Overflow;
        UIBuilderKit.Stretch(dialog.messageText.rectTransform, 36f, 100f, 36f, 110f);

        RectTransform buttons = UIBuilderKit.CreateRect("Buttons", panel);
        UIBuilderKit.Anchor(buttons, 0f, 0f, 1f, 0f, 36f, 30f, 36f, -94f);
        HorizontalLayoutGroup layout = UIBuilderKit.AddHorizontalLayout(buttons.gameObject, 12f, TextAnchor.MiddleRight);
        layout.childForceExpandHeight = true;

        dialog.cancelButton = CreateDialogButton(buttons, "CancelButton", false, out dialog.cancelLabel);
        dialog.alternativeButton = CreateDialogButton(buttons, "AlternativeButton", false, out dialog.alternativeLabel);
        dialog.confirmButton = CreateDialogButton(buttons, "ConfirmButton", true, out dialog.confirmLabel);

        root.gameObject.SetActive(false);
        return dialog;
    }

    private static Button CreateDialogButton(Transform parent, string name, bool accent, out TMP_Text label)
    {
        Button button = UIBuilderKit.CreateButton(name, parent, null,
            accent ? UIBuilderKit.AccentColor : UIBuilderKit.ControlColor,
            accent ? UIBuilderKit.DarkTextColor : UIBuilderKit.TextColor, 22f, out TextMeshProUGUI text);
        text.text = name;
        label = text;
        UIBuilderKit.AddLayoutElement(button.gameObject, 64f, 210f);
        return button;
    }
}
