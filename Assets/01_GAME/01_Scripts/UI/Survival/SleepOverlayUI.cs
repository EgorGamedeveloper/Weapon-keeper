using System.Collections;
using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран сна поверх HUD: затемнение, часы, которые крутятся до утра, и карточка итогов дня с кнопкой
/// «Проснуться». Шаги зовёт SleepService по очереди; всё идёт в реальном времени (SetUpdate(true)) —
/// на время сна timeScale = 0.
/// </summary>
public class SleepOverlayUI : MonoBehaviour
{
    [Header("Затемнение")]
    [Tooltip("Полноэкранный чёрный слой (CanvasGroup, alpha 0, без приёма кликов, пока скрыт).")]
    public CanvasGroup group;

    [Tooltip("Надпись в центре: «Спокойной ночи…» или «Вы отключились от усталости».")]
    public Text titleText;

    [Tooltip("Часы, которые крутятся до утра.")]
    public Text clockText;

    [Header("Итоги дня")]
    [Tooltip("Карточка итогов (CanvasGroup, alpha 0).")]
    public CanvasGroup summaryGroup;

    [Tooltip("Заголовок карточки: «День 3 — итоги».")]
    public Text summaryTitle;

    [Tooltip("Строки итогов.")]
    public Text summaryBody;

    [Tooltip("Кнопка «Проснуться». Кроме неё, закрывают Пробел, Enter и E.")]
    public Button continueButton;

    [Header("Цвета")]
    [Tooltip("Цвет хороших новостей в итогах («Выспался»).")]
    public Color goodColor = new Color(0.55f, 0.9f, 0.55f);

    [Tooltip("Цвет плохих новостей («Отключился», остаток усталости).")]
    public Color badColor = new Color(1f, 0.5f, 0.4f);

    private bool continuePressed;

    private void Awake()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        if (summaryGroup != null)
        {
            summaryGroup.alpha = 0f;
            summaryGroup.blocksRaycasts = false;
            summaryGroup.interactable = false;
        }
        if (continueButton != null) continueButton.onClick.AddListener(() => continuePressed = true);
    }

    private void OnDestroy()
    {
        if (group != null) group.DOKill();
        if (summaryGroup != null) summaryGroup.DOKill();
    }

    /// <summary>Экран гаснет. passOut — вместо «Спокойной ночи» надпись об отключке.</summary>
    public IEnumerator FadeOut(float duration, bool passOut)
    {
        if (titleText != null) titleText.text = Loc.Get(passOut ? "hud.sleep.passed_out" : "hud.sleep.good_night");
        if (clockText != null) clockText.text = "";
        if (group == null) yield break;

        group.blocksRaycasts = true;
        group.DOKill();
        yield return group.DOFade(1f, duration).SetUpdate(true).WaitForCompletion();
    }

    /// <summary>Часы крутятся от from до to (TotalHours) за duration секунд.</summary>
    public IEnumerator SpinClock(double from, double to, float duration)
    {
        if (clockText == null || duration <= 0f) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            clockText.text = GameClock.FormatTime(GameClock.HourOf(from + (to - from) * t), 10);
            yield return null;
        }
        clockText.text = GameClock.FormatTime(GameClock.HourOf(to));
    }

    /// <summary>Карточка итогов дня; ждёт «Проснуться» (или Пробел, Enter, E).</summary>
    public IEnumerator ShowSummary(DayReport report)
    {
        if (summaryGroup == null) yield break;

        if (summaryTitle != null) summaryTitle.text = Loc.Get("hud.sleep.summary_title", report.day);
        if (summaryBody != null) summaryBody.text = BuildSummary(report);

        continuePressed = false;
        // Кнопка внутри общего затемнения: пока оно не интерактивно, CanvasGroup-родитель глушит и её.
        if (group != null) group.interactable = true;
        summaryGroup.blocksRaycasts = true;
        summaryGroup.interactable = true;
        summaryGroup.DOKill();
        summaryGroup.DOFade(1f, 0.35f).SetUpdate(true);

        // Кадр, в котором карточка появилась, не считается: клик, уложивший игрока, не должен её закрыть.
        yield return null;
        while (!continuePressed
               && !Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.E))
            yield return null;

        summaryGroup.blocksRaycasts = false;
        summaryGroup.interactable = false;
        if (group != null) group.interactable = false;
        yield return summaryGroup.DOFade(0f, 0.25f).SetUpdate(true).WaitForCompletion();
    }

    /// <summary>Экран проявляется — утро.</summary>
    public IEnumerator FadeIn(float duration)
    {
        if (group == null) yield break;

        group.DOKill();
        yield return group.DOFade(0f, duration).SetUpdate(true).WaitForCompletion();
        group.blocksRaycasts = false;
    }

    private string BuildSummary(DayReport report)
    {
        var sb = new StringBuilder();
        AppendLine(sb, Loc.Get("hud.sleep.summary_money", report.moneyEarned));
        AppendLine(sb, Loc.Get("hud.sleep.summary_xp", report.xpGained));
        if (report.itemsShelved >= 0) AppendLine(sb, Loc.Get("hud.sleep.summary_shelved", report.itemsShelved));
        if (report.enemiesKilled >= 0) AppendLine(sb, Loc.Get("hud.sleep.summary_killed", report.enemiesKilled));

        sb.Append('\n');
        if (report.passedOut) AppendLine(sb, Colored(Loc.Get("hud.sleep.summary_passed_out"), badColor));
        else if (report.wellRested) AppendLine(sb, Colored(Loc.Get("hud.sleep.summary_well_rested"), goodColor));
        else AppendLine(sb, Loc.Get("hud.sleep.summary_slept", Mathf.RoundToInt(report.hoursSlept)));

        if (report.fatigueAfter >= 0.5f)
            AppendLine(sb, Colored(Loc.Get("hud.sleep.summary_fatigue_left", Mathf.RoundToInt(report.fatigueAfter)), badColor));
        return sb.ToString().TrimEnd();
    }

    private static void AppendLine(StringBuilder sb, string line) => sb.Append(line).Append('\n');

    private static string Colored(string text, Color color) =>
        "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
}
