using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Один разговор по рации (нода «Рация» сюжетного графа).</summary>
public class RadioCall
{
    /// <summary>Id ноды — для лога.</summary>
    public string id;
    public StoryLocText[] speaker;
    public string portraitId;
    public StoryRadioLine[] lines;
    /// <summary>Разговор закончен (дослушан или пролистан).</summary>
    public Action onFinished;
}

/// <summary>Портрет говорящего по id из графа.</summary>
[Serializable]
public class RadioPortrait
{
    [Tooltip("Id из поля «Портрет» ноды «Рация».")]
    public string id;
    [Tooltip("Картинка говорящего.")]
    public Sprite sprite;
}

/// <summary>
/// Переговоры по рации:
/// 1) входящий вызов — плашка «Входящий вызов по рации — [E] ответить» и писк рации; вызов ждёт ответа,
///    а пока открыта пауза или окно (навыки, терминал), плашка прячется;
/// 2) ответ на E — HUD гаснет, оружие убирается в кобуру, предмет из рук прячется, перед камерой поднимается
///    рация, внизу экрана идёт строка: имя говорящего и «печатающийся» текст;
/// 3) E во время разговора — первое нажатие допечатывает реплику, второе переходит к следующей; без нажатий
///    реплики листаются сами по своей длительности;
/// 4) конец разговора — рация опускается, HUD и оружие возвращаются как были.
///
/// Ходить и смотреть во время разговора можно; взаимодействие с предметами, смена режима и стрельба
/// выключены (disableDuringCall + кобура). Разговоры идут по очереди. Время разговора — игровое: на паузе
/// текст замирает. DefaultExecutionOrder(-300): E обрабатывается раньше PlayerItemInteraction (-200), и
/// тем же нажатием, которым ответили, игрок не снимет предмет с полки — компонент к этому моменту уже выключен.
/// </summary>
[DefaultExecutionOrder(-300)]
public class RadioCallUI : MonoBehaviour
{
    [Header("Входящий вызов")]
    [Tooltip("Плашка входящего вызова (проявляется и гаснет).")]
    public CanvasGroup incomingGroup;
    [Tooltip("Панель плашки — «пульсирует», пока рация пищит.")]
    public RectTransform incomingPanel;
    [Tooltip("Текст «Входящий вызов по рации».")]
    public TMP_Text incomingTitle;
    [Tooltip("Подсказка «[E] Ответить».")]
    public TMP_Text incomingHint;

    [Header("Разговор")]
    [Tooltip("Строка субтитров внизу экрана (проявляется на время разговора).")]
    public CanvasGroup talkGroup;
    [Tooltip("Имя говорящего.")]
    public TMP_Text speakerText;
    [Tooltip("Текст реплики (печатается по буквам).")]
    public TMP_Text lineText;
    [Tooltip("Подсказка «[E] Дальше».")]
    public TMP_Text skipHint;
    [Tooltip("Портрет говорящего. Пусто или нет картинки — портрет скрыт.")]
    public Image portraitImage;
    [Tooltip("Скорость «печати», символов в секунду.")]
    [Min(1f)] public float charsPerSecond = 45f;
    [Tooltip("Минимальная длительность реплики после того, как она допечатана, с.")]
    [Min(0f)] public float minLineHold = 1.2f;

    [Header("Рация в руке")]
    [Tooltip("Модель рации перед камерой (дочерний объект камеры). Выключена, пока нет разговора.")]
    public Transform radioModel;
    [Tooltip("Положение рации в кадре во время разговора (локально к камере).")]
    public Vector3 raisedLocalPosition = new Vector3(0.18f, -0.16f, 0.42f);
    [Tooltip("Положение, откуда рация поднимается и куда опускается (ниже кадра).")]
    public Vector3 loweredLocalPosition = new Vector3(0.22f, -0.55f, 0.35f);
    [Tooltip("Сколько секунд рация поднимается/опускается.")]
    [Min(0.05f)] public float raiseDuration = 0.35f;

    [Header("Игрок")]
    [Tooltip("Клавиша ответа и пропуска реплик. Если задан PlayerItemInteraction — берётся его клавиша «взять» (E).")]
    public KeyCode answerKey = KeyCode.E;
    [Tooltip("Взаимодействие с предметами: его клавиша действия (takeKey) становится клавишей рации.")]
    public PlayerItemInteraction itemInteraction;
    [Tooltip("Корни HUD, которые гаснут на время разговора (CanvasGroup на канвасах интерфейса).")]
    public CanvasGroup[] hudGroups = Array.Empty<CanvasGroup>();
    [Tooltip("Компоненты, выключаемые на время разговора: взаимодействие с предметами, смена режима инвентаря.")]
    public Behaviour[] disableDuringCall = Array.Empty<Behaviour>();
    [Tooltip("Кобура оружия: оружие убирается на время разговора и возвращается, только если было в руках.")]
    public WeaponHolster weaponHolster;
    [Tooltip("Предмет уборки в руке: прячется на время разговора.")]
    public EquippedItemHolder heldItems;
    [Tooltip("Общая блокировка ввода окон: пока открыто окно (навыки, терминал, пауза), на вызов не ответить.")]
    public GameplayInputBlocker inputBlocker;

