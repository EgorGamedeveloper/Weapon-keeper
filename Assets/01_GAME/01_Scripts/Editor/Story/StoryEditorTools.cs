using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Меню Tools/Weapon Keeper/Story — связь веб-редактора сюжета (Tools/StoryEditor, страница в claude.ai) с игрой:
/// - Import Story Graph — story_graph.json → ассеты QuestData квестов графа, их тексты в strings.csv,
///   StoryQuestCatalog, в TestScene — StoryDirector, интерфейс рации и модель рации в руке, ссылка в SaveLoadService;
/// - Export Scene Catalog — scene_catalog.json со всеми целями для редактора (точки ремонта, двери, пятна, полки,
///   зоны, предметы, враги, заказы, ящики, объекты сюжета) — чтобы в редакторе выбирать их из списков;
/// - Export Existing Quests To Graph — разовая миграция: текущие квесты QuestManager и их связи → стартовый граф;
/// - Open Story Editor — открыть веб-редактор.
/// Повторный запуск любого пункта безопасен.
/// </summary>
public static class StoryEditorTools
{
    public const string StoryFolder = "Assets/01_GAME/08_Story";
    public const string GraphPath = StoryFolder + "/story_graph.json";
    public const string CatalogPath = StoryFolder + "/scene_catalog.json";
    public const string QuestFolder = "Assets/01_GAME/04_Data/Quests/Story";
    public const string QuestCatalogPath = "Assets/01_GAME/04_Data/Story/StoryQuestCatalog.asset";
    public const string EditorUrl = "https://claude.ai/artifact/P8aAP6hr6d9pWyf9W1pciN";

    private const string MenuRoot = "Tools/Weapon Keeper/Story/";
    private const string Title = "Сюжет";
    private const string QuestSection = "Сюжет: квесты";
    private static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

    // ───────────────────────── Import ─────────────────────────

    [MenuItem(MenuRoot + "Import Story Graph", priority = 0)]
    private static void ImportStoryGraph()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;

        string fullPath = FullPath(GraphPath);
        if (!File.Exists(fullPath))
        {
            EditorUtility.DisplayDialog(Title, $"Нет файла {GraphPath}.\n\nСкачайте граф из веб-редактора («Скачать JSON») и положите " +
                                               "его туда, или попросите Claude синхронизировать граф.", "OK");
            return;
        }

        StoryGraphData graph = StoryGraphData.Parse(File.ReadAllText(fullPath, Encoding.UTF8), out string parseError);
        if (graph == null)
        {
            EditorUtility.DisplayDialog(Title, $"story_graph.json не читается: {parseError}", "OK");
            return;
        }

        List<string> errors = ValidateGraph(graph);
        if (errors.Count > 0)
        {
            Debug.LogError("[Story] Граф с ошибками:\n" + string.Join("\n", errors));
            EditorUtility.DisplayDialog(Title, $"В графе ошибок: {errors.Count} (список — в консоли). Исправьте их в редакторе " +
                                               "(кнопка «Проверка») и импортируйте снова.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var report = new StringBuilder();
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
        List<QuestData> quests = ImportQuests(graph, report);

        StoryQuestCatalog catalog = UIBuilderKit.LoadOrCreateAsset<StoryQuestCatalog>(QuestCatalogPath);
        catalog.quests = quests;
        EditorUtility.SetDirty(catalog);

        WriteQuestTexts(graph, report);
        AssetDatabase.SaveAssets();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(StoryUIBuilder.RadioPath) == null)
        {
            StoryUIBuilder.BuildRadioUI();
            report.AppendLine("✔ Собран префаб " + StoryUIBuilder.RadioPath + ".");
        }

        string scenePath = WeaponKeeperSetup.GameplayScenePath;
        if (File.Exists(FullPath(scenePath)))
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine().AppendLine($"— {scene.name} —");
            SetupScene(scene, catalog, report);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("✔ Сцена сохранена.");
        }
        else report.AppendLine($"✖ Нет сцены {scenePath} — StoryDirector не расставлен.");

