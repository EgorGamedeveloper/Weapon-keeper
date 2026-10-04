using UnityEngine;
using UnityEngine.UI;

/// <summary>Раздел каталога: иконка и подпись («БОЕПРИПАСЫ») и сетка плиток под ними.</summary>
public class TerminalCatalogGroupUI : MonoBehaviour
{
    [Tooltip("Иконка раздела.")]
    public Image icon;

    [Tooltip("Подпись раздела.")]
    public Text label;

    [Tooltip("Сетка плиток (Grid Layout Group).")]
    public Transform grid;
}
