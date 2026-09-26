using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Достижения и статистика без Steam: файл persistentDataPath/achievements.sav (JSON). Для билдов без Steam
/// и Play в редакторе. Расширение .sav — файл попадает под Steam Auto-Cloud (маска *.sav), как и сейв.
/// Запись атомарная (временный файл + замена), битый файл — чистое состояние и предупреждение.
/// </summary>
public class LocalAchievementsBackend : IPlatformAchievements
{
    public const string FileName = "achievements.sav";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    [Serializable]
    public class StatEntry
    {
        public string name;
        public double value;
    }

    [Serializable]
    public class UnlockEntry
    {
        public string name;
        public string unlockedAtUtc;
    }

    [Serializable]
    public class FileData
    {
        public int version = 1;
        public List<StatEntry> stats = new List<StatEntry>();
        public List<UnlockEntry> unlocked = new List<UnlockEntry>();
    }

    private readonly Dictionary<string, double> stats = new Dictionary<string, double>();
    private readonly Dictionary<string, string> unlocked = new Dictionary<string, string>();
    private bool dirty;

    public LocalAchievementsBackend() => Load();

    public string Name => "Local (" + FileName + ")";
    public bool ShowsOwnNotifications => false;

    public bool IsUnlocked(string apiName) => !string.IsNullOrEmpty(apiName) && unlocked.ContainsKey(apiName);

    public bool Unlock(string apiName)
    {
        if (string.IsNullOrEmpty(apiName)) return false;
        if (unlocked.ContainsKey(apiName)) return true;
        unlocked[apiName] = DateTime.UtcNow.ToString("o");
        dirty = true;
        return true;
    }

    public bool SetStat(string apiName, int value) => SetStatValue(apiName, value);
    public bool SetStat(string apiName, float value) => SetStatValue(apiName, value);

    public bool GetStat(string apiName, out int value)
    {
        bool found = stats.TryGetValue(apiName ?? "", out double stored);
        value = found ? (int)stored : 0;
        return found;
    }

    public bool GetStat(string apiName, out float value)
    {
        bool found = stats.TryGetValue(apiName ?? "", out double stored);
        value = found ? (float)stored : 0f;
        return found;
    }

    // Промежуточного уведомления у локального бэкенда нет — прогресс виден в окне достижений.
    public void IndicateProgress(string apiName, uint current, uint max) { }

    public bool Store()
    {
        if (!dirty) return true;

        var data = new FileData();
        foreach (KeyValuePair<string, double> pair in stats) data.stats.Add(new StatEntry { name = pair.Key, value = pair.Value });
        foreach (KeyValuePair<string, string> pair in unlocked) data.unlocked.Add(new UnlockEntry { name = pair.Key, unlockedAtUtc = pair.Value });

        try
        {
            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath)) File.Replace(tempPath, FilePath, null);
            else File.Move(tempPath, FilePath);
            dirty = false;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Achievements] Не удалось записать {FilePath}: {e.Message}");
            return false;
        }
    }

    public void ResetAll(bool achievementsToo)
    {
        stats.Clear();
        if (achievementsToo) unlocked.Clear();
        dirty = true;
        Store();
    }

    public void Shutdown() => Store();

    private bool SetStatValue(string apiName, double value)
    {
        if (string.IsNullOrEmpty(apiName)) return false;
        stats[apiName] = value;
        dirty = true;
        return true;
    }

    private void Load()
    {
        if (!File.Exists(FilePath)) return;

        try
        {
            var data = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath));
            if (data == null) return;
            if (data.stats != null)
                foreach (StatEntry entry in data.stats)
                    if (!string.IsNullOrEmpty(entry.name)) stats[entry.name] = entry.value;
            if (data.unlocked != null)
                foreach (UnlockEntry entry in data.unlocked)
                    if (!string.IsNullOrEmpty(entry.name)) unlocked[entry.name] = entry.unlockedAtUtc;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Achievements] {FileName} повреждён ({e.Message}) — локальные достижения начаты заново.");
            stats.Clear();
            unlocked.Clear();
        }
    }
}
