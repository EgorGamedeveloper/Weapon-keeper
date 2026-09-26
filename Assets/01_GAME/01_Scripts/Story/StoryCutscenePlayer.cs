using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Показ кат-сцен сюжета (нода «Кат-сцена» → StoryDirector → сюда). Кат-сцены идут по очереди и не
/// перебивают разговор по рации: пока рация в руке, следующая ждёт; рация, в свою очередь, не звонит во время
/// кат-сцены (RadioCallUI.cutscenes).
///
/// На время показа: управление игрока выключено (disableDuringCutscene), оружие в кобуре, предмет из рук спрятан,
/// HUD погашен (если нода не просит оставить), сверху и снизу — чёрные полосы. Пропуск — удерживать клавишу
/// действия (E) skipHoldSeconds секунд; подсказка с полоской заполнения. Время показа игровое: на паузе стоит.
/// </summary>
public class StoryCutscenePlayer : MonoBehaviour
{
    private class Request
    {
        public StoryCutscene cutscene;
        public bool skippable;
        public bool hideHud;
        public Action onFinished;
    }

    [Header("Игрок")]
    [Tooltip("Взаимодействие с предметами: его клавиша действия (takeKey) становится клавишей пропуска.")]
    public PlayerItemInteraction itemInteraction;
    [Tooltip("Клавиша пропуска, если PlayerItemInteraction не задан.")]
    public KeyCode skipKey = KeyCode.E;
    [Tooltip("Компоненты, выключаемые на время кат-сцены: движение игрока, взаимодействие, смена режима инвентаря.")]
    public Behaviour[] disableDuringCutscene = Array.Empty<Behaviour>();
    [Tooltip("Кобура оружия: оружие убирается на время кат-сцены и возвращается, только если было в руках.")]
    public WeaponHolster weaponHolster;
    [Tooltip("Предмет уборки в руке: прячется на время кат-сцены.")]
    public EquippedItemHolder heldItems;
    [Tooltip("Корни HUD, которые гаснут на время кат-сцены (если у ноды включено «Прятать интерфейс»).")]
    public CanvasGroup[] hudGroups = Array.Empty<CanvasGroup>();
    [Tooltip("Рация: кат-сцена не начнётся посреди разговора.")]
    public RadioCallUI radio;

    [Header("Экран")]
    [Tooltip("Чёрные полосы сверху и снизу (проявляются на время показа). Пусто — без полос.")]
    public CanvasGroup letterbox;
    [Tooltip("Подсказка пропуска (видна только у кат-сцен, которые можно пропустить).")]
    public CanvasGroup skipGroup;
    [Tooltip("Текст подсказки «Удерживайте [E] — пропустить».")]
    public TMP_Text skipText;
    [Tooltip("Полоска заполнения при удержании (Image Type = Filled).")]
    public Image skipFill;

    [Header("Пропуск")]
    [Tooltip("Сколько секунд держать клавишу, чтобы пропустить.")]
    [Min(0.1f)] public float skipHoldSeconds = 1f;

    /// <summary>Идёт кат-сцена.</summary>
    public bool IsPlaying => current != null;

    /// <summary>Кат-сцена началась / закончилась.</summary>
    public event Action<StoryCutscene> OnCutsceneStarted;
    public event Action<StoryCutscene> OnCutsceneFinished;

    private readonly Queue<Request> queue = new Queue<Request>();
    private Request current;
    private float hold;

    private readonly Dictionary<Behaviour, bool> disabledStates = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<CanvasGroup, float> hudAlpha = new Dictionary<CanvasGroup, float>();
    private bool weaponWasOut;
    private bool heldRootWasActive;
    private LocalizationService localization;

    private void Awake()
    {
        if (itemInteraction != null) skipKey = itemInteraction.takeKey;
        SetGroup(letterbox, 0f);
        SetGroup(skipGroup, 0f);
    }

    private void OnEnable()
    {
        localization = LocalizationService.Instance;
        if (localization != null) localization.OnLanguageChanged += RefreshTexts;
    }

    private void OnDisable()
    {
        if (localization != null) localization.OnLanguageChanged -= RefreshTexts;
        localization = null;
        if (current != null) Finish(true, true);
    }

