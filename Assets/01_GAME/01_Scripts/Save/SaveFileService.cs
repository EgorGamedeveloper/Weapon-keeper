using System.IO;
using UnityEngine;

/// <summary>
/// Где лежит файл сохранения и что с ним можно сделать на уровне файловой системы.
/// Формат самих данных — в SaveGameData/SaveLoadService, сюда он не протекает.
///
/// Путь — <see cref="Application.persistentDataPath"/>, то есть на Windows
/// %USERPROFILE%\AppData\LocalLow\GEGA Games\Gun Keeper\. Это выбрано не случайно: ровно на
/// этот путь настраивается Steam Auto-Cloud (Root = WinAppDataLocalLow,
/// Subdirectory = GEGA Games/Gun Keeper, Pattern = *.sav), и тогда Steam синхронизирует сейвы
/// сам, без единой строчки кода в игре. Отсюда же два правила: расширение у всех файлов сейва
/// одно, и PlayerPrefs для сохранений не используется — на Windows это реестр, в облако он
/// не уедет.
///
/// ВАЖНО: путь собирается из companyName/productName (GEGA Games / Gun Keeper). Менять их
/// после релиза нельзя — игроки потеряют доступ к своим сохранениям.
/// </summary>
public static class SaveFileService
{
    private const string SaveFileName = "savegame.sav";

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool HasSave() => File.Exists(SavePath);

    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}
