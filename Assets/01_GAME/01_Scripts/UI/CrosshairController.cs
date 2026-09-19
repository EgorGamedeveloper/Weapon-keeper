using UnityEngine;

/// <summary>
/// Переключает UI в центре экрана: обычная точка-маркер взаимодействия (dotReticle)
/// или прицел (weaponCrosshair) — прицел показывается, когда в руках активное оружие.
/// </summary>
public class CrosshairController : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerInventoryModeController modeController;

    [Header("UI")]
    [Tooltip("Обычная точка/маркер для взаимодействия с предметами и полками.")]
    public GameObject dotReticle;
    [Tooltip("Прицел, показывается только когда экипировано и активно оружие.")]
    public GameObject weaponCrosshair;

    private void Update()
    {
        bool weaponActive = modeController != null && modeController.IsWeaponEquipped();

        if (dotReticle != null) dotReticle.SetActive(!weaponActive);
        if (weaponCrosshair != null) weaponCrosshair.SetActive(weaponActive);
    }
}
