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
/// - Export Data Texts — названия и описания из ассетов данных (предметы, квесты, навыки, ящики, заказы) —
///   в strings.csv, ключи из id ассетов;
/// - Validate — пустые ячейки, готовность языков, повторы ключей, ключи из сцен/префабов/данных/кода,
///   которых нет в таблице, невыгруженные тексты данных;
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
        new Regex("\"((?:settings|pause|ach|achievements|menu|common|dialog|toast|lang|debug|skills|terminal|hud|interact)\\.[a-z0-9_.]+)\"");

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

    /// <summary>
    /// Тексты, содержимое которых задаёт код: на них ссылается поле скрипта игры, и исходник этого скрипта
    /// пишет в поле текст (field.text = ..., SetText(field ...), передаёт поле в метод). Надпись, которую код
    /// только показывает и прячет (field.gameObject.SetActive — «Нет доставок» в терминале), остаётся
    /// статичной — её экстрактор переводит. Если исходник скрипта не нашёлся, текст на всякий случай
    /// считается кодовым.
    /// </summary>
    private static HashSet<Component> CollectCodeDrivenTexts()
    {
        var result = new HashSet<Component>();
        var sources = new Dictionary<System.Type, string>();
        foreach (GameObject root in EnumerateSceneRoots())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is LocalizedText) continue;
                if (behaviour.GetType().Assembly.GetName().Name != "Assembly-CSharp") continue;

                string source = GetSourceChain(behaviour.GetType(), sources);
                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = property.propertyType != SerializedPropertyType.String;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                    Component text = property.objectReferenceValue as TMP_Text;
                    if (text == null) text = property.objectReferenceValue as Text;
                    if (text == null) continue;

                    string field = property.propertyPath.Split('.')[0];
                    if (source == null || WritesText(source, field)) result.Add(text);
                }
            }
        }
        return result;
    }

    /// <summary>Исходники класса и его базовых классов из Assembly-CSharp (null — какой-то не нашёлся).</summary>
    private static string GetSourceChain(System.Type type, Dictionary<System.Type, string> cache)
    {
        if (cache.TryGetValue(type, out string cached)) return cached;

        var builder = new StringBuilder();
        for (System.Type current = type; current != null && current.Assembly == type.Assembly; current = current.BaseType)
        {
            string text = null;
            foreach (string guid in AssetDatabase.FindAssets(current.Name + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script == null || script.GetClass() != current) continue;
                text = script.text;
                break;
            }

            if (text == null)
            {
                builder = null;
                break;
            }
            builder.Append(text).Append('\n');
        }

        string result = builder?.ToString();
        cache[type] = result;
        return result;
    }

    /// <summary>Пишет ли код в текст поля: field.text = / field.SetText( / SetText(field / field передаётся
    /// аргументом метода (Bind(title, ...), SetText(label, ...)).</summary>
    private static bool WritesText(string source, string field)
    {
        string name = Regex.Escape(field);
        return Regex.IsMatch(source, @"\b" + name + @"\s*(\[[^\]]*\])?\s*\.\s*(text\s*\+?=[^=]|SetText\s*\()")
               || Regex.IsMatch(source, @"[(,]\s*" + name + @"\s*[,)]");
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

        // Заполненность по языкам. Пустые ru/en — ошибки (это базовые языки игры); у остальных языков пустая
        // ячейка — ещё не переведено (в игре покажется английский), это только процент готовности.
        var completion = new StringBuilder();
        for (int column = 1; column < table.ColumnCount; column++)
        {
            var empty = new List<string>();
            foreach (string[] row in table.Rows)
                if (string.IsNullOrWhiteSpace(row[column])) empty.Add(row[0]);

            string language = table.Header[column].Trim();
            int filled = table.Rows.Count - empty.Count;
            int percent = table.Rows.Count > 0 ? filled * 100 / table.Rows.Count : 100;
            completion.Append($"{language}: {filled}/{table.Rows.Count} ({percent}%)\n");
            if (empty.Count == 0) continue;

            string code = GameLanguages.ColumnToSteamCode(language);
            bool required = code == GameLanguages.Russian || code == GameLanguages.English;
            if (required) problems += empty.Count;
            report.Append(required ? "• Пустые ячейки «" : "• Не переведено на «").Append(language)
                  .Append($"» ({empty.Count}): ").Append(string.Join(", ", empty)).Append('\n');
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

        // Тексты данных (предметы, квесты...), которые ещё не выгружены.
        var dataProblems = new List<string>();
        var notExported = new List<string>();
        foreach (DataText dataText in CollectDataTexts(dataProblems))
            if (!keySet.Contains(dataText.key)) notExported.Add($"{dataText.key}  ({dataText.assetPath})");

        if (notExported.Count > 0)
        {
            problems += notExported.Count;
            report.Append($"• Тексты данных не выгружены ({notExported.Count}) — Localization/Export Data Texts:\n    ")
                  .Append(string.Join("\n    ", notExported)).Append('\n');
        }

        foreach (string problem in dataProblems)
        {
            problems++;
            report.Append("• ").Append(problem).Append('\n');
        }

        string header = $"Ключей: {table.Rows.Count}. Готовность языков:\n{completion}";
        string text = problems == 0 ? "strings.csv в порядке. " + header : $"Найдено проблем: {problems}. {header}\n{report}";

        if (problems == 0) Debug.Log("[Localization] " + text);
        else Debug.LogWarning("[Localization] " + text);

        EditorUtility.DisplayDialog("Проверка локализации",
            problems == 0 ? text : $"Найдено проблем: {problems}. Подробности — в консоли.\n\n{header}", "OK");
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

    // ───────────────────────── Export Data Texts ─────────────────────────

    /// <summary>Один текст данных: ключ таблицы, текст из ассета, раздел таблицы и откуда он.</summary>
    private readonly struct DataText
    {
        public readonly string key;
        public readonly string value;
        public readonly string section;
        public readonly string assetPath;

        public DataText(string key, string value, string section, string assetPath)
        {
            this.key = key;
            this.value = value;
            this.section = section;
            this.assetPath = assetPath;
        }
    }

    /// <summary>
    /// Названия и описания из ассетов данных (предметы, квесты, навыки, ветки навыков, ящики, заказы) —
    /// в strings.csv: ключ из id ассета (как у ItemData.DisplayName и т.п.), текст ассета — в столбец ru,
    /// остальные языки пустые. Каждый тип данных — в своём разделе таблицы. Уже выгруженные ключи не
    /// дублируются; если текст в ассете изменился, спрашивает, что считать правильным — ассет или таблицу.
    /// Повторный запуск безопасен: запускайте после добавления новых предметов/квестов.
    /// </summary>
    [MenuItem(MenuRoot + "Export Data Texts")]
    private static void ExportDataTexts()
    {
        StringsTable table = StringsTable.Load(StringsPath);
        int russianColumn = table.FindLanguageColumn(GameLanguages.Russian);
        if (russianColumn < 0)
        {
            EditorUtility.DisplayDialog("Локализация", $"В {StringsPath} нет столбца ru.", "OK");
            return;
        }

        var problems = new List<string>();
        var added = new List<DataText>();
        var changed = new List<(DataText text, string tableValue)>();
        foreach (DataText text in CollectDataTexts(problems))
        {
            string[] row = table.FindRow(text.key);
            if (row == null) added.Add(text);
            else if (row[russianColumn] != text.value) changed.Add((text, row[russianColumn]));
        }

        if (problems.Count > 0)
            Debug.LogWarning("[Localization] Проблемы данных:\n" + string.Join("\n", problems));

        string summary = $"Новых текстов: {added.Count}.\nТекст в ассете не совпадает с ru в таблице: {changed.Count}." +
                         (problems.Count > 0 ? $"\nПроблем с id: {problems.Count} (подробности — в консоли)." : "");
        if (added.Count == 0 && changed.Count == 0)
        {
            EditorUtility.DisplayDialog("Тексты данных", "Все тексты данных уже в таблице.\n" + summary, "OK");
            return;
        }

        bool takeFromAssets = false;
        if (changed.Count > 0)
        {
            var diff = new StringBuilder("[Localization] Текст в ассете ≠ ru в таблице:\n");
            foreach ((DataText text, string tableValue) in changed)
                diff.Append(text.key).Append("\n    ассет:   ").Append(text.value.Replace("\n", "\\n"))
                    .Append("\n    таблица: ").Append(tableValue.Replace("\n", "\\n")).Append('\n');
            Debug.Log(diff.ToString());

            int choice = EditorUtility.DisplayDialogComplex("Тексты данных",
                summary + "\n\nДля несовпавших (список — в консоли): взять русский текст из ассетов (переводы " +
                "этих строк стоит проверить) или оставить таблицу как есть?",
                "Взять из ассетов", "Отмена", "Оставить таблицу");
            if (choice == 1) return;
            takeFromAssets = choice == 0;
        }
        else if (!EditorUtility.DisplayDialog("Тексты данных", summary + "\n\nДописать их в strings.csv?", "Дописать", "Отмена"))
            return;

        foreach (DataText text in added)
        {
            var row = new string[table.ColumnCount];
            row[0] = text.key;
            row[russianColumn] = text.value;
            table.AddToSection(text.section, row);
        }

        if (takeFromAssets)
            foreach ((DataText text, string _) in changed)
                table.SetCell(text.key, russianColumn, text.value);

        table.Save();
        AssetDatabase.ImportAsset(StringsPath, ImportAssetOptions.ForceUpdate);

        var log = new StringBuilder("[Localization] Тексты данных в strings.csv:\n");
        foreach (DataText text in added) log.Append("+ ").Append(text.key).Append(" = ").Append(text.value.Replace("\n", "\\n")).Append('\n');
        if (takeFromAssets)
            foreach ((DataText text, string _) in changed) log.Append("~ ").Append(text.key).Append(" (ru обновлён — проверьте переводы)\n");
        Debug.Log(log.ToString());

        EditorUtility.DisplayDialog("Тексты данных",
            $"Добавлено: {added.Count}" + (takeFromAssets ? $", обновлено ru: {changed.Count}" : "") +
            ".\nВпишите переводы в остальные столбцы strings.csv и проверьте Localization/Validate.", "OK");
    }

    /// <summary>Все тексты данных игры. Ассеты без id или с повторяющимся id попадают в problems.</summary>
    private static List<DataText> CollectDataTexts(List<string> problems)
    {
        var result = new List<DataText>();
        var ids = new Dictionary<string, string>();

        foreach ((ItemData item, string path) in LoadAll<ItemData>())
        {
            if (!CheckId("item", item.itemId, path, ids, problems)) continue;
            AddDataText(result, "item", item.itemId, "name", item.itemName, "Данные: предметы", path);
            AddDataText(result, "item", item.itemId, "desc", item.description, "Данные: предметы", path);
        }

        foreach ((QuestData quest, string path) in LoadAll<QuestData>())
        {
            if (!CheckId("quest", quest.questId, path, ids, problems)) continue;
            AddDataText(result, "quest", quest.questId, "title", quest.title, "Данные: квесты", path);
            AddDataText(result, "quest", quest.questId, "desc", quest.description, "Данные: квесты", path);
        }

        foreach ((SkillBranch branch, string path) in LoadAll<SkillBranch>())
        {
            if (!CheckId("skillbranch", branch.LocalizationId, path, ids, problems)) continue;
            AddDataText(result, "skillbranch", branch.LocalizationId, "name", branch.displayName, "Данные: ветки навыков", path);
        }

        foreach ((SkillData skill, string path) in LoadAll<SkillData>())
        {
            if (!CheckId("skill", skill.skillId, path, ids, problems)) continue;
            AddDataText(result, "skill", skill.skillId, "title", skill.title, "Данные: навыки", path);
            AddDataText(result, "skill", skill.skillId, "desc", skill.description, "Данные: навыки", path);
        }

        foreach ((LootBoxData box, string path) in LoadAll<LootBoxData>())
        {
            if (!CheckId("lootbox", box.lootBoxId, path, ids, problems)) continue;
            AddDataText(result, "lootbox", box.lootBoxId, "title", box.title, "Данные: ящики терминала", path);
            AddDataText(result, "lootbox", box.lootBoxId, "desc", box.description, "Данные: ящики терминала", path);
        }

        foreach ((ShippingOrderData order, string path) in LoadAll<ShippingOrderData>())
        {
            if (!CheckId("order", order.orderId, path, ids, problems)) continue;
            AddDataText(result, "order", order.orderId, "customer", order.customer, "Данные: заказы", path);
            AddDataText(result, "order", order.orderId, "message", order.message, "Данные: заказы", path);
        }

        return result;
    }

    private static IEnumerable<(T asset, string path)> LoadAll<T>() where T : ScriptableObject
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/01_GAME" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) yield return (asset, path);
        }
    }

    private static bool CheckId(string kind, string id, string path, Dictionary<string, string> ids, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            problems.Add($"{path}: пустой id — тексты не переводятся (заполните id в инспекторе).");
            return false;
        }

        string fullId = kind + "." + id.Trim();
        if (ids.TryGetValue(fullId, out string other))
        {
            problems.Add($"{path}: id «{id}» уже занят ассетом {other} — у двух ассетов были бы одни и те же строки.");
            return false;
        }

        ids[fullId] = path;
        return true;
    }

    private static void AddDataText(List<DataText> result, string kind, string id, string field, string value, string section, string path)
    {
        // Пустой текст не выгружаем: в игре и так покажется пустота, а в таблице была бы вечная «пустая ячейка».
        if (string.IsNullOrWhiteSpace(value)) return;
        result.Add(new DataText(Loc.DataKey(kind, id, field), value, section, path));
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

    /// <summary>
    /// strings.csv как таблица для редакторских инструментов. Разделитель — тот, что в файле (запятая, «;» из
    /// Excel или табуляция); запись — весь файл заново тем же разделителем, UTF-8 с BOM (без BOM Excel
    /// открывает кириллицу кракозябрами). Разделы и комментарии («# ...») сохраняются на своих местах; каждая
    /// строка дополняется пустыми ячейками до числа столбцов заголовка — новый язык достаточно дописать в
    /// заголовок, ячейки под него появятся у всех строк при следующей записи.
    /// </summary>
    private class StringsTable
    {
        public List<string> Header { get; private set; } = new List<string>();
        /// <summary>Строки с ключами (без комментариев и заголовка).</summary>
        public List<string[]> Rows { get; } = new List<string[]>();
        public int ColumnCount => Header.Count;
        public char Delimiter { get; private set; } = CsvUtility.DefaultDelimiter;

        public IEnumerable<string> Keys
        {
            get { foreach (string[] row in Rows) yield return row[0]; }
        }

        private string path;
        // Все строки файла по порядку: заголовок, комментарии, строки с ключами.
        private readonly List<string[]> lines = new List<string[]>();

        public static StringsTable Load(string assetPath)
        {
            var table = new StringsTable { path = assetPath };
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            string text = File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : "";
            if (text.Trim().Length == 0) text = "key,ru,en\n";

            table.Delimiter = CsvUtility.DetectDelimiter(text);
            foreach (List<string> parsed in CsvUtility.Parse(text, table.Delimiter)) table.lines.Add(parsed.ToArray());
            table.Header = new List<string>(table.lines[0]);

            for (int i = 0; i < table.lines.Count; i++)
            {
                if (table.lines[i].Length < table.ColumnCount)
                {
                    string[] padded = table.lines[i];
                    System.Array.Resize(ref padded, table.ColumnCount);
                    for (int c = 0; c < padded.Length; c++) padded[c] = padded[c] ?? "";
                    table.lines[i] = padded;
                }

                if (i == 0 || IsComment(table.lines[i])) continue;
                table.lines[i][0] = table.lines[i][0].Trim();
                table.Rows.Add(table.lines[i]);
            }
            return table;
        }

        public bool HasKey(string key) => FindRow(key) != null;

        public string[] FindRow(string key)
        {
            foreach (string[] row in Rows)
                if (row[0] == key) return row;
            return null;
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

        /// <summary>Добавить строку в конец таблицы.</summary>
        public void AppendRow(string[] row) => AddToSection(null, row);

        /// <summary>Добавить строку в конец раздела «# section» (раздела нет — он создаётся в конце таблицы).</summary>
        public void AddToSection(string section, string[] row)
        {
            row = Normalize(row);
            Rows.Add(row);

            if (string.IsNullOrEmpty(section))
            {
                lines.Add(row);
                return;
            }

            string title = "# " + section;
            int start = lines.FindIndex(line => line[0].Trim() == title);
            if (start < 0)
            {
                string[] comment = Normalize(new[] { title });
                lines.Add(comment);
                lines.Add(row);
                return;
            }

            int insertAt = start + 1;
            while (insertAt < lines.Count && !IsComment(lines[insertAt])) insertAt++;
            lines.Insert(insertAt, row);
        }

        public void SetCell(string key, int column, string value)
        {
            string[] row = FindRow(key);
            if (row != null && column > 0 && column < row.Length) row[column] = value ?? "";
        }

        public void Save()
        {
            var builder = new StringBuilder();
            foreach (string[] line in lines) builder.Append(CsvUtility.FormatRow(line, Delimiter)).Append('\n');

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, builder.ToString(), new UTF8Encoding(true));
        }

        private string[] Normalize(string[] row)
        {
            var result = new string[System.Math.Max(ColumnCount, row.Length)];
            for (int i = 0; i < result.Length; i++) result[i] = i < row.Length ? row[i] ?? "" : "";
            return result;
        }

        private static bool IsComment(string[] line)
        {
            string key = line.Length > 0 ? line[0].Trim() : "";
            return key.Length == 0 || key[0] == '#';
        }
    }
}
