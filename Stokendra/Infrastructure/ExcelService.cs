using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Stokendra.Infrastructure;

/// <summary>
/// ClosedXML Excel işlemleri için merkezi servis.
/// Küçük/orta veri setlerinde tam stillerle ClosedXML, büyük veri setlerinde bellek dostu OpenXmlWriter (SAX) kullanır.
/// </summary>
public static class ExcelService
{
    private const int StreamingThreshold = 2500;

    // ── Renk sabitleri ────────────────────────────────────────────────────
    private static readonly XLColor HeaderBg    = XLColor.FromArgb(30, 60, 110);
    private static readonly XLColor HeaderFg    = XLColor.White;
    private static readonly XLColor ZebraRowBg  = XLColor.FromArgb(245, 247, 250);
    private static readonly XLColor BorderColor = XLColor.FromArgb(200, 200, 200);

    /// <summary>
    /// Verilen koleksiyonu Excel dosyasına aktarır; özelleştirme yoksa ve satır sayısı büyükse akış (SAX) moduna geçer.
    /// </summary>
    public static void ExportToExcel<T>(
        string filePath,
        string sheetName,
        string[] headers,
        IEnumerable<T> items,
        Func<T, object?[]> rowMapper,
        Action<IXLWorksheet>? customizeSheet = null)
    {
        if (customizeSheet == null && items is ICollection<T> collection && collection.Count > StreamingThreshold)
        {
            ExportStreaming(filePath, sheetName, headers, collection, rowMapper);
            return;
        }

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

            if (rowCount <= StreamingThreshold)
            {
                for (int r = 2; r <= rowCount + 1; r += 2)
                {
                    worksheet.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = ZebraRowBg;
                }
            }
        }

        int adjustRows = Math.Max(1, Math.Min(rowCount + 1, 150));
        worksheet.Columns().AdjustToContents(1, adjustRows);
        customizeSheet?.Invoke(worksheet);

