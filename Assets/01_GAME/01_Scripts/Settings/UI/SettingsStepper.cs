using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Строка настроек-переключатель «‹ значение ›» для коротких списков (режим окна, лимит FPS, MSAA).</summary>
public class SettingsStepper : SettingsControl
{
    [Header("Переключатель ‹ ›")]
    [Tooltip("Кнопка «‹» — предыдущий вариант.")]
    public Button previousButton;

    [Tooltip("Кнопка «›» — следующий вариант.")]
    public Button nextButton;

    [Tooltip("Текущий вариант.")]
    public TMP_Text valueText;

    [Tooltip("Листать по кругу: после последнего — первый.")]
    public bool wrap = false;

    private Func<IList<string>> optionsProvider;
    private Func<int> getter;
    private Action<int> setter;
    private bool interactable = true;

    private void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
    }

    public void Bind(Func<IList<string>> getOptions, Func<int> get, Action<int> set)
    {
        optionsProvider = getOptions;
        getter = get;
        setter = set;
        Refresh();
    }

    public override void Refresh()
    {
        if (optionsProvider == null || getter == null) return;

        IList<string> options = optionsProvider();
        int index = options.Count > 0 ? Mathf.Clamp(getter(), 0, options.Count - 1) : 0;
        if (valueText != null) valueText.text = options.Count > 0 ? options[index] : "";

        if (previousButton != null) previousButton.interactable = interactable && (wrap || index > 0);
        if (nextButton != null) nextButton.interactable = interactable && (wrap || index < options.Count - 1);
    }

    public override void SetInteractable(bool value)
    {
        base.SetInteractable(value);
        interactable = value;
        Refresh();
    }

    private void Step(int direction)
    {
        if (optionsProvider == null || getter == null || !interactable) return;

        int count = optionsProvider().Count;
        if (count == 0) return;

        int index = Mathf.Clamp(getter(), 0, count - 1) + direction;
        if (wrap) index = (index % count + count) % count;
        else index = Mathf.Clamp(index, 0, count - 1);

        setter?.Invoke(index);
        Refresh();
        RaiseChanged();
    }
}
