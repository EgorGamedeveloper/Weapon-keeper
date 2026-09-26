using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Инструменты локализации (меню Tools/Weapon Keeper/Localization):
/// - Extract Texts From Open Scene — тексты открытых сцен без LocalizedText уходят в strings.csv
///   (ключ scene.путь_к_объекту, текущий текст — в столбец ru, en пустой), на объект вешается LocalizedText;
/// - Validate — пустые ячейки, повторы ключей, ключи из сцен/префабов/данных/кода, которых нет в таблице;
/// - Check Font — есть ли в шрифте TMP по умолчанию (с запасными) все символы таблицы;
/// - Reload Strings — перечитать таблицу в Play Mode после правки CSV.
///
/// Тексты, которые пишет код (на них ссылается поле какого-нибудь скрипта игры: уровень, баланс и т.п.),
/// экстрактор не трогает — LocalizedText перезаписал бы их при смене языка. Их переводят в самом коде.
/// </summary>
public static class LocalizationEditorTools
{
    public const string StringsPath = "Assets/01_GAME/07_Localization/strings.csv";
    private const string MenuRoot = "Tools/Weapon Keeper/Localization/";

    // Префиксы ключей, которые код передаёт строковыми литералами (Loc.Get("settings.title") и т.п.) —
    // по ним Validate ищет в .cs ключи, которых нет в таблице.
    private static readonly Regex CodeKeyPattern =
        new Regex("\"((?:settings|pause|ach|achievements|menu|common|dialog|toast|lang|debug)\\.[a-z0-9_.]+)\"");

    // ───────────────────────── Extract ─────────────────────────

