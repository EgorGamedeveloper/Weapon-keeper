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

    private void Awake()
    {
        if (active != null && active != this)
        {
            Destroy(gameObject);
            return;
        }

        active = this;
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
        SceneManager.LoadScene(gameplaySceneName);
    }

    /// <summary>Продолжить с сохранения. Без файла сейва ничего не делает.</summary>
    public void ContinueGame()
    {
        if (!SaveFileService.HasSave()) return;

        LoadSaveOnStart = true;
        SceneManager.LoadScene(gameplaySceneName);
    }
}
