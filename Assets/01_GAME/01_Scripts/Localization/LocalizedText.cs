using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Переводимый текст: вешается на объект с TMP_Text или со старым UI.Text (весь существующий интерфейс
/// проекта пока на нём) и подставляет строку по ключу из strings.csv, перечитывая её при смене языка.
///
/// Без LocalizationService в игре текст не трогается — остаётся тот, что набран в инспекторе. Поэтому
/// русский текст в сцене — это заодно и «запасной» текст.
///
/// Текст, который пишет код (уровень игрока, баланс и т.п.), сюда не подходит: компонент перезаписал
/// бы его при смене языка. Такие строки код берёт сам через Loc.Get.
/// </summary>
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [Header("Строка")]
    [Tooltip("Ключ строки в strings.csv (например settings.title).")]
    public string key;

    private TMP_Text tmpText;
    private Text legacyText;
    private LocalizationService service;
    private object[] formatArgs;

    private void OnEnable()
    {
        service = LocalizationService.Instance;
        if (service != null) service.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (service != null) service.OnLanguageChanged -= Refresh;
        service = null;
    }

    /// <summary>Сменить ключ (из кода, например подпись строки настроек).</summary>
    public void SetKey(string newKey)
    {
        key = newKey;
        formatArgs = null;
        Refresh();
    }

    /// <summary>Сменить ключ и параметры подстановки ({0}, {1}...).</summary>
    public void SetKey(string newKey, params object[] args)
    {
        key = newKey;
        formatArgs = args;
        Refresh();
    }

    /// <summary>Перечитать строку на текущем языке.</summary>
    public void Refresh()
    {
        if (service == null || string.IsNullOrEmpty(key)) return;

        // Компоненты ищутся лениво: SetKey могут позвать до Awake — у копии неактивного шаблона.
        if (tmpText == null && legacyText == null)
        {
            tmpText = GetComponent<TMP_Text>();
            legacyText = GetComponent<Text>();
        }

        string value = formatArgs != null && formatArgs.Length > 0 ? service.Get(key, formatArgs) : service.Get(key);
        if (tmpText != null) tmpText.text = value;
        else if (legacyText != null) legacyText.text = value;
    }
}
