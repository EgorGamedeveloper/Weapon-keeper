using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Локализация на текстовой таблице strings.csv (UTF-8, первая строка — key и столбцы языков: ru, en...).
/// Пакет Unity Localization не подошёл: его таблицы — бинарные .asset, их не поправить текстом и не
/// слить в git без конфликтов.
///
/// Язык обозначается кодом Steam API (russian, english) — см. GameLanguages. Какой язык включить,
/// решает SettingsService (сохранённый выбор игрока, при первом запуске — DetectDefaultLanguage).
///
/// Строки без перевода: текущий язык → fallbackLanguage (английский) → сам ключ и одно предупреждение
/// в лог на ключ — чтобы пропуск был виден, но не заспамил консоль.
///
/// Статический Instance — осознанное исключение из правила «без синглтонов»: сервис живёт в
/// PersistentServices через все сцены, а читают его сотни LocalizedText и код любых окон. Искать его
/// через FindFirstObjectByType из каждого текста — лишний проход по сцене на каждый OnEnable, а защита
/// от второго экземпляра (дубликат приезжает с каждой сценой) всё равно требует статического поля.
/// </summary>
[DefaultExecutionOrder(-1900)]
[DisallowMultipleComponent]
public class LocalizationService : MonoBehaviour
{
    /// <summary>Живой экземпляр сервиса (null, если в игре его нет — тексты тогда остаются как в сцене).</summary>
    public static LocalizationService Instance { get; private set; }

    [Header("Таблица строк")]
    [Tooltip("strings.csv (Assets/01_GAME/07_Localization). Первая строка — key и заголовки столбцов языков " +
             "(ru, en...), дальше — по строке на ключ. Экранирование — по RFC 4180, «\\n» внутри ячейки — перевод строки.")]
    public TextAsset stringsTable;

    [Tooltip("Язык, на который откатывается строка без перевода (код Steam: english, russian...).")]
    public string fallbackLanguage = GameLanguages.English;

    /// <summary>Текущий язык — код Steam (russian, english...).</summary>
    public string CurrentLanguage { get; private set; }

    /// <summary>Культура текущего языка — форматирование чисел в строках с параметрами.</summary>
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

    /// <summary>Язык сменился — тексты должны перечитать строки.</summary>
    public event Action OnLanguageChanged;

