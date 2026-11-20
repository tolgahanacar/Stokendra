using ClosedXML.Excel;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stokendra.Infrastructure;

public static class ExcelImportHelper
{
    /// <summary>
    /// Imports data from an Excel file dynamically finding columns based on headers.
    /// </summary>
    public static List<T> ImportData<T>(
        string filePath,
        Dictionary<string, string[]> columnMappings,
        Dictionary<string, int>? fallbackIndices,
        Func<IXLRangeRow, Dictionary<string, int>, T?> rowParser)
    {
        var result = new List<T>();
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);
        
        var firstRow = worksheet.FirstRowUsed();
        if (firstRow == null) return result;

        int colCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        
        var colIndices = new Dictionary<string, int>();
        foreach (var key in columnMappings.Keys) colIndices[key] = 0;

        for (int i = 1; i <= colCount; i++)
        {
            string header = (firstRow.Cell(i).GetString() ?? "").Trim().ToTurkishLower();
            foreach (var kvp in columnMappings)
            {
                if (colIndices[kvp.Key] == 0 && kvp.Value.Any(v => header == v))
                {
                    colIndices[kvp.Key] = i;
                    break;
                }
            }
        }

        // Apply fallback if the main required columns (first two in mapping) are not found
        if (fallbackIndices != null && columnMappings.Count >= 2)
        {
            var firstTwo = columnMappings.Keys.Take(2).ToList();
            if (colIndices[firstTwo[0]] == 0 && colIndices[firstTwo[1]] == 0)
            {
                foreach (var kvp in fallbackIndices)
                {
                    if (colIndices.ContainsKey(kvp.Key))
                        colIndices[kvp.Key] = kvp.Value;
                }
            }
        }

        var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();
        
        int lastRowNumber = 1;
        int consecutiveEmptyRows = 0;
        const int MaxConsecutiveEmpty = 50;
        const int MaxAllowedImportRows = 50000;

        foreach (var row in rows)
        {
            if (result.Count >= MaxAllowedImportRows)
                break;

            int currentRowNumber = row.RowNumber();
            // Circuit breaker 1: if there is a gap of 50+ unassigned/sparse blank rows
            if (currentRowNumber - lastRowNumber > MaxConsecutiveEmpty)
                break;

            lastRowNumber = currentRowNumber;

            var item = rowParser(row, colIndices);
            if (item != null)
            {
                consecutiveEmptyRows = 0;
                result.Add(item);
            }
            else
            {
                consecutiveEmptyRows++;
                // Circuit breaker 2: if 50 consecutive rows evaluate to empty/null
                if (consecutiveEmptyRows >= MaxConsecutiveEmpty)
                {
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Safely extracts a DateTime from an Excel cell (handles OLE dates, DateTime types, and strings).
    /// </summary>
    public static DateTime ParseCellDate(IXLCell cell, DateTime? defaultDate = null)
    {
        if (cell.TryGetValue<DateTime>(out var dt))
        {
            if (dt.Year >= 1990 && dt.Year <= 2100)
                return dt;
        }

        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var dDate))
        {
            if (dDate > 1000 && dDate < 100000)
            {
                try
                {
                    var dtOa = DateTime.FromOADate(dDate);
                    if (dtOa.Year >= 1990 && dtOa.Year <= 2100)
                        return dtOa;
                }
                catch { }
            }
        }

        var str = cell.GetString();
        if (!string.IsNullOrWhiteSpace(str))
        {
            if (DateTime.TryParse(str, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var dtParsed))
            {
                if (dtParsed.Year >= 1990 && dtParsed.Year <= 2100)
                    return dtParsed;
            }
            if (DateTime.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dtParsed))
            {
                if (dtParsed.Year >= 1990 && dtParsed.Year <= 2100)
                    return dtParsed;
            }
        }

        return defaultDate ?? DateTime.Now;
    }

    /// <summary>
    /// Safely extracts a double from an Excel cell.
    /// </summary>
    public static double ParseCellDouble(IXLCell cell, double defaultValue = 0.0)
    {
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var val))
            return val;

        var str = cell.GetString();
        if (!string.IsNullOrWhiteSpace(str))
        {
            str = str.Trim();
            if (double.TryParse(str.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var iVal))
                return iVal;
            if (double.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out var cVal))
                return cVal;
        }

        return defaultValue;
    }

    /// <summary>
    /// Safely extracts an int from an Excel cell.
    /// </summary>
    public static int ParseCellInt(IXLCell cell, int defaultValue = 0)
    {
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var dVal))
            return (int)Math.Round(dVal);

        var str = cell.GetString();
        if (!string.IsNullOrWhiteSpace(str))
        {
            str = str.Trim();
            if (int.TryParse(str, out var parsed))
                return parsed;
            if (double.TryParse(str.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dParsed))
                return (int)Math.Round(dParsed);
        }

        return defaultValue;
    }

    /// <summary>
    /// Safely extracts a string from an Excel cell (handles numeric, boolean, text, formulas without throwing InvalidCastException).
    /// </summary>
    public static string ParseCellString(IXLCell? cell, string defaultValue = "")
    {
        if (cell == null || cell.IsEmpty()) return defaultValue;
        return (cell.GetString() ?? defaultValue).Trim();
    }
}

