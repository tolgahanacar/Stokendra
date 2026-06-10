using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using Stokendra.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace Stokendra.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserRepository _users;
    private readonly IConfigRepository _config;
    private readonly IDialogService _dialogService;
    private readonly IBackupService _backupService;
    private readonly ILogger _logger;
    private readonly IDbConnectionFactory _connectionFactory;

    [ObservableProperty] private string _companyName   = "";
    [ObservableProperty] private string _dbPath        = "";
    [ObservableProperty] private string _oldPassword   = "";
    [ObservableProperty] private string _newPassword   = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool   _isSuccess;
    [ObservableProperty] private string _appVersion    = "";
    [ObservableProperty] private string _autoBackupPath = "";
    
    // New detailed & smart properties
    [ObservableProperty] private string _selectedLanguage = "Türkçe";
    [ObservableProperty] private int _lowStockThreshold = 3;
    [ObservableProperty] private string _masterSecurityCode = "";
    [ObservableProperty] private string _databaseSize = "Bilinmiyor";

    public List<string> Languages { get; } = new() { "Türkçe", "English" };
    public ObservableCollection<string> ActivityLogs { get; } = new();

    public SettingsViewModel(
        IUserRepository users,
        IConfigRepository config,
        IDialogService dialogService,
        IBackupService backupService,
        ILogger logger,
        IDbConnectionFactory connectionFactory)
    {
        _users = users;
        _config = config;
        _dialogService = dialogService;
        _backupService = backupService;
        _logger = logger;
        _connectionFactory = connectionFactory;
        Load();
    }

    private void Load()
    {
        var settings = AppServices.Current.Settings;
        CompanyName       = settings.CompanyName;
        DbPath            = settings.DbPath;
        AutoBackupPath    = settings.AutoBackupPath;
        LowStockThreshold = settings.LowStockThreshold;
        MasterSecurityCode = settings.MasterSecurityCode;
        SelectedLanguage  = settings.Language == "en" ? "English" : "Türkçe";
        
        AppVersion = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";
        
        UpdateDatabaseInfo();
        LoadActivityLogs();
    }

    private void UpdateDatabaseInfo()
    {
        try
        {
            if (File.Exists(DbPath))
            {
                var fileInfo = new FileInfo(DbPath);
                double sizeInKb = fileInfo.Length / 1024.0;
                DatabaseSize = sizeInKb > 1024 
                    ? $"{(sizeInKb / 1024.0):N2} MB" 
                    : $"{sizeInKb:N0} KB";
            }
            else
            {
                DatabaseSize = "Bilinmiyor";
            }
        }
        catch
        {
            DatabaseSize = "Bilinmiyor";
        }
    }

    private void LoadActivityLogs()
    {
        ActivityLogs.Clear();
        try
        {
            if (File.Exists(AppPaths.ActivityLogPath))
            {
                var lines = File.ReadLines(AppPaths.ActivityLogPath).Reverse().Take(12).ToList();
                foreach (var line in lines)
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 4)
                    {
                        ActivityLogs.Add($"{parts[0]} | {parts[1]} | {parts[2]} | {parts[3]}");
                    }
                    else
                    {
                        ActivityLogs.Add(line);
                    }
                }
            }
            if (ActivityLogs.Count == 0)
            {
                ActivityLogs.Add("Henüz aktivite kaydı bulunmuyor.");
            }
        }
        catch (Exception ex)
        {
            ActivityLogs.Add($"Loglar yüklenemedi: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var settings = AppServices.Current.Settings;
        settings.CompanyName = CompanyName.Trim();
        settings.AutoBackupPath = AutoBackupPath.Trim();
        settings.LowStockThreshold = LowStockThreshold;
        settings.MasterSecurityCode = MasterSecurityCode.Trim();
        
        string newLang = SelectedLanguage == "English" ? "en" : "tr";
        bool langChanged = newLang != settings.Language;
        settings.Language = newLang;

        bool dbPathChanged = false;
        if (!string.IsNullOrWhiteSpace(DbPath) && DbPath.Trim() != settings.DbPath)
        {
            settings.DbPath = DbPath.Trim();
            dbPathChanged = true;
        }

        bool saved = settings.Kaydet();
        if (saved)
        {
            try
            {
                _config.SetConfig("CompanyName", settings.CompanyName);
                _config.SetConfig("AutoBackupPath", settings.AutoBackupPath);
                _config.SetConfig("DbPath", settings.DbPath);
                _config.SetConfig("LowStockThreshold", settings.LowStockThreshold.ToString());
                _config.SetConfig("MasterSecurityCode", settings.MasterSecurityCode);
                _config.SetConfig("LastSettingsUpdated", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
                _config.WriteAuditLog("Update", "AppConfig", 0, $"Settings updated. Company: {settings.CompanyName}, BackupPath: {settings.AutoBackupPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update database AppConfig or AuditLog", ex);
            }

            LoadActivityLogs();

            if (langChanged || dbPathChanged)
            {
                StatusMessage = "Ayarlar kaydedildi. Dil veya veritabanı değişikliklerinin geçerli olması için lütfen uygulamayı yeniden başlatın.";
            }
            else
            {
                StatusMessage = "Ayarlar kaydedildi.";
            }
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
    public async Task SelectDbFile()
    {
        var path = await _dialogService.OpenFileAsync("Veritabanı Dosyası Seç", "SQLite Veritabanı (*.db)|*.db");
        if (!string.IsNullOrEmpty(path))
        {
            DbPath = path;
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

        bool ok = await Task.Run(() => _users.ChangePassword(username, OldPassword, NewPassword));
        if (ok)
        {
            StatusMessage   = "Şifre başarıyla değiştirildi.";
            IsSuccess       = true;
            OldPassword     = "";
            NewPassword     = "";
            ConfirmPassword = "";
            LoadActivityLogs();
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
                _config.CreateBackup(destPath);
            });
            StatusMessage = "Veritabanı yedeği başarıyla oluşturuldu.";
            IsSuccess = true;
            LoadActivityLogs();
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
            
            string tempZip = await _backupService.ExportAllExcelAsync(Path.GetTempPath());
            
            if (File.Exists(zipPath)) File.Delete(zipPath);
            File.Move(tempZip, zipPath);

            StatusMessage = "Tam Excel yedeği (ZIP) başarıyla alındı.";
            IsSuccess = true;
            LoadActivityLogs();
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
            LoadActivityLogs();
            await _dialogService.ShowMessageAsync("Başarılı", "Audit log tablosu boşaltıldı.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Audit log truncate error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task CheckDatabaseIntegrityAsync()
    {
        StatusMessage = "Veritabanı bütünlük kontrolü yapılıyor...";
        IsSuccess = false;

        try
        {
            string result = await Task.Run(() =>
            {
                using var conn = _connectionFactory.CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var val = cmd.ExecuteScalar()?.ToString();
                return val ?? "Bilinmeyen hata";
            });

            if (result.Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Bütünlük kontrolü: Başarılı (Herhangi bir bozulma yok).";
                IsSuccess = true;
                LoadActivityLogs();
                await _dialogService.ShowMessageAsync("Başarılı", "Veritabanı sağlık kontrolü başarılı. Herhangi bir bozulma veya indeks hatası bulunamadı.");
            }
            else
            {
                StatusMessage = $"Hata: Bütünlük kontrolü başarısız ({result}).";
                IsSuccess = false;
                await _dialogService.ShowMessageAsync("Hata", $"Bütünlük kontrolü başarısız:\n{result}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Database integrity check error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task OptimizeDatabaseAsync()
    {
        StatusMessage = "Veritabanı optimize ediliyor (VACUUM)...";
        IsSuccess = false;

        try
        {
            await Task.Run(() =>
            {
                using var conn = _connectionFactory.CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "VACUUM;";
                cmd.ExecuteNonQuery();
            });

            UpdateDatabaseInfo();
            StatusMessage = "Veritabanı başarıyla optimize edildi (VACUUM yapıldı).";
            IsSuccess = true;
            LoadActivityLogs();
            await _dialogService.ShowMessageAsync("Başarılı", "Veritabanı optimizasyonu tamamlandı. SQLite dosya boyutu küçültüldü ve kullanılmayan alanlar serbest bırakıldı.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Database vacuum error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task ClearActivityLogsAsync()
    {
        bool confirm = await _dialogService.ShowConfirmAsync("Aktivite Günlüğü", "Tüm aktivite geçmişi silinecektir. Devam etmek istiyor musunuz?");
        if (!confirm) return;

        try
        {
            if (File.Exists(AppPaths.ActivityLogPath))
            {
                File.WriteAllText(AppPaths.ActivityLogPath, "");
            }
            LoadActivityLogs();
            StatusMessage = "Aktivite geçmişi temizlendi.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Clear activity logs error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
    }
}
