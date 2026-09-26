using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Меню Tools/Weapon Keeper/Settings:
/// - Build Settings UI — пересобрать префабы окна настроек, меню паузы и постоянных сервисов и расставить
///   их в открытой сцене: в сцене меню (есть MainMenuUI) — окно настроек и кнопка «Настройки»; в игровой
///   (есть PlayerCharacterController) — окно настроек, меню паузы, Volume настроек, ссылки у
///   CursorLockController;
/// - Assign Audio Channels — всем AudioSource сцены без канала проставить SFX.
/// Повторный запуск безопасен: уже настроенное не дублируется.
/// </summary>
public static class SettingsSceneSetup
{
    private const string MenuRoot = "Tools/Weapon Keeper/Settings/";
    private const string Title = "Настройки";

    [MenuItem(MenuRoot + "Build Settings UI")]
    private static void BuildSettingsUI()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;

        SettingsUIBuilder.BuildSettingsWindow();
        SettingsUIBuilder.BuildPauseMenu();
        PersistentServicesBuilder.Build();
        AssetDatabase.SaveAssets();

        Scene scene = SceneManager.GetActiveScene();
        var report = new StringBuilder("✔ Префабы собраны в " + SettingsUIBuilder.PrefabFolder + ".\n");
        SetupScene(scene, report);
        SceneSetupUtility.Finish(scene, Title, report);
    }

    [MenuItem(MenuRoot + "Assign Audio Channels")]
    private static void AssignAudioChannelsMenu()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;
        Scene scene = SceneManager.GetActiveScene();
        var report = new StringBuilder();
        AssignAudioChannels(scene, report);
        SceneSetupUtility.Finish(scene, Title, report);
    }

    /// <summary>Расставить окна и сервисы настроек в сцене (префабы должны быть уже собраны).</summary>
    public static void SetupScene(Scene scene, StringBuilder report)
    {
        SceneSetupUtility.EnsurePrefabInstance<PersistentServicesRoot>(scene, PersistentServicesBuilder.PrefabPath, report);
        SceneSetupUtility.EnsureEventSystem(scene, report);

        MainMenuUI menu = SceneSetupUtility.FindInScene<MainMenuUI>(scene);
        bool gameplay = SceneSetupUtility.FindInScene<PlayerCharacterController>(scene) != null;
        if (menu == null && !gameplay)
        {
            report.AppendLine("• В сцене нет ни MainMenuUI, ни игрока — окна не добавлены (только сервисы).");
            return;
        }

        SettingsWindow settingsWindow =
            SceneSetupUtility.EnsurePrefabInstance<SettingsWindow>(scene, SettingsUIBuilder.SettingsWindowPath, report);

        if (menu != null) SetupMainMenu(menu, settingsWindow, report);
        if (gameplay) SetupGameplay(scene, settingsWindow, report);
    }

    private static void SetupMainMenu(MainMenuUI menu, SettingsWindow settingsWindow, StringBuilder report)
    {
        Undo.RecordObject(menu, "Settings button");
        menu.settingsWindow = settingsWindow;
        if (menu.settingsButton == null)
        {
            menu.settingsButton = CreateSettingsButton(menu, report);
            if (menu.settingsButton != null) report.AppendLine("✔ В главное меню добавлена кнопка «Настройки» — проверьте её место.");
        }
        SceneSetupUtility.MarkModified(menu);

        LocalizeButton(menu.newGameButton, "menu.new_game", report);
        LocalizeButton(menu.continueButton, "menu.continue", report);
        LocalizeButton(menu.quitButton, "menu.quit", report);
        LocalizeButton(menu.confirmYesButton, "menu.confirm_yes", report);
        LocalizeButton(menu.confirmNoButton, "menu.confirm_no", report);
    }

    /// <summary>
    /// Перевод подписи существующей кнопки меню: LocalizedText вешается, только если текст кнопки совпадает
    /// с русской строкой таблицы (без учёта регистра) — иначе игра заменила бы задуманную надпись нашей.
    /// Несовпавшие подписи переводятся через Localization/Extract Texts From Open Scene.
    /// </summary>
    private static void LocalizeButton(Button button, string key, StringBuilder report)
    {
        if (button == null) return;

        Component label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) label = button.GetComponentInChildren<Text>(true);
        if (label == null || label.GetComponent<LocalizedText>() != null) return;

        string current = label is TMP_Text tmp ? tmp.text : ((Text)label).text;
        if (string.Compare(current?.Trim(), UIBuilderKit.Ru(key), System.StringComparison.OrdinalIgnoreCase) != 0)
        {
            report.AppendLine($"• Подпись «{current}» не совпала с «{UIBuilderKit.Ru(key)}» ({key}) — переведите её через Extract Texts.");
            return;
        }

        var localized = Undo.AddComponent<LocalizedText>(label.gameObject);
        localized.key = key;
        SceneSetupUtility.MarkModified(localized);
        report.AppendLine($"✔ Подпись «{current}» переводится ({key}).");
    }

    /// <summary>Копия кнопки «Выход» (того же вида) перед ней. Обработчики из инспектора не копируются.</summary>
    private static Button CreateSettingsButton(MainMenuUI menu, StringBuilder report)
    {
        Button template = menu.quitButton != null ? menu.quitButton : menu.continueButton;
        if (template == null)
        {
            report.AppendLine("✖ У MainMenuUI не заданы кнопки — «Настройки» добавьте вручную и назначьте в settingsButton.");
            return null;
        }

        Transform parent = template.transform.parent;
        GameObject copy = Object.Instantiate(template.gameObject, parent);
        Undo.RegisterCreatedObjectUndo(copy, "Settings button");
        copy.name = "SettingsButton";
        copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex());

        var button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();

        // Подпись — старый UI.Text (как у остальных кнопок меню) или TMP; перевод через LocalizedText.
        Component label = copy.GetComponentInChildren<TMP_Text>(true);
        if (label == null) label = copy.GetComponentInChildren<Text>(true);
        if (label is TMP_Text tmp) tmp.text = UIBuilderKit.Ru("menu.settings");
        else if (label is Text legacy) legacy.text = UIBuilderKit.Ru("menu.settings");
        if (label != null)
        {
            LocalizedText localized = label.GetComponent<LocalizedText>();
            if (localized == null) localized = label.gameObject.AddComponent<LocalizedText>();
            localized.key = "menu.settings";
        }

        // Без LayoutGroup кнопки стоят по координатам: «Настройки» встаёт на место «Выхода», а тот
        // сдвигается на шаг между кнопками.
        if (parent != null && parent.GetComponent<LayoutGroup>() == null)
        {
            var templateRect = (RectTransform)template.transform;
            var copyRect = (RectTransform)copy.transform;
            Vector2 step = new Vector2(0f, -(templateRect.rect.height + 16f));
            if (menu.continueButton != null && menu.continueButton != template)
                step = templateRect.anchoredPosition - ((RectTransform)menu.continueButton.transform).anchoredPosition;

            Undo.RecordObject(templateRect, "Settings button");
            copyRect.anchoredPosition = templateRect.anchoredPosition;
            templateRect.anchoredPosition += step;
        }

        return button;
    }

    private static void SetupGameplay(Scene scene, SettingsWindow settingsWindow, StringBuilder report)
    {
        PauseMenu pause = SceneSetupUtility.EnsurePrefabInstance<PauseMenu>(scene, SettingsUIBuilder.PauseMenuPath, report);
        if (pause != null)
        {
            Undo.RecordObject(pause, "Pause menu");
            pause.settingsWindow = settingsWindow;
            if (pause.inputBlocker == null) pause.inputBlocker = SceneSetupUtility.FindInScene<GameplayInputBlocker>(scene);
            SceneSetupUtility.MarkModified(pause);

            if (pause.inputBlocker == null)
                report.AppendLine("• В сцене нет GameplayInputBlocker — на паузе не выключатся обзор мышью и стрельба.");

            foreach (CursorLockController cursor in SceneSetupUtility.FindAllInScene<CursorLockController>(scene))
            {
                Undo.RecordObject(cursor, "Pause menu");
                cursor.pauseMenu = pause;
                SceneSetupUtility.MarkModified(cursor);
            }
        }

        SceneSetupUtility.EnsureSceneObject<PostProcessSettingsApplier>(scene, "SettingsPostProcess", report);
    }

    /// <summary>Всем AudioSource сцены без канала — SFX. Музыку, окружение и звуки интерфейса переключите
    /// вручную в компоненте AudioChannelVolume.</summary>
    public static void AssignAudioChannels(Scene scene, StringBuilder report)
    {
        int added = 0;
        foreach (AudioSource source in SceneSetupUtility.FindAllInScene<AudioSource>(scene))
        {
            if (source.GetComponent<AudioChannelVolume>() != null) continue;
            Undo.AddComponent<AudioChannelVolume>(source.gameObject);
            added++;
        }

        report.AppendLine(added > 0
            ? $"✔ Канал SFX проставлен {added} источникам звука (музыку/окружение/UI переключите вручную)."
            : "• У всех источников звука канал уже есть.");
    }
}
