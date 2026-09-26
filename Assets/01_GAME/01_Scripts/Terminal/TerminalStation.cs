using DG.Tweening;
using UnityEngine;

/// <summary>
/// Старый компьютер в магазине. Клик открывает окно терминала (поставки и заказы), но только когда
/// починен генератор: до этого экран тёмный, а подсказка объясняет, что нужно сделать. Состояние
/// питания читается из RepairPoint.IsRepaired — после загрузки сейва тоже, без отдельного сохранения
/// (тот же приём, что у LightsActivator).
/// </summary>
[RequireComponent(typeof(Collider))]
public class TerminalStation : MonoBehaviour, IInteractable
{
    [Header("Окно")]
    [Tooltip("Окно терминала на канвасе.")]
    public TerminalWindow window;

    [Header("Питание")]
    [Tooltip("Точка ремонта генератора. Пусто — терминал работает всегда.")]
    public RepairPoint powerSource;

    [Tooltip("Светящийся экран — включается, когда есть питание.")]
    public GameObject screenOn;

    [Tooltip("Тёмный экран — показывается без питания.")]
    public GameObject screenOff;

    public bool IsPowered => powerSource == null || powerSource.IsRepaired;

    public string InteractTitle => "Терминал";
    public string InteractHint => IsPowered ? "ЛКМ — открыть терминал" : "Нет питания — почините генератор";
    public bool CanInteract => IsPowered && window != null;

    private void OnEnable()
    {
        if (powerSource != null) powerSource.OnRepaired += HandlePowerRestored;
    }

    private void OnDisable()
    {
        if (powerSource != null) powerSource.OnRepaired -= HandlePowerRestored;
    }

    // Start, а не Awake: SaveLoadService восстанавливает генератор в своём Awake.
    private void Start() => RefreshScreen(false);

    public void Interact()
    {
        if (CanInteract) window.Open();
    }

    private void HandlePowerRestored(RepairPoint _) => RefreshScreen(true);

    private void RefreshScreen(bool animate)
    {
        bool powered = IsPowered;
        if (screenOff != null) screenOff.SetActive(!powered);
        if (screenOn == null) return;

        screenOn.SetActive(powered);
        if (!powered || !animate) return;

        // Экран «включается» как ЭЛТ: полоска по вертикали раскрывается в полный кадр.
        Transform t = screenOn.transform;
        Vector3 full = t.localScale;
        t.DOKill();
        t.localScale = new Vector3(full.x, full.y * 0.05f, full.z);
        t.DOScale(full, 0.35f).SetEase(Ease.OutBack);
    }
}
