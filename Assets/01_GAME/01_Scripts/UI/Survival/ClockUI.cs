using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Часы в HUD: «☀ 14:30 · День 3». Минуты идут шагом minuteStep, чтобы цифры не мельтешили. После вечера
/// солнце сменяется луной, ночью время желтеет, а ближе к отключке — краснеет и мигает. Когда игрок вымотан
/// или уже ночь, под часами мигает «Пора спать».
/// </summary>
public class ClockUI : MonoBehaviour
{
    [Header("Источник")]
    [Tooltip("Игровые часы.")]
    public GameClock clock;

    [Tooltip("Выносливость: «Пора спать», когда игрок вымотан.")]
    public PlayerStamina stamina;

    [Tooltip("Сон: час отключки (чтобы заранее покраснеть). Пусто — 02:00.")]
    public SleepService sleepService;

    [Header("Элементы")]
    [Tooltip("Время «14:30».")]
    public Text timeText;

    [Tooltip("«День 3».")]
    public Text dayText;

    [Tooltip("Солнце или луна.")]
    public Image icon;

    [Tooltip("Картинка солнца.")]
    public Sprite sunSprite;

    [Tooltip("Картинка луны.")]
    public Sprite moonSprite;

    [Tooltip("Надпись «Пора спать» под часами.")]
    public Text sleepHint;

    [Header("Вид")]
    [Tooltip("Шаг минут на часах (10 — 14:30, 14:40…).")]
    [Range(1, 30)] public int minuteStep = 10;

    [Tooltip("Цвет времени днём.")]
    public Color dayColor = Color.white;

    [Tooltip("Цвет времени ночью (сонливость).")]
    public Color nightColor = new Color(1f, 0.8f, 0.35f);

    [Tooltip("Цвет времени за час до отключки.")]
    public Color lateColor = new Color(1f, 0.4f, 0.3f);

    private string lastTime;
    private int lastDay = -1;

    private void LateUpdate()
    {
        if (clock == null) return;

        float hour = clock.Hour;
        string time = GameClock.FormatTime(hour, minuteStep);
        if (time != lastTime && timeText != null)
        {
            lastTime = time;
            timeText.text = time;
        }

        int day = clock.Day;
        if (dayText != null && day != lastDay)
        {
            lastDay = day;
            dayText.text = Loc.Get("hud.clock.day", day);
        }

        if (icon != null)
        {
            Sprite sprite = clock.IsEvening ? moonSprite : sunSprite;
            if (sprite != null && icon.sprite != sprite) icon.sprite = sprite;
        }

        float passOutHour = sleepService != null ? sleepService.settings.passOutHour : 2f;
        bool late = GameClock.IsBetween(hour, Mathf.Repeat(passOutHour - 1f, 24f), passOutHour);
        if (timeText != null)
        {
            Color color = late
                ? Color.Lerp(lateColor, dayColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f))
                : clock.IsNight ? nightColor : dayColor;
            timeText.color = color;
        }

        if (sleepHint != null)
        {
            bool show = clock.IsNight || (stamina != null && stamina.IsExhausted);
            if (sleepHint.gameObject.activeSelf != show) sleepHint.gameObject.SetActive(show);
            if (show)
            {
                sleepHint.text = Loc.Get("hud.clock.sleep_hint");
                Color c = sleepHint.color;
                c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                sleepHint.color = c;
            }
        }
    }
}
