using System.Collections.Generic;
using System.Text;

/// <summary>
/// Разбор и запись CSV по RFC 4180: поля через разделитель; поле в двойных кавычках может содержать
/// разделитель, переводы строк и саму кавычку (удвоенную: ""). Нужен таблице локализации strings.csv —
/// её правят в любом табличном редакторе и сливают в git как обычный текст.
///
/// Разделитель определяется по строке заголовка: запятая (Google Таблицы, LibreOffice), точка с запятой
/// (Excel с русскими региональными настройками сохраняет CSV так) или табуляция — таблицу можно
/// сохранить из любого редактора, не думая о формате.
///
/// Разбор снисходительный: кавычка посреди незакавыченного поля считается обычным символом, текст
/// после закрывающей кавычки дописывается в то же поле — битая строка не роняет загрузку всей таблицы.
/// </summary>
public static class CsvUtility
{
    /// <summary>Разделитель по умолчанию (для новых таблиц).</summary>
    public const char DefaultDelimiter = ',';

    /// <summary>Разобрать текст CSV в список строк таблицы. Разделитель определяется сам (см. у класса),
    /// BOM в начале пропускается, полностью пустые строки не попадают в результат.</summary>
    public static List<List<string>> Parse(string text) => Parse(text, DetectDelimiter(text));

    /// <summary>Разобрать текст CSV с заданным разделителем.</summary>
    public static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(text)) return rows;

        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int i = text[0] == '﻿' ? 1 : 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" внутри кавычек — это сама кавычка, одиночная " закрывает поле.
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
                i++;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
                i++;
            }
            else if (c == '\r' || c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                AddRowIfNotEmpty(rows, row);
                row = new List<string>();
                i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            }
            else
            {
                field.Append(c);
                i++;
            }
        }

        // Последняя строка без перевода строки в конце файла.
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            AddRowIfNotEmpty(rows, row);
        }

        return rows;
    }

    /// <summary>Разделитель таблицы по первой строке (заголовку): тот из «, ; Tab», что встречается в ней
    /// вне кавычек чаще всего. Пустой текст или строка без разделителей — запятая.</summary>
    public static char DetectDelimiter(string text)
    {
        if (string.IsNullOrEmpty(text)) return DefaultDelimiter;

        int commas = 0, semicolons = 0, tabs = 0;
        bool inQuotes = false;
        foreach (char c in text)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (inQuotes) continue;
            else if (c == '\r' || c == '\n') break;
            else if (c == ',') commas++;
            else if (c == ';') semicolons++;
            else if (c == '\t') tabs++;
        }

        if (semicolons > commas && semicolons >= tabs) return ';';
        if (tabs > commas && tabs > semicolons) return '\t';
        return DefaultDelimiter;
    }

    /// <summary>Экранировать одно поле: кавычки нужны, если в нём есть разделитель, кавычка, перевод
    /// строки или пробелы по краям.</summary>
    public static string Escape(string value, char delimiter = DefaultDelimiter)
    {
        if (string.IsNullOrEmpty(value)) return "";

        bool needsQuotes = value.IndexOf(delimiter) >= 0 || value.IndexOf('"') >= 0
                           || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0
                           || value[0] == ' ' || value[value.Length - 1] == ' ';
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    /// <summary>Собрать строку CSV из полей (без перевода строки в конце).</summary>
    public static string FormatRow(IList<string> fields, char delimiter = DefaultDelimiter)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0) builder.Append(delimiter);
            builder.Append(Escape(fields[i], delimiter));
        }
        return builder.ToString();
    }

    private static void AddRowIfNotEmpty(List<List<string>> rows, List<string> row)
    {
        foreach (string value in row)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Add(row);
                return;
            }
        }
    }
}
