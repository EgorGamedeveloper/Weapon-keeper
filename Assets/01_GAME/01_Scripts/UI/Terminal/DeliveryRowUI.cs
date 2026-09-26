using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Строка «в пути» во вкладке «Поставки»: название и обратный отсчёт с полосой.</summary>
public class DeliveryRowUI : MonoBehaviour
{
    public Text title;
    public Text time;

    [Tooltip("Полоса прогресса доставки (Image Type = Filled).")]
    public Image fill;

    public void Bind(SupplyDelivery delivery)
    {
        if (title != null) title.text = delivery.box.DisplayTitle.ToUpperInvariant();
        if (time != null)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, delivery.remaining));
            time.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
        if (fill != null) fill.fillAmount = delivery.Progress;
    }
}
