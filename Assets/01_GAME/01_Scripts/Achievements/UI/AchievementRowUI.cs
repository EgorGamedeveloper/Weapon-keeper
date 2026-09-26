using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Строка окна достижений: иконка, название, описание, шкала прогресса, отметка «Получено».</summary>
public class AchievementRowUI : MonoBehaviour
{
    [Header("Строка")]
    [Tooltip("Прозрачность строки: неполученные — приглушены.")]
    public CanvasGroup group;

    [Tooltip("Иконка. Без спрайта у достижения — цветная плашка-заглушка.")]
    public Image icon;

    [Tooltip("Название.")]
    public TMP_Text titleText;

    [Tooltip("Описание.")]
    public TMP_Text descriptionText;

    [Header("Прогресс")]
    [Tooltip("Корень шкалы прогресса — скрывается у полученных и одношаговых достижений.")]
    public GameObject progressRoot;

    [Tooltip("Заливка шкалы (Image типа Filled).")]
    public Image progressFill;

    [Tooltip("Текст «12 / 25».")]
    public TMP_Text progressText;

    [Tooltip("Отметка «Получено».")]
    public TMP_Text statusText;

    [Header("Цвета")]
    [Tooltip("Цвет заглушки иконки у полученного достижения.")]
    public Color unlockedColor = new Color(1f, 0.82f, 0.25f, 1f);

    [Tooltip("Цвет заглушки/затемнения иконки у неполученного.")]
    public Color lockedColor = new Color(0.35f, 0.35f, 0.38f, 1f);

    public void Bind(AchievementData achievement, bool unlocked, double value)
    {
        bool secret = achievement.hidden && !unlocked;

        if (titleText != null) titleText.text = secret ? Loc.Get("achievements.hidden.title") : Loc.Get(achievement.nameKey);
        if (descriptionText != null)
            descriptionText.text = secret ? Loc.Get("achievements.hidden.description") : Loc.Get(achievement.descriptionKey);

        if (icon != null)
        {
            Sprite sprite = unlocked || achievement.lockedIcon == null ? achievement.icon : achievement.lockedIcon;
            if (sprite != null)
            {
                icon.sprite = sprite;
                // Своей серой иконки нет — затемняем цветную.
                icon.color = unlocked || achievement.lockedIcon != null ? Color.white : lockedColor;
            }
            else
            {
                icon.color = unlocked ? unlockedColor : lockedColor;
            }
        }

        bool showProgress = !unlocked && !secret && achievement.HasProgress;
        if (progressRoot != null) progressRoot.SetActive(showProgress);
        if (showProgress)
        {
            double clamped = System.Math.Min(value, achievement.target);
            if (progressFill != null) progressFill.fillAmount = (float)(clamped / achievement.target);
            if (progressText != null) progressText.text = Loc.Get("achievements.progress", (long)clamped, achievement.target);
        }

        if (statusText != null) statusText.text = unlocked ? Loc.Get("achievements.unlocked") : "";
        if (group != null) group.alpha = unlocked ? 1f : 0.7f;
    }
}
