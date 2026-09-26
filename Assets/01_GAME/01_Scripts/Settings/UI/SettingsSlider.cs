using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Строка настроек с ползунком и значением справа («75°», «80%»).</summary>
public class SettingsSlider : SettingsControl
{
    [Header("Ползунок")]
    [Tooltip("Ползунок значения.")]
    public Slider slider;

    [Tooltip("Текст значения справа от ползунка.")]
    public TMP_Text valueText;

    private Func<float> getter;
    private Action<float> setter;
    private Func<float, string> formatter;

    /// <summary>Привязать к значению. setter получает сырое значение ползунка — округление на стороне
    /// вызывающего; formatter превращает значение в подпись справа.</summary>
    public void Bind(float min, float max, bool wholeNumbers, Func<float> get, Action<float> set, Func<float, string> format)
    {
        getter = get;
        setter = set;
        formatter = format;

        // Диапазон — до подписки: смена min/max может сама поднять onValueChanged.
        slider.onValueChanged.RemoveListener(HandleValueChanged);
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.onValueChanged.AddListener(HandleValueChanged);
        Refresh();
    }

    public override void Refresh()
    {
        if (getter == null) return;
        float value = getter();
        slider.SetValueWithoutNotify(value);
        UpdateValueText(value);
    }

    public override void SetInteractable(bool interactable)
    {
        base.SetInteractable(interactable);
        slider.interactable = interactable;
    }

    private void HandleValueChanged(float value)
    {
        setter?.Invoke(value);
        // Показываем то, что реально записалось (setter мог округлить).
        UpdateValueText(getter != null ? getter() : value);
        RaiseChanged();
    }

    private void UpdateValueText(float value)
    {
        if (valueText != null) valueText.text = formatter != null ? formatter(value) : value.ToString("0.##");
    }
}
