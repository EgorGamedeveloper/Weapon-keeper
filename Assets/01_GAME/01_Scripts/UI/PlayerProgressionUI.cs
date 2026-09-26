using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Компактный индикатор уровня и опыта игрока: текст уровня + полоса опыта до следующего.</summary>
public class PlayerProgressionUI : MonoBehaviour
{
    [Tooltip("Источник уровня и опыта.")]
    public PlayerProgression playerProgression;
    [Tooltip("Текст с текущим уровнем.")]
    public Text levelText;
    [Tooltip("Полоса опыта (Image Type = Filled).")]
    public Image xpFill;

    [Tooltip("Длительность анимации подтягивания полосы опыта к новому значению.")]
    public float fillTweenDuration = 0.25f;

    [Tooltip("Сила эффекта \"щелчка\" текста уровня при левелапе.")]
    public float levelUpPunchScale = 0.15f;

    [Header("Подсказка про очки навыков")]
    [Tooltip("Текст «+N очков · Tab» под полосой опыта. Виден, только пока есть неизрасходованные очки.")]
    public Text skillPointsHint;

    [Tooltip("Формат подсказки: {0} — число очков.")]
    public string skillPointsHintFormat = DefaultSkillPointsHintFormat;

    // Пока формат в инспекторе не меняли, подсказка переводится (hud.skill_points_hint).
    private const string DefaultSkillPointsHintFormat = "+{0} оч. навыков · Tab";

    private Tween hintPulse;
    private LocalizationService localization;

    private void OnEnable()
    {
        if (playerProgression == null) return;

        playerProgression.OnXPChanged += HandleXPChanged;
        playerProgression.OnLevelUp += HandleLevelUp;
        playerProgression.OnUnlockPointsChanged += RefreshSkillPointsHint;

        RefreshLevelText();
        SetFillImmediate(playerProgression.CurrentXP, playerProgression.XPToNextLevel);
        RefreshSkillPointsHint(playerProgression.UnlockPoints);

        // Язык сменили в настройках — «Ур.» и подсказка про очки перерисовываются сразу.
        localization = LocalizationService.Instance;
        if (localization != null) localization.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        hintPulse?.Kill();
        if (localization != null) localization.OnLanguageChanged -= HandleLanguageChanged;
        localization = null;
        if (playerProgression == null) return;

        playerProgression.OnXPChanged -= HandleXPChanged;
        playerProgression.OnLevelUp -= HandleLevelUp;
        playerProgression.OnUnlockPointsChanged -= RefreshSkillPointsHint;
    }

    // Мягко мигает, пока очки не потрачены — игрок замечает, что пора открыть дерево навыков.
    private void RefreshSkillPointsHint(int points)
    {
        if (skillPointsHint == null) return;

        bool show = points > 0;
        skillPointsHint.gameObject.SetActive(show);
        hintPulse?.Kill();
        if (!show) return;

        skillPointsHint.text = skillPointsHintFormat == DefaultSkillPointsHintFormat
            ? Loc.Get("hud.skill_points_hint", points)
            : string.Format(skillPointsHintFormat, points);
        Color c = skillPointsHint.color;
        skillPointsHint.color = new Color(c.r, c.g, c.b, 1f);
        hintPulse = skillPointsHint.DOFade(0.4f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    private void HandleLanguageChanged()
    {
        RefreshLevelText();
        RefreshSkillPointsHint(playerProgression.UnlockPoints);
    }

    private void HandleXPChanged(int currentXP, int xpToNextLevel)
    {
        if (xpFill == null) return;

        float target = xpToNextLevel > 0 ? (float)currentXP / xpToNextLevel : 0f;
        xpFill.DOKill();
        xpFill.DOFillAmount(target, fillTweenDuration);
    }

    private void HandleLevelUp(int newLevel, int unlockPointsGranted)
    {
        RefreshLevelText();

        if (levelText == null) return;
        levelText.transform.DOKill();
        levelText.transform.DOPunchScale(Vector3.one * levelUpPunchScale, 0.3f);
    }

    private void RefreshLevelText()
    {
        if (levelText != null) levelText.text = Loc.Get("hud.level_short", playerProgression.CurrentLevel);
    }

    private void SetFillImmediate(int currentXP, int xpToNextLevel)
    {
        if (xpFill != null) xpFill.fillAmount = xpToNextLevel > 0 ? (float)currentXP / xpToNextLevel : 0f;
    }
}
