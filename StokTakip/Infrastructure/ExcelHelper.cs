using ClosedXML.Excel;

namespace StokTakip.Infrastructure;

/// <summary>
/// ClosedXML Excel işlemleri için merkezi yardımcı.
/// Tekrarlanan header yazma, stil uygulama ve zebra satır renklendirme
/// tek yerde tanımlıdır.
/// </summary>
public static class ExcelHelper
{
    // ── Renk sabitleri ────────────────────────────────────────────────────

    private static readonly XLColor HeaderBg    = XLColor.FromArgb(30, 60, 110);
    private static readonly XLColor HeaderFg    = XLColor.White;
    private static readonly XLColor ZebraRowBg  = XLColor.FromArgb(245, 247, 250);
    private static readonly XLColor BorderColor = XLColor.FromArgb(200, 200, 200);

    // ── Header ────────────────────────────────────────────────────────────

    /// <summary>
    /// Çalışma sayfasına stillendirilmiş başlık satırı yazar.
    /// </summary>
    /// <param name="ws">Hedef çalışma sayfası.</param>
    /// <param name="headers">Başlık metinleri.</param>
    public static void WriteHeaders(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBg;
            cell.Style.Font.FontColor = HeaderFg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    // ── Tablo stili ───────────────────────────────────────────────────────

    /// <summary>
    /// Veri aralığına zebra satır renklendirmesi ve kenarlık uygular.
    /// </summary>
    /// <param name="ws">Hedef çalışma sayfası.</param>
    /// <param name="rowCount">Veri satırı sayısı (başlık hariç).</param>
    /// <param name="colCount">Sütun sayısı.</param>
    public static void ApplyTableStyle(IXLWorksheet ws, int rowCount, int colCount)
    {
        if (rowCount <= 0) return;

        var range = ws.Range(1, 1, rowCount + 1, colCount);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = BorderColor;
        range.Style.Border.InsideBorderColor = XLColor.FromArgb(220, 220, 220);

        for (int r = 2; r <= rowCount + 1; r++)
        {
            if (r % 2 == 0)
                ws.Range(r, 1, r, colCount).Style.Fill.BackgroundColor = ZebraRowBg;
        }
    }

    // ── Kaydet ────────────────────────────────────────────────────────────

    /// <summary>
    /// Çalışma kitabını belirtilen yola kaydeder.
    /// Başarı/hata mesajını gösterir.
    /// </summary>
    /// <param name="wb">Kaydedilecek çalışma kitabı.</param>
    /// <param name="filePath">Hedef dosya yolu.</param>
    /// <returns>Kayıt başarılı ise true.</returns>
    public static bool SaveWithFeedback(XLWorkbook wb, string filePath)
    {
        try
        {
            wb.SaveAs(filePath);
            MessageBox.Show(
                LocalizationManager.L("export_success", filePath),
                LocalizationManager.L("info"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Excel save error: " + ex);
            MessageBox.Show(
                LocalizationManager.L("export_error") + "\n" + ex.Message,
                LocalizationManager.L("error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }
    }

    // ── Tam export yardımcısı ─────────────────────────────────────────────

    /// <summary>
    /// Tek çağrıyla: header yaz, veri doldur, stil uygula, sütunları ayarla, kaydet.
    /// </summary>
    /// <param name="sheetName">Çalışma sayfası adı.</param>
    /// <param name="headers">Başlık metinleri.</param>
    /// <param name="rows">Veri satırları (her satır object[] dizisi).</param>
    /// <param name="filePath">Hedef dosya yolu.</param>
    /// <returns>Kayıt başarılı ise true.</returns>
    public static bool ExportTable(
        string sheetName,
        string[] headers,
        IEnumerable<object?[]> rows,
        string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);

        WriteHeaders(ws, headers);

        int rowIdx = 2;
        foreach (var row in rows)
        {
            for (int c = 0; c < row.Length; c++)
                ws.Cell(rowIdx, c + 1).Value = row[c] is null
                    ? XLCellValue.FromObject("")
                    : XLCellValue.FromObject(row[c]!);
            rowIdx++;
        }

        int dataRows = rowIdx - 2;
        ApplyTableStyle(ws, dataRows, headers.Length);
        ws.Columns().AdjustToContents();

        return SaveWithFeedback(wb, filePath);
    }
}
