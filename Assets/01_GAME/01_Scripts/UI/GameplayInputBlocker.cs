using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Общая блокировка игрового ввода для модальных окон (навыки, терминал, пауза) и режима работы с
/// объектом (PlayerToolActions: тряпка по пятну, лом-рычаг). Пока есть хоть один владелец, выключены
/// обзор мышью, клики по миру, движение, захват курсора и стрельба.
///
/// Курсор освобождают только владельцы-окна (releaseCursor = true). Режим работы блокирует ввод, но
/// курсор оставляет захваченным: мышь там — рычаг или тряпка. Курсор меняется только тогда, когда
/// приходит или уходит владелец-окно, поэтому режим, закончившийся при открытом окне (или меню паузы),
/// курсор не трогает. Счётчик владельцев не даёт им «спорить»: состояние компонентов снимается при
/// первом владельце и возвращается при последнем, а IsBlocked подсказывает окнам не открываться поверх
/// чужого окна.
/// </summary>
public class GameplayInputBlocker : MonoBehaviour
{
    [Header("Что выключать")]
    [Tooltip("Компоненты игрока, выключаемые на время окна: обзор мышью, клики по миру, движение, захват курсора.")]
    public Behaviour[] disableWhileBlocked = Array.Empty<Behaviour>();

    [Tooltip("Точка крепления оружия: все Weapon под ней выключаются, чтобы клик по окну не стрелял.")]
    public Transform weaponMount;

    /// <summary>Открыто хотя бы одно модальное окно (владелец, освободивший курсор). Режим работы с
    /// объектом окном не считается: пауза и окна открываются поверх него, а он сам завершается.</summary>
    public bool IsBlocked => cursorOwners.Count > 0;

    private readonly HashSet<object> owners = new HashSet<object>();
    private readonly HashSet<object> cursorOwners = new HashSet<object>();
    private readonly Dictionary<Behaviour, bool> savedStates = new Dictionary<Behaviour, bool>();

    /// <summary>Окно открылось (releaseCursor = true) или начался режим работы с объектом (false).</summary>
    public void Acquire(object owner, bool releaseCursor = true)
    {
        if (owner == null || !owners.Add(owner)) return;

        if (owners.Count == 1)
        {
            savedStates.Clear();
            foreach (var behaviour in disableWhileBlocked) Block(behaviour);
            if (weaponMount != null)
                foreach (var weapon in weaponMount.GetComponentsInChildren<Weapon>(true)) Block(weapon);
        }

        if (!releaseCursor) return;
        cursorOwners.Add(owner);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Окно закрылось или режим работы закончился.</summary>
    public void Release(object owner)
    {
        if (owner == null || !owners.Remove(owner)) return;

        if (owners.Count == 0)
        {
            // Возвращаем только то, что было включено до блокировки.
            foreach (var pair in savedStates)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            savedStates.Clear();
        }

        // Закрылось последнее окно — курсор снова захвачен, даже если режим работы ещё идёт.
        if (cursorOwners.Remove(owner) && cursorOwners.Count == 0)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Block(Behaviour behaviour)
    {
        if (behaviour == null || savedStates.ContainsKey(behaviour)) return;
        savedStates[behaviour] = behaviour.enabled;
        behaviour.enabled = false;
    }
}
