using System.Collections.Generic;
using System.Text;

/// <summary>
/// Разбор и запись CSV по RFC 4180: поля через запятую; поле в двойных кавычках может содержать
/// запятые, переводы строк и саму кавычку (удвоенную: ""). Нужен таблице локализации strings.csv —
/// её правят в любом табличном редакторе и сливают в git как обычный текст.
///
/// Разбор снисходительный: кавычка посреди незакавыченного поля считается обычным символом, текст
/// после закрывающей кавычки дописывается в то же поле — битая строка не роняет загрузку всей таблицы.
/// </summary>
public static class CsvUtility
{
    private static readonly char[] CharsRequiringQuotes = { ',', '"', '\r', '\n' };

    /// <summary>Разобрать текст CSV в список строк таблицы. BOM в начале пропускается, полностью
    /// пустые строки не попадают в результат.</summary>
    public static List<List<string>> Parse(string text)
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
            else if (c == ',')
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

    /// <summary>Экранировать одно поле: кавычки нужны, если в нём есть запятая, кавычка, перевод
    /// строки или пробелы по краям.</summary>
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        bool needsQuotes = value.IndexOfAny(CharsRequiringQuotes) >= 0
                           || value[0] == ' ' || value[value.Length - 1] == ' ';
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    /// <summary>Собрать строку CSV из полей (без перевода строки в конце).</summary>
    public static string FormatRow(IList<string> fields)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append(Escape(fields[i]));
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
