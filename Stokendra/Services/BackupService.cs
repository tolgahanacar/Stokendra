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
            _settings.Save();
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
        string[] headers = { "KodNo", "Stok Adı", "Kategori", "Birim", "MinStok", "Açıklama" };
        string filePath = Path.Combine(tempDir, "StokKartlari.xlsx");
        await Task.Run(() => ExcelService.ExportToExcel(
            filePath,
            "StokKartlari",
            headers,
            tumKartlar,
            k => new object?[] { k.Code, k.Name, k.Category, k.Unit, k.MinStock, k.Description }
        ));
    }

    private async Task ExportStokHareketleriAsync(string tempDir)
    {
        var hareketler = await _movementRepository.GetAllAsync();
        string[] headers = { "Stok Kodu", "Stok Adı", "Teslim Edilen", "Tür ([G] Giriş / [Ç] Çıkış)", "Miktar", "Departman", "Tarih (dd.MM.yyyy)", "Açıklama" };
        string filePath = Path.Combine(tempDir, "StokHareketleri.xlsx");
        await Task.Run(() => ExcelService.ExportToExcel(
            filePath,
            "StokHareketleri",
            headers,
            hareketler,
            h => new object?[] {
                h.StockCardCode,
                h.StockCardName,
                h.Recipient,
                (h.Type == "Entry" || h.Type == "Giris") ? "[G] Giriş" : "[Ç] Çıkış",
                h.Quantity,
                h.Department,
                h.Date.ToString("dd.MM.yyyy HH:mm"),
                h.Description
            }
        ));
    }

    private async Task ExportServisKayitlariAsync(string tempDir)
    {
        var servisler = await _serviceRecordRepository.GetAllAsync();
        string[] headers = { "Bakım Tarihi", "Cihaz Adı", "Seri Numarası", "Firma", "Sorun", "Sonuç" };
        string filePath = Path.Combine(tempDir, "ServisKayitlari.xlsx");
        await Task.Run(() => ExcelService.ExportToExcel(
            filePath,
            "ServisKayitlari",
            headers,
            servisler,
            s => new object?[] {
                s.ServiceDate.ToString("dd.MM.yyyy"),
                s.DeviceName,
                s.SerialNumber,
                s.Company,
                s.Issue,
                s.Result
            }
        ));
    }

    private async Task ExportNotlarAsync(string tempDir)
    {
        var notlar = await _noteRepository.GetAllAsync();
        if (notlar.Count == 0) return;
        string[] headers = { "ID", "Tarih", "Başlık", "İçerik" };
        string filePath = Path.Combine(tempDir, "Notlar.xlsx");
        await Task.Run(() => ExcelService.ExportToExcel(
            filePath,
            "Notlar",
            headers,
            notlar,
            n => new object?[] {
                n.Id,
                n.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                n.Title,
                n.Content
            }
        ));
    }

    private async Task ExportDepartmanlarAsync(string tempDir)
    {
        var deptlar = await _departmentRepository.GetAllAsync();
        if (deptlar.Count == 0) return;
        string[] headers = { "Departman Adı" };
        string filePath = Path.Combine(tempDir, "Departmanlar.xlsx");
        await Task.Run(() => ExcelService.ExportToExcel(
            filePath,
            "Departmanlar",
            headers,
            deptlar,
            d => new object?[] { d }
        ));
    }

    private async Task ExportBirimlerAsync(string tempDir)
    {
        var birimler = _configRepository.GetUnits();
        if (birimler.Count == 0) return;
        string[] headers = { "ID", "Birim Adı" };
        string filePath = Path.Combine(tempDir, "Birimler.xlsx");
        await Task.Run(() => ExcelService.ExportToExcel(
            filePath,
            "Birimler",
            headers,
            birimler,
            b => new object?[] { b.Id, b.Name }
        ));
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
        string[] headers = { "ID", "Tarih", "İşlem Tipi", "Tablo", "Kayıt ID", "Detay" };
        string filePath = Path.Combine(tempDir, "AuditLog.xlsx");
        await Task.Run(() => ExcelService.ExportToExcel(
            filePath,
            "AuditLog",
            headers,
            rows,
            r => new object?[] { r.Id, r.Date, r.Action, r.TableName, r.RecordId, r.Details }
        ));
    }
}
