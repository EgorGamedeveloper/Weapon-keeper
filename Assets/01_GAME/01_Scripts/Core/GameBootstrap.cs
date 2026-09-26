using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Точка входа в игру. Живёт в сцене Bootstrap (индекс 0 в Build Settings), переживает загрузку
/// уровней через DontDestroyOnLoad и держит то, что нужно игре целиком: конфиг, каталог предметов
/// и решение «начать заново или продолжить».
///
/// Глобальной статической точки доступа сознательно нет: потребители находят бутстрап через
/// FindFirstObjectByType — тем же приёмом, каким в проекте уже пользуются оба трекера прогресса.
/// Статическое поле здесь только одно и приватное — защита от второго экземпляра, без которой
/// DontDestroyOnLoad-объект размножался бы при возврате в меню.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    private static GameBootstrap active;

    [Header("Данные игры")]
    [Tooltip("Общий конфиг: дальности взаимодействия, покачивание предмета, клавиши, пороги прогресса.")]
    public GameConfig config;

    [Tooltip("Каталог предметов: по нему сейв превращает строковые id обратно в ItemData.")]
    public ItemCatalog itemCatalog;

    [Header("Сцены")]
    [Tooltip("Имя игровой сцены, которую запускает меню.")]
    public string gameplaySceneName = "TestScene";

    /// <summary>Нажали «Продолжить» — игровая сцена должна применить сейв вместо старта с нуля.</summary>
    public bool LoadSaveOnStart { get; private set; }

    /// <summary>Сейв, прочитанный и проверенный в ContinueGame; забирается SaveLoadService.</summary>
    private SaveGameData pendingSave;

    /// <summary>Сцена меню — та, в которой лежит бутстрап (запоминается до DontDestroyOnLoad).</summary>
    private string menuSceneName;

    private void Awake()
    {
        if (active != null && active != this)
        {
            Destroy(gameObject);
            return;
        }

        active = this;
        menuSceneName = gameObject.scene.name;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
    }

    /// <summary>
    /// Начать заново. Файл сейва намеренно НЕ удаляется: он будет перезаписан ближайшим
    /// автосохранением. Так случайный клик по «Новой игре» не уничтожает прогресс мгновенно
    /// и без предупреждения.
    /// </summary>
    public void StartNewGame()
    {
        LoadSaveOnStart = false;
        pendingSave = null;
        SceneManager.LoadScene(gameplaySceneName);
    }

    /// <summary>
    /// Продолжить с сохранения. Сейв читается здесь один раз: битый файл или сейв из более новой
    /// версии игры не запускают уровень вовсе (иначе ближайший автосейв перезаписал бы его пустым
    /// состоянием). Уровень — тот, в котором сохранялись, если он есть в сборке.
    /// Возвращает false, если продолжать не с чего.
    /// </summary>
    public bool ContinueGame()
    {
        SaveGameData data = SaveFileService.Load();
        if (data == null) return false;

        if (data.version > SaveGameData.CurrentVersion)
        {
            Debug.LogError($"[GameBootstrap] Сейв версии {data.version} новее поддерживаемой ({SaveGameData.CurrentVersion}) — загрузка отменена.");
            return false;
        }

        pendingSave = data;
        LoadSaveOnStart = true;

        string sceneName = !string.IsNullOrEmpty(data.sceneName) && Application.CanStreamedLevelBeLoaded(data.sceneName)
            ? data.sceneName
            : gameplaySceneName;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    /// <summary>
    /// Выйти из уровня в главное меню (меню паузы). Прогресс сохраняется тем же SaveLoadService.SaveNow, что
    /// и автосейв при закрытии игры (без Bootstrap сохранение выключено — это делает сам SaveLoadService).
    ///
    /// Сцена меню содержит свой GameBootstrap, поэтому этот уходит: иначе новый уничтожил бы себя как
    /// дубликат, а кнопки меню остались бы со ссылкой на него. Уничтожение отложено до конца кадра, а
    /// загрузка сцены — до следующего, так что меню встретит уже нового, живого бутстрапа.
    /// </summary>
    public void ReturnToMainMenu()
    {
        SaveLoadService saveLoad = FindAnyObjectByType<SaveLoadService>();
        if (saveLoad != null) saveLoad.SaveNow();

        LoadSaveOnStart = false;
        pendingSave = null;
        if (active == this) active = null;

        if (!string.IsNullOrEmpty(menuSceneName) && Application.CanStreamedLevelBeLoaded(menuSceneName))
            SceneManager.LoadScene(menuSceneName);
        else
            SceneManager.LoadScene(0);
        Destroy(gameObject);
    }

    /// <summary>Отдать сейв, прочитанный в ContinueGame (один раз — повторный вызов вернёт null).</summary>
    public SaveGameData TakePendingSave()
    {
        SaveGameData data = pendingSave;
        pendingSave = null;
        return data;
    }
}
