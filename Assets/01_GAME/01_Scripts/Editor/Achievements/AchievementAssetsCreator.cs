using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ассеты статистики и достижений: Tools/Weapon Keeper/Achievements/Create Achievement Assets создаёт
/// стартовый набор (StatDefinition, AchievementData, AchievementCatalog в 04_Data/Achievements) и
/// пересобирает каталог из всех ассетов папки. Уже существующие ассеты не перезаписываются — правки
/// дизайнера сохраняются, заполняются только пустые API-имена и ключи.
///
/// Export Steam Tables — Markdown-таблицы статистики и достижений (RU/EN из strings.csv) для ручного ввода
/// в Steamworks → App Admin → Stats &amp; Achievements.
/// </summary>
public static class AchievementAssetsCreator
{
    public const string DataFolder = "Assets/01_GAME/04_Data/Achievements";
    public const string CatalogPath = DataFolder + "/AchievementCatalog.asset";
    public const string AllStainsAchievementPath = DataFolder + "/List/Ach_ACH_ALL_STAINS.asset";
    private const string StatsFolder = DataFolder + "/Stats";
    private const string ListFolder = DataFolder + "/List";
    private const string MenuRoot = "Tools/Weapon Keeper/Achievements/";

    private struct StatSpec
    {
        public StatId id;
        public string apiName;
        public int maxValue;
        public int maxChange;

        public StatSpec(StatId id, string apiName, int maxValue = 0, int maxChange = 0)
        {
            this.id = id;
            this.apiName = apiName;
            this.maxValue = maxValue;
            this.maxChange = maxChange;
        }
    }

    private struct AchievementSpec
    {
        public string apiName;
        public string key;
        public StatId stat;
        public int target;
        public float[] steps;
        public bool hidden;
        public bool scripted;

        public AchievementSpec(string apiName, string key, StatId stat, int target, float[] steps = null,
                               bool hidden = false, bool scripted = false)
        {
            this.apiName = apiName;
            this.key = key;
            this.stat = stat;
            this.target = target;
            this.steps = steps ?? new float[0];
            this.hidden = hidden;
            this.scripted = scripted;
        }
    }

    private static readonly float[] Half = { 0.5f };
    private static readonly float[] Quarters = { 0.25f, 0.5f, 0.75f };

    // Стартовый набор статистики. Max Change — защита от накрутки: больше за одну отправку не вырасти.
    private static readonly StatSpec[] StarterStats =
    {
        new StatSpec(StatId.EnemiesKilled, "enemies_killed", 0, 500),
        new StatSpec(StatId.ItemsShelved, "items_shelved", 0, 500),
        new StatSpec(StatId.ItemsPickedUp, "items_picked_up", 0, 1000),
        new StatSpec(StatId.WallsRepaired, "walls_repaired", 0, 50),
        new StatSpec(StatId.StainsCleaned, "stains_cleaned", 0, 200),
        new StatSpec(StatId.ObjectsBroken, "objects_broken", 0, 200),
        new StatSpec(StatId.QuestsCompleted, "quests_completed", 0, 50),
        new StatSpec(StatId.ItemsDelivered, "items_delivered", 0, 200),
        new StatSpec(StatId.MoneyEarned, "money_earned", 0, 1000000),
        new StatSpec(StatId.Jumps, "jumps", 0, 5000),
        new StatSpec(StatId.DistanceWalkedM, "distance_walked_m", 0, 100000),
        new StatSpec(StatId.PlayTimeMin, "play_time_min", 0, 600),
        new StatSpec(StatId.MaxLevel, "max_level", 1000, 100),
        new StatSpec(StatId.RepairsCompleted, "repairs_completed", 0, 50),
        new StatSpec(StatId.DebrisDisposed, "debris_disposed", 0, 500),
    };

