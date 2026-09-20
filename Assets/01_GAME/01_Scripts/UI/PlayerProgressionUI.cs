using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Компактный индикатор уровня и опыта игрока: текст уровня + полоса опыта до следующего.</summary>
public class PlayerProgressionUI : MonoBehaviour
{
    public PlayerProgression playerProgression;
    public Text levelText;
    public Image xpFill;

    [Tooltip("Длительность анимации подтягивания полосы опыта к новому значению.")]
    public float fillTweenDuration = 0.25f;

    [Tooltip("Сила эффекта \"щелчка\" текста уровня при левелапе.")]
    public float levelUpPunchScale = 0.15f;

    private void OnEnable()
    {
        if (playerProgression == null) return;

        playerProgression.OnXPChanged += HandleXPChanged;
        playerProgression.OnLevelUp += HandleLevelUp;

        RefreshLevelText();
        SetFillImmediate(playerProgression.CurrentXP, playerProgression.XPToNextLevel);
    }

    private void OnDisable()
    {
        if (playerProgression == null) return;

        playerProgression.OnXPChanged -= HandleXPChanged;
        playerProgression.OnLevelUp -= HandleLevelUp;
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
        if (levelText != null) levelText.text = $"Ур. {playerProgression.CurrentLevel}";
    }

    private void SetFillImmediate(int currentXP, int xpToNextLevel)
    {
        if (xpFill != null) xpFill.fillAmount = xpToNextLevel > 0 ? (float)currentXP / xpToNextLevel : 0f;
    }
}
