using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Справочник языков: столбец strings.csv ↔ код языка Steam API ↔ SystemLanguage ↔ культура для
/// форматирования чисел. Внутри игры язык везде обозначается кодом Steam (russian, english): его отдаёт
/// SteamApps.GetCurrentGameLanguage, им же названы файлы rich presence.
///
/// Чтобы добавить язык, достаточно столбца в strings.csv с кодом из этой таблицы (например de) или с кодом
/// Steam (german). Язык, которого здесь нет, тоже заработает — столбец тогда считается кодом Steam как есть,
/// а в списке языков показывается сам код. Порядок языков в меню — порядок столбцов таблицы.
/// </summary>
public static class GameLanguages
{
    public const string Russian = "russian";
    public const string English = "english";

    /// <summary>Описание одного языка.</summary>
    public readonly struct Info
    {
        /// <summary>Заголовок столбца в strings.csv (ru, en...).</summary>
        public readonly string column;
        /// <summary>Код языка Steam API (russian, english...).</summary>
        public readonly string steamCode;
        /// <summary>Название языка на нём самом — так он показывается в выпадающем списке.</summary>
        public readonly string nativeName;
        /// <summary>Имя культуры .NET для форматирования чисел.</summary>
        public readonly string culture;
        /// <summary>Язык системы, которому соответствует этот язык.</summary>
        public readonly SystemLanguage systemLanguage;

        public Info(string column, string steamCode, string nativeName, string culture, SystemLanguage systemLanguage)
        {
            this.column = column;
            this.steamCode = steamCode;
            this.nativeName = nativeName;
            this.culture = culture;
            this.systemLanguage = systemLanguage;
        }
    }

    /// <summary>Известные языки. Коды Steam — partner.steamgames.com/doc/store/localization/languages.</summary>
    public static readonly Info[] Known =
    {
        new Info("ru", Russian, "Русский", "ru-RU", SystemLanguage.Russian),
        new Info("en", English, "English", "en-US", SystemLanguage.English),
        new Info("de", "german", "Deutsch", "de-DE", SystemLanguage.German),
        new Info("fr", "french", "Français", "fr-FR", SystemLanguage.French),
        new Info("es", "spanish", "Español", "es-ES", SystemLanguage.Spanish),
        new Info("pt-br", "brazilian", "Português (Brasil)", "pt-BR", SystemLanguage.Portuguese),
        new Info("it", "italian", "Italiano", "it-IT", SystemLanguage.Italian),
        new Info("pl", "polish", "Polski", "pl-PL", SystemLanguage.Polish),
        new Info("uk", "ukrainian", "Українська", "uk-UA", SystemLanguage.Ukrainian),
        new Info("tr", "turkish", "Türkçe", "tr-TR", SystemLanguage.Turkish),
        new Info("zh-cn", "schinese", "简体中文", "zh-CN", SystemLanguage.ChineseSimplified),
        new Info("ja", "japanese", "日本語", "ja-JP", SystemLanguage.Japanese),
        new Info("ko", "koreana", "한국어", "ko-KR", SystemLanguage.Korean),
        new Info("zh-tw", "tchinese", "繁體中文", "zh-TW", SystemLanguage.ChineseTraditional),
        new Info("pt", "portuguese", "Português", "pt-PT", SystemLanguage.Portuguese),
        new Info("es-419", "latam", "Español (Latinoamérica)", "es-MX", SystemLanguage.Unknown),
        new Info("cs", "czech", "Čeština", "cs-CZ", SystemLanguage.Czech),
        new Info("nl", "dutch", "Nederlands", "nl-NL", SystemLanguage.Dutch),
        new Info("sv", "swedish", "Svenska", "sv-SE", SystemLanguage.Swedish),
        new Info("fi", "finnish", "Suomi", "fi-FI", SystemLanguage.Finnish),
        new Info("da", "danish", "Dansk", "da-DK", SystemLanguage.Danish),
        new Info("no", "norwegian", "Norsk", "nb-NO", SystemLanguage.Norwegian),
        new Info("hu", "hungarian", "Magyar", "hu-HU", SystemLanguage.Hungarian),
        new Info("ro", "romanian", "Română", "ro-RO", SystemLanguage.Romanian),
        new Info("bg", "bulgarian", "Български", "bg-BG", SystemLanguage.Bulgarian),
        new Info("el", "greek", "Ελληνικά", "el-GR", SystemLanguage.Greek),
        new Info("th", "thai", "ไทย", "th-TH", SystemLanguage.Thai),
        new Info("vi", "vietnamese", "Tiếng Việt", "vi-VN", SystemLanguage.Vietnamese),
        new Info("id", "indonesian", "Bahasa Indonesia", "id-ID", SystemLanguage.Indonesian),
        new Info("ar", "arabic", "العربية", "ar-SA", SystemLanguage.Arabic),
    };

    /// <summary>Код Steam по заголовку столбца CSV. Незнакомый столбец считается кодом Steam как есть.</summary>
    public static string ColumnToSteamCode(string column)
    {
        if (string.IsNullOrEmpty(column)) return column;
        string normalized = column.Trim().ToLowerInvariant();
        foreach (Info info in Known)
            if (info.column == normalized || info.steamCode == normalized) return info.steamCode;
        return normalized;
    }

    /// <summary>Заголовок столбца CSV по коду Steam (для редакторских инструментов).</summary>
    public static string SteamCodeToColumn(string steamCode)
    {
        foreach (Info info in Known)
            if (info.steamCode == steamCode) return info.column;
        return steamCode;
    }

    /// <summary>Название языка на нём самом (для выпадающего списка). Незнакомый — код как есть.</summary>
    public static string GetNativeName(string steamCode)
    {
        foreach (Info info in Known)
            if (info.steamCode == steamCode) return info.nativeName;
        return steamCode;
    }

    /// <summary>Культура для форматирования чисел. Если культура недоступна на платформе — инвариантная.</summary>
    public static CultureInfo GetCulture(string steamCode)
    {
        foreach (Info info in Known)
        {
            if (info.steamCode != steamCode) continue;
            try
            {
                return CultureInfo.GetCultureInfo(info.culture);
            }
            catch (Exception)
            {
                return CultureInfo.InvariantCulture;
            }
        }
        return CultureInfo.InvariantCulture;
    }

    /// <summary>Язык, соответствующий языку системы.</summary>
    public static bool TryFromSystemLanguage(SystemLanguage systemLanguage, out Info language)
    {
        foreach (Info info in Known)
        {
            // Unknown — у языка нет своего SystemLanguage (латиноамериканский испанский): по системе не выбирается.
            if (systemLanguage == SystemLanguage.Unknown || info.systemLanguage != systemLanguage) continue;
            language = info;
            return true;
        }

        language = default;
        return false;
    }
}