    // 20 стартовых достижений: ключи ach.<key>.name / ach.<key>.desc в strings.csv.
    private static readonly AchievementSpec[] StarterAchievements =
    {
        new AchievementSpec("ACH_FIRST_KILL", "first_kill", StatId.EnemiesKilled, 1),
        new AchievementSpec("ACH_KILL_10", "kill_10", StatId.EnemiesKilled, 10, Half),
        new AchievementSpec("ACH_KILL_100", "kill_100", StatId.EnemiesKilled, 100, Quarters),
        new AchievementSpec("ACH_FIRST_SHELF", "first_shelf", StatId.ItemsShelved, 1),
        new AchievementSpec("ACH_SHELF_25", "shelf_25", StatId.ItemsShelved, 25, Half),
        new AchievementSpec("ACH_SHELF_100", "shelf_100", StatId.ItemsShelved, 100, Quarters),
        new AchievementSpec("ACH_FIRST_WALL", "first_wall", StatId.WallsRepaired, 1),
        new AchievementSpec("ACH_ALL_STAINS", "all_stains", StatId.StainsCleaned, 1, null, false, true),
        new AchievementSpec("ACH_BREAK_5", "break_5", StatId.ObjectsBroken, 5, Half),
        new AchievementSpec("ACH_DEBRIS_10", "debris_10", StatId.DebrisDisposed, 10, Half),
        new AchievementSpec("ACH_FIRST_QUEST", "first_quest", StatId.QuestsCompleted, 1),
        new AchievementSpec("ACH_QUESTS_5", "quests_5", StatId.QuestsCompleted, 5, Half),
        new AchievementSpec("ACH_FIRST_DELIVERY", "first_delivery", StatId.ItemsDelivered, 1),
        new AchievementSpec("ACH_LEVEL_5", "level_5", StatId.MaxLevel, 5),
        new AchievementSpec("ACH_LEVEL_10", "level_10", StatId.MaxLevel, 10),
        new AchievementSpec("ACH_MONEY_1000", "money_1000", StatId.MoneyEarned, 1000, Half),
        new AchievementSpec("ACH_MONEY_10000", "money_10000", StatId.MoneyEarned, 10000, Quarters),
        new AchievementSpec("ACH_PLAY_1H", "play_1h", StatId.PlayTimeMin, 60, Half),
        new AchievementSpec("ACH_JUMPS_100", "jumps_100", StatId.Jumps, 100, null, true),
        new AchievementSpec("ACH_WALK_5KM", "walk_5km", StatId.DistanceWalkedM, 5000, Quarters),
    };

    [MenuItem(MenuRoot + "Create Achievement Assets")]
    private static void CreateAssetsMenu()
    {
        AchievementCatalog catalog = CreateOrUpdateAssets();
        string problems = catalog.Validate();
        EditorUtility.DisplayDialog("Достижения",
            $"Каталог: {catalog.stats.Count} статистик, {catalog.achievements.Count} достижений.\n" +
            (string.IsNullOrEmpty(problems) ? "Проблем нет." : "Проблемы: " + problems), "OK");
        Selection.activeObject = catalog;
    }

