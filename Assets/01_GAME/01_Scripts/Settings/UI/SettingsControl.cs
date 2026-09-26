using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Строка окна настроек: подпись и контрол значения. Значение читается и пишется через привязку
/// (Bind у наследников) к рабочей копии настроек в SettingsWindow — строка не знает, какую настройку правит.
/// Наследники: SettingsSlider, SettingsDropdown, SettingsToggle, SettingsStepper.
/// </summary>
public abstract class SettingsControl : MonoBehaviour
{
    [Header("Строка")]
    [Tooltip("Подпись настройки.")]
    public TMP_Text label;

    [Tooltip("Перевод подписи: ключ ставит окно настроек. Пусто — подпись пишется через Loc.Get.")]
    public LocalizedText labelLocalization;

    [Tooltip("Группа строки: гасится и перестаёт реагировать, когда настройка недоступна (например, лимит FPS при VSync).")]
    public CanvasGroup rowGroup;

    /// <summary>Игрок изменил значение.</summary>
    public event Action Changed;

    /// <summary>Подпись по ключу strings.csv.</summary>
    public void SetLabel(string key)
    {
        if (labelLocalization != null) labelLocalization.SetKey(key);
        else if (label != null) label.text = Loc.Get(key);
    }

    /// <summary>Перечитать значение из привязки и тексты вариантов (после смены языка, сброса, применения).</summary>
    public abstract void Refresh();

    /// <summary>Доступна ли настройка сейчас.</summary>
    public virtual void SetInteractable(bool interactable)
    {
        if (rowGroup == null) return;
        rowGroup.alpha = interactable ? 1f : 0.4f;
        rowGroup.interactable = interactable;
    }

    protected void RaiseChanged() => Changed?.Invoke();
}
