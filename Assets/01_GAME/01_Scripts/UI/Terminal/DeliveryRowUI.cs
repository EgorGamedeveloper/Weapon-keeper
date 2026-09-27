using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Строка «в пути» во вкладке «Поставки»: название, когда приедет («В 17:40») и полоса.</summary>
public class DeliveryRowUI : MonoBehaviour
{
    public Text title;
    public Text time;

    [Tooltip("Полоса прогресса доставки (Image Type = Filled).")]
    public Image fill;

    /// <summary>arrivalTime — когда приедет (GameClock.TotalHours); меньше нуля — часов нет, показывается остаток.</summary>
    public void Bind(SupplyDelivery delivery, double arrivalTime)
    {
        if (title != null) title.text = delivery.box.DisplayTitle.ToUpperInvariant();
        if (time != null)
        {
            if (arrivalTime >= 0.0)
                time.text = Loc.Get("terminal.delivery_eta", GameClock.FormatTime(GameClock.HourOf(arrivalTime), 10));
            else
            {
                int minutes = Mathf.CeilToInt(Mathf.Max(0f, delivery.remaining) * 60f);
                time.text = (minutes / 60) + ":" + (minutes % 60).ToString("00");
            }
        }
        if (fill != null) fill.fillAmount = delivery.Progress;
    }
}
