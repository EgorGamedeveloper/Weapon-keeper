using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сюжетный граф — модель файла story_graph.json, который собирается в веб-редакторе сюжета
/// (Tools/StoryEditor) и импортируется меню Tools/Weapon Keeper/Story/Import Story Graph.
///
/// Нода — один плоский класс со всеми полями всех типов: JsonUtility не умеет полиморфизм и словари,
/// лишние поля у ноды просто пустые. Тексты — массивы {lang, text} по столбцам языков strings.csv (ru, en...).
/// Формат совпадает с тем, что пишет редактор (toFileFormat в story_editor.html) — менять вместе.
/// </summary>
[Serializable]
public class StoryGraphData
{
    public int version;
    public string[] languages = Array.Empty<string>();
    public StoryNodeData[] nodes = Array.Empty<StoryNodeData>();
    public StoryLinkData[] links = Array.Empty<StoryLinkData>();

    /// <summary>Разобрать JSON графа. Битый файл — null и сообщение в error.</summary>
    public static StoryGraphData Parse(string json, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "файл пуст";
            return null;
        }

        try
        {
            StoryGraphData graph = JsonUtility.FromJson<StoryGraphData>(json.TrimStart('﻿'));
            if (graph == null || graph.nodes == null)
            {
                error = "в файле нет списка nodes";
                return null;
            }
            graph.links ??= Array.Empty<StoryLinkData>();
            graph.languages ??= Array.Empty<string>();
            return graph;
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }
    }
}

/// <summary>Нитка графа: нода to включается, когда завершилась нода from.</summary>
[Serializable]
public class StoryLinkData
{
    public string from;
    public string fromPort = "out";
    public string to;
    public string toPort = "in";
}

/// <summary>Текст на одном языке (lang — столбец strings.csv: ru, en, de...).</summary>
[Serializable]
public class StoryLocText
{
    public string lang;
    public string text;
}

/// <summary>Реплика переговоров по рации.</summary>
[Serializable]
public class StoryRadioLine
{
    public float seconds = 4f;
    public StoryLocText[] text = Array.Empty<StoryLocText>();
}

/// <summary>
/// Нода графа. type: start, quest, radio, trigger, wait, and, action, note — какие поля читаются,
/// зависит от типа (см. комментарии у полей).
/// </summary>
[Serializable]
public class StoryNodeData
{
    public string id;
    public string type;
    public float x;
    public float y;

    // quest
    public string questId;
    public StoryLocText[] title = Array.Empty<StoryLocText>();
    public StoryLocText[] description = Array.Empty<StoryLocText>();
    public string questType;
    public int targetCount = 1;
    public int xp;
    public int money;

    // quest и trigger: цели (id объектов из каталога сцены)
    public string target;
    public string category;
    public string item;
    public string zone;

    // radio
    public StoryLocText[] speaker = Array.Empty<StoryLocText>();
    public string portrait;
    public StoryRadioLine[] lines = Array.Empty<StoryRadioLine>();

    // trigger
    public string trigger;
    public int count = 1;
    public float value;
    public string orderId;
    public string lootBoxId;
    public string enemyType;

    // wait
    public float seconds;

    // action
    public string action;

    // note
    public string text;

    public const string Start = "start";
    public const string Quest = "quest";
    public const string Radio = "radio";
    public const string Trigger = "trigger";
    public const string Wait = "wait";
    public const string And = "and";
    public const string Action = "action";
    public const string Note = "note";
}

/// <summary>Выбор текста графа на языке игры.</summary>
public static class StoryText
{
    /// <summary>Текст на текущем языке игры, иначе на английском, иначе первый непустой.</summary>
    public static string Pick(StoryLocText[] texts)
    {
        if (texts == null || texts.Length == 0) return "";

        string current = LocalizationService.Instance != null
            ? GameLanguages.SteamCodeToColumn(LocalizationService.Instance.CurrentLanguage)
            : GameLanguages.SteamCodeToColumn(GameLanguages.English);

        string english = null, first = null;
        foreach (StoryLocText entry in texts)
        {
            if (entry == null || string.IsNullOrEmpty(entry.text)) continue;
            if (string.Equals(entry.lang, current, StringComparison.OrdinalIgnoreCase)) return entry.text;
            if (english == null && string.Equals(entry.lang, "en", StringComparison.OrdinalIgnoreCase)) english = entry.text;
            first ??= entry.text;
        }
        return english ?? first ?? "";
    }

    /// <summary>Текст на конкретном языке (столбец) — для импорта в strings.csv.</summary>
    public static string Get(StoryLocText[] texts, string lang)
    {
        if (texts == null) return "";
        foreach (StoryLocText entry in texts)
            if (entry != null && string.Equals(entry.lang, lang, StringComparison.OrdinalIgnoreCase)) return entry.text ?? "";
        return "";
    }

    /// <summary>Все языки, на которых в наборе есть непустой текст.</summary>
    public static IEnumerable<string> Languages(StoryLocText[] texts)
    {
        if (texts == null) yield break;
        foreach (StoryLocText entry in texts)
            if (entry != null && !string.IsNullOrEmpty(entry.lang) && !string.IsNullOrEmpty(entry.text)) yield return entry.lang;
    }
}
