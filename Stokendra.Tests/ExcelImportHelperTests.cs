using Xunit;
using ClosedXML.Excel;
using Stokendra.Infrastructure;
using System;

namespace Stokendra.Tests;

public class ExcelImportHelperTests
{
    [Fact]
    public void Test_ParseCellDate_FromDateTimeCell()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        var expected = new DateTime(2025, 5, 20);
        ws.Cell(1, 1).Value = expected;

        var result = ExcelImportHelper.ParseCellDate(ws.Cell(1, 1));

        Assert.Equal(expected.Date, result.Date);
    }

    [Fact]
    public void Test_ParseCellDate_FromStringCell()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "20.05.2025";

        var result = ExcelImportHelper.ParseCellDate(ws.Cell(1, 1));

        Assert.Equal(2025, result.Year);
        Assert.Equal(5, result.Month);
        Assert.Equal(20, result.Day);
    }

    [Fact]
    public void Test_ParseCellDouble_HandlesCommaAndDot()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        
        ws.Cell(1, 1).Value = 15.5;
        ws.Cell(1, 2).Value = "15,5";
        ws.Cell(1, 3).Value = "15.5";

        Assert.Equal(15.5, ExcelImportHelper.ParseCellDouble(ws.Cell(1, 1)));
        Assert.Equal(15.5, ExcelImportHelper.ParseCellDouble(ws.Cell(1, 2)));
        Assert.Equal(15.5, ExcelImportHelper.ParseCellDouble(ws.Cell(1, 3)));
    }

    [Fact]
    public void Test_ParseCellInt_HandlesNumericAndString()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");

        ws.Cell(1, 1).Value = 42;
        ws.Cell(1, 2).Value = 42.0;
        ws.Cell(1, 3).Value = "42";

        Assert.Equal(42, ExcelImportHelper.ParseCellInt(ws.Cell(1, 1)));
        Assert.Equal(42, ExcelImportHelper.ParseCellInt(ws.Cell(1, 2)));
        Assert.Equal(42, ExcelImportHelper.ParseCellInt(ws.Cell(1, 3)));
    }

    [Fact]
    public void Test_ParseCellDate_FromNumericOleDate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        // Excel serial date 45678 is 2025-01-21
        ws.Cell(1, 1).Value = 45678.0;

        var result = ExcelImportHelper.ParseCellDate(ws.Cell(1, 1));

        Assert.Equal(2025, result.Year);
        Assert.Equal(1, result.Month);
        Assert.Equal(21, result.Day);
    }

    [Fact]
    public void Test_ParseCellString_HandlesNumbersBooleansAndEmpty()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");

        ws.Cell(1, 1).Value = 12345;
        ws.Cell(1, 2).Value = true;
        ws.Cell(1, 3).Value = "  Test Title  ";

        Assert.Equal("12345", ExcelImportHelper.ParseCellString(ws.Cell(1, 1)));
        Assert.Equal("TRUE", ExcelImportHelper.ParseCellString(ws.Cell(1, 2)).ToUpperInvariant());
        Assert.Equal("Test Title", ExcelImportHelper.ParseCellString(ws.Cell(1, 3)));
        Assert.Equal("", ExcelImportHelper.ParseCellString(ws.Cell(1, 4)));
        Assert.Equal("Default", ExcelImportHelper.ParseCellString(ws.Cell(1, 4), "Default"));
    }

    [Fact]
    public void Test_ParseCellDate_BoundaryYears_FallbackSaneDefault()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        var fallback = new DateTime(2026, 1, 1);

        // Year 0001 (DateTime.MinValue string)
        ws.Cell(1, 1).Value = "01.01.0001";
        // Year 9999
        ws.Cell(1, 2).Value = "31.12.9999";
        // Year 1850 (too old for business stock tracking)
        ws.Cell(1, 3).Value = "15.06.1850";

        var res1 = ExcelImportHelper.ParseCellDate(ws.Cell(1, 1), fallback);
        var res2 = ExcelImportHelper.ParseCellDate(ws.Cell(1, 2), fallback);
        var res3 = ExcelImportHelper.ParseCellDate(ws.Cell(1, 3), fallback);

        Assert.Equal(fallback, res1);
        Assert.Equal(fallback, res2);
        Assert.Equal(fallback, res3);
    }

    [Fact]
    public void Test_ImportData_CircuitBreaker_StopsAtConsecutiveEmptyRows()
    {
        string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Stokendra_Test_CircuitBreaker_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Sheet1");
                ws.Cell(1, 1).Value = "Code";
                ws.Cell(1, 2).Value = "Name";

                // Add 2 valid rows
                ws.Cell(2, 1).Value = "C001";
                ws.Cell(2, 2).Value = "Item 1";
                ws.Cell(3, 1).Value = "C002";
                ws.Cell(3, 2).Value = "Item 2";

                // Leave rows 4 to 65 empty (60 empty rows)
                // Add a row way at row 70 that should NOT be reached due to circuit breaker (50 consecutive empty)
                ws.Cell(70, 1).Value = "C070";
                ws.Cell(70, 2).Value = "Item 70";

                wb.SaveAs(tempFile);
            }

            var mappings = new System.Collections.Generic.Dictionary<string, string[]>
            {
                { "code", new[] { "code" } },
                { "name", new[] { "name" } }
            };

            var imported = ExcelImportHelper.ImportData(tempFile, mappings, null, (row, col) =>
            {
                string code = ExcelImportHelper.ParseCellString(row.Cell(col["code"]));
                if (string.IsNullOrEmpty(code)) return null;
                return code;
            });

            // Circuit breaker stopped after row 3 + 50 empty rows, so row 70 was ignored!
            Assert.Equal(2, imported.Count);
            Assert.Equal("C001", imported[0]);
            Assert.Equal("C002", imported[1]);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
            {
                try { System.IO.File.Delete(tempFile); } catch { }
            }
        }
    }
}