        Debug.Log($"[{Title}] Импорт графа:\n{report}");
        EditorUtility.DisplayDialog(Title, report.ToString(), "OK");
    }

    /// <summary>Ошибки, из-за которых граф нельзя импортировать (то же, что красные пункты «Проверки» в редакторе).</summary>
    public static List<string> ValidateGraph(StoryGraphData graph)
    {
        var errors = new List<string>();
        var ids = new HashSet<string>();
        var questIds = new HashSet<string>();
        var known = new HashSet<string> { StoryNodeData.Start, StoryNodeData.Quest, StoryNodeData.Radio, StoryNodeData.Trigger,
                                          StoryNodeData.Wait, StoryNodeData.And, StoryNodeData.Action, StoryNodeData.Note };
        var triggerKinds = new HashSet<string>(StoryTrigger.Kinds);
        var actions = new HashSet<string> { "giveMoney", "giveXp", "enableObject", "disableObject" };

        foreach (StoryNodeData node in graph.nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.id)) { errors.Add("Нода без id."); continue; }
            if (!ids.Add(node.id)) errors.Add($"Повтор id ноды «{node.id}».");
            if (!known.Contains(node.type)) errors.Add($"{node.id}: неизвестный тип «{node.type}».");

            if (node.type == StoryNodeData.Quest)
            {
                if (string.IsNullOrEmpty(node.questId) || !IdPattern.IsMatch(node.questId)) errors.Add($"{node.id}: плохой id квеста «{node.questId}».");
                else if (!questIds.Add(node.questId)) errors.Add($"{node.id}: id квеста «{node.questId}» повторяется.");
                if (!Enum.TryParse(node.questType, out QuestData.QuestType _)) errors.Add($"{node.id}: неизвестный тип квеста «{node.questType}».");
                if (!StoryText.Languages(node.title).Any()) errors.Add($"{node.id}: у квеста нет названия.");
            }
            if (node.type == StoryNodeData.Trigger && !triggerKinds.Contains(node.trigger)) errors.Add($"{node.id}: неизвестный вид триггера «{node.trigger}».");
            if (node.type == StoryNodeData.Action && !actions.Contains(node.action)) errors.Add($"{node.id}: неизвестное событие «{node.action}».");
            if (node.type == StoryNodeData.Radio && (node.lines == null || node.lines.Length == 0)) errors.Add($"{node.id}: у рации нет реплик.");
        }

        if (!graph.nodes.Any(n => n != null && n.type == StoryNodeData.Start)) errors.Add("Нет ноды «Старт».");
        foreach (StoryLinkData link in graph.links)
            if (link == null || !ids.Contains(link.from) || !ids.Contains(link.to)) errors.Add($"Нитка на несуществующую ноду: {link?.from} → {link?.to}.");
        return errors;
    }

    private static List<QuestData> ImportQuests(StoryGraphData graph, StringBuilder report)
    {
        var result = new List<QuestData>();
        Dictionary<string, QuestData> existing = LoadAll<QuestData>().Where(q => !string.IsNullOrEmpty(q.questId))
            .GroupBy(q => q.questId).ToDictionary(g => g.Key, g => g.First());
        string mainLanguage = graph.languages.Length > 0 ? graph.languages[0] : "ru";
        int created = 0, updated = 0;

        foreach (StoryNodeData node in graph.nodes)
        {
            if (node == null || node.type != StoryNodeData.Quest) continue;

            if (!existing.TryGetValue(node.questId, out QuestData quest))
            {
                UIBuilderKit.EnsureFolder(QuestFolder);
                quest = ScriptableObject.CreateInstance<QuestData>();
                quest.questId = node.questId;
                AssetDatabase.CreateAsset(quest, $"{QuestFolder}/Quest_{node.questId}.asset");
                existing[node.questId] = quest;
                created++;
            }
            else updated++;

            quest.questId = node.questId;
            quest.title = FirstNonEmpty(StoryText.Get(node.title, mainLanguage), StoryText.Pick(node.title));
            quest.description = FirstNonEmpty(StoryText.Get(node.description, mainLanguage), StoryText.Pick(node.description));
            Enum.TryParse(node.questType, out quest.type);
            quest.targetCount = Mathf.Max(1, node.targetCount);
            quest.targetPersistentId = node.target ?? "";
            quest.targetZoneId = node.zone ?? "";
            quest.xpReward = Mathf.Max(0, node.xp);
            quest.moneyReward = Mathf.Max(0, node.money);
            // Порядок и условия открытия задаёт граф — у квестов графа своих условий нет.
            quest.prerequisiteQuests = Array.Empty<QuestData>();
            quest.requiredLevel = 1;

            quest.targetCategory = string.IsNullOrEmpty(node.category) ? null : LoadAll<ShelfCategory>().FirstOrDefault(c => c.name == node.category);
            if (!string.IsNullOrEmpty(node.category) && quest.targetCategory == null)
                report.AppendLine($"• {node.questId}: нет категории полки «{node.category}».");

            quest.targetItem = string.IsNullOrEmpty(node.item) ? null : LoadAll<ItemData>().FirstOrDefault(i => i.itemId == node.item);
            if (!string.IsNullOrEmpty(node.item) && quest.targetItem == null)
                report.AppendLine($"• {node.questId}: нет предмета «{node.item}».");

            EditorUtility.SetDirty(quest);
            result.Add(quest);
        }

        report.AppendLine($"✔ Квесты графа: {result.Count} (новых {created}, обновлено {updated}).");
        return result;
    }

    /// <summary>Названия и описания квестов графа — в strings.csv (раздел «Сюжет: квесты»), каждый язык графа —
    /// в свой столбец (нового языка столбец добавляется). Пустые тексты графа не стирают перевод в таблице.</summary>
    private static void WriteQuestTexts(StoryGraphData graph, StringBuilder report)
    {
        var table = LocalizationEditorTools.StringsTable.Load(LocalizationEditorTools.StringsPath);
        int written = 0;
        foreach (StoryNodeData node in graph.nodes)
        {
            if (node == null || node.type != StoryNodeData.Quest) continue;
            written += Upsert(table, Loc.DataKey("quest", node.questId, "title"), node.title);
            written += Upsert(table, Loc.DataKey("quest", node.questId, "desc"), node.description);
        }
        table.Save();
        AssetDatabase.ImportAsset(LocalizationEditorTools.StringsPath, ImportAssetOptions.ForceUpdate);
        report.AppendLine($"✔ Тексты квестов в strings.csv: {written}.");
    }

    private static int Upsert(LocalizationEditorTools.StringsTable table, string key, StoryLocText[] texts)
    {
        var values = new Dictionary<int, string>();
        foreach (string lang in StoryText.Languages(texts))
            values[table.EnsureLanguageColumn(lang)] = StoryText.Get(texts, lang);
        if (values.Count == 0) return 0;
        table.Upsert(QuestSection, key, values);
        return 1;
    }

    // ───────────────────────── Сцена ─────────────────────────

    /// <summary>StoryDirector, интерфейс рации и модель рации в игровой сцене, со всеми ссылками.</summary>
    public static void SetupScene(Scene scene, StoryQuestCatalog catalog, StringBuilder report)
    {
        RadioCallUI radio = SceneSetupUtility.EnsurePrefabInstance<RadioCallUI>(scene, StoryUIBuilder.RadioPath, report);
        if (radio != null) WireRadio(scene, radio, report);

        StoryDirector director = SceneSetupUtility.EnsureSceneObject<StoryDirector>(scene, "Story", report);
        Undo.RecordObject(director, "Story");
        director.graphJson = AssetDatabase.LoadAssetAtPath<TextAsset>(GraphPath);
        director.questCatalog = catalog;
        director.questManager = SceneSetupUtility.FindInScene<QuestManager>(scene);
        director.radio = radio;
        if (director.itemCatalog == null) director.itemCatalog = LoadAll<ItemCatalog>().FirstOrDefault();
        SceneSetupUtility.MarkModified(director);
        if (director.questManager == null) report.AppendLine("✖ В сцене нет QuestManager — квесты графа не запустятся.");

        SaveLoadService saveLoad = SceneSetupUtility.FindInScene<SaveLoadService>(scene);
        if (saveLoad != null)
        {
            Undo.RecordObject(saveLoad, "Story");
            saveLoad.storyDirector = director;
            SceneSetupUtility.MarkModified(saveLoad);
            report.AppendLine("✔ Сюжет сохраняется в сейв (SaveLoadService.storyDirector).");
        }
        else report.AppendLine("• В сцене нет SaveLoadService — сюжет не будет сохраняться.");
    }

    private static void WireRadio(Scene scene, RadioCallUI radio, StringBuilder report)
    {
        Undo.RecordObject(radio, "Radio");
        radio.itemInteraction = SceneSetupUtility.FindInScene<PlayerItemInteraction>(scene);
        radio.weaponHolster = SceneSetupUtility.FindInScene<WeaponHolster>(scene);
        radio.heldItems = SceneSetupUtility.FindInScene<EquippedItemHolder>(scene);
        radio.inputBlocker = SceneSetupUtility.FindInScene<GameplayInputBlocker>(scene);

        var disable = new List<Behaviour>();
        if (radio.itemInteraction != null) disable.Add(radio.itemInteraction);
        PlayerInventoryModeController mode = SceneSetupUtility.FindInScene<PlayerInventoryModeController>(scene);
        if (mode != null) disable.Add(mode);
        PlacementVision vision = SceneSetupUtility.FindInScene<PlacementVision>(scene);
        if (vision != null) disable.Add(vision);
        radio.disableDuringCall = disable.ToArray();

        radio.hudGroups = FindHudGroups(scene).ToArray();
        report.AppendLine($"✔ Рация: гаснут на время разговора канвасов HUD — {radio.hudGroups.Length}, выключаются компонентов — {disable.Count}.");

        if (radio.radioModel == null)
        {
            Camera camera = SceneSetupUtility.FindAllInScene<Camera>(scene).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camera != null)
            {
                radio.radioModel = StoryUIBuilder.BuildRadioModel(camera.transform);
                report.AppendLine("✔ Модель рации (примитивы) поставлена под камеру — замените своей моделью в RadioCallUI.radioModel.");
            }
            else report.AppendLine("• Нет камеры с тегом MainCamera — модель рации не поставлена.");
        }
        SceneSetupUtility.MarkModified(radio);
    }

    /// <summary>Канвасы HUD: экранные корневые канвасы сцены, кроме окон настроек, паузы, достижений и самой рации.</summary>
    private static List<CanvasGroup> FindHudGroups(Scene scene)
    {
        var groups = new List<CanvasGroup>();
        foreach (Canvas canvas in SceneSetupUtility.FindAllInScene<Canvas>(scene))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (canvas.GetComponentInChildren<RadioCallUI>(true) != null || canvas.GetComponentInChildren<SettingsWindow>(true) != null
                || canvas.GetComponentInChildren<PauseMenu>(true) != null || canvas.GetComponentInChildren<AchievementsWindow>(true) != null
                || canvas.GetComponentInChildren<AchievementToastUI>(true) != null || canvas.GetComponentInChildren<ConfirmDialog>(true) != null)
                continue;

            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(canvas.gameObject);
                SceneSetupUtility.MarkModified(group);
            }
            if (!groups.Contains(group)) groups.Add(group);
        }
        return groups;
    }

    // ───────────────────────── Каталог сцены ─────────────────────────

    [Serializable] private class CatalogEntry { public string id; public string name; }

    [Serializable]
    private class SceneCatalog
    {
        public List<CatalogEntry> repairPoints = new List<CatalogEntry>();
        public List<CatalogEntry> breakables = new List<CatalogEntry>();
        public List<CatalogEntry> stains = new List<CatalogEntry>();
        public List<CatalogEntry> shelves = new List<CatalogEntry>();
        public List<CatalogEntry> categories = new List<CatalogEntry>();
        public List<CatalogEntry> zones = new List<CatalogEntry>();
        public List<CatalogEntry> items = new List<CatalogEntry>();
        public List<CatalogEntry> enemySpawns = new List<CatalogEntry>();
        public List<CatalogEntry> enemyTypes = new List<CatalogEntry>();
        public List<CatalogEntry> orders = new List<CatalogEntry>();
        public List<CatalogEntry> lootBoxes = new List<CatalogEntry>();
        public List<CatalogEntry> storyObjects = new List<CatalogEntry>();
        public List<CatalogEntry> quests = new List<CatalogEntry>();
        public List<CatalogEntry> speakers = new List<CatalogEntry>();
    }

    [MenuItem(MenuRoot + "Export Scene Catalog", priority = 20)]
    private static void ExportSceneCatalog()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;
        Scene scene = SceneManager.GetActiveScene();
        if (SceneSetupUtility.FindInScene<QuestManager>(scene) == null
            && !EditorUtility.DisplayDialog(Title, $"В открытой сцене «{scene.name}» нет QuestManager — это точно игровая сцена? " +
                                                   "Каталог строится по открытой сцене.", "Всё равно выгрузить", "Отмена"))
            return;

        // Объекты-цели без PersistentId: без него сюжет их не найдёт.
        var missing = new List<Component>();
        CollectMissingIds<RepairPoint>(scene, missing);
        CollectMissingIds<Breakable>(scene, missing);
        CollectMissingIds<CleanableStain>(scene, missing);
        CollectMissingIds<Shelf>(scene, missing);
        CollectMissingIds<EnemySpawnPoint>(scene, missing);
        if (missing.Count > 0 && EditorUtility.DisplayDialog(Title,
                $"У {missing.Count} объектов (точки ремонта, двери, пятна, полки, точки спавна) нет PersistentId — сюжет не сможет на них " +
                "ссылаться. Добавить? Сцена изменится, её нужно будет сохранить.", "Добавить", "Пропустить"))
        {
            foreach (Component component in missing) AddPersistentId(component.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        var catalog = new SceneCatalog();
        AddById<RepairPoint>(scene, catalog.repairPoints, null);
        AddById<Breakable>(scene, catalog.breakables, null);
        AddById<CleanableStain>(scene, catalog.stains, null);
        AddById<Shelf>(scene, catalog.shelves, s => s.acceptedCategory != null ? s.acceptedCategory.name : null);
        AddById<EnemySpawnPoint>(scene, catalog.enemySpawns, p => p.enemyData != null ? p.enemyData.enemyId : null);

        foreach (DeliveryZone zone in SceneSetupUtility.FindAllInScene<DeliveryZone>(scene))
            if (!string.IsNullOrEmpty(zone.zoneId) && catalog.zones.All(e => e.id != zone.zoneId))
                catalog.zones.Add(Entry(zone.zoneId, $"{zone.zoneId} — {ShortPath(zone.transform)}"));
        foreach (StoryObject storyObject in SceneSetupUtility.FindAllInScene<StoryObject>(scene))
            if (!string.IsNullOrEmpty(storyObject.storyId))
                catalog.storyObjects.Add(Entry(storyObject.storyId, $"{storyObject.storyId} — {ShortPath(storyObject.transform)}"));

        foreach (ShelfCategory category in LoadAll<ShelfCategory>())
            catalog.categories.Add(Entry(category.name, string.IsNullOrEmpty(category.categoryName) ? category.name : $"{category.categoryName} ({category.name})"));
        foreach (ItemData item in LoadAll<ItemData>())
            if (!string.IsNullOrEmpty(item.itemId)) catalog.items.Add(Entry(item.itemId, $"{item.itemName} ({item.itemId})"));
        foreach (EnemyData enemy in LoadAll<EnemyData>())
            if (!string.IsNullOrEmpty(enemy.enemyId)) catalog.enemyTypes.Add(Entry(enemy.enemyId, enemy.name));
        foreach (ShippingOrderData order in LoadAll<ShippingOrderData>())
            if (!string.IsNullOrEmpty(order.orderId)) catalog.orders.Add(Entry(order.orderId, $"{order.customer} ({order.orderId})"));
        foreach (LootBoxData box in LoadAll<LootBoxData>())
            if (!string.IsNullOrEmpty(box.lootBoxId)) catalog.lootBoxes.Add(Entry(box.lootBoxId, $"{box.title} ({box.lootBoxId})"));

        // Квесты вне графа (для триггера «Выполнен квест»).
        StoryQuestCatalog storyQuests = AssetDatabase.LoadAssetAtPath<StoryQuestCatalog>(QuestCatalogPath);
        foreach (QuestData quest in LoadAll<QuestData>())
            if (!string.IsNullOrEmpty(quest.questId) && (storyQuests == null || !storyQuests.quests.Contains(quest)))
                catalog.quests.Add(Entry(quest.questId, $"{quest.title} ({quest.questId})"));

        RadioCallUI radio = SceneSetupUtility.FindInScene<RadioCallUI>(scene);
        if (radio != null)
            foreach (RadioPortrait portrait in radio.portraits)
                if (portrait != null && !string.IsNullOrEmpty(portrait.id)) catalog.speakers.Add(Entry(portrait.id, portrait.id));

        UIBuilderKit.EnsureFolder(StoryFolder);
        File.WriteAllText(FullPath(CatalogPath), JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(CatalogPath);

        string summary = $"Точки ремонта: {catalog.repairPoints.Count}, разрушаемые: {catalog.breakables.Count}, пятна: {catalog.stains.Count}, " +
                         $"полки: {catalog.shelves.Count}, зоны: {catalog.zones.Count}, предметы: {catalog.items.Count}, точки спавна: {catalog.enemySpawns.Count}, " +
                         $"объекты сюжета: {catalog.storyObjects.Count}.";
        Debug.Log($"[Story] {CatalogPath}: {summary}");
        EditorUtility.DisplayDialog(Title, $"Каталог сохранён в {CatalogPath}.\n\n{summary}\n\nЗагрузите его в веб-редакторе (кнопка «Каталог») " +
                                           "или закоммитьте — Claude загрузит сам." + (missing.Count > 0 ? "\n\nНе забудьте сохранить сцену." : ""), "OK");
    }

    private static void CollectMissingIds<T>(Scene scene, List<Component> missing) where T : Component
    {
        foreach (T component in SceneSetupUtility.FindAllInScene<T>(scene))
            if (component.GetComponent<PersistentId>() == null) missing.Add(component);
    }

    private static void AddPersistentId(GameObject go)
    {
        var persistentId = Undo.AddComponent<PersistentId>(go);
        var serialized = new SerializedObject(persistentId);
        SerializedProperty id = serialized.FindProperty("id");
        if (id != null && string.IsNullOrEmpty(id.stringValue))
        {
            id.stringValue = Guid.NewGuid().ToString("N");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        SceneSetupUtility.MarkModified(persistentId);
    }

    private static void AddById<T>(Scene scene, List<CatalogEntry> list, Func<T, string> extra) where T : Component
    {
        foreach (T component in SceneSetupUtility.FindAllInScene<T>(scene))
        {
            PersistentId persistentId = component.GetComponent<PersistentId>();
            if (persistentId == null || string.IsNullOrEmpty(persistentId.Id)) continue;
            string suffix = extra != null ? extra(component) : null;
            list.Add(Entry(persistentId.Id, ShortPath(component.transform) + (string.IsNullOrEmpty(suffix) ? "" : $" [{suffix}]")));
        }
    }

    // ───────────────────────── Миграция текущих квестов ─────────────────────────

    [MenuItem(MenuRoot + "Export Existing Quests To Graph", priority = 21)]
    private static void ExportExistingQuests()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;
        QuestManager manager = SceneSetupUtility.FindInScene<QuestManager>(SceneManager.GetActiveScene());
        if (manager == null)
        {
            EditorUtility.DisplayDialog(Title, "Откройте игровую сцену (TestScene) — квесты берутся из её QuestManager.", "OK");
            return;
        }
        if (File.Exists(FullPath(GraphPath)) && !EditorUtility.DisplayDialog(Title,
                $"{GraphPath} уже есть. Заменить его графом из текущих квестов?", "Заменить", "Отмена"))
            return;

        StoryGraphData graph = BuildGraphFromQuests(manager.autoStartQuests.Where(q => q != null).Distinct().ToList());
        UIBuilderKit.EnsureFolder(StoryFolder);
        File.WriteAllText(FullPath(GraphPath), JsonUtility.ToJson(graph, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(GraphPath);

        EditorUtility.DisplayDialog(Title, $"Граф из {graph.nodes.Count(n => n.type == StoryNodeData.Quest)} квестов сохранён в {GraphPath}.\n\n" +
                                           "Откройте его в веб-редакторе («Открыть JSON») или закоммитьте — Claude загрузит его в редактор.", "OK");
    }

    /// <summary>Квест → нода «Квест»; prerequisiteQuests → нитки; requiredLevel &gt; 1 → триггер уровня (+ «И»);
    /// квест без условий — от «Старта». Раскладка — колонками по глубине цепочки.</summary>
    private static StoryGraphData BuildGraphFromQuests(List<QuestData> quests)
    {
        var table = LocalizationEditorTools.StringsTable.Load(LocalizationEditorTools.StringsPath);
        var languages = table.LanguageColumns().ToList();
        if (languages.Count == 0) languages.Add("ru");

        var nodes = new List<StoryNodeData>();
        var links = new List<StoryLinkData>();
        var start = new StoryNodeData { id = "n_start", type = StoryNodeData.Start, x = 0, y = 0 };
        nodes.Add(start);

        var depth = new Dictionary<QuestData, int>();
        int Depth(QuestData q, int guard)
        {
            if (depth.TryGetValue(q, out int d)) return d;
            if (guard > 50) return 0;
            int best = 0;
            foreach (QuestData p in q.prerequisiteQuests ?? Array.Empty<QuestData>())
                if (p != null && quests.Contains(p)) best = Mathf.Max(best, Depth(p, guard + 1) + 1);
            return depth[q] = best;
        }

        var rowsPerColumn = new Dictionary<int, int>();
        var nodeByQuest = new Dictionary<QuestData, StoryNodeData>();
        foreach (QuestData quest in quests)
        {
            int column = Depth(quest, 0);
            int row = rowsPerColumn.TryGetValue(column, out int r) ? r : 0;
            rowsPerColumn[column] = row + 1;

            var node = new StoryNodeData
            {
                id = "q_" + quest.questId,
                type = StoryNodeData.Quest,
                x = 340 + column * 340,
                y = row * 190,
                questId = quest.questId,
                title = Texts(table, languages, Loc.DataKey("quest", quest.questId, "title"), quest.title),
                description = Texts(table, languages, Loc.DataKey("quest", quest.questId, "desc"), quest.description),
                questType = quest.type.ToString(),
                targetCount = quest.targetCount,
                target = quest.targetPersistentId ?? "",
                category = quest.targetCategory != null ? quest.targetCategory.name : "",
                item = quest.targetItem != null ? quest.targetItem.itemId : "",
                zone = quest.targetZoneId ?? "",
                xp = quest.xpReward,
                money = quest.moneyReward,
            };
            nodes.Add(node);
            nodeByQuest[quest] = node;
        }

        foreach (QuestData quest in quests)
        {
            StoryNodeData node = nodeByQuest[quest];
            var sources = (quest.prerequisiteQuests ?? Array.Empty<QuestData>()).Where(p => p != null && nodeByQuest.ContainsKey(p))
                .Select(p => nodeByQuest[p].id).ToList();

            if (quest.requiredLevel > 1)
            {
                var level = new StoryNodeData { id = "n_level_" + quest.questId, type = StoryNodeData.Trigger, trigger = "levelReached",
                                                value = quest.requiredLevel, x = node.x - 300, y = node.y + 90 };
                nodes.Add(level);
                sources.Add(level.id);
            }

            if (sources.Count == 0) links.Add(Link(start.id, node.id));
            else if (sources.Count == 1) links.Add(Link(sources[0], node.id));
            else
            {
                var and = new StoryNodeData { id = "n_and_" + quest.questId, type = StoryNodeData.And, x = node.x - 150, y = node.y + 40 };
                nodes.Add(and);
                foreach (string source in sources) links.Add(Link(source, and.id));
                links.Add(Link(and.id, node.id));
            }
        }

        return new StoryGraphData { version = 1, languages = languages.ToArray(), nodes = nodes.ToArray(), links = links.ToArray() };
    }

    private static StoryLocText[] Texts(LocalizationEditorTools.StringsTable table, List<string> languages, string key, string fallback)
    {
        var result = new List<StoryLocText>();
        string[] row = key != null ? table.FindRow(key) : null;
        for (int i = 0; i < languages.Count; i++)
        {
            string text = row != null && i + 1 < row.Length ? row[i + 1] : "";
            if (string.IsNullOrEmpty(text) && i == 0) text = fallback;
            if (!string.IsNullOrEmpty(text)) result.Add(new StoryLocText { lang = languages[i], text = text });
        }
        return result.ToArray();
    }

    private static StoryLinkData Link(string from, string to) => new StoryLinkData { from = from, fromPort = "out", to = to, toPort = "in" };

    // ───────────────────────── Прочее ─────────────────────────

    [MenuItem(MenuRoot + "Open Story Editor (web)", priority = 40)]
    private static void OpenEditor() => Application.OpenURL(EditorUrl);

    [MenuItem(MenuRoot + "Rebuild Radio UI", priority = 41)]
    private static void RebuildRadioUI()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;
        StoryUIBuilder.BuildRadioUI();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(Title, $"Префаб {StoryUIBuilder.RadioPath} пересобран. Ссылки в сцене сохранятся — это тот же префаб.", "OK");
    }

    private static IEnumerable<T> LoadAll<T>() where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/01_GAME" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) yield return asset;
        }
    }

    private static CatalogEntry Entry(string id, string name) => new CatalogEntry { id = id, name = name };

    private static string ShortPath(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null && parts.Count < 3; t = t.parent) parts.Insert(0, t.name);
        return string.Join("/", parts);
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";

    private static string FullPath(string assetPath) => Path.Combine(Directory.GetCurrentDirectory(), assetPath);
}
