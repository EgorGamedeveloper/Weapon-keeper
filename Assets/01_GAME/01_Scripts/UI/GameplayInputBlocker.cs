using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Общая блокировка игрового ввода для модальных окон (навыки, терминал). Пока открыто хоть одно
/// окно, выключены обзор мышью, клики по миру, движение, захват курсора и стрельба, курсор свободен.
/// Счётчик владельцев не даёт двум окнам «спорить»: закрытие одного не вернёт управление, пока
/// открыто другое, а IsBlocked подсказывает окнам не открываться поверх чужого.
/// </summary>
public class GameplayInputBlocker : MonoBehaviour
{
    [Header("Что выключать")]
    [Tooltip("Компоненты игрока, выключаемые на время окна: обзор мышью, клики по миру, движение, захват курсора.")]
    public Behaviour[] disableWhileBlocked = Array.Empty<Behaviour>();

    [Tooltip("Точка крепления оружия: все Weapon под ней выключаются, чтобы клик по окну не стрелял.")]
    public Transform weaponMount;

    /// <summary>Открыто хотя бы одно модальное окно.</summary>
    public bool IsBlocked => owners.Count > 0;

    private readonly HashSet<object> owners = new HashSet<object>();
    private readonly Dictionary<Behaviour, bool> savedStates = new Dictionary<Behaviour, bool>();

    /// <summary>Окно открылось.</summary>
    public void Acquire(object owner)
    {
        if (owner == null || !owners.Add(owner) || owners.Count > 1) return;

        savedStates.Clear();
        foreach (var behaviour in disableWhileBlocked) Block(behaviour);
        if (weaponMount != null)
            foreach (var weapon in weaponMount.GetComponentsInChildren<Weapon>(true)) Block(weapon);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Окно закрылось.</summary>
    public void Release(object owner)
    {
        if (owner == null || !owners.Remove(owner) || owners.Count > 0) return;

        // Возвращаем только то, что было включено до открытия окна.
        foreach (var pair in savedStates)
            if (pair.Key != null) pair.Key.enabled = pair.Value;
        savedStates.Clear();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Block(Behaviour behaviour)
    {
        if (behaviour == null || savedStates.ContainsKey(behaviour)) return;
        savedStates[behaviour] = behaviour.enabled;
        behaviour.enabled = false;
    }
}
