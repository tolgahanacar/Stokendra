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
            string header = firstRow.Cell(i).GetValue<string>().Trim().ToTurkishLower();
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
        
        foreach (var row in rows)
        {
            var item = rowParser(row, colIndices);
            if (item != null)
            {
                result.Add(item);
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
            return dt;

        var str = cell.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(str))
        {
            if (DateTime.TryParse(str, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var dtParsed))
                return dtParsed;
            if (DateTime.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dtParsed))
                return dtParsed;
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

        var str = cell.GetValue<string>();
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

        var str = cell.GetValue<string>();
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
}

