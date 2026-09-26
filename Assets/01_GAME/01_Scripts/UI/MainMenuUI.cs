using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главное меню: новая игра / продолжить / выход. Живёт в сцене Bootstrap рядом с GameBootstrap,
/// всю работу делегирует ему — меню знает только про кнопки.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Если не заполнено — ищется в сцене при старте.")]
    public GameBootstrap bootstrap;

    [Tooltip("Кнопка «Новая игра».")]
    public Button newGameButton;
    [Tooltip("Кнопка «Продолжить» — недоступна, пока нет сейва.")]
    public Button continueButton;
    [Tooltip("Кнопка «Выход».")]
    public Button quitButton;

    [Header("Кнопка «Продолжить»: явный серый, когда сейва нет")]
    [Tooltip("Стандартный ColorBlock.disabledColor тонирует УМНОЖЕНИЕМ поверх обычного цвета кнопки — " +
             "на тёмном фоне разница почти не видна. Красим фон кнопки напрямую, а не полагаемся на него.")]
    public Color continueActiveColor = new Color(0.16f, 0.16f, 0.18f, 1f);
    [Tooltip("Цвет фона «Продолжить», когда сейва нет.")]
    public Color continueDisabledColor = new Color(0.24f, 0.24f, 0.26f, 0.5f);

    [Header("Подтверждение новой игры")]
    [Tooltip("Панель «Начать заново? Сохранённый прогресс будет перезаписан» — показывается, только если сейв " +
             "уже есть. Пусто — новая игра начинается без вопроса.")]
    public GameObject confirmNewGamePanel;

    [Tooltip("Кнопка «Да, начать заново» на панели подтверждения.")]
    public Button confirmYesButton;

    [Tooltip("Кнопка «Отмена» на панели подтверждения.")]
    public Button confirmNoButton;

    private void Awake()
    {
        if (bootstrap == null) bootstrap = FindFirstObjectByType<GameBootstrap>();
    }

    private void OnEnable()
    {
        // В игре курсор захвачен контроллером от первого лица — в меню его надо вернуть,
        // иначе по кнопкам нечем кликать.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // «Продолжить» недоступно, пока сейва нет: живая кнопка, которая ничего не делает,
        // выглядит как сломанная игра.
        if (continueButton != null)
        {
            SetContinueAvailable(SaveFileService.HasSave());
            continueButton.onClick.AddListener(HandleContinue);
        }

        if (newGameButton != null) newGameButton.onClick.AddListener(HandleNewGame);
        if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);

        if (confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(HandleConfirmNewGame);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(HandleCancelNewGame);
    }

    private void OnDisable()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveListener(HandleNewGame);
        if (continueButton != null) continueButton.onClick.RemoveListener(HandleContinue);
        if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuit);
        if (confirmYesButton != null) confirmYesButton.onClick.RemoveListener(HandleConfirmNewGame);
        if (confirmNoButton != null) confirmNoButton.onClick.RemoveListener(HandleCancelNewGame);
    }

    private void SetContinueAvailable(bool available)
    {
        continueButton.interactable = available;
        var background = continueButton.GetComponent<Image>();
        if (background != null) background.color = available ? continueActiveColor : continueDisabledColor;
    }

    private void HandleNewGame()
    {
        // Файл сейва «Новая игра» не удаляет, но первый же автосейв его перезапишет — спрашиваем.
        if (confirmNewGamePanel != null && SaveFileService.HasSave())
        {
            confirmNewGamePanel.SetActive(true);
            return;
        }

        if (bootstrap != null) bootstrap.StartNewGame();
    }

    private void HandleConfirmNewGame()
    {
        if (confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);
        if (bootstrap != null) bootstrap.StartNewGame();
    }

    private void HandleCancelNewGame()
    {
        if (confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);
    }

    private void HandleContinue()
    {
        // Сейв битый или из более новой версии игры — уровень не грузится, кнопка гаснет.
        if (bootstrap != null && !bootstrap.ContinueGame() && continueButton != null)
            SetContinueAvailable(false);
    }

    private void HandleQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
