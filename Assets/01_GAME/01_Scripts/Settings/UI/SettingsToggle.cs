using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Строка настроек с переключателем «вкл/выкл».</summary>
public class SettingsToggle : SettingsControl
{
    [Header("Переключатель")]
    [Tooltip("Флажок значения.")]
    public Toggle toggle;

    [Tooltip("Текст состояния рядом с флажком («Вкл»/«Выкл»). Пусто — без текста.")]
    public TMP_Text stateText;

    private Func<bool> getter;
    private Action<bool> setter;

    public void Bind(Func<bool> get, Action<bool> set)
    {
        getter = get;
        setter = set;
        toggle.onValueChanged.RemoveListener(HandleValueChanged);
        toggle.onValueChanged.AddListener(HandleValueChanged);
        Refresh();
    }

    public override void Refresh()
    {
        if (getter == null) return;
        bool value = getter();
        toggle.SetIsOnWithoutNotify(value);
        UpdateStateText(value);
    }

    public override void SetInteractable(bool interactable)
    {
        base.SetInteractable(interactable);
        toggle.interactable = interactable;
    }

    private void HandleValueChanged(bool value)
    {
        setter?.Invoke(value);
        UpdateStateText(value);
        RaiseChanged();
    }

    private void UpdateStateText(bool value)
    {
        if (stateText != null) stateText.text = Loc.Get(value ? "common.on" : "common.off");
    }
}
