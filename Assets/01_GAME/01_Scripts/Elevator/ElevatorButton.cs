using DG.Tweening;
using UnityEngine;

/// <summary>
/// Кнопка лифта. Панель на самой платформе везёт на другой этаж; кнопки вызова на этажах
/// подгоняют платформу к себе. Без ремонта лифта кнопки только подсказывают, что лифт сломан.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ElevatorButton : MonoBehaviour, IInteractable
{
    public enum Kind { Panel, CallBottom, CallTop }

    [Header("Лифт")]
    public ElevatorPlatform elevator;

    [Tooltip("Panel — на платформе (ехать); CallBottom/CallTop — вызов на нижний/верхний этаж.")]
    public Kind kind = Kind.Panel;

    [Tooltip("Что «нажимается» визуально (необязательно).")]
    public Transform pressVisual;

    public string InteractTitle => "Лифт";

    public string InteractHint
    {
        get
        {
            if (elevator == null) return "";
            if (!elevator.IsWorking) return "Лифт не работает — нужен ремонт";
            if (elevator.IsMoving) return "Лифт едет…";
            switch (kind)
            {
                case Kind.Panel: return elevator.AtTop ? "ЛКМ — вниз" : "ЛКМ — на крышу";
                case Kind.CallBottom: return elevator.AtTop ? "ЛКМ — вызвать лифт" : "Лифт здесь";
                default: return elevator.AtTop ? "Лифт здесь" : "ЛКМ — вызвать лифт";
            }
        }
    }

    public bool CanInteract
    {
        get
        {
            if (elevator == null || !elevator.IsWorking || elevator.IsMoving) return false;
            switch (kind)
            {
                case Kind.Panel: return true;
                case Kind.CallBottom: return elevator.AtTop;
                default: return !elevator.AtTop;
            }
        }
    }

    public void Interact()
    {
        if (!CanInteract) return;

        if (pressVisual != null)
        {
            pressVisual.DOKill(true);
            pressVisual.DOPunchScale(Vector3.one * -0.2f, 0.2f, 6, 0.5f);
        }

        switch (kind)
        {
            case Kind.Panel: elevator.Move(!elevator.AtTop); break;
            case Kind.CallBottom: elevator.Move(false); break;
            default: elevator.Move(true); break;
        }
    }
}
