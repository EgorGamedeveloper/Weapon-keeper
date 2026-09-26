using DG.Tweening;
using UnityEngine;

/// <summary>
/// Лифт на крышу: платформа ездит между нижней и верхней точкой. Работает, только когда починена
/// его точка ремонта (состояние берётся из RepairPoint.IsRepaired, после загрузки сейва тоже).
///
/// Игрок, стоящий на платформе, едет вместе с ней: на время поездки выключается контроллер движения,
/// а CharacterController сдвигается на ту же дельту, что и платформа, каждый кадр. Перепривязка игрока
/// к платформе как к родителю с CharacterController ненадёжна — поэтому так.
/// Позиция лифта не сохраняется: после загрузки он внизу, а вызвать его можно с любого этажа.
/// </summary>
public class ElevatorPlatform : MonoBehaviour
{
    [Header("Ремонт")]
    [Tooltip("Точка ремонта лифта. Пусто — лифт работает всегда.")]
    public RepairPoint repairPoint;

    [Header("Движение")]
    [Tooltip("Сама платформа (с коллайдером пола).")]
    public Transform platform;

    [Tooltip("Нижняя остановка.")]
    public Transform bottomStop;

    [Tooltip("Верхняя остановка (крыша).")]
    public Transform topStop;

    [Tooltip("Время поездки между остановками, секунды.")]
    [Min(0.1f)] public float travelTime = 4f;

    public Ease ease = Ease.InOutSine;

    [Header("Игрок")]
    [Tooltip("CharacterController игрока.")]
    public CharacterController player;

    [Tooltip("Контроллер движения игрока — выключается на время поездки.")]
    public Behaviour movementController;

    [Tooltip("Размер зоны над платформой, в которой игрок считается «на лифте» (X, высота, Z).")]
    public Vector3 rideArea = new Vector3(2.4f, 2.5f, 2.4f);

    public bool IsWorking => repairPoint == null || repairPoint.IsRepaired;
    public bool IsMoving { get; private set; }
    public bool AtTop { get; private set; }

    private void Start()
    {
        if (platform != null && bottomStop != null) platform.position = bottomStop.position;
    }

    /// <summary>Игрок стоит на платформе.</summary>
    public bool PlayerOnPlatform()
    {
        if (player == null || platform == null) return false;
        // Без учёта масштаба: платформа — отмасштабированный куб, InverseTransformPoint поделил бы на него.
        Vector3 local = Quaternion.Inverse(platform.rotation) * (player.transform.position - platform.position);
        return Mathf.Abs(local.x) <= rideArea.x * 0.5f && Mathf.Abs(local.z) <= rideArea.z * 0.5f
            && local.y >= -0.2f && local.y <= rideArea.y;
    }

    /// <summary>Отправить платформу на крышу (true) или вниз (false). Игрок на платформе едет вместе с ней.</summary>
    public void Move(bool toTop)
    {
        if (!IsWorking || IsMoving || platform == null || bottomStop == null || topStop == null) return;
        if (toTop == AtTop) return;

        bool carry = PlayerOnPlatform();
        IsMoving = true;
        if (carry && movementController != null) movementController.enabled = false;

        Vector3 last = platform.position;
        platform.DOMove(toTop ? topStop.position : bottomStop.position, travelTime)
            .SetEase(ease)
            .OnUpdate(() =>
            {
                Vector3 delta = platform.position - last;
                last = platform.position;
                if (carry && player != null) player.Move(delta);
            })
            .OnComplete(() =>
            {
                IsMoving = false;
                AtTop = toTop;
                if (carry && movementController != null) movementController.enabled = true;
            });
    }

    private void OnDrawGizmosSelected()
    {
        if (platform == null) return;
        Gizmos.matrix = Matrix4x4.TRS(platform.position, platform.rotation, Vector3.one);
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, rideArea.y * 0.5f, 0f), rideArea);
    }
}
