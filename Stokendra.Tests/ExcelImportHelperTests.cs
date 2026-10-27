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
}
