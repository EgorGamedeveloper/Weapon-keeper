using UnityEngine;

/// <summary>
/// Место для сна (матрас). Наведение — подсказка «ЛКМ — спать до 06:00» или причина, почему нельзя
/// («Ещё рано», «Сначала отбейтесь»); клик — SleepService.Sleep. Сюда же игрока переносит после
/// отключки от усталости (wakePoint).
/// </summary>
public class SleepSpot : MonoBehaviour, IInteractable
{
    [Tooltip("Сервис сна на игроке.")]
    public SleepService sleepService;

    [Tooltip("Где игрок приходит в себя после отключки. Пусто — в метре перед матрасом (по его оси Z).")]
    public Transform wakePoint;

    public string InteractTitle => Loc.Get("hud.sleep.title");

    public string InteractHint => sleepService != null ? sleepService.GetSleepHint() : "";

    public bool CanInteract => sleepService != null && sleepService.CanSleep(out _);

    public void Interact()
    {
        if (sleepService != null) sleepService.Sleep(this);
    }

    /// <summary>Точка, где игрок приходит в себя у этого матраса. Поворот не задаётся: взгляд ведёт
    /// MouseRotator, и запись поворота корня после его Start всё равно была бы перетёрта.</summary>
    public Vector3 WakePosition
    {
        get
        {
            if (wakePoint != null) return wakePoint.position;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            return transform.position + forward.normalized;
        }
    }
}