    [Header("Звук")]
    [Tooltip("Источник звуков рации (канал UI или SFX).")]
    public AudioSource audioSource;
    [Tooltip("Писк входящего вызова (повторяется, пока не ответят).")]
    public AudioClip ringClip;
    [Tooltip("Пауза между писками, с.")]
    [Min(0.3f)] public float ringInterval = 1.6f;
    [Tooltip("Щелчок/треск в начале разговора.")]
    public AudioClip answerClip;
    [Tooltip("Щелчок в конце разговора.")]
    public AudioClip hangUpClip;

    [Header("Портреты")]
    [Tooltip("Портреты говорящих по id из графа.")]
    public List<RadioPortrait> portraits = new List<RadioPortrait>();

    /// <summary>Идёт разговор (рация в руке).</summary>
    public bool IsTalking => state == State.Talking;

    /// <summary>Разговор начался / закончился.</summary>
    public event Action<RadioCall> OnCallStarted;
    public event Action<RadioCall> OnCallFinished;

    private enum State { Idle, Ringing, Talking }

    private readonly Queue<RadioCall> queue = new Queue<RadioCall>();
    private State state;
    private RadioCall current;
    private int lineIndex;
    private float typed;
    private float holdTimer;
    private float ringTimer;
    private bool incomingShown;
    private bool talkBlocked;

    private readonly Dictionary<Behaviour, bool> disabledStates = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<CanvasGroup, float> hudAlpha = new Dictionary<CanvasGroup, float>();
    private bool weaponWasOut;
    private bool heldRootWasActive;
    private LocalizationService localization;

    private void Awake()
    {
        if (itemInteraction != null) answerKey = itemInteraction.takeKey;
        HideGroup(incomingGroup);
        HideGroup(talkGroup);
        if (radioModel != null) radioModel.gameObject.SetActive(false);
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
        if (state == State.Talking) RestorePlayer(true);
    }

    /// <summary>Поставить разговор в очередь. Вызов придёт, как только закончится текущий.</summary>
    public void Enqueue(RadioCall call)
    {
        if (call == null) return;
        if (call.lines == null || call.lines.Length == 0)
        {
            call.onFinished?.Invoke();
            return;
        }
        queue.Enqueue(call);
    }

    private void Update()
    {
        switch (state)
        {
            case State.Idle:
                if (queue.Count > 0)
                {
                    current = queue.Dequeue();
                    state = State.Ringing;
                    ringTimer = 0f;
                }
                break;

            case State.Ringing:
                UpdateRinging();
                break;

            case State.Talking:
                UpdateTalking();
                break;
        }
    }

    // ───────────────────────── Вызов ─────────────────────────

    /// <summary>Можно отвечать: игра не на паузе, окна закрыты, курсор в игре.</summary>
    private bool CanTalk =>
        Time.timeScale > 0f && (inputBlocker == null || !inputBlocker.IsBlocked) && Cursor.lockState == CursorLockMode.Locked;

    private void UpdateRinging()
    {
        bool can = CanTalk;
        if (can != incomingShown)
        {
            incomingShown = can;
            if (can)
            {
                RefreshTexts();
                UiWindowAnimation.Show(incomingGroup, incomingPanel);
                PulseIncoming(true);
            }
            else
            {
                PulseIncoming(false);
                UiWindowAnimation.Hide(incomingGroup, incomingPanel);
            }
        }
        if (!can) return;

        ringTimer -= Time.deltaTime;
        if (ringTimer <= 0f)
        {
            ringTimer = ringInterval;
            Play(ringClip);
        }

        if (Input.GetKeyDown(answerKey)) Answer();
    }

    private void Answer()
    {
        incomingShown = false;
        PulseIncoming(false);
        UiWindowAnimation.Hide(incomingGroup, incomingPanel);

        state = State.Talking;
        talkBlocked = false;
        lineIndex = 0;
        HidePlayer();
        Play(answerClip);
        UiWindowAnimation.Show(talkGroup, null);
        ShowLine();
        OnCallStarted?.Invoke(current);
    }

