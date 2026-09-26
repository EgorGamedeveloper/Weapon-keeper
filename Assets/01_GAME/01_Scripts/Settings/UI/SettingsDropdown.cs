using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Строка настроек с выпадающим списком (разрешение, пресет качества, язык).</summary>
public class SettingsDropdown : SettingsControl
{
    [Header("Список")]
    [Tooltip("Выпадающий список TMP.")]
    public TMP_Dropdown dropdown;

    private Func<IList<string>> optionsProvider;
    private Func<int> getter;
    private Action<int> setter;
    private readonly List<string> options = new List<string>();

    /// <summary>Привязать: список вариантов перечитывается при каждом Refresh (он может зависеть от языка).</summary>
    public void Bind(Func<IList<string>> getOptions, Func<int> get, Action<int> set)
    {
        optionsProvider = getOptions;
        getter = get;
        setter = set;
        dropdown.onValueChanged.RemoveListener(HandleValueChanged);
        dropdown.onValueChanged.AddListener(HandleValueChanged);
        Refresh();
    }

    public override void Refresh()
    {
        if (optionsProvider == null || getter == null) return;
        if (dropdown.IsExpanded) dropdown.Hide();

        options.Clear();
        options.AddRange(optionsProvider());
        dropdown.ClearOptions();
        dropdown.AddOptions(options);

        int index = options.Count > 0 ? Mathf.Clamp(getter(), 0, options.Count - 1) : 0;
        dropdown.SetValueWithoutNotify(index);
        dropdown.RefreshShownValue();
    }

    public override void SetInteractable(bool interactable)
    {
        base.SetInteractable(interactable);
        dropdown.interactable = interactable;
    }

    private void HandleValueChanged(int index)
    {
        setter?.Invoke(index);
        RaiseChanged();
    }
}
