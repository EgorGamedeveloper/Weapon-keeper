/// <summary>
/// Короткий доступ к строкам из кода окон: Loc.Get("settings.title"). Без LocalizationService в игре
/// возвращает сам ключ — окно не падает, а пропущенная настройка сцены сразу видна.
///
/// Тексты данных (названия и описания предметов, квестов, навыков...) берутся через GetOr: ключ строится из
/// id ассета (DataKey), а если строки в таблице ещё нет — показывается текст, вписанный в сам ассет.
/// Выгружает такие тексты в таблицу меню Tools/Weapon Keeper/Localization/Export Data Texts.
/// </summary>
public static class Loc
{
    /// <summary>Строка по ключу на текущем языке.</summary>
    public static string Get(string key)
    {
        LocalizationService service = LocalizationService.Instance;
        return service != null ? service.Get(key) : key;
    }

    /// <summary>Строка по ключу с подстановкой параметров ({0}, {1}...).</summary>
    public static string Get(string key, params object[] args)
    {
        LocalizationService service = LocalizationService.Instance;
        if (service != null) return service.Get(key, args);
        return args == null || args.Length == 0 ? key : key + " " + string.Join(" ", args);
    }

    /// <summary>Строка по ключу, а если её нет в таблице (или нет сервиса) — fallback, без предупреждения.</summary>
    public static string GetOr(string key, string fallback)
    {
        LocalizationService service = LocalizationService.Instance;
        return service != null && service.TryGet(key, out string value) ? value : fallback;
    }

    /// <summary>Ключ текста данных: «item.ammobox_556.name». Пустой id — null (тогда GetOr вернёт текст ассета).</summary>
    public static string DataKey(string kind, string id, string field)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return kind + "." + id.Trim() + "." + field;
    }
}
