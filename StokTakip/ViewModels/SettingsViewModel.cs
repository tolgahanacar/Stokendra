using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Infrastructure;
using System;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace StokTakip.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserRepository _users;
    private readonly IStockCardRepository _stockCards;
    private readonly IMovementRepository _movements;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly INoteRepository _notes;

    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private string _companyName   = "";
    [ObservableProperty] private string _dbPath        = "";
    [ObservableProperty] private string _oldPassword   = "";
    [ObservableProperty] private string _newPassword   = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool   _isSuccess;
    [ObservableProperty] private string _appVersion    = "";
    [ObservableProperty] private string _autoBackupPath = "";

    public SettingsViewModel(
        IUserRepository users,
        IStockCardRepository stockCards,
        IMovementRepository movements,
        IServiceRecordRepository serviceRecords,
        INoteRepository notes,
        IDialogService dialogService,
        ILogger logger)
    {
        _users = users;
        _stockCards = stockCards;
        _movements = movements;
        _serviceRecords = serviceRecords;
        _notes = notes;
        _dialogService = dialogService;
        _logger = logger;
        Load();
    }

    private void Load()
    {
        var settings = AppServices.Current.Settings;
        CompanyName    = settings.CompanyName;
        DbPath         = settings.DbPath;
        AutoBackupPath = settings.AutoBackupPath;
        AppVersion     = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "4.0.0"}";
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var settings = AppServices.Current.Settings;
        settings.CompanyName = CompanyName.Trim();
        settings.AutoBackupPath = AutoBackupPath.Trim();
        settings.Kaydet();
        StatusMessage = "Ayarlar kaydedildi.";
        IsSuccess     = true;
    }

    [RelayCommand]
    public async Task SelectBackupFolder()
    {
        var path = await _dialogService.OpenFolderAsync("Yedekleme Klasörü Seç");
        if (!string.IsNullOrEmpty(path))
        {
            AutoBackupPath = path;
        }
    }

    [RelayCommand]
    public void ChangePassword()
    {
        StatusMessage = "";
        IsSuccess     = false;

        if (string.IsNullOrWhiteSpace(OldPassword) ||
            string.IsNullOrWhiteSpace(NewPassword) ||
            string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            StatusMessage = "Tüm şifre alanlarını doldurun.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "Yeni şifreler eşleşmiyor.";
            return;
        }

        var policyError = _users.ValidatePasswordPolicy(NewPassword,
            AppServices.Current.Session?.Username);
        if (policyError != null)
        {
            StatusMessage = policyError;
            return;
        }

        var username = AppServices.Current.Session?.Username ?? "admin";
        bool ok = _users.ChangePassword(username, OldPassword, NewPassword);
        if (ok)
        {
            StatusMessage   = "Şifre başarıyla değiştirildi.";
            IsSuccess       = true;
            OldPassword     = "";
            NewPassword     = "";
            ConfirmPassword = "";
        }
        else
        {
            StatusMessage = "Mevcut şifre hatalı.";
        }
    }

    [RelayCommand]
    public async Task DatabaseBackupAsync()
    {
        var settings = AppServices.Current.Settings;
        string dbPath = AppPaths.NormalizeDatabasePath(settings.DbPath);
        
        if (!System.IO.File.Exists(dbPath))
        {
            StatusMessage = "Veritabanı dosyası bulunamadı.";
            IsSuccess = false;
            return;
        }

        string fileName = $"Stokendra_DB_{DateTime.Now:yyyyMMdd_HHmm}.db";
        string? destPath = await _dialogService.SaveFileAsync("Veritabanı Yedeği Kaydet", fileName, "SQLite Veritabanı (*.db)|*.db");
        
        if (string.IsNullOrEmpty(destPath)) return;

        try
        {
            StatusMessage = "Veritabanı yedekleniyor...";
            await Task.Run(() => {
                System.IO.File.Copy(dbPath, destPath, true);
            });
            StatusMessage = "Veritabanı yedeği başarıyla oluşturuldu.";
            IsSuccess = true;
            await _dialogService.ShowMessageAsync("Başarılı", "Veritabanı dosyası kopyalandı.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Database backup error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task FullBackupAsync()
    {
        string zipFileName = $"Stokendra_FullExcel_Backup_{DateTime.Now:ddMMyyyy}.zip";
        string? zipPath = await _dialogService.SaveFileAsync("Tam Excel Yedeği Kaydet", zipFileName, "Zip Arşivi (*.zip)|*.zip");
        if (string.IsNullOrEmpty(zipPath)) return;

        try
        {
            StatusMessage = "Tam yedek hazırlanıyor...";
            await Task.Run(async () => {
                using var fs = new FileStream(zipPath, FileMode.Create);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

                // 1. Stok Hareketleri (Import Uyumlu)
                var movements = await _movements.GetAllAsync();
                var movEntry = archive.CreateEntry("Stok Hareketleri.xlsx");
                using (var entryStream = movEntry.Open())
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Stok Hareketleri");
                    string[] headers = { "Stok Kodu", "Stok Adı", "Teslim Edilen", "Tür ([G] Giriş / [Ç] Çıkış)", "Miktar", "Departman", "Tarih (dd.MM.yyyy)", "Açıklama" };
                    for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                    
                    for (int i = 0; i < movements.Count; i++)
                    {
                        var m = movements[i];
                        ws.Cell(i + 2, 1).Value = m.StokKartKodNo;
                        ws.Cell(i + 2, 2).Value = m.StokKartAd;
                        ws.Cell(i + 2, 3).Value = m.TeslimEdilen;
                        ws.Cell(i + 2, 4).Value = m.Tur == "Giris" ? "[G] Giriş" : "[Ç] Çıkış";
                        ws.Cell(i + 2, 5).Value = m.Miktar;
                        ws.Cell(i + 2, 6).Value = m.Departman;
                        ws.Cell(i + 2, 7).Value = m.Tarih.ToString("dd.MM.yyyy HH:mm");
                        ws.Cell(i + 2, 8).Value = m.Aciklama;
                    }
                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(entryStream);
                }

                // 2. Stok Kartları
                var cards = await _stockCards.GetAllAsync();
                var cardEntry = archive.CreateEntry("Stok Kartları.xlsx");
                using (var entryStream = cardEntry.Open())
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Stok Kartları");
                    string[] headers = { "Kod", "Ad", "Kategori", "Mevcut Stok", "Min Stok", "Birim", "Konum" };
                    for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                    for (int i = 0; i < cards.Count; i++)
                    {
                        ws.Cell(i + 2, 1).Value = cards[i].KodNo;
                        ws.Cell(i + 2, 2).Value = cards[i].Ad;
                        ws.Cell(i + 2, 3).Value = cards[i].Kategori;
                        ws.Cell(i + 2, 4).Value = cards[i].MevcutStok;
                        ws.Cell(i + 2, 5).Value = cards[i].MinStok;
                        ws.Cell(i + 2, 6).Value = cards[i].Birim;
                        ws.Cell(i + 2, 7).Value = cards[i].Konum;
                    }
                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(entryStream);
                }

                // 3. Servis Kayıtları
                var services = await _serviceRecords.GetAllAsync();
                var srvEntry = archive.CreateEntry("Servis Kayıtları.xlsx");
                using (var entryStream = srvEntry.Open())
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Servis Kayıtları");
                    string[] headers = { "Tarih", "Cihaz Adı", "Seri No", "Firma", "Sorun", "Sonuç" };
                    for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                    for (int i = 0; i < services.Count; i++)
                    {
                        ws.Cell(i + 2, 1).Value = services[i].BakimTarihi.ToString("dd.MM.yyyy HH:mm");
                        ws.Cell(i + 2, 2).Value = services[i].CihazAdi;
                        ws.Cell(i + 2, 3).Value = services[i].SeriNumarasi;
                        ws.Cell(i + 2, 4).Value = services[i].Firma;
                        ws.Cell(i + 2, 5).Value = services[i].Sorun;
                        ws.Cell(i + 2, 6).Value = services[i].Sonuc;
                    }
                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(entryStream);
                }

                // 4. Notlar
                var notes = _notes.GetAll();
                var noteEntry = archive.CreateEntry("Notlar.xlsx");
                using (var entryStream = noteEntry.Open())
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Notlar");
                    string[] headers = { "Tarih", "Başlık", "İçerik" };
                    for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                    for (int i = 0; i < notes.Count; i++)
                    {
                        ws.Cell(i + 2, 1).Value = notes[i].Tarih.ToString("dd.MM.yyyy HH:mm");
                        ws.Cell(i + 2, 2).Value = notes[i].Baslik;
                        ws.Cell(i + 2, 3).Value = notes[i].Icerik;
                    }
                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(entryStream);
                }
            });

            StatusMessage = "Tam Excel yedeği (ZIP) başarıyla alındı.";
            IsSuccess = true;
            await _dialogService.ShowMessageAsync("Başarılı", "Tüm veriler ayrı Excel dosyaları olarak ZIP içinde yedeklendi.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Full zip backup error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
    }
}
