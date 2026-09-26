using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Меню паузы в игровой сцене: продолжить / настройки / выйти в главное меню / выйти из игры.
///
/// Esc открывает паузу, только если в прошлом кадре курсор был захвачен: значит, игрок в игре, а не в
/// другом окне (навыки, терминал — те сами закрываются по Esc и держат GameplayInputBlocker). Состояние
/// берётся из прошлого кадра (LateUpdate), потому что редактор сам освобождает курсор по Esc ещё до
/// Update. DefaultExecutionOrder(-100): пауза читает Esc раньше окон, открытых поверх неё (настройки
/// закрываются своим Update позже в том же кадре), и раньше CursorLockController (1000), который
/// иначе освобождал бы курсор тем же нажатием, которым пауза закрылась (см. HandledEscapeThisFrame).
///
/// Пауза: Time.timeScale = 0, AudioListener.pause, игровой ввод выключен тем же GameplayInputBlocker,
/// что у окна навыков и терминала (они же не откроются поверх паузы). Все анимации — в реальном времени.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    [Header("Окно")]
    [Tooltip("Корень меню (затемнение + панель) — включается при открытии, проявляется и гаснет.")]
    public CanvasGroup windowGroup;

    [Tooltip("Панель меню — «выпрыгивает» при открытии.")]
    public RectTransform panel;

    [Header("Кнопки")]
    [Tooltip("Продолжить игру.")]
    public Button resumeButton;

    [Tooltip("Открыть окно настроек.")]
    public Button settingsButton;

    [Tooltip("Открыть список достижений. Без окна достижений кнопка скрывается.")]
    public Button achievementsButton;

    [Tooltip("Выйти в главное меню (с сохранением).")]
    public Button mainMenuButton;

    [Tooltip("Выйти из игры (с сохранением).")]
    public Button quitButton;

    [Header("Окна")]
    [Tooltip("Окно настроек (префаб SettingsWindow в этой же сцене).")]
    public SettingsWindow settingsWindow;

    [Tooltip("Диалог подтверждения выхода.")]
    public ConfirmDialog dialog;

    [Header("Игра")]
    [Tooltip("Общая блокировка игрового ввода (как у окна навыков и терминала). Пусто — пауза сама только " +
             "освобождает курсор, а обзор мышью и стрельба не выключаются.")]
    public GameplayInputBlocker inputBlocker;

    [Tooltip("Клавиша паузы.")]
    public KeyCode pauseKey = KeyCode.Escape;

    /// <summary>Меню паузы открыто (игра стоит).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>В этом кадре Esc уже обработан паузой — CursorLockController его пропускает.</summary>
    public bool HandledEscapeThisFrame => escapeFrame == Time.frameCount;

    /// <summary>Пауза включилась (true) или выключилась (false).</summary>
    public event Action<bool> OnPauseChanged;

    private float timeScaleBeforePause = 1f;
    private bool audioPausedBeforePause;
    private bool cursorLockedLastFrame;
    private int escapeFrame = -1;

    private void Awake()
    {
        UiWindowAnimation.HideImmediate(windowGroup);

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(AskQuitToMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(AskQuitGame);
        if (achievementsButton != null) achievementsButton.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // Сцену выгружают прямо на паузе (выход в меню) — время и звук не должны остаться остановленными.
        if (!IsOpen) return;
        IsOpen = false;
        RestoreGameState();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(pauseKey)) return;

        if (!IsOpen)
        {
            if (!CanOpenFromKeyboard()) return;
            escapeFrame = Time.frameCount;
            Open();
            return;
        }

        // Окно настроек поверх паузы закроется своим Update — позже в этом же кадре.
        if (settingsWindow != null && settingsWindow.IsOpen) return;

        escapeFrame = Time.frameCount;
        if (dialog != null && dialog.IsOpen) dialog.Cancel();
        else Resume();
    }

    private void LateUpdate() => cursorLockedLastFrame = Cursor.lockState == CursorLockMode.Locked;

    private bool CanOpenFromKeyboard() => cursorLockedLastFrame && (inputBlocker == null || !inputBlocker.IsBlocked);

    // ───────────────────────── Открытие / закрытие ─────────────────────────

    /// <summary>Поставить игру на паузу и показать меню.</summary>
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        audioPausedBeforePause = AudioListener.pause;
        AudioListener.pause = true;

        if (inputBlocker != null) inputBlocker.Acquire(this);
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        UiWindowAnimation.Show(windowGroup, panel);
        OnPauseChanged?.Invoke(true);
    }

    /// <summary>Снять паузу.</summary>
    public void Resume()
    {
        if (!IsOpen) return;
        if (settingsWindow != null && settingsWindow.IsOpen) settingsWindow.Close();
        if (dialog != null && dialog.IsOpen) dialog.Cancel();

        IsOpen = false;
        UiWindowAnimation.Hide(windowGroup, panel);
        RestoreGameState();
        OnPauseChanged?.Invoke(false);
    }

    private void RestoreGameState()
    {
        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        AudioListener.pause = audioPausedBeforePause;

        if (inputBlocker != null) inputBlocker.Release(this);
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ───────────────────────── Кнопки ─────────────────────────

    private void OpenSettings()
    {
        if (settingsWindow != null) settingsWindow.Open();
    }

    private void AskQuitToMainMenu()
    {
        if (dialog == null)
        {
            QuitToMainMenu();
            return;
        }

        // Без Bootstrap (Play прямо на игровой сцене) сохранение выключено — честно предупреждаем.
        bool saves = FindAnyObjectByType<GameBootstrap>() != null;
        dialog.Show(Loc.Get("pause.to_menu.title"), Loc.Get(saves ? "pause.to_menu.message" : "pause.to_menu.message_no_save"),
            Loc.Get("pause.to_menu.confirm"), QuitToMainMenu, Loc.Get("common.cancel"));
    }

    private void AskQuitGame()
    {
        if (dialog == null)
        {
            QuitGame();
            return;
        }

        dialog.Show(Loc.Get("pause.quit.title"), Loc.Get("pause.quit.message"),
            Loc.Get("pause.quit.confirm"), QuitGame, Loc.Get("common.cancel"));
    }

    private void QuitToMainMenu()
    {
        // Время и курсор — до загрузки меню: DontDestroyOnLoad-сервисы переживут сцену, а timeScale глобален.
        if (IsOpen)
        {
            IsOpen = false;
            RestoreGameState();
            OnPauseChanged?.Invoke(false);
        }

        GameBootstrap bootstrap = FindAnyObjectByType<GameBootstrap>();
        if (bootstrap != null) bootstrap.ReturnToMainMenu();
        else SceneManager.LoadScene(0); // Play прямо на игровой сцене: меню — первая сцена в Build Settings.
    }

    private void QuitGame()
    {
        // Сейв при выходе делает SaveLoadService.OnApplicationQuit.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
