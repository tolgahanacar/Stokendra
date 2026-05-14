using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Infrastructure;
using StokTakip.Services;
using System;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace StokTakip.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserRepository _users;
    private readonly IConfigRepository _config;
    private readonly IDialogService _dialogService;
    private readonly IBackupService _backupService;
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
        IConfigRepository config,
        IDialogService dialogService,
        IBackupService backupService,
        ILogger logger)
    {
        _users = users;
        _config = config;
        _dialogService = dialogService;
        _backupService = backupService;
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
        bool saved = settings.Kaydet();
        if (saved)
        {
            StatusMessage = "Ayarlar kaydedildi.";
            IsSuccess     = true;
        }
        else
        {
            StatusMessage = "Ayarlar kaydedilemedi. Disk alanını kontrol edin.";
            IsSuccess     = false;
        }
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
    public async Task ChangePasswordAsync()
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

        // PBKDF2 600k iterasyon — Task.Run ile UI thread'i bloklamadan çalıştır
        bool ok = await Task.Run(() => _users.ChangePassword(username, OldPassword, NewPassword));
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
        string fileName = $"Stokendra_DB_{DateTime.Now:yyyyMMdd_HHmm}.db";
        string? destPath = await _dialogService.SaveFileAsync("Veritabanı Yedeği Kaydet", fileName, "SQLite Veritabanı (*.db)|*.db");
        
        if (string.IsNullOrEmpty(destPath)) return;

        try
        {
            StatusMessage = "Veritabanı yedekleniyor...";
            await Task.Run(() => {
                // SQLite backup API — WAL modunda güvenli kopya oluşturur.
                // File.Copy WAL dosyasını dahil etmez ve bozuk yedek üretir.
                _config.CreateBackup(destPath);
            });
            StatusMessage = "Veritabanı yedeği başarıyla oluşturuldu.";
            IsSuccess = true;
            await _dialogService.ShowMessageAsync("Başarılı", "Veritabanı güvenli şekilde yedeklendi.");
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
        string? zipPath = await _dialogService.SaveFileAsync("Tam Excel Yedeği Kaydet", 
            $"Stokendra_FullExcel_Backup_{DateTime.Now:ddMMyyyy}.zip", "Zip Arşivi (*.zip)|*.zip");
        if (string.IsNullOrEmpty(zipPath)) return;

        try
        {
            StatusMessage = "Tam yedek hazırlanıyor...";
            
            // Note: BackupService generates its own timestamped name, 
            // but for user experience we move it to the requested location.
            string tempZip = await _backupService.ExportAllExcelAsync(Path.GetTempPath());
            
            if (File.Exists(zipPath)) File.Delete(zipPath);
            File.Move(tempZip, zipPath);

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

    [RelayCommand]
    public async Task TruncateAuditLog()
    {
        bool confirm = await _dialogService.ShowConfirmAsync("Audit Log Temizleme", "Tüm işlem geçmişi silinecektir. Bu işlem geri alınamaz. Devam etmek istiyor musunuz?");
        if (!confirm) return;

        try
        {
            await Task.Run(() => _config.TruncateAuditLog());
            StatusMessage = "İşlem geçmişi başarıyla temizlendi.";
            IsSuccess = true;
            await _dialogService.ShowMessageAsync("Başarılı", "Audit log tablosu boşaltıldı.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Audit log truncate error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
    }
}
