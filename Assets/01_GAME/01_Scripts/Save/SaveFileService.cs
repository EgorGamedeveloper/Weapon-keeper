using System;
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

    /// <summary>
    /// Записать сейв. Атомарно: сначала во временный файл, потом File.Replace (или Move, если
    /// целевого файла ещё нет) — если игра вылетит посреди записи, старый сейв останется целым,
    /// а не окажется наполовину переписанным мусором.
    /// </summary>
    public static void Save(SaveGameData data)
    {
        string json = JsonUtility.ToJson(data, true);
        string tempPath = SavePath + ".tmp";

        File.WriteAllText(tempPath, json);

        if (File.Exists(SavePath)) File.Replace(tempPath, SavePath, null);
        else File.Move(tempPath, SavePath);
    }

    /// <summary>Прочитать сейв. Возвращает null, если файла нет или он повреждён (не должно
    /// уронить игру — вызывающий код просто не должен применять состояние в этом случае).</summary>
    public static SaveGameData Load()
    {
        if (!File.Exists(SavePath)) return null;

        try
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<SaveGameData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveFileService] Не удалось прочитать сейв: {e.Message}");
            return null;
        }
    }
}