    [MenuItem(MenuRoot + "Extract Texts From Open Scene")]
    private static void ExtractTextsFromOpenScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Локализация", "Выйдите из Play Mode: изменения сцены в игре не сохраняются.", "OK");
            return;
        }

        StringsTable table = StringsTable.Load(StringsPath);
        int russianColumn = table.FindLanguageColumn(GameLanguages.Russian);
        if (russianColumn < 0)
        {
            EditorUtility.DisplayDialog("Локализация", $"В {StringsPath} нет столбца ru.", "OK");
            return;
        }

        HashSet<Component> codeDriven = CollectCodeDrivenTexts();
        var candidates = new List<(Component text, string value)>();
        int alreadyLocalized = 0, skippedCodeDriven = 0, skippedNoLetters = 0, skippedControls = 0;
        var codeDrivenPaths = new List<string>();

        foreach (Component text in EnumerateSceneTexts())
        {
            string value = GetText(text);
            if (text.GetComponent<LocalizedText>() != null) { alreadyLocalized++; continue; }
            if (string.IsNullOrWhiteSpace(value) || !HasLetters(value)) { skippedNoLetters++; continue; }
            if (IsPartOfInputControl(text.transform)) { skippedControls++; continue; }
            if (codeDriven.Contains(text))
            {
                skippedCodeDriven++;
                codeDrivenPaths.Add(GetPath(text.transform));
                continue;
            }
            candidates.Add((text, value));
        }

        string summary = $"Новых текстов: {candidates.Count}.\n" +
                         $"Уже с LocalizedText: {alreadyLocalized}.\n" +
                         $"Пишутся кодом (пропущены): {skippedCodeDriven}.\n" +
                         $"Без букв (цифры, символы): {skippedNoLetters}.\n" +
                         $"Внутри выпадающих списков и полей ввода: {skippedControls}.";

        if (codeDrivenPaths.Count > 0)
            Debug.Log("[Localization] Тексты, которые пишет код — их переводят в самом коде через Loc.Get:\n" +
                      string.Join("\n", codeDrivenPaths));

        if (candidates.Count == 0)
        {
            EditorUtility.DisplayDialog("Локализация", summary, "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Извлечь тексты в strings.csv?",
                summary + "\n\nКлючи — scene.путь_к_объекту, текст сцены уйдёт в столбец ru, перевод en нужно будет вписать.",
                "Извлечь", "Отмена"))
            return;

        var newKeys = new HashSet<string>();
        var log = new StringBuilder("[Localization] Извлечено в strings.csv:\n");
        foreach ((Component text, string value) in candidates)
        {
            string key = MakeUniqueKey(BuildKey(text), table, newKeys);
            newKeys.Add(key);

            var row = new string[table.ColumnCount];
            row[0] = key;
            row[russianColumn] = value;
            table.AppendRow(row);

            var localized = Undo.AddComponent<LocalizedText>(text.gameObject);
            localized.key = key;
            MarkModified(localized);
            EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
            log.Append(key).Append(" = ").Append(value.Replace("\n", "\\n")).Append('\n');
        }

        table.Save();
        AssetDatabase.ImportAsset(StringsPath, ImportAssetOptions.ForceUpdate);
        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("Локализация", $"Добавлено строк: {candidates.Count}. Впишите английский перевод в {StringsPath} " +
                                                   "и сохраните сцену.", "OK");
    }

    /// <summary>Тексты, на которые ссылаются поля скриптов игры, — их содержимое задаёт код.</summary>
    private static HashSet<Component> CollectCodeDrivenTexts()
    {
        var result = new HashSet<Component>();
        foreach (GameObject root in EnumerateSceneRoots())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is LocalizedText) continue;
                if (behaviour.GetType().Assembly.GetName().Name != "Assembly-CSharp") continue;

                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = property.propertyType != SerializedPropertyType.String;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue is TMP_Text tmp) result.Add(tmp);
                    else if (property.objectReferenceValue is Text legacy) result.Add(legacy);
                }
            }
        }
        return result;
    }

    // ───────────────────────── Validate ─────────────────────────

    [MenuItem(MenuRoot + "Validate")]
    private static void Validate()
    {
        StringsTable table = StringsTable.Load(StringsPath);
        var report = new StringBuilder();
        int problems = 0;

        if (table.ColumnCount < 2 || table.Header[0].Trim().ToLowerInvariant() != "key")
        {
            report.Append("• Первая строка должна быть «key,ru,en...».\n");
            problems++;
        }

        // Повторы ключей.
        var seen = new HashSet<string>();
        foreach (string key in table.Keys)
        {
            if (seen.Add(key)) continue;
            report.Append("• Повтор ключа: ").Append(key).Append('\n');
            problems++;
        }

        // Пустые ячейки по языкам.
        for (int column = 1; column < table.ColumnCount; column++)
        {
            var empty = new List<string>();
            foreach (string[] row in table.Rows)
                if (column >= row.Length || string.IsNullOrWhiteSpace(row[column])) empty.Add(row[0]);

            if (empty.Count == 0) continue;
            problems += empty.Count;
            report.Append($"• Пустые ячейки «{table.Header[column]}» ({empty.Count}): ")
                  .Append(string.Join(", ", empty)).Append('\n');
        }

        // Ключи, которые где-то используются, но которых нет в таблице.
        var keySet = new HashSet<string>(table.Keys);
        var missing = new SortedSet<string>();
        foreach ((string key, string source) in CollectUsedKeys())
            if (!keySet.Contains(key)) missing.Add($"{key}  ({source})");

        if (missing.Count > 0)
        {
            problems += missing.Count;
            report.Append($"• Ключи, которых нет в таблице ({missing.Count}):\n    ").Append(string.Join("\n    ", missing)).Append('\n');
        }

        string text = problems == 0
            ? $"strings.csv в порядке: {table.Rows.Count} ключей, языки: {string.Join(", ", table.LanguageColumns())}."
            : $"Найдено проблем: {problems}.\n\n{report}";

        if (problems == 0) Debug.Log("[Localization] " + text);
        else Debug.LogWarning("[Localization] " + text);

        EditorUtility.DisplayDialog("Проверка локализации",
            problems == 0 ? text : $"Найдено проблем: {problems}. Подробности — в консоли.", "OK");
    }

    /// <summary>Ключи из LocalizedText открытых сцен и префабов игры, из строковых полей *Key у данных
    /// (AchievementData.nameKey и т.п.) и из строковых литералов кода с известными префиксами.</summary>
    private static IEnumerable<(string key, string source)> CollectUsedKeys()
    {
        foreach (GameObject root in EnumerateSceneRoots())
            foreach (LocalizedText text in root.GetComponentsInChildren<LocalizedText>(true))
                if (!string.IsNullOrEmpty(text.key)) yield return (text.key, "сцена " + text.gameObject.scene.name + ": " + GetPath(text.transform));

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/01_GAME" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            foreach (LocalizedText text in prefab.GetComponentsInChildren<LocalizedText>(true))
                if (!string.IsNullOrEmpty(text.key)) yield return (text.key, path);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/01_GAME/04_Data" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) continue;

            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.String;
                if (property.propertyType == SerializedPropertyType.String && property.name.EndsWith("Key")
                    && !string.IsNullOrEmpty(property.stringValue))
                    yield return (property.stringValue, path);
            }
        }

        string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/01_GAME/01_Scripts");
        foreach (string file in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/Editor/")) continue;
            foreach (Match match in CodeKeyPattern.Matches(File.ReadAllText(file)))
            {
                string key = match.Groups[1].Value;
                if (!LooksLikeFileName(key)) yield return (key, Path.GetFileName(file));
            }
        }
    }

    /// <summary>«settings.cfg», «achievements.sav» — имена файлов, а не ключи строк.</summary>
    private static bool LooksLikeFileName(string literal)
    {
        string extension = Path.GetExtension(literal);
        return extension == ".cfg" || extension == ".sav" || extension == ".csv" || extension == ".json"
               || extension == ".txt" || extension == ".asset" || extension == ".prefab" || extension == ".tmp";
    }

    // ───────────────────────── Font ─────────────────────────

    [MenuItem(MenuRoot + "Check Font (Cyrillic + Latin)")]
    private static void CheckFont()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            EditorUtility.DisplayDialog("Шрифт", "В TMP Settings не задан шрифт по умолчанию (Default Font Asset).", "OK");
            return;
        }

        // Алфавиты целиком + всё, что реально встречается в таблице.
        var characters = new SortedSet<char>();
        const string alphabets = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
                                 "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
                                 ".,:;!?-–—«»\"'()[]%$№+×/‹›…";
        foreach (char c in alphabets) characters.Add(c);
        StringsTable table = StringsTable.Load(StringsPath);
        foreach (string[] row in table.Rows)
            for (int column = 1; column < row.Length; column++)
                foreach (char c in row[column])
                    if (!char.IsWhiteSpace(c) && !char.IsControl(c)) characters.Add(c);

        var text = new StringBuilder();
        foreach (char c in characters) text.Append(c);

        // tryAddCharacter: динамические шрифты (как «LiberationSans SDF - Fallback») добирают глифы на лету —
        // так же, как это произойдёт в игре.
        bool ok = font.HasCharacters(text.ToString(), out uint[] missing, true, true);
        if (ok)
        {
            string message = $"Шрифт «{font.name}» (с запасными) содержит все {characters.Count} символов таблицы, включая кириллицу и латиницу.";
            Debug.Log("[Localization] " + message);
            EditorUtility.DisplayDialog("Шрифт", message, "OK");
            return;
        }

        var missingText = new StringBuilder();
        foreach (uint code in missing) missingText.Append(char.ConvertFromUtf32((int)code));
        string warning = $"В шрифте «{font.name}» и его запасных нет символов: {missingText}\n\n" +
                         "Добавьте в Fallback Font Assets шрифт с этими символами (например, динамический TMP-ассет " +
                         "из LiberationSans.ttf или Roboto) или назначьте другой Default Font Asset в TMP Settings.";
        Debug.LogWarning("[Localization] " + warning);
        EditorUtility.DisplayDialog("Шрифт", warning, "OK");
    }

    // ───────────────────────── Reload ─────────────────────────

    [MenuItem(MenuRoot + "Reload Strings (Play Mode)")]
    private static void ReloadStrings()
    {
        AssetDatabase.ImportAsset(StringsPath, ImportAssetOptions.ForceUpdate);
        if (LocalizationService.Instance != null) LocalizationService.Instance.Reload();
    }

    [MenuItem(MenuRoot + "Reload Strings (Play Mode)", true)]
    private static bool ReloadStringsValidate() => EditorApplication.isPlaying;

    // ───────────────────────── Общее ─────────────────────────

    private static IEnumerable<GameObject> EnumerateSceneRoots()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) yield return root;
        }
    }

    private static IEnumerable<Component> EnumerateSceneTexts()
    {
        foreach (GameObject root in EnumerateSceneRoots())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) yield return text;
            foreach (Text text in root.GetComponentsInChildren<Text>(true)) yield return text;
        }
    }

    private static string GetText(Component component)
    {
        if (component is TMP_Text tmp) return tmp.text;
        if (component is Text legacy) return legacy.text;
        return null;
    }

    private static bool HasLetters(string value)
    {
        foreach (char c in value)
            if (char.IsLetter(c)) return true;
        return false;
    }

    /// <summary>Подписи выпадающих списков и поля ввода заполняет сам контрол — их не переводим.</summary>
    private static bool IsPartOfInputControl(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.GetComponent<TMP_Dropdown>() != null || current.GetComponent<Dropdown>() != null) return true;
            if (current.GetComponent<TMP_InputField>() != null || current.GetComponent<InputField>() != null) return true;
        }
        return false;
    }

    private static string GetPath(Transform transform)
    {
        var parts = new List<string>();
        for (Transform current = transform; current != null; current = current.parent) parts.Insert(0, current.name);
        return string.Join("/", parts);
    }

    /// <summary>Ключ «сцена.путь.к.объекту» из латиницы, цифр и подчёркиваний.</summary>
    private static string BuildKey(Component text)
    {
        var parts = new List<string> { Sanitize(text.gameObject.scene.name) };
        var path = new List<string>();
        for (Transform current = text.transform; current != null; current = current.parent) path.Insert(0, Sanitize(current.name));
        parts.AddRange(path);
        return string.Join(".", parts);
    }

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char c in name.ToLowerInvariant())
        {
            bool latinOrDigit = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
            if (latinOrDigit) builder.Append(c);
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_') builder.Append('_');
        }
        string result = builder.ToString().Trim('_');
        return result.Length > 0 ? result : "obj";
    }

    private static string MakeUniqueKey(string baseKey, StringsTable table, HashSet<string> pending)
    {
        string key = baseKey;
        for (int suffix = 2; table.HasKey(key) || pending.Contains(key); suffix++) key = baseKey + "_" + suffix;
        return key;
    }

    private static void MarkModified(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    /// <summary>strings.csv как таблица: чтение, добавление строк в конец, запись UTF-8 без BOM.
    /// Существующие строки файла при записи не переформатируются — новые дописываются в конец.</summary>
    private class StringsTable
    {
        public List<string> Header { get; private set; } = new List<string>();
        public List<string[]> Rows { get; } = new List<string[]>();
        public int ColumnCount => Header.Count;

        public IEnumerable<string> Keys
        {
            get { foreach (string[] row in Rows) yield return row[0]; }
        }

        private string path;
        private string originalText = "";
        private readonly List<string[]> appended = new List<string[]>();

        public static StringsTable Load(string assetPath)
        {
            var table = new StringsTable { path = assetPath };
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (File.Exists(fullPath)) table.originalText = File.ReadAllText(fullPath, Encoding.UTF8);
            if (table.originalText.Length == 0) table.originalText = "key,ru,en\n";

            List<List<string>> parsed = CsvUtility.Parse(table.originalText);
            if (parsed.Count > 0) table.Header = parsed[0];
            for (int i = 1; i < parsed.Count; i++)
            {
                string key = parsed[i][0].Trim();
                if (key.Length == 0 || key[0] == '#') continue;
                var row = parsed[i].ToArray();
                row[0] = key;
                table.Rows.Add(row);
            }
            return table;
        }

        public bool HasKey(string key)
        {
            foreach (string[] row in Rows)
                if (row[0] == key) return true;
            return false;
        }

        public int FindLanguageColumn(string steamCode)
        {
            for (int column = 1; column < Header.Count; column++)
                if (GameLanguages.ColumnToSteamCode(Header[column]) == steamCode) return column;
            return -1;
        }

        public IEnumerable<string> LanguageColumns()
        {
            for (int column = 1; column < Header.Count; column++) yield return Header[column];
        }

        public void AppendRow(string[] row)
        {
            for (int i = 0; i < row.Length; i++) row[i] = row[i] ?? "";
            appended.Add(row);
            Rows.Add(row);
        }

        public void Save()
        {
            var builder = new StringBuilder(originalText);
            if (builder.Length > 0 && builder[builder.Length - 1] != '\n') builder.Append('\n');
            foreach (string[] row in appended) builder.Append(CsvUtility.FormatRow(row)).Append('\n');

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, builder.ToString(), new UTF8Encoding(false));
            originalText = builder.ToString();
            appended.Clear();
        }
    }
}
