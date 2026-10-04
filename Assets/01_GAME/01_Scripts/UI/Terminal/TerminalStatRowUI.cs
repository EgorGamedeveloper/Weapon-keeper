using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка параметра «Цена ................ $ 250»: подпись слева, значение справа, между ними — точечная
/// линия, которая растягивается ровно на свободное место (по preferredWidth обоих текстов).
/// </summary>
public class TerminalStatRowUI : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Подпись (слева).")]
    public Text label;

    [Tooltip("Значение (справа).")]
    public Text value;

    [Tooltip("Точечная линия между ними (Image Type = Tiled, якоря растянуты по ширине строки).")]
    public RectTransform dots;

    [Tooltip("Зазор между текстом и точками, px.")]
    [Min(0f)] public float gap = 6f;

    private Color defaultValueColor;
    private bool colorCaptured;

    public void Set(string labelText, string valueText) => Set(labelText, valueText, null);

    /// <summary>Подпись и значение; valueColor — свой цвет значения (например, предупреждение), null — обычный.</summary>
    public void Set(string labelText, string valueText, Color? valueColor)
    {
        if (!colorCaptured && value != null) { defaultValueColor = value.color; colorCaptured = true; }
        if (label != null) label.text = labelText;
        if (value != null)
        {
            value.text = valueText;
            value.color = valueColor ?? defaultValueColor;
        }
        LayoutDots();
    }

    private void OnRectTransformDimensionsChange() => LayoutDots();

    private void LayoutDots()
    {
        if (dots == null) return;
        float left = label != null ? label.preferredWidth + gap : 0f;
        float right = value != null ? value.preferredWidth + gap : 0f;
        dots.offsetMin = new Vector2(left, dots.offsetMin.y);
        dots.offsetMax = new Vector2(-right, dots.offsetMax.y);
    }
}