    /// <summary>Создать недостающие ассеты и пересобрать каталог. Возвращает каталог.</summary>
    public static AchievementCatalog CreateOrUpdateAssets()
    {
        UIBuilderKit.EnsureFolder(StatsFolder);
        UIBuilderKit.EnsureFolder(ListFolder);

        foreach (StatSpec spec in StarterStats)
        {
            string path = $"{StatsFolder}/Stat_{spec.apiName}.asset";
            var stat = AssetDatabase.LoadAssetAtPath<StatDefinition>(path);
            if (stat == null)
            {
                stat = ScriptableObject.CreateInstance<StatDefinition>();
                stat.stat = spec.id;
                stat.apiName = spec.apiName;
                stat.valueType = StatValueType.Int;
                stat.incrementOnly = true;
                stat.maxValue = spec.maxValue;
                stat.maxChange = spec.maxChange;
                stat.nameKey = "stat." + spec.apiName;
                AssetDatabase.CreateAsset(stat, path);
            }
            else if (string.IsNullOrEmpty(stat.apiName))
            {
                stat.apiName = spec.apiName;
                EditorUtility.SetDirty(stat);
            }
        }

        for (int i = 0; i < StarterAchievements.Length; i++)
        {
            AchievementSpec spec = StarterAchievements[i];
            string path = $"{ListFolder}/Ach_{spec.apiName}.asset";
            var achievement = AssetDatabase.LoadAssetAtPath<AchievementData>(path);
            if (achievement == null)
            {
                achievement = ScriptableObject.CreateInstance<AchievementData>();
                achievement.apiName = spec.apiName;
                achievement.nameKey = $"ach.{spec.key}.name";
                achievement.descriptionKey = $"ach.{spec.key}.desc";
                achievement.hidden = spec.hidden;
                achievement.condition = spec.scripted ? AchievementCondition.Scripted : AchievementCondition.StatThreshold;
                achievement.progressStat = spec.stat;
                achievement.target = spec.target;
                achievement.progressSteps = spec.steps;
                achievement.order = (i + 1) * 10;
                AssetDatabase.CreateAsset(achievement, path);
            }
            else if (string.IsNullOrEmpty(achievement.apiName))
            {
                achievement.apiName = spec.apiName;
                EditorUtility.SetDirty(achievement);
            }
        }

        var catalog = UIBuilderKit.LoadOrCreateAsset<AchievementCatalog>(CatalogPath);
        catalog.stats = LoadAll<StatDefinition>(DataFolder);
        catalog.stats.Sort((a, b) => ((int)a.stat).CompareTo((int)b.stat));
        catalog.achievements = LoadAll<AchievementData>(DataFolder);
        catalog.achievements.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.apiName, b.apiName));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static List<T> LoadAll<T>(string folder) where T : ScriptableObject
    {
        var result = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) result.Add(asset);
        }
        return result;
    }

    // ───────────────────────── Таблицы для партнёрки ─────────────────────────

    [MenuItem(MenuRoot + "Export Steam Tables (Markdown)")]
    private static void ExportSteamTables()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AchievementCatalog>(CatalogPath);
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Достижения", "Нет каталога — сначала Create Achievement Assets.", "OK");
            return;
        }

        Dictionary<string, (string ru, string en)> strings = LoadStrings();
        string Tr(string key, bool english) =>
            strings.TryGetValue(key ?? "", out var pair) ? (english ? pair.en : pair.ru) : key;

        var md = new StringBuilder();
        md.AppendLine("<!-- Сгенерировано: Tools/Weapon Keeper/Achievements/Export Steam Tables (Markdown). -->");
        md.AppendLine("## Статистика (Stats)").AppendLine();
        md.AppendLine("| API Name | Type | Increment Only | Min | Max | Max Change | Default | Display Name (EN) | Название (RU) |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (StatDefinition stat in catalog.stats)
        {
            if (stat == null) continue;
            md.AppendLine($"| `{stat.apiName}` | {(stat.valueType == StatValueType.Int ? "INT" : "FLOAT")} | {(stat.incrementOnly ? "да" : "нет")} | " +
                          $"{stat.minValue} | {(stat.maxValue > 0 ? stat.maxValue.ToString() : "—")} | {(stat.maxChange > 0 ? stat.maxChange.ToString() : "—")} | 0 | " +
                          $"{Tr(stat.nameKey, true)} | {Tr(stat.nameKey, false)} |");
        }

        md.AppendLine().AppendLine("## Достижения (Achievements)").AppendLine();
        md.AppendLine("| API Name | Name (EN) | Description (EN) | Название (RU) | Описание (RU) | Hidden | Progress Stat | Min | Max |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (AchievementData achievement in catalog.achievements)
        {
            if (achievement == null) continue;
            StatDefinition stat = catalog.GetStat(achievement.progressStat);
            bool withProgress = achievement.HasProgress && stat != null;
            md.AppendLine($"| `{achievement.apiName}` | {Tr(achievement.nameKey, true)} | {Tr(achievement.descriptionKey, true)} | " +
                          $"{Tr(achievement.nameKey, false)} | {Tr(achievement.descriptionKey, false)} | {(achievement.hidden ? "да" : "нет")} | " +
                          $"{(withProgress ? "`" + stat.apiName + "`" : "—")} | {(withProgress ? "0" : "—")} | {(withProgress ? achievement.target.ToString() : "—")} |");
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), "Docs/Steam/STEAM_TABLES.generated.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, md.ToString(), new UTF8Encoding(false));
        Debug.Log("[Achievements] Таблицы для Steamworks записаны в " + path + "\n" + md);
        EditorUtility.DisplayDialog("Достижения", "Таблицы записаны в Docs/Steam/STEAM_TABLES.generated.md.", "OK");
    }

    private static Dictionary<string, (string ru, string en)> LoadStrings()
    {
        var result = new Dictionary<string, (string, string)>();
        string path = Path.Combine(Directory.GetCurrentDirectory(), LocalizationEditorTools.StringsPath);
        if (!File.Exists(path)) return result;

        List<List<string>> table = CsvUtility.Parse(File.ReadAllText(path, Encoding.UTF8));
        if (table.Count == 0) return result;

        int ru = -1, en = -1;
        for (int i = 1; i < table[0].Count; i++)
        {
            string code = GameLanguages.ColumnToSteamCode(table[0][i]);
            if (code == GameLanguages.Russian) ru = i;
            else if (code == GameLanguages.English) en = i;
        }

        for (int r = 1; r < table.Count; r++)
        {
            List<string> row = table[r];
            string ruText = ru > 0 && ru < row.Count ? row[ru] : "";
            string enText = en > 0 && en < row.Count ? row[en] : "";
            result[row[0].Trim()] = (ruText, enText);
        }
        return result;
    }

    // ───────────────────────── Отладка (Play Mode) ─────────────────────────

    [MenuItem(MenuRoot + "Debug/Reset All Stats and Achievements (Play Mode)")]
    private static void ResetAllDebug()
    {
        if (StatsService.Instance == null) return;
        if (EditorUtility.DisplayDialog("Достижения", $"Сбросить всю статистику и достижения ({StatsService.Instance.Backend.Name})?", "Сбросить", "Отмена"))
            StatsService.Instance.ResetAll();
    }

    [MenuItem(MenuRoot + "Debug/Reset All Stats and Achievements (Play Mode)", true)]
    private static bool ResetAllDebugValidate() => EditorApplication.isPlaying && StatsService.Instance != null;

    [MenuItem(MenuRoot + "Debug/Unlock All (Play Mode)")]
    private static void UnlockAllDebug()
    {
        StatsService stats = StatsService.Instance;
        if (stats == null || stats.Catalog == null) return;
        foreach (AchievementData achievement in stats.Catalog.achievements) stats.Unlock(achievement);
    }

    [MenuItem(MenuRoot + "Debug/Unlock All (Play Mode)", true)]
    private static bool UnlockAllDebugValidate() => EditorApplication.isPlaying && StatsService.Instance != null;

    [MenuItem(MenuRoot + "Debug/Open Save Folder (achievements.sav, settings.cfg)")]
    private static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);
}
