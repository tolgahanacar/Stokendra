using ClosedXML.Excel;
using System.IO.Compression;
using StokTakip.Infrastructure;
using StokTakip.Models;
using StokTakip.Data.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;

namespace StokTakip;

public static class BackupManager
{
    public static async Task CheckWeeklyBackupAsync()
    {
        try
        {
            var path = AppServices.Current.Settings.AutoBackupPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

            var lastBackup = AppServices.Current.Settings.LastBackupDate;
            if (lastBackup.HasValue && (DateTime.Now - lastBackup.Value).TotalDays < 7) return;

            await PerformBackupAsync(path);

            AppServices.Current.Settings.LastBackupDate = DateTime.Now;
            AppServices.Current.Settings.Kaydet();
        }
        catch { } // Fail silently
    }

    /// <summary>
    /// Tam yedekleme: SQLite kopyası + tüm verilerin Excel dışa aktarımı (ZIP)
    /// </summary>
    public static async Task<string> PerformBackupAsync(string destFolder)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string tempDir = Path.Combine(Path.GetTempPath(), $"Stokendra_Backup_{timestamp}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. SQLite DB kopyası
            CopyDatabase(tempDir);

            // 2. Stok Kartları (tüm alanlar)
            await ExportStokKartlariAsync(tempDir);

            // 3. Stok Hareketleri
            await ExportStokHareketleriAsync(tempDir);

            // 4. Servis Kayıtları
            await ExportServisKayitlariAsync(tempDir);

            // 5. Notlar
            ExportNotlar(tempDir);

            // 6. Departmanlar
            await ExportDepartmanlarAsync(tempDir);

            // 7. SQL dışa aktarım
            ExportSql(tempDir);

            // 8. ZIP oluştur
            string zipFile = Path.Combine(destFolder, $"Stokendra_FullBackup_{timestamp}.zip");
            if (File.Exists(zipFile)) File.Delete(zipFile);
            
            await Task.Run(() => ZipFile.CreateFromDirectory(tempDir, zipFile, CompressionLevel.Fastest, false));
            return zipFile;
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Sadece Excel dosyalarını dışa aktarır (ZIP olarak)
    /// </summary>
    public static async Task<string> ExportAllExcelAsync(string destFolder)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string tempDir = Path.Combine(Path.GetTempPath(), $"Stokendra_Excel_{timestamp}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await ExportStokKartlariAsync(tempDir);
            await ExportStokHareketleriAsync(tempDir);
            await ExportServisKayitlariAsync(tempDir);
            ExportNotlar(tempDir);
            await ExportDepartmanlarAsync(tempDir);

            string zipFile = Path.Combine(destFolder, $"Stokendra_ExcelExport_{timestamp}.zip");
            if (File.Exists(zipFile)) File.Delete(zipFile);
            
            await Task.Run(() => ZipFile.CreateFromDirectory(tempDir, zipFile, CompressionLevel.Fastest, false));
            return zipFile;
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // ═══════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════

    private static void CopyDatabase(string tempDir)
    {
        string dbPath = string.IsNullOrWhiteSpace(AppServices.Current.Settings.DbPath)
            ? AppPaths.DefaultDatabasePath
            : AppServices.Current.Settings.DbPath;

        if (!File.Exists(dbPath)) return;

        string destFile = Path.Combine(tempDir, "stok.db");
        ServiceContainer.GetService<IConfigRepository>().CreateBackup(destFile);

        string origName = Path.GetFileName(dbPath);
        if (!origName.Equals("stok.db", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(destFile, Path.Combine(tempDir, origName), true);
        }
    }

    private static void ExportSql(string tempDir)
    {
        try
        {
            string sqlFile = Path.Combine(tempDir, "stokendra_backup.sql");
            ServiceContainer.GetService<IReportRepository>().ExportSqlBackup(sqlFile);
        }
        catch { /* SQL export opsiyonel */ }
    }

    private static async Task ExportStokKartlariAsync(string tempDir)
    {
        var tumKartlar = await ServiceContainer.GetService<IStockCardRepository>().GetAllAsync();

        if (tumKartlar.Count == 0) return;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("StokKartlari");

        string[] headers = { "ID", "Kod No", "Ad", "Kart Tipi", "Üst Kart", "Kategori", "Birim", "Mevcut Stok", "Min Stok", "Konum", "Tedarikçi", "Barkod", "Birim Fiyat", "Açıklama" };
        SetHeaders(ws, headers);

        for (int i = 0; i < tumKartlar.Count; i++)
        {
            var k = tumKartlar[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = k.Id;
            ws.Cell(r, 2).Value = k.KodNo;
            ws.Cell(r, 3).Value = k.Ad;
            ws.Cell(r, 4).Value = k.KartTipi;
            ws.Cell(r, 5).Value = k.UstKartAd;
            ws.Cell(r, 6).Value = k.Kategori;
            ws.Cell(r, 7).Value = k.Birim;
            ws.Cell(r, 8).Value = k.MevcutStok;
            ws.Cell(r, 9).Value = k.MinStok;
            ws.Cell(r, 10).Value = k.Konum;
            ws.Cell(r, 11).Value = k.Tedarikci;
            ws.Cell(r, 12).Value = k.Barkod;
            ws.Cell(r, 13).Value = k.BirimFiyat;
            ws.Cell(r, 14).Value = k.Aciklama;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, tumKartlar.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "StokKartlari.xlsx"));
    }

    private static async Task ExportStokHareketleriAsync(string tempDir)
    {
        var hareketler = await ServiceContainer.GetService<IMovementRepository>().GetAllAsync(null, null, null, null, null);
        if (hareketler.Count == 0) return;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("StokHareketleri");

        string[] headers = { "ID", "Tarih", "Stok Kodu", "Stok Adı", "Tür", "Miktar", "Teslim Edilen/Alınan", "Departman", "Açıklama" };
        SetHeaders(ws, headers);

        for (int i = 0; i < hareketler.Count; i++)
        {
            var h = hareketler[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = h.Id;
            ws.Cell(r, 2).Value = h.Tarih.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(r, 3).Value = h.StokKartKodNo;
            ws.Cell(r, 4).Value = h.StokKartAd;
            ws.Cell(r, 5).Value = h.Tur == "Giris" ? "Giriş" : (h.Tur == "Cikis" ? "Çıkış" : "Boş");
            ws.Cell(r, 6).Value = h.Miktar;
            ws.Cell(r, 7).Value = h.TeslimEdilen;
            ws.Cell(r, 8).Value = h.Departman;
            ws.Cell(r, 9).Value = h.Aciklama;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, hareketler.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "StokHareketleri.xlsx"));
    }

    private static async Task ExportServisKayitlariAsync(string tempDir)
    {
        var servisler = await ServiceContainer.GetService<IServiceRecordRepository>().GetAllAsync(null, null, null);
        if (servisler.Count == 0) return;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("ServisKayitlari");

        string[] headers = { "ID", "Cihaz Adı", "Seri Numarası", "Firma", "Bakım Tarihi", "Sorun", "Sonuç" };
        SetHeaders(ws, headers);

        for (int i = 0; i < servisler.Count; i++)
        {
            var s = servisler[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = s.Id;
            ws.Cell(r, 2).Value = s.CihazAdi;
            ws.Cell(r, 3).Value = s.SeriNumarasi;
            ws.Cell(r, 4).Value = s.Firma;
            ws.Cell(r, 5).Value = s.BakimTarihi.ToString("dd.MM.yyyy");
            ws.Cell(r, 6).Value = s.Sorun;
            ws.Cell(r, 7).Value = s.Sonuc;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, servisler.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "ServisKayitlari.xlsx"));
    }

    private static void ExportNotlar(string tempDir)
    {
        var notlar = ServiceContainer.GetService<INoteRepository>().GetAll();
        if (notlar.Count == 0) return;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Notlar");

        string[] headers = { "ID", "Tarih", "Başlık", "İçerik" };
        SetHeaders(ws, headers);

        for (int i = 0; i < notlar.Count; i++)
        {
            var n = notlar[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = n.Id;
            ws.Cell(r, 2).Value = n.Tarih.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(r, 3).Value = n.Baslik;
            ws.Cell(r, 4).Value = n.Icerik;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, notlar.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "Notlar.xlsx"));
    }

    private static async Task ExportDepartmanlarAsync(string tempDir)
    {
        var deptlar = await ServiceContainer.GetService<IDepartmentRepository>().GetAllAsync();
        if (deptlar.Count == 0) return;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Departmanlar");

        string[] headers = { "Departman Adı" };
        SetHeaders(ws, headers);

        for (int i = 0; i < deptlar.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = deptlar[i];
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, deptlar.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "Departmanlar.xlsx"));
    }

    private static void SetHeaders(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 37, 52);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void ApplyTableStyle(IXLWorksheet ws, int rowCount, int colCount)
    {
        if (rowCount == 0) return;

        var range = ws.Range(1, 1, rowCount + 1, colCount);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromArgb(180, 180, 180);
        range.Style.Border.InsideBorderColor = XLColor.FromArgb(220, 220, 220);

        for (int r = 2; r <= rowCount + 1; r++)
        {
            if (r % 2 == 0)
            {
                ws.Range(r, 1, r, colCount).Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
            }
        }
    }
}
