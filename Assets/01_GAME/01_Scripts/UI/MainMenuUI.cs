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

    public Button newGameButton;
    public Button continueButton;
    public Button quitButton;

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
            continueButton.interactable = SaveFileService.HasSave();
            continueButton.onClick.AddListener(HandleContinue);
        }

        if (newGameButton != null) newGameButton.onClick.AddListener(HandleNewGame);
        if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
    }

    private void OnDisable()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveListener(HandleNewGame);
        if (continueButton != null) continueButton.onClick.RemoveListener(HandleContinue);
        if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuit);
    }

    private void HandleNewGame()
    {
        if (bootstrap != null) bootstrap.StartNewGame();
    }

    private void HandleContinue()
    {
        if (bootstrap != null) bootstrap.ContinueGame();
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