    /// <summary>Языки таблицы (коды Steam) в порядке столбцов.</summary>
    public IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            EnsureLoaded();
            return columnLanguages;
        }
    }

    // Ключ → значения по столбцам языков (индекс = столбец таблицы минус один).
    private readonly Dictionary<string, string[]> strings = new Dictionary<string, string[]>();
    private readonly List<string> columnLanguages = new List<string>();
    private readonly HashSet<string> warnedKeys = new HashSet<string>();
    private int currentColumn = -1;
    private int fallbackColumn = -1;
    private bool loaded;

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
        EnsureLoaded();

        // До SettingsService (он применит сохранённый язык в своём Awake) — разумный язык по умолчанию,
        // чтобы тексты, проснувшиеся раньше, не остались на ключах.
        if (string.IsNullOrEmpty(CurrentLanguage)) SetLanguage(DetectDefaultLanguage());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Разобрать таблицу, если это ещё не сделано. Можно звать до Awake (порядок сервисов
    /// на одном объекте задаёт DefaultExecutionOrder, но защититься от ошибки разметки дёшево).</summary>
    public void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        strings.Clear();
        columnLanguages.Clear();

        if (stringsTable == null)
        {
            Debug.LogWarning("[Localization] Не назначена таблица strings.csv — тексты останутся такими, как в сцене.", this);
            return;
        }

        List<List<string>> table = CsvUtility.Parse(stringsTable.text);
        if (table.Count == 0)
        {
            Debug.LogWarning("[Localization] strings.csv пуст.", this);
            return;
        }

        List<string> header = table[0];
        for (int column = 1; column < header.Count; column++)
            columnLanguages.Add(GameLanguages.ColumnToSteamCode(header[column]));

        for (int r = 1; r < table.Count; r++)
        {
            List<string> row = table[r];
            string key = row[0].Trim();
            // Пустой ключ или «#...» — комментарий/разделитель секций.
            if (key.Length == 0 || key[0] == '#') continue;

            var values = new string[columnLanguages.Count];
            for (int column = 0; column < values.Length; column++)
                values[column] = column + 1 < row.Count ? Unescape(row[column + 1]) : "";

            if (strings.ContainsKey(key))
                Debug.LogWarning($"[Localization] Ключ '{key}' повторяется в strings.csv — используется последнее значение.", this);
            strings[key] = values;
        }

        fallbackColumn = columnLanguages.IndexOf(fallbackLanguage);
        currentColumn = string.IsNullOrEmpty(CurrentLanguage) ? -1 : columnLanguages.IndexOf(CurrentLanguage);
    }

    /// <summary>Перечитать таблицу (после правки strings.csv в редакторе) и обновить все тексты.</summary>
    public void Reload()
    {
        loaded = false;
        warnedKeys.Clear();
        EnsureLoaded();
        OnLanguageChanged?.Invoke();
    }

    /// <summary>Есть ли такой язык в таблице.</summary>
    public bool IsSupported(string languageCode)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(languageCode) && columnLanguages.Contains(languageCode);
    }

    /// <summary>Есть ли такой ключ в таблице.</summary>
    public bool HasKey(string key)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(key) && strings.ContainsKey(key);
    }

    /// <summary>Строка по ключу на текущем языке (см. правила отката у класса).</summary>
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        EnsureLoaded();

        if (strings.TryGetValue(key, out string[] values))
        {
            if (currentColumn >= 0 && !string.IsNullOrEmpty(values[currentColumn])) return values[currentColumn];
            if (fallbackColumn >= 0 && !string.IsNullOrEmpty(values[fallbackColumn])) return values[fallbackColumn];
        }

        if (warnedKeys.Add(key))
            Debug.LogWarning($"[Localization] Нет строки '{key}' ни для '{CurrentLanguage}', ни для '{fallbackLanguage}' — показан ключ.");
        return key;
    }

    /// <summary>
    /// Строка по ключу на текущем языке, иначе на запасном — без предупреждения, если её нет. Для текстов
    /// данных (названия предметов, квестов...): пока их не выгрузили в таблицу, показывается текст из ассета.
    /// </summary>
    public bool TryGet(string key, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        EnsureLoaded();

        if (!strings.TryGetValue(key, out string[] values)) return false;
        if (currentColumn >= 0 && !string.IsNullOrEmpty(values[currentColumn])) value = values[currentColumn];
        else if (fallbackColumn >= 0 && !string.IsNullOrEmpty(values[fallbackColumn])) value = values[fallbackColumn];
        return value != null;
    }

    /// <summary>Строка по ключу с подстановкой параметров ({0}, {1:0.0}...) в культуре текущего языка.</summary>
    public string Get(string key, params object[] args)
    {
        string format = Get(key);
        if (args == null || args.Length == 0) return format;

        try
        {
            return string.Format(CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            if (warnedKeys.Add("format:" + key))
                Debug.LogWarning($"[Localization] Строка '{key}' не подходит под параметры ({args.Length}): «{format}».");
            return format;
        }
    }

    /// <summary>Переключить язык (код Steam). Незнакомый язык заменяется запасным. Тексты обновляются
    /// сразу — перезапуск не нужен.</summary>
    public void SetLanguage(string languageCode)
    {
        EnsureLoaded();

        if (!IsSupported(languageCode))
        {
            string replacement = IsSupported(fallbackLanguage) ? fallbackLanguage
                : columnLanguages.Count > 0 ? columnLanguages[0] : languageCode;
            if (!string.IsNullOrEmpty(languageCode) && languageCode != replacement)
                Debug.LogWarning($"[Localization] Языка '{languageCode}' нет в strings.csv — включён '{replacement}'.");
            languageCode = replacement;
        }

        if (languageCode == CurrentLanguage) return;

        CurrentLanguage = languageCode;
        currentColumn = columnLanguages.IndexOf(languageCode);
        CurrentCulture = GameLanguages.GetCulture(languageCode);
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Язык по умолчанию для первого запуска: язык игры в Steam → язык системы → fallbackLanguage —
    /// из тех, что есть в таблице.
    /// </summary>
    public string DetectDefaultLanguage()
    {
        EnsureLoaded();

        // SteamManager инициализируется раньше (DefaultExecutionOrder -2000), язык Steam уже известен.
        string steamLanguage = SteamManager.Instance != null ? SteamManager.Instance.GetCurrentGameLanguage() : null;
        if (IsSupported(steamLanguage)) return steamLanguage;

        if (GameLanguages.TryFromSystemLanguage(Application.systemLanguage, out GameLanguages.Info system)
            && IsSupported(system.steamCode))
            return system.steamCode;

        if (IsSupported(fallbackLanguage)) return fallbackLanguage;
        return columnLanguages.Count > 0 ? columnLanguages[0] : fallbackLanguage;
    }

    /// <summary>«\n», набранное в ячейке двумя символами, — перевод строки (переводчики так пишут чаще,
    /// чем переносят строку внутри кавычек).</summary>
    private static string Unescape(string value) =>
        string.IsNullOrEmpty(value) ? "" : value.Replace("\\n", "\n");
}