    /// <summary>Поставить кат-сцену в очередь. onFinished — когда досмотрена или пропущена.</summary>
    public void Enqueue(StoryCutscene cutscene, bool skippable, bool hideHud, Action onFinished)
    {
        if (cutscene == null)
        {
            onFinished?.Invoke();
            return;
        }
        queue.Enqueue(new Request { cutscene = cutscene, skippable = skippable, hideHud = hideHud, onFinished = onFinished });
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return; // пауза: показ стоит

        if (current == null)
        {
            if (queue.Count == 0 || (radio != null && radio.IsTalking)) return;
            Begin(queue.Dequeue());
            return;
        }

        if (current.skippable && Input.GetKey(skipKey))
        {
            hold += Time.unscaledDeltaTime;
            if (hold >= skipHoldSeconds)
            {
                Finish(true, false);
                return;
            }
        }
        else hold = Mathf.Max(0f, hold - Time.unscaledDeltaTime * 2f);
        if (skipFill != null) skipFill.fillAmount = hold / skipHoldSeconds;

        if (current.cutscene == null || current.cutscene.Tick(Time.deltaTime)) Finish(false, false);
    }

    private void Begin(Request request)
    {
        current = request;
        hold = 0f;
        HidePlayer(request.hideHud);
        Fade(letterbox, 1f, 0.35f);
        if (request.skippable)
        {
            RefreshTexts();
            if (skipFill != null) skipFill.fillAmount = 0f;
            Fade(skipGroup, 1f, 0.3f);
        }
        request.cutscene.Begin();
        OnCutsceneStarted?.Invoke(request.cutscene);
    }

    private void Finish(bool skipped, bool immediate)
    {
        Request finished = current;
        current = null;
        hold = 0f;
        if (finished.cutscene != null) finished.cutscene.End(skipped);
        RestorePlayer(immediate);
        Fade(letterbox, 0f, immediate ? 0f : 0.35f);
        Fade(skipGroup, 0f, immediate ? 0f : 0.2f);
        OnCutsceneFinished?.Invoke(finished.cutscene);
        finished.onFinished?.Invoke();
    }

    private void RefreshTexts()
    {
        if (skipText != null) skipText.text = Loc.Get("cutscene.skip", skipKey.ToString());
    }

    // ───────────────────────── Игрок на время показа ─────────────────────────

    private void HidePlayer(bool hideHud)
    {
        disabledStates.Clear();
        foreach (Behaviour behaviour in disableDuringCutscene)
        {
            if (behaviour == null || disabledStates.ContainsKey(behaviour)) continue;
            disabledStates[behaviour] = behaviour.enabled;
            behaviour.enabled = false;
        }

        weaponWasOut = weaponHolster != null && !weaponHolster.IsHolstered;
        if (weaponWasOut) weaponHolster.Holster();

        Transform heldRoot = heldItems != null ? heldItems.heldItemVisualRoot : null;
        heldRootWasActive = heldRoot != null && heldRoot.gameObject.activeSelf;
        if (heldRootWasActive) heldRoot.gameObject.SetActive(false);

        hudAlpha.Clear();
        if (!hideHud) return;
        foreach (CanvasGroup group in hudGroups)
        {
            if (group == null || hudAlpha.ContainsKey(group)) continue;
            hudAlpha[group] = group.alpha;
            group.DOKill();
            group.DOFade(0f, 0.25f);
        }
    }

    private void RestorePlayer(bool immediate)
    {
        foreach (KeyValuePair<Behaviour, bool> pair in disabledStates)
            if (pair.Key != null) pair.Key.enabled = pair.Value;
        disabledStates.Clear();

        if (weaponWasOut && weaponHolster != null) weaponHolster.Unholster();
        weaponWasOut = false;

        Transform heldRoot = heldItems != null ? heldItems.heldItemVisualRoot : null;
        if (heldRootWasActive && heldRoot != null) heldRoot.gameObject.SetActive(true);
        heldRootWasActive = false;

        foreach (KeyValuePair<CanvasGroup, float> pair in hudAlpha)
        {
            if (pair.Key == null) continue;
            pair.Key.DOKill();
            if (immediate) pair.Key.alpha = pair.Value;
            else pair.Key.DOFade(pair.Value, 0.3f);
        }
        hudAlpha.Clear();
    }

    private static void Fade(CanvasGroup group, float alpha, float seconds)
    {
        if (group == null) return;
        group.DOKill();
        if (seconds <= 0f) group.alpha = alpha;
        else group.DOFade(alpha, seconds);
    }

    private static void SetGroup(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
