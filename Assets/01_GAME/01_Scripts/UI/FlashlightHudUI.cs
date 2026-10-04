using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Значок налобного фонарика на панели справа внизу: появляется, когда игрок подобрал фонарик
/// (PlayerFlashlight.IsOwned), под ним — клавиша и маленькая шкала заряда батареи. Включённый фонарик —
/// яркий значок, выключенный — приглушённый; шкала краснеет, когда батарея садится.
/// </summary>
public class FlashlightHudUI : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Фонарик игрока.")]
    public PlayerFlashlight flashlight;

    [Tooltip("Корень значка: включается, только когда фонарик подобран.")]
    public GameObject root;

    [Tooltip("Картинка значка — её яркость показывает, включён ли фонарик.")]
    public Image icon;

    [Tooltip("Заливка шкалы заряда (Image типа Filled, горизонтально).")]
    public Image chargeFill;

    [Tooltip("Подпись с клавишей фонарика. Пусто — не трогаем.")]
    public TMP_Text keyLabel;

    [Header("Цвета")]
    [Tooltip("Значок включённого фонарика.")]
    public Color onColor = Color.white;

    [Tooltip("Значок выключенного фонарика.")]
    public Color offColor = new Color(1f, 1f, 1f, 0.55f);

    [Tooltip("Шкала, пока заряда достаточно.")]
    public Color chargeColor = new Color(0.95f, 0.85f, 0.35f, 1f);

    [Tooltip("Шкала, когда батарея садится.")]
    public Color lowChargeColor = new Color(0.9f, 0.2f, 0.15f, 1f);

    private void OnEnable()
    {
        if (flashlight != null) flashlight.OnAcquired += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (flashlight != null) flashlight.OnAcquired -= Refresh;
    }

    private void Update()
    {
        if (flashlight == null || !flashlight.IsOwned) { SetVisible(false); return; }
        SetVisible(true);

        if (icon != null) icon.color = flashlight.IsOn ? onColor : offColor;
        if (chargeFill == null) return;
        chargeFill.fillAmount = flashlight.Charge01;
        chargeFill.color = flashlight.IsLowCharge ? lowChargeColor : chargeColor;
    }

    private void Refresh()
    {
        SetVisible(flashlight != null && flashlight.IsOwned);
        if (keyLabel != null && flashlight != null)
            keyLabel.text = flashlight.toggleKey.ToString().Replace("Alpha", "");
    }

    private void SetVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible) root.SetActive(visible);
    }
}