        try
        {
            workbook.SaveAs(filePath);
        }
        catch (IOException ex) when (IsFileLocked(ex))
        {
            throw new InvalidOperationException(LocalizationManager.L("file_in_use_error", Path.GetFileName(filePath)), ex);
        }
    }

    /// <summary>
    /// Büyük tabloları DOM oluşturmadan OpenXmlWriter (SAX) üzerinden sabit bellekle diske yazar.
    /// </summary>
    public static void ExportStreaming<T>(
        string filePath,
        string sheetName,
        string[] headers,
        IEnumerable<T> items,
        Func<T, object?[]> rowMapper)
    {
        try
        {
            using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStreamingStylesheet();
            stylesPart.Stylesheet.Save();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = SanitizeSheetName(sheetName)
            });

            using (var writer = OpenXmlWriter.Create(worksheetPart))
            {
                writer.WriteStartElement(new Worksheet());

                if (headers.Length > 0)
                {
                    writer.WriteStartElement(new Columns());
                    for (uint i = 0; i < headers.Length; i++)
                    {
                        double colWidth = Math.Clamp((headers[i]?.Length ?? 10) + 8, 16, 48);
                        writer.WriteElement(new Column
                        {
                            Min = i + 1,
                            Max = i + 1,
                            Width = colWidth,
                            CustomWidth = true
                        });
                    }
                    writer.WriteEndElement();
                }

                writer.WriteStartElement(new SheetData());

                // Header row
                writer.WriteStartElement(new Row { RowIndex = 1U });
                for (int c = 0; c < headers.Length; c++)
                {
                    WriteTextCell(writer, headers[c] ?? "", styleIndex: 1U);
                }
                writer.WriteEndElement();

                // Data rows
                uint rowIndex = 2U;
                foreach (var item in items)
                {
                    writer.WriteStartElement(new Row { RowIndex = rowIndex++ });
                    var values = rowMapper(item);
                    for (int c = 0; c < headers.Length; c++)
                    {
                        object? val = c < values.Length ? values[c] : null;
                        WriteTypedCell(writer, val, styleIndex: 2U);
                    }
                    writer.WriteEndElement();
                }

                writer.WriteEndElement(); // SheetData
                writer.WriteEndElement(); // Worksheet
                writer.Close();
            }

            workbookPart.Workbook.Save();
        }
        catch (IOException ex) when (IsFileLocked(ex))
        {
            throw new InvalidOperationException(LocalizationManager.L("file_in_use_error", Path.GetFileName(filePath)), ex);
        }
    }

    private static void WriteTypedCell(OpenXmlWriter writer, object? val, uint styleIndex)
    {
        if (val is null)
        {
            WriteTextCell(writer, "", styleIndex);
            return;
        }

        switch (val)
        {
            case double d:
                if (double.IsNaN(d) || double.IsInfinity(d))
                    WriteTextCell(writer, "", styleIndex);
                else
                    WriteNumericCell(writer, d.ToString(CultureInfo.InvariantCulture), styleIndex);
                return;
            case float f:
                if (float.IsNaN(f) || float.IsInfinity(f))
                    WriteTextCell(writer, "", styleIndex);
                else
                    WriteNumericCell(writer, f.ToString(CultureInfo.InvariantCulture), styleIndex);
                return;
            case decimal m:
                WriteNumericCell(writer, m.ToString(CultureInfo.InvariantCulture), styleIndex);
                return;
            case int or long or short or byte or uint or ulong or ushort or sbyte:
                WriteNumericCell(writer, Convert.ToString(val, CultureInfo.InvariantCulture) ?? "0", styleIndex);
                return;
            case bool b:
                writer.WriteStartElement(new Cell { DataType = CellValues.Boolean, StyleIndex = styleIndex });
                writer.WriteElement(new CellValue(b ? "1" : "0"));
                writer.WriteEndElement();
                return;
            case DateTime dt:
                WriteTextCell(writer, dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture), styleIndex);
                return;
            default:
                WriteTextCell(writer, val.ToString() ?? "", styleIndex);
                return;
        }
    }

    private static void WriteNumericCell(OpenXmlWriter writer, string invariantNumber, uint styleIndex)
    {
        writer.WriteStartElement(new Cell { DataType = CellValues.Number, StyleIndex = styleIndex });
        writer.WriteElement(new CellValue(invariantNumber));
        writer.WriteEndElement();
    }

    private static void WriteTextCell(OpenXmlWriter writer, string text, uint styleIndex)
    {
        string safe = StripInvalidXmlChars(text);
        writer.WriteStartElement(new Cell { DataType = CellValues.InlineString, StyleIndex = styleIndex });
        writer.WriteStartElement(new InlineString());
        writer.WriteElement(new Text(safe) { Space = SpaceProcessingModeValues.Preserve });
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string StripInvalidXmlChars(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        StringBuilder? sb = null;
        for (int i = 0; i < input.Length; i++)
        {
            char ch = input[i];
            bool valid = ch == 0x9 || ch == 0xA || ch == 0xD || (ch >= 0x20 && ch <= 0xD7FF) || (ch >= 0xE000 && ch <= 0xFFFD);
            if (!valid)
            {
                sb ??= new StringBuilder(input, 0, i, input.Length);
            }
            else if (sb != null)
            {
                sb.Append(ch);
            }
        }
        return sb?.ToString() ?? input;
    }

    private static string SanitizeSheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Sheet1";
        var sb = new StringBuilder(Math.Min(name.Length, 31));
        foreach (char ch in name)
        {
            if (ch is ':' or '\\' or '/' or '?' or '*' or '[' or ']')
                sb.Append('_');
            else
                sb.Append(ch);
            if (sb.Length == 31) break;
        }
        string cleaned = sb.ToString().Trim('\'');
        return string.IsNullOrWhiteSpace(cleaned) ? "Sheet1" : cleaned;
    }

    private static Stylesheet CreateStreamingStylesheet()
    {
        return new Stylesheet(
            new Fonts(
                new Font(),
                new Font(new Bold(), new Color { Rgb = new HexBinaryValue("FFFFFFFF") })),
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(new ForegroundColor { Rgb = new HexBinaryValue("FF1E3C6E") }) { PatternType = PatternValues.Solid })),
            new Borders(
                new Border(),
                new Border(
                    new LeftBorder(new Color { Rgb = new HexBinaryValue("FFC8C8C8") }) { Style = BorderStyleValues.Thin },
                    new RightBorder(new Color { Rgb = new HexBinaryValue("FFC8C8C8") }) { Style = BorderStyleValues.Thin },
                    new TopBorder(new Color { Rgb = new HexBinaryValue("FFC8C8C8") }) { Style = BorderStyleValues.Thin },
                    new BottomBorder(new Color { Rgb = new HexBinaryValue("FFC8C8C8") }) { Style = BorderStyleValues.Thin })),
            new CellFormats(
                new CellFormat(),
                new CellFormat
                {
                    FontId = 1U,
                    FillId = 2U,
                    BorderId = 1U,
                    ApplyFont = true,
                    ApplyFill = true,
                    ApplyBorder = true,
                    Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center }
                },
                new CellFormat
                {
                    FontId = 0U,
                    BorderId = 1U,
                    ApplyBorder = true
                }));
    }

    private static bool IsFileLocked(IOException ex)
    {
        int errorCode = ex.HResult & 0xFFFF;
        // 32 = ERROR_SHARING_VIOLATION, 33 = ERROR_LOCK_VIOLATION
        return errorCode == 32 || errorCode == 33;
    }
}
