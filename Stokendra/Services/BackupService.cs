using ClosedXML.Excel;
using System.IO.Compression;
using Stokendra.Infrastructure;
using Stokendra.Models;
using Stokendra.Data.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;

namespace Stokendra.Services;

public class BackupService : IBackupService
{
    private readonly AppSettings _settings;
    private readonly IConfigRepository _configRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IStockCardRepository _stockCardRepository;
    private readonly IMovementRepository _movementRepository;
    private readonly IServiceRecordRepository _serviceRecordRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDbConnectionFactory _connectionFactory;

    public BackupService(
        AppSettings settings,
        IConfigRepository configRepository,
        IReportRepository reportRepository,
        IStockCardRepository stockCardRepository,
        IMovementRepository movementRepository,
        IServiceRecordRepository serviceRecordRepository,
        INoteRepository noteRepository,
        IDepartmentRepository departmentRepository,
        IDbConnectionFactory connectionFactory)
    {
        _settings = settings;
        _configRepository = configRepository;
        _reportRepository = reportRepository;
        _stockCardRepository = stockCardRepository;
        _movementRepository = movementRepository;
        _serviceRecordRepository = serviceRecordRepository;
        _noteRepository = noteRepository;
        _departmentRepository = departmentRepository;
        _connectionFactory = connectionFactory;
    }

    public async Task CheckAutoBackupAsync()
    {
        try
        {
            var path = _settings.AutoBackupPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

            var lastBackup = _settings.LastBackupDate;
            if (lastBackup.HasValue && (DateTime.Now.Date - lastBackup.Value.Date).TotalDays < 3) return;

            // Arka planda tam yedeklemeyi başlat (UI'ı kilitlememek için Task.Run kullanıyoruz)
            await Task.Run(async () => 
            {
                try
                {
                    await PerformBackupAsync(path);
                    CleanOldBackups(path, 7);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Auto backup execution failed.", ex);
                    throw; // Re-throw to prevent updating LastBackupDate
                }
            });

            _settings.LastBackupDate = DateTime.Now;
            _settings.Kaydet();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Auto backup background check failed.", ex);
        }
    }

