using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ведёт сюжет по графу story_graph.json (веб-редактор Tools/StoryEditor → Import Story Graph).
///
/// Правила графа: нода включается, когда завершилась ЛЮБАЯ нода, чья нитка в неё входит; нода «И» — когда
/// завершились ВСЕ. «Старт» и триггеры без входящих ниток включаются в начале игры. Включённая нода
/// завершается по-своему: квест — когда выполнен, рация — когда разговор закончен, кат-сцена — когда досмотрена
/// или пропущена, триггер — когда игрок сделал нужное, ожидание — через N секунд, «И» и «Событие» — сразу.
///
/// Сейв хранит только завершённые ноды (в порядке завершения). Всё остальное выводится заново: при загрузке
/// ноды, готовые к включению, включаются снова — квест продолжится (его прогресс восстановил QuestManager),
/// триггер перепроверит состояние сцены, недослушанный разговор придёт заново. Уже выданные награды нод
/// «Событие» не повторяются, а включение/выключение объектов сюжета применяется снова (сцена грузится в
/// исходном виде).
///
/// DefaultExecutionOrder(-1100): просыпается раньше SaveLoadService (-1000) — квесты графа уже
/// зарегистрированы в QuestManager, когда сейв ищет их по id. Сам граф пускается кадром позже Start, когда все
/// системы сцены проснулись и восстановленные квесты подписались на свои цели.
/// </summary>
[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
public class StoryDirector : MonoBehaviour
{
    [Header("Граф")]
    [Tooltip("story_graph.json (Assets/01_GAME/08_Story). Ставит Import Story Graph.")]
    public TextAsset graphJson;

    [Tooltip("Ассеты квестов графа (создаёт Import Story Graph).")]
    public StoryQuestCatalog questCatalog;

    [Header("Системы")]
    [Tooltip("Квесты. Пусто — найдётся в сцене.")]
    public QuestManager questManager;

    [Tooltip("Переговоры по рации. Пусто — ноды «Рация» пропускаются с предупреждением.")]
    public RadioCallUI radio;

    [Tooltip("Показ кат-сцен. Пусто — ноды «Кат-сцена» пропускаются с предупреждением.")]
    public StoryCutscenePlayer cutscenes;

    [Tooltip("Каталог предметов — для триггеров по конкретному предмету. Пусто — возьмётся у GameBootstrap.")]
    public ItemCatalog itemCatalog;

    [Header("Отладка")]
    [Tooltip("Писать в консоль, какие ноды включаются и завершаются.")]
    public bool logFlow;

    /// <summary>Нода завершилась (id ноды).</summary>
    public event Action<string> OnNodeCompleted;

    /// <summary>Разобранный граф (null — графа нет или он битый).</summary>
    public StoryGraphData Graph { get; private set; }

    private readonly Dictionary<string, StoryNodeData> nodes = new Dictionary<string, StoryNodeData>();
    private readonly Dictionary<string, List<string>> outgoing = new Dictionary<string, List<string>>();
    private readonly Dictionary<string, List<string>> incoming = new Dictionary<string, List<string>>();

    // Завершённые ноды: множество для проверок и список — порядок завершения (для повторного применения событий).
    private readonly HashSet<string> done = new HashSet<string>();
    private readonly List<string> doneOrder = new List<string>();
    private readonly HashSet<string> active = new HashSet<string>();

    private readonly Dictionary<string, StoryTrigger> triggers = new Dictionary<string, StoryTrigger>();
    private readonly Dictionary<string, Coroutine> waits = new Dictionary<string, Coroutine>();
    private readonly Dictionary<QuestData, string> questNodes = new Dictionary<QuestData, string>();

    private StoryScene scene;
    private bool running;

    private void Awake()
    {
        if (questManager == null) questManager = FindAnyObjectByType<QuestManager>();
        LoadGraph();

        // Раньше загрузки сейва: SaveLoadService найдёт квесты графа по id.
        if (questManager != null && questCatalog != null) questManager.RegisterStoryQuests(questCatalog.quests);
    }

    private void Start() => StartCoroutine(RunNextFrame());

    private void OnDestroy()
    {
        if (questManager != null) questManager.OnQuestCompleted -= HandleQuestCompleted;
        foreach (StoryTrigger trigger in triggers.Values) trigger.End();
        triggers.Clear();
        running = false;
    }

    // ───────────────────────── Сейв ─────────────────────────

    /// <summary>Завершённые ноды в порядке завершения — для SaveGameData.storyDoneNodes.</summary>
    public string[] CaptureDoneNodes() => doneOrder.ToArray();

    /// <summary>Восстановить завершённые ноды из сейва (без событий; ноды, которых больше нет в графе, пропускаются).</summary>
    public void RestoreDoneNodes(string[] ids)
    {
        if (ids == null) return;
        foreach (string id in ids)
            if (!string.IsNullOrEmpty(id) && nodes.ContainsKey(id) && done.Add(id)) doneOrder.Add(id);
    }

    /// <summary>Нода завершена.</summary>
    public bool IsDone(string nodeId) => done.Contains(nodeId);

    /// <summary>Нода сейчас включена и ждёт своего завершения.</summary>
    public bool IsActive(string nodeId) => active.Contains(nodeId);

    /// <summary>Включённые ноды — для отладки.</summary>
    public IEnumerable<string> ActiveNodeIds => active;

    // ───────────────────────── Граф ─────────────────────────

    private void LoadGraph()
    {
        if (graphJson == null)
        {
            Debug.LogWarning("[Story] Не назначен story_graph.json — сюжет выключен (Tools/Weapon Keeper/Story/Import Story Graph).", this);
            return;
        }

        Graph = StoryGraphData.Parse(graphJson.text, out string error);
        if (Graph == null)
        {
            Debug.LogError($"[Story] story_graph.json не читается: {error}", this);
            return;
        }

        foreach (StoryNodeData node in Graph.nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.id)) continue;
            nodes[node.id] = node;
            outgoing[node.id] = new List<string>();
            incoming[node.id] = new List<string>();
        }
        foreach (StoryLinkData link in Graph.links)
        {
            if (link == null || !nodes.ContainsKey(link.from) || !nodes.ContainsKey(link.to)) continue;
            if (!outgoing[link.from].Contains(link.to)) outgoing[link.from].Add(link.to);
            if (!incoming[link.to].Contains(link.from)) incoming[link.to].Add(link.from);
        }
    }

    private IEnumerator RunNextFrame()
    {
        yield return null;
        if (nodes.Count == 0) yield break;

        scene = StoryScene.Collect(this);
        if (questManager != null) questManager.OnQuestCompleted += HandleQuestCompleted;
        running = true;

        // Сцена грузится в исходном виде — объекты сюжета возвращаются в то состояние, в которое их привели пройденные ноды.
        foreach (string id in doneOrder)
        {
            StoryNodeData node = nodes[id];
            if (node.type == StoryNodeData.Action && IsObjectAction(node.action)) ExecuteAction(node);
        }

        foreach (StoryNodeData node in Graph.nodes)
            if (node != null && nodes.ContainsKey(node.id ?? "") && !done.Contains(node.id) && IsReady(node))
                Activate(node);
    }

    /// <summary>Готова ли нода к включению по состоянию входящих ниток.</summary>
    private bool IsReady(StoryNodeData node)
    {
        if (node.type == StoryNodeData.Note) return false;
        if (node.type == StoryNodeData.Start) return true;

        List<string> sources = incoming[node.id];
        if (sources.Count == 0) return node.type == StoryNodeData.Trigger; // триггер без входа слушает с начала игры

        if (node.type == StoryNodeData.And)
        {
            foreach (string source in sources) if (!done.Contains(source)) return false;
            return true;
        }

        foreach (string source in sources) if (done.Contains(source)) return true;
        return false;
    }

    private void Activate(StoryNodeData node)
    {
        if (!running || done.Contains(node.id) || !active.Add(node.id)) return;
        if (logFlow) Debug.Log($"[Story] ▶ {node.type} {node.id}", this);

        switch (node.type)
        {
            case StoryNodeData.Start:
            case StoryNodeData.And:
                Complete(node);
                break;

            case StoryNodeData.Quest:
                ActivateQuest(node);
                break;

            case StoryNodeData.Radio:
                ActivateRadio(node);
                break;

            case StoryNodeData.Cutscene:
                ActivateCutscene(node);
                break;

            case StoryNodeData.Trigger:
                ActivateTrigger(node);
                break;

            case StoryNodeData.Wait:
                waits[node.id] = StartCoroutine(WaitRoutine(node));
                break;

            case StoryNodeData.Action:
                ExecuteAction(node);
                Complete(node);
                break;

            default:
                Debug.LogWarning($"[Story] Нода {node.id}: неизвестный тип «{node.type}» — пропущена.", this);
                Complete(node);
                break;
        }
    }

    private void Complete(StoryNodeData node)
    {
        if (!done.Add(node.id)) return;
        doneOrder.Add(node.id);
        active.Remove(node.id);

        if (triggers.TryGetValue(node.id, out StoryTrigger trigger))
        {
            trigger.End();
            triggers.Remove(node.id);
        }
        if (waits.TryGetValue(node.id, out Coroutine wait))
        {
            if (wait != null) StopCoroutine(wait);
            waits.Remove(node.id);
        }

        if (logFlow) Debug.Log($"[Story] ✔ {node.type} {node.id}", this);
        OnNodeCompleted?.Invoke(node.id);

        foreach (string nextId in outgoing[node.id])
        {
            StoryNodeData next = nodes[nextId];
            if (!done.Contains(nextId) && !active.Contains(nextId) && IsReady(next)) Activate(next);
        }
    }

    // ───────────────────────── Типы нод ─────────────────────────

    private void ActivateQuest(StoryNodeData node)
    {
        QuestData quest = questCatalog != null ? questCatalog.Find(node.questId) : null;
        if (quest == null && questManager != null) quest = questManager.FindByQuestId(node.questId);
        if (quest == null || questManager == null)
        {
            // Сюжет останавливается на этой ноде — ошибку видно сразу, а не через три квеста.
            Debug.LogError($"[Story] Квест «{node.questId}» (нода {node.id}) не найден — перезапустите Import Story Graph.", this);
            return;
        }

        questNodes[quest] = node.id;
        if (questManager.IsCompleted(quest)) Complete(node);
        else questManager.StartQuest(quest); // уже активный (восстановлен из сейва) повторно не стартует
    }

    private void HandleQuestCompleted(QuestProgress progress)
    {
        if (progress == null || progress.data == null) return;
        if (questNodes.TryGetValue(progress.data, out string nodeId) && active.Contains(nodeId)) Complete(nodes[nodeId]);
    }

    private void ActivateRadio(StoryNodeData node)
    {
        if (radio == null)
        {
            Debug.LogWarning($"[Story] Нода «Рация» {node.id}: на сцене нет RadioCallUI — разговор пропущен.", this);
            Complete(node);
            return;
        }

        radio.Enqueue(new RadioCall
        {
            id = node.id,
            speaker = node.speaker,
            portraitId = node.portrait,
            lines = node.lines,
            onFinished = () => { if (active.Contains(node.id)) Complete(node); },
        });
    }

    private void ActivateCutscene(StoryNodeData node)
    {
        StoryCutscene cutscene = scene != null ? scene.FindCutscene(node.target) : null;
        if (cutscenes == null || cutscene == null)
        {
            // Сюжет не застревает: нода проходит сразу, в консоли видно, чего не хватает.
            Debug.LogWarning(cutscenes == null
                ? $"[Story] Нода «Кат-сцена» {node.id}: на сцене нет StoryCutscenePlayer — кат-сцена пропущена."
                : $"[Story] Нода «Кат-сцена» {node.id}: в сцене нет StoryCutscene с id «{node.target}» — кат-сцена пропущена.", this);
            Complete(node);
            return;
        }
        cutscenes.Enqueue(cutscene, node.skippable, node.hideHud, () => { if (active.Contains(node.id)) Complete(node); });
    }

    private void ActivateTrigger(StoryNodeData node)
    {
        StoryTrigger trigger = StoryTrigger.Create(node.trigger);
        if (trigger == null)
        {
            Debug.LogError($"[Story] Нода {node.id}: неизвестный вид триггера «{node.trigger}».", this);
            return;
        }

        triggers[node.id] = trigger;
        trigger.Begin(node, scene, () => { if (active.Contains(node.id)) Complete(node); });
    }

    private IEnumerator WaitRoutine(StoryNodeData node)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, node.seconds));
        waits.Remove(node.id);
        if (active.Contains(node.id)) Complete(node);
    }

    private static bool IsObjectAction(string action) => action == "enableObject" || action == "disableObject";

    private void ExecuteAction(StoryNodeData node)
    {
        switch (node.action)
        {
            case "giveMoney":
                if (scene.Wallet != null) scene.Wallet.Add(Mathf.RoundToInt(node.value));
                else scene.Warn(node.id, $"Событие {node.id}: в сцене нет PlayerWallet.");
                break;

            case "giveXp":
                if (scene.Progression != null) scene.Progression.AddXP(Mathf.RoundToInt(node.value));
                else scene.Warn(node.id, $"Событие {node.id}: в сцене нет PlayerProgression.");
                break;

            case "enableObject":
            case "disableObject":
                StoryObject target = scene.FindStoryObject(node.target);
                if (target != null) target.SetStoryActive(node.action == "enableObject");
                else scene.Warn(node.id, $"Событие {node.id}: в сцене нет StoryObject «{node.target}».");
                break;

            default:
                scene.Warn(node.id, $"Событие {node.id}: неизвестное действие «{node.action}».");
                break;
        }
    }
}
