using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полоса выносливости внизу экрана. Длина полосы — весь максимум бара, на ней четыре зоны слева направо:
///   [белая — сколько сил сейчас][тёмная — до потолка, восстановится сама]
///   [янтарная — часть потолка, которую держит стимулятор][тёмно-красная — заперто усталостью до сна]
/// Под полосой — состояние («Одышка», «Вымотан — пора спать»), рядом — иконки действующих эффектов.
/// Когда бар полон и ничего не происходит, полоса притухает, но не исчезает: усталость видна всегда.
/// </summary>
public class StaminaBarUI : MonoBehaviour
{
    [Header("Источник")]
    [Tooltip("Выносливость игрока.")]
    public PlayerStamina stamina;

    [Tooltip("Действующие эффекты (иконки рядом с полосой).")]
    public PlayerStatusEffects effects;

    [Tooltip("Игровые часы: остаток времени эффектов.")]
    public GameClock clock;

    [Header("Зоны (Image Type = Filled, Horizontal)")]
    [Tooltip("Текущая выносливость — белая, от левого края.")]
    public Image currentFill;

    [Tooltip("До потолка без стимуляторов — тёмная, от левого края (рисуется поверх янтарной).")]
    public Image capFill;

    [Tooltip("До потолка со стимуляторами — янтарная, от левого края (рисуется под тёмной).")]
    public Image maskFill;

    [Tooltip("Заперто усталостью — тёмно-красная, от ПРАВОГО края (Fill Origin = Right).")]
    public Image lockedFill;

    [Header("Цвета")]
    [Tooltip("Текущая выносливость.")]
    public Color currentColor = new Color(0.95f, 0.95f, 0.9f);

    [Tooltip("Текущая выносливость при одышке.")]
    public Color windedColor = new Color(1f, 0.45f, 0.35f);

    [Tooltip("Заперто усталостью.")]
    public Color lockedColor = new Color(0.45f, 0.08f, 0.08f, 0.85f);

    [Tooltip("Заперто усталостью, когда игрок вымотан (мигает между этим цветом и обычным).")]
    public Color exhaustedLockedColor = new Color(0.8f, 0.15f, 0.12f, 0.95f);

    [Header("Состояние")]
    [Tooltip("Надпись под полосой: «Одышка», «Тяжёлый груз», «Вы устали», «Вымотан — пора спать».")]
    public Text stateText;

    [Header("Эффекты")]
    [Tooltip("Контейнер иконок эффектов (Horizontal Layout Group).")]
    public RectTransform effectsRoot;

    [Tooltip("Шаблон иконки эффекта (выключенный объект внутри effectsRoot).")]
    public StatusEffectIconUI iconTemplate;

    [Header("Притухание")]
    [Tooltip("Прозрачность всей полосы (CanvasGroup).")]
    public CanvasGroup group;

    [Tooltip("Прозрачность, когда бар полон и ничего не меняется.")]
    [Range(0f, 1f)] public float idleAlpha = 0.45f;

    [Tooltip("Через сколько секунд покоя полоса притухает.")]
    [Min(0f)] public float idleDelay = 2.5f;

    [Tooltip("Скорость появления и притухания, долей в секунду.")]
    [Min(0.1f)] public float fadeSpeed = 4f;

    [Tooltip("Резкость, с которой зоны догоняют реальные значения.")]
    [Min(1f)] public float followSharpness = 14f;

    private readonly List<StatusEffectIconUI> icons = new List<StatusEffectIconUI>();
    private float shownCurrent;
    private float shownCap;
    private float shownCapNoMask;
    private float lastActiveTime = float.NegativeInfinity;
    private float lastCap = -1f;
    private string lastState;

    private void Awake()
    {
        if (iconTemplate != null) iconTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (effects != null) effects.OnEffectsChanged += RebuildIcons;
        RebuildIcons();
        SnapToValues();
    }

    private void OnDisable()
    {
        if (effects != null) effects.OnEffectsChanged -= RebuildIcons;
    }

    private void LateUpdate()
    {
        if (stamina == null) return;

        float max = Mathf.Max(1f, stamina.Max);
        float current = stamina.Current / max;
        float cap = stamina.Cap / max;
        float capNoMask = stamina.CapWithoutEffects / max;

        float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        shownCurrent = Mathf.Lerp(shownCurrent, current, k);
        shownCap = Mathf.Lerp(shownCap, cap, k);
        shownCapNoMask = Mathf.Lerp(shownCapNoMask, capNoMask, k);

        if (currentFill != null)
        {
            currentFill.fillAmount = shownCurrent;
            currentFill.color = stamina.IsWinded ? windedColor : currentColor;
        }
        if (capFill != null) capFill.fillAmount = shownCapNoMask;
        if (maskFill != null) maskFill.fillAmount = shownCap;
        if (lockedFill != null)
        {
            lockedFill.fillAmount = 1f - shownCap;
            lockedFill.color = stamina.IsExhausted
                ? Color.Lerp(lockedColor, exhaustedLockedColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f))
                : lockedColor;
        }

        UpdateStateText();
        UpdateIcons();
        UpdateAlpha(current, cap);
    }

    private void UpdateStateText()
    {
        if (stateText == null) return;

        string state = "";
        if (stamina.IsExhausted) state = Loc.Get("hud.stamina.exhausted");
        else if (stamina.IsWinded) state = Loc.Get("hud.stamina.winded");
        else if (stamina.IsCarryingHeavy) state = Loc.Get("hud.stamina.heavy");
        else if (stamina.IsTired) state = Loc.Get("hud.stamina.tired");

        if (state == lastState) return;
        lastState = state;
        stateText.text = state;
    }

    private void UpdateAlpha(float current, float cap)
    {
        if (group == null) return;

        // «Активна» — пока бар не полон, есть особое состояние или эффекты, или потолок только что сдвинулся.
        bool active = current < cap - 0.005f || stamina.IsWinded || stamina.IsExhausted || stamina.IsCarryingHeavy
                      || (effects != null && effects.Active.Count > 0) || Mathf.Abs(cap - lastCap) > 0.0005f;
        lastCap = cap;
        if (active) lastActiveTime = Time.unscaledTime;

        float target = Time.unscaledTime - lastActiveTime < idleDelay ? 1f : idleAlpha;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
    }

    private void SnapToValues()
    {
        if (stamina == null) return;
        float max = Mathf.Max(1f, stamina.Max);
        shownCurrent = stamina.Current / max;
        shownCap = stamina.Cap / max;
        shownCapNoMask = stamina.CapWithoutEffects / max;
    }

    private void RebuildIcons()
    {
        if (iconTemplate == null || effectsRoot == null) return;

        int count = effects != null ? effects.Active.Count : 0;
        while (icons.Count < count)
        {
            StatusEffectIconUI icon = Instantiate(iconTemplate, effectsRoot);
            icons.Add(icon);
        }
        for (int i = 0; i < icons.Count; i++) icons[i].gameObject.SetActive(i < count);
        UpdateIcons();
    }

    private void UpdateIcons()
    {
        if (effects == null || icons.Count == 0) return;

        double now = clock != null ? clock.TotalHours : 0.0;
        int count = Mathf.Min(icons.Count, effects.Active.Count);
        for (int i = 0; i < count; i++) icons[i].Bind(effects.Active[i], now);
    }
}
