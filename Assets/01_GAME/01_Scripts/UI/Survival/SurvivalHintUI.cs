using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подсказки выживания у нижнего края экрана:
/// - постоянная — «F — съесть: Консервы · сытость 40%», пока в руке еда или стимулятор;
/// - короткие сообщения — отказы («Не лезет», «Сердце не выдержит») и перемены состояния
///   («Вы устали», «Вы вымотаны — идите спать или выпейте кофе»).
/// </summary>
public class SurvivalHintUI : MonoBehaviour
{
    [Header("Источник")]
    [Tooltip("Еда и стимуляторы: что в руке и сытость.")]
    public PlayerConsumption consumption;

    [Tooltip("Выносливость: сообщения «Вы устали» и «Вы вымотаны».")]
    public PlayerStamina stamina;

    [Header("Элементы")]
    [Tooltip("Постоянная подсказка «F — съесть: …».")]
    public Text useHintText;

    [Tooltip("Короткое сообщение.")]
    public Text messageText;

    [Tooltip("Прозрачность сообщения (CanvasGroup).")]
    public CanvasGroup messageGroup;

    [Header("Сообщения")]
    [Tooltip("Сколько секунд сообщение висит на экране.")]
    [Min(0.2f)] public float messageDuration = 2.5f;

    [Tooltip("Обычный цвет сообщения.")]
    public Color normalColor = Color.white;

    [Tooltip("Цвет предупреждения.")]
    public Color warningColor = new Color(1f, 0.55f, 0.4f);

    private Sequence messageSequence;
    private ItemData lastItem;
    private int lastSatiety = -1;
    private LocalizationService localization;

    private void Awake()
    {
        if (messageGroup != null) messageGroup.alpha = 0f;
        if (useHintText != null) useHintText.text = "";
    }

    private void OnEnable()
    {
        // Язык сменили в настройках — подсказка перерисуется в следующем кадре.
        localization = LocalizationService.Instance;
        if (localization != null) localization.OnLanguageChanged += HandleLanguageChanged;

        if (stamina == null) return;
        stamina.OnTiredChanged += HandleTiredChanged;
        stamina.OnExhaustedChanged += HandleExhaustedChanged;
    }

    private void OnDisable()
    {
        messageSequence?.Kill();
        if (localization != null) localization.OnLanguageChanged -= HandleLanguageChanged;
        localization = null;

        if (stamina == null) return;
        stamina.OnTiredChanged -= HandleTiredChanged;
        stamina.OnExhaustedChanged -= HandleExhaustedChanged;
    }

    private void HandleLanguageChanged()
    {
        lastItem = null;
        lastSatiety = -1;
    }

    /// <summary>Показать короткое сообщение. warning — предупреждающим цветом.</summary>
    public void ShowMessage(string text, bool warning = false)
    {
        if (messageText == null || string.IsNullOrEmpty(text)) return;

        messageText.text = text;
        messageText.color = warning ? warningColor : normalColor;
        if (messageGroup == null) return;

        messageSequence?.Kill();
        messageSequence = DOTween.Sequence()
            .Append(messageGroup.DOFade(1f, 0.15f))
            .AppendInterval(messageDuration)
            .Append(messageGroup.DOFade(0f, 0.4f))
            .SetUpdate(true);
    }

    private void LateUpdate()
    {
        if (useHintText == null || consumption == null) return;

        ItemData item = consumption.ActiveConsumable;
        int satiety = Mathf.RoundToInt(consumption.Satiety);
        if (item == lastItem && satiety == lastSatiety) return;
        lastItem = item;
        lastSatiety = satiety;

        useHintText.text = item != null ? BuildUseHint(item, satiety) : "";
    }

    private string BuildUseHint(ItemData item, int satiety)
    {
        ConsumableData consumable = item.consumable;
        string verbKey = consumable.kind == ConsumableKind.Drink ? "hud.use.drink"
                       : consumable.kind == ConsumableKind.Injection ? "hud.use.inject"
                       : "hud.use.eat";
        string hint = Loc.Get(verbKey, consumption.useKey.ToString(), item.DisplayName);
        if (consumable.satiety > 0f) hint += "  ·  " + Loc.Get("hud.use.satiety", satiety);
        return hint;
    }

    private void HandleTiredChanged(bool tired)
    {
        if (tired && stamina != null && !stamina.IsExhausted) ShowMessage(Loc.Get("hud.stamina.tired_message"));
    }

    private void HandleExhaustedChanged(bool exhausted)
    {
        ShowMessage(Loc.Get(exhausted ? "hud.stamina.exhausted_message" : "hud.stamina.recovered_message"), exhausted);
    }
}