    private void CleanOldBackups(string destFolder, int keepDays)
    {
        try
        {
            var files = Directory.GetFiles(destFolder, "Stokendra_FullBackup_*.zip");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if ((DateTime.Now - fileInfo.CreationTime).TotalDays > keepDays)
                {
                    fileInfo.Delete();
                }
            }
        }
        catch { }
    }

    public async Task<string> PerformBackupAsync(string destFolder)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string tempDir = Path.Combine(Path.GetTempPath(), $"Stokendra_Backup_{timestamp}");
        Directory.CreateDirectory(tempDir);

        try
        {
            CopyDatabase(tempDir);
            await ExportStokKartlariAsync(tempDir);
            await ExportStokHareketleriAsync(tempDir);
            await ExportServisKayitlariAsync(tempDir);
            await ExportNotlarAsync(tempDir);
            await ExportDepartmanlarAsync(tempDir);
            await ExportBirimlerAsync(tempDir);
            await ExportAuditLogAsync(tempDir);

            string zipFile = Path.Combine(destFolder, $"Stokendra_FullBackup_{timestamp}.zip");
            if (File.Exists(zipFile)) File.Delete(zipFile);
            
            await Task.Run(() => ZipFile.CreateFromDirectory(tempDir, zipFile, CompressionLevel.Fastest, false));
            return zipFile;
        }
        finally
        {
            // Veritabanı ve geçici kopyalar üzerindeki kilitleri kaldırmak için SQLite havuzlarını temizle
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    public async Task<string> ExportAllExcelAsync(string destFolder)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string tempDir = Path.Combine(Path.GetTempPath(), $"Stokendra_Excel_{timestamp}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await ExportStokKartlariAsync(tempDir);
            await ExportStokHareketleriAsync(tempDir);
            await ExportServisKayitlariAsync(tempDir);
            await ExportNotlarAsync(tempDir);
            await ExportDepartmanlarAsync(tempDir);
            await ExportBirimlerAsync(tempDir);
            await ExportAuditLogAsync(tempDir);

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

    private void CopyDatabase(string tempDir)
    {
        string dbPath = string.IsNullOrWhiteSpace(_settings.DbPath)
            ? AppPaths.DefaultDatabasePath
            : _settings.DbPath;

        if (!File.Exists(dbPath)) return;

        string destFile = Path.Combine(tempDir, "stok.db");
        _configRepository.CreateBackup(destFile);

        string origName = Path.GetFileName(dbPath);
        if (!origName.Equals("stok.db", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(destFile, Path.Combine(tempDir, origName), true);
        }
    }



    private async Task ExportStokKartlariAsync(string tempDir)
    {
        var tumKartlar = await _stockCardRepository.GetAllAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("StokKartlari");

        // İçe aktarma (Import) yapısına birebir uyumlu başlıklar
        string[] headers = { "KodNo", "Stok Adı", "Kategori", "Birim", "Konum", "Tedarikçi", "Barkod", "BirimFiyat", "MinStok", "Açıklama" };
        SetHeaders(ws, headers);

        for (int i = 0; i < tumKartlar.Count; i++)
        {
            var k = tumKartlar[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = k.Code;
            ws.Cell(r, 2).Value = k.Name;
            ws.Cell(r, 3).Value = k.Category;
            ws.Cell(r, 4).Value = k.Unit;
            ws.Cell(r, 5).Value = k.Location;
            ws.Cell(r, 6).Value = k.Supplier;
            ws.Cell(r, 7).Value = k.Barcode;
            ws.Cell(r, 8).Value = k.UnitPrice;
            ws.Cell(r, 9).Value = k.MinStock;
            ws.Cell(r, 10).Value = k.Description;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, tumKartlar.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "StokKartlari.xlsx"));
    }

    private async Task ExportStokHareketleriAsync(string tempDir)
    {
        var hareketler = await _movementRepository.GetAllAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("StokHareketleri");

        // İçe aktarma (Import) yapısına birebir uyumlu başlıklar
        string[] headers = { "Stok Kodu", "Stok Adı", "Teslim Edilen", "Tür ([G] Giriş / [Ç] Çıkış)", "Miktar", "Departman", "Tarih (dd.MM.yyyy)", "Açıklama" };
        SetHeaders(ws, headers);

        for (int i = 0; i < hareketler.Count; i++)
        {
            var h = hareketler[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = h.StockCardCode;
            ws.Cell(r, 2).Value = h.StockCardName;
            ws.Cell(r, 3).Value = h.Recipient;
            ws.Cell(r, 4).Value = (h.Type == "Entry" || h.Type == "Giris") ? "[G] Giriş" : "[Ç] Çıkış";
            ws.Cell(r, 5).Value = h.Quantity;
            ws.Cell(r, 6).Value = h.Department;
            ws.Cell(r, 7).Value = h.Date.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(r, 8).Value = h.Description;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, hareketler.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "StokHareketleri.xlsx"));
    }

    private async Task ExportServisKayitlariAsync(string tempDir)
    {
        var servisler = await _serviceRecordRepository.GetAllAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("ServisKayitlari");

        // İçe aktarma (Import) yapısına birebir uyumlu başlıklar
        string[] headers = { "Bakım Tarihi", "Cihaz Adı", "Seri Numarası", "Firma", "Sorun", "Sonuç" };
        SetHeaders(ws, headers);

        for (int i = 0; i < servisler.Count; i++)
        {
            var s = servisler[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = s.ServiceDate.ToString("dd.MM.yyyy");
            ws.Cell(r, 2).Value = s.DeviceName;
            ws.Cell(r, 3).Value = s.SerialNumber;
            ws.Cell(r, 4).Value = s.Company;
            ws.Cell(r, 5).Value = s.Issue;
            ws.Cell(r, 6).Value = s.Result;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, servisler.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "ServisKayitlari.xlsx"));
    }

    private async Task ExportNotlarAsync(string tempDir)
    {
        var notlar = await _noteRepository.GetAllAsync();
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
            ws.Cell(r, 2).Value = n.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(r, 3).Value = n.Title;
            ws.Cell(r, 4).Value = n.Content;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, notlar.Count, headers.Length);
        wb.SaveAs(Path.Combine(tempDir, "Notlar.xlsx"));
    }

    private async Task ExportDepartmanlarAsync(string tempDir)
    {
        var deptlar = await _departmentRepository.GetAllAsync();
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

    private void SetHeaders(IXLWorksheet ws, string[] headers)
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

    private void ApplyTableStyle(IXLWorksheet ws, int rowCount, int colCount)
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
                ws.Range(r, 1, r, colCount).Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
        }
    }

    private async Task ExportBirimlerAsync(string tempDir)
    {
        var birimler = _configRepository.GetUnits();
        if (birimler.Count == 0) return;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Birimler");

        string[] headers = { "ID", "Birim Adı" };
        SetHeaders(ws, headers);

        for (int i = 0; i < birimler.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = birimler[i].Id;
            ws.Cell(i + 2, 2).Value = birimler[i].Name;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, birimler.Count, headers.Length);
        await Task.Run(() => wb.SaveAs(Path.Combine(tempDir, "Birimler.xlsx")));
    }

    private async Task ExportAuditLogAsync(string tempDir)
    {
        using var conn = _connectionFactory.CreateConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Date, Action, TableName, RecordId, Details FROM AuditLog ORDER BY Id DESC LIMIT 10000";
        
        var rows = new List<(int Id, string Date, string Action, string TableName, int RecordId, string Details)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((
                reader.GetInt32(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                reader.IsDBNull(5) ? "" : reader.GetString(5)
            ));
        }

        if (rows.Count == 0) return;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("AuditLog");

        string[] headers = { "ID", "Tarih", "İşlem Tipi", "Tablo", "Kayıt ID", "Detay" };
        SetHeaders(ws, headers);

        for (int i = 0; i < rows.Count; i++)
        {
            int r = i + 2;
            ws.Cell(r, 1).Value = rows[i].Id;
            ws.Cell(r, 2).Value = rows[i].Date;
            ws.Cell(r, 3).Value = rows[i].Action;
            ws.Cell(r, 4).Value = rows[i].TableName;
            ws.Cell(r, 5).Value = rows[i].RecordId;
            ws.Cell(r, 6).Value = rows[i].Details;
        }

        ws.Columns().AdjustToContents();
        ApplyTableStyle(ws, rows.Count, headers.Length);
        await Task.Run(() => wb.SaveAs(Path.Combine(tempDir, "AuditLog.xlsx")));
    }
}
