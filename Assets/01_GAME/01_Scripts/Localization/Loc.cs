/// <summary>
/// Короткий доступ к строкам из кода окон: Loc.Get("settings.title"). Без LocalizationService в игре
/// возвращает сам ключ — окно не падает, а пропущенная настройка сцены сразу видна.
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
}