    private void PulseIncoming(bool on)
    {
        if (incomingPanel == null) return;
        incomingPanel.DOKill();
        incomingPanel.localScale = Vector3.one;
        if (on) incomingPanel.DOScale(1.04f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetDelay(0.3f);
    }

    // ───────────────────────── Разговор ─────────────────────────

    private void UpdateTalking()
    {
        // Открыли окно (навыки, терминал) или паузу: HUD возвращается, чтобы окно было видно; текст замирает
        // сам — время игровое.
        bool blocked = inputBlocker != null && inputBlocker.IsBlocked;
        if (blocked != talkBlocked)
        {
            talkBlocked = blocked;
            foreach (KeyValuePair<CanvasGroup, float> pair in hudAlpha)
            {
                if (pair.Key == null) continue;
                pair.Key.DOKill();
                pair.Key.alpha = blocked ? pair.Value : 0f;
            }
            if (talkGroup != null) { talkGroup.DOKill(); talkGroup.alpha = blocked ? 0f : 1f; }
        }
        if (blocked || Time.timeScale <= 0f) return;

        StoryRadioLine line = current.lines[lineIndex];
        string text = lineText != null ? lineText.text : "";
        bool fullyTyped = typed >= text.Length;

        if (Input.GetKeyDown(answerKey))
        {
            if (!fullyTyped) typed = text.Length;
            else
            {
                NextLine();
                return;
            }
        }

        if (!fullyTyped)
        {
            typed += charsPerSecond * Time.deltaTime;
            if (lineText != null) lineText.maxVisibleCharacters = Mathf.Min(text.Length, Mathf.FloorToInt(typed));
            return;
        }

        if (lineText != null) lineText.maxVisibleCharacters = text.Length;
        holdTimer += Time.deltaTime;
        float typingTime = text.Length / charsPerSecond;
        if (holdTimer >= Mathf.Max(minLineHold, line.seconds - typingTime)) NextLine();
    }

    private void ShowLine()
    {
        typed = 0f;
        holdTimer = 0f;
        RefreshTexts();
        if (lineText != null) lineText.maxVisibleCharacters = 0;
    }

    private void NextLine()
    {
        lineIndex++;
        if (lineIndex < current.lines.Length)
        {
            ShowLine();
            return;
        }
        Finish();
    }

    private void Finish()
    {
        RadioCall finished = current;
        current = null;
        state = State.Idle;
        UiWindowAnimation.Hide(talkGroup, null);
        Play(hangUpClip);
        RestorePlayer(false);
        OnCallFinished?.Invoke(finished);
        finished.onFinished?.Invoke();
    }

    private void RefreshTexts()
    {
        string key = answerKey.ToString();
        if (incomingTitle != null) incomingTitle.text = Loc.Get("radio.incoming");
        if (incomingHint != null) incomingHint.text = Loc.Get("radio.answer", key);
        if (skipHint != null) skipHint.text = Loc.Get("radio.skip", key);
        if (current == null) return;

        if (speakerText != null) speakerText.text = StoryText.Pick(current.speaker);
        if (portraitImage != null)
        {
            Sprite sprite = FindPortrait(current.portraitId);
            portraitImage.sprite = sprite;
            portraitImage.gameObject.SetActive(sprite != null);
        }
        if (state == State.Talking && lineText != null && lineIndex < current.lines.Length)
        {
            lineText.text = StoryText.Pick(current.lines[lineIndex].text);
            lineText.maxVisibleCharacters = Mathf.Min(lineText.text.Length, Mathf.FloorToInt(typed));
        }
    }

    private Sprite FindPortrait(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (RadioPortrait portrait in portraits)
            if (portrait != null && portrait.id == id) return portrait.sprite;
        return null;
    }

    // ───────────────────────── Игрок на время разговора ─────────────────────────

    private void HidePlayer()
    {
        disabledStates.Clear();
        foreach (Behaviour behaviour in disableDuringCall)
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
        foreach (CanvasGroup group in hudGroups)
        {
            if (group == null || hudAlpha.ContainsKey(group)) continue;
            hudAlpha[group] = group.alpha;
            group.DOKill();
            group.DOFade(0f, 0.25f);
        }

        if (radioModel != null)
        {
            radioModel.DOKill();
            radioModel.gameObject.SetActive(true);
            radioModel.localPosition = loweredLocalPosition;
            radioModel.DOLocalMove(raisedLocalPosition, raiseDuration).SetEase(Ease.OutCubic);
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

        if (radioModel == null) return;
        radioModel.DOKill();
        if (immediate) radioModel.gameObject.SetActive(false);
        else
            radioModel.DOLocalMove(loweredLocalPosition, raiseDuration).SetEase(Ease.InCubic)
                .OnComplete(() => { if (radioModel != null) radioModel.gameObject.SetActive(false); });
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private static void HideGroup(CanvasGroup group)
    {
        if (group != null) UiWindowAnimation.HideImmediate(group);
    }
}
