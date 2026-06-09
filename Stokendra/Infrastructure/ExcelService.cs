using ClosedXML.Excel;
using System;
using System.Collections.Generic;

namespace Stokendra.Infrastructure;

/// <summary>
/// ClosedXML Excel işlemleri için merkezi servis.
/// Tüm Excel export işlemleri burada tek tip ve premium tasarımla stillendirilir.
/// </summary>
public static class ExcelService
{
    // ── Renk sabitleri ────────────────────────────────────────────────────
    private static readonly XLColor HeaderBg    = XLColor.FromArgb(30, 60, 110);
    private static readonly XLColor HeaderFg    = XLColor.White;
    private static readonly XLColor ZebraRowBg  = XLColor.FromArgb(245, 247, 250);
    private static readonly XLColor BorderColor = XLColor.FromArgb(200, 200, 200);

    /// <summary>
    /// Verilen koleksiyonu premium stil ile Excel dosyasına aktarır.
    /// </summary>
    public static void ExportToExcel<T>(
        string filePath,
        string sheetName,
        string[] headers,
        IEnumerable<T> items,
        Func<T, object?[]> rowMapper)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Headers
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBg;
            cell.Style.Font.FontColor = HeaderFg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data
        int row = 2;
        foreach (var item in items)
        {
            var values = rowMapper(item);
            for (int i = 0; i < values.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = values[i] is null
                    ? XLCellValue.FromObject("")
                    : XLCellValue.FromObject(values[i]!);
            }
            row++;
        }

        int rowCount = row - 2;
        if (rowCount > 0)
        {
            var range = worksheet.Range(1, 1, rowCount + 1, headers.Length);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = BorderColor;
            range.Style.Border.InsideBorderColor = XLColor.FromArgb(220, 220, 220);

            for (int r = 2; r <= rowCount + 1; r++)
            {
                if (r % 2 == 0)
                    worksheet.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = ZebraRowBg;
            }
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
