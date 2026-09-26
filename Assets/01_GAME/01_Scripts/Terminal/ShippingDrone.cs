using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Дрон доставки: прилетает сверху к зоне отправки, подхватывает коробку и улетает. Чисто визуальный —
/// заказ к этому моменту уже закрыт (см. ShippingPad).
/// </summary>
public class ShippingDrone : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Модель дрона — скрыта, пока дрон не летит.")]
    public Transform body;

    [Tooltip("Точка под дроном, к которой цепляется коробка.")]
    public Transform grabPoint;

    [Tooltip("Винты — крутятся в полёте.")]
    public Transform[] rotors = Array.Empty<Transform>();

    [Header("Полёт")]
    [Tooltip("С какой высоты над площадкой дрон прилетает.")]
    public float approachHeight = 14f;

    [Tooltip("На какой высоте над коробкой дрон зависает, чтобы её подхватить.")]
    public float hoverHeight = 0.9f;

    [Tooltip("Смещение точки прилёта/отлёта по горизонтали — дрон заходит по диагонали.")]
    public Vector3 approachOffset = new Vector3(8f, 0f, 4f);

    [Tooltip("Время спуска и подъёма, секунды.")]
    public float flightDuration = 2.2f;

    private Sequence sequence;

    private void Awake()
    {
        if (body != null) body.gameObject.SetActive(false);
        foreach (var rotor in rotors)
            if (rotor != null) rotor.DOLocalRotate(new Vector3(0f, 360f, 0f), 0.15f, RotateMode.FastBeyond360).SetLoops(-1).SetEase(Ease.Linear);
    }

    /// <summary>Прилететь, подхватить груз и улететь; onDone — когда дрон скрылся.</summary>
    public void PickUp(Transform cargo, Action onDone)
    {
        if (body == null) { onDone?.Invoke(); return; }

        Vector3 hover = transform.position + Vector3.up * hoverHeight;
        Vector3 far = transform.position + Vector3.up * approachHeight + approachOffset;
        Vector3 away = transform.position + Vector3.up * approachHeight - approachOffset;

        sequence?.Kill();
        body.position = far;
        body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(hover - far, Vector3.up).normalized);
        body.gameObject.SetActive(true);

        sequence = DOTween.Sequence()
            .Append(body.DOMove(hover, flightDuration).SetEase(Ease.OutCubic))
            .AppendInterval(0.35f)
            .AppendCallback(() =>
            {
                if (cargo == null) return;
                cargo.SetParent(grabPoint != null ? grabPoint : body, true);
                cargo.DOLocalMove(Vector3.zero, 0.25f);
            })
            .AppendInterval(0.35f)
            .Append(body.DOMove(away, flightDuration).SetEase(Ease.InCubic))
            .AppendCallback(() =>
            {
                body.gameObject.SetActive(false);
                onDone?.Invoke();
            });
    }

    private void OnDestroy()
    {
        sequence?.Kill();
        foreach (var rotor in rotors) if (rotor != null) rotor.DOKill();
    }
}
