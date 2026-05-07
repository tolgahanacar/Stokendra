using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;

namespace StokTakip.Infrastructure;

public static class ExcelService
{
    public static void ExportToExcel<T>(string filePath, string sheetName, string[] headers, IEnumerable<T> items, System.Func<T, object?[]> rowMapper)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Headers
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E0E0");
        }

        // Data
        int row = 2;
        foreach (var item in items)
        {
            var values = rowMapper(item);
            for (int i = 0; i < values.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = XLCellValue.FromObject(values[i]);
            }
            row++;
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
