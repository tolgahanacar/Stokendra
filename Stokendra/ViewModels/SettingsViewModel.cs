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
    private readonly IUpdateService _updateService;

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
    [ObservableProperty] private string _databaseSize = "";

    public List<string> Languages { get; } = new() { "Türkçe", "English" };
    public ObservableCollection<string> ActivityLogs { get; } = new();

    public SettingsViewModel(
        IUserRepository users,
        IConfigRepository config,
        IDialogService dialogService,
        IBackupService backupService,
        ILogger logger,
        IDbConnectionFactory connectionFactory,
        IUpdateService updateService)
    {
        _users = users;
        _config = config;
        _dialogService = dialogService;
        _backupService = backupService;
        _logger = logger;
        _connectionFactory = connectionFactory;
        _updateService = updateService;
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
                DatabaseSize = LocalizationManager.L("db_size_unknown");
            }
        }
        catch
        {
            DatabaseSize = LocalizationManager.L("db_size_unknown");
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
                ActivityLogs.Add(LocalizationManager.L("no_activity_log"));
            }
        }
        catch (Exception ex)
        {
            ActivityLogs.Add(string.Format(LocalizationManager.L("activity_log_load_failed"), ex.Message));
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
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

        bool saved = await settings.SaveAsync();
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
                StatusMessage = LocalizationManager.L("saved_restart");
                IsSuccess     = true;

                string infoTitle = LocalizationManager.L("info");
                string infoMsg = langChanged 
                    ? LocalizationManager.L("restart_required") 
                    : LocalizationManager.L("db_path_changed_restart");

                await _dialogService.ShowMessageAsync(infoTitle, infoMsg);

                try
                {
                    string? processPath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(processPath))
                    {
                        System.Diagnostics.Process.Start(processPath);
                    }
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to restart application automatically", ex);
                }
            }
            else
            {
                StatusMessage = LocalizationManager.L("settings_saved");
                IsSuccess     = true;
            }
        }
        else
        {
            StatusMessage = LocalizationManager.L("save_settings_failed");
            IsSuccess     = false;
        }
    }

    [RelayCommand]
    public async Task SelectBackupFolder()
    {
        var path = await _dialogService.OpenFolderAsync(LocalizationManager.L("select_backup_folder_title"));
        if (!string.IsNullOrEmpty(path))
        {
            AutoBackupPath = path;
        }
    }

    [RelayCommand]
    public async Task SelectDbFile()
    {
        var path = await _dialogService.OpenFileAsync(LocalizationManager.L("select_db_file_title"), LocalizationManager.L("db_file_filter"));
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
            StatusMessage = LocalizationManager.L("fill_all_password_fields");
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = LocalizationManager.L("passwords_dont_match");
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

        bool ok = await _users.ChangePasswordAsync(username, OldPassword, NewPassword);
        if (ok)
        {
            StatusMessage   = LocalizationManager.L("password_changed_success");
            IsSuccess       = true;
            OldPassword     = "";
            NewPassword     = "";
            ConfirmPassword = "";
            LoadActivityLogs();
        }
        else
        {
            StatusMessage = LocalizationManager.L("current_password_incorrect");
        }
    }

    [RelayCommand]
    public async Task DatabaseBackupAsync()
    {
        string fileName = $"Stokendra_DB_{DateTime.Now:yyyyMMdd_HHmm}.db";
        string? destPath = await _dialogService.SaveFileAsync(LocalizationManager.L("save_db_backup_title"), fileName, LocalizationManager.L("db_file_filter"));
        
        if (string.IsNullOrEmpty(destPath)) return;

        try
        {
            StatusMessage = LocalizationManager.L("db_backing_up");
            await Task.Run(() => {
                _config.CreateBackup(destPath);
            });
            StatusMessage = LocalizationManager.L("db_backup_success");
            IsSuccess = true;
            LoadActivityLogs();
            await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("db_backup_dialog_msg"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Database backup error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task FullBackupAsync()
    {
        string? zipPath = await _dialogService.SaveFileAsync(LocalizationManager.L("save_full_excel_backup_title"), 
            $"Stokendra_FullExcel_Backup_{DateTime.Now:ddMMyyyy}.zip", LocalizationManager.L("zip_file_filter"));
        if (string.IsNullOrEmpty(zipPath)) return;

        try
        {
            StatusMessage = LocalizationManager.L("full_backup_preparing");
            
            string tempZip = await _backupService.ExportAllExcelAsync(Path.GetTempPath());
            
            if (File.Exists(zipPath)) File.Delete(zipPath);
            File.Move(tempZip, zipPath);

            StatusMessage = LocalizationManager.L("full_backup_success");
            IsSuccess = true;
            LoadActivityLogs();
            await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("full_backup_dialog_msg"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Full zip backup error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task TruncateAuditLog()
    {
        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("audit_log_clear_confirm_title"), LocalizationManager.L("audit_log_clear_confirm_msg"));
        if (!confirm) return;

        try
        {
            await Task.Run(() => _config.TruncateAuditLog());
            StatusMessage = LocalizationManager.L("audit_log_cleared_success");
            IsSuccess = true;
            LoadActivityLogs();
            await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("audit_log_cleared_dialog_msg"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Audit log truncate error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task CheckDatabaseIntegrityAsync()
    {
        StatusMessage = LocalizationManager.L("db_integrity_checking");
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
                StatusMessage = LocalizationManager.L("db_integrity_success_status");
                IsSuccess = true;
                LoadActivityLogs();
                await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("db_integrity_success_dialog_msg"));
            }
            else
            {
                StatusMessage = string.Format(LocalizationManager.L("db_integrity_failed_status"), result);
                IsSuccess = false;
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("db_integrity_failed_dialog_msg"), result));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Database integrity check error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task OptimizeDatabaseAsync()
    {
        StatusMessage = LocalizationManager.L("db_optimizing");
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
            StatusMessage = LocalizationManager.L("db_optimize_success_status");
            IsSuccess = true;
            LoadActivityLogs();
            await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("db_optimize_success_dialog_msg"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Database vacuum error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task ClearActivityLogsAsync()
    {
        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("activity_log_clear_confirm_title"), LocalizationManager.L("activity_log_clear_confirm_msg"));
        if (!confirm) return;

        try
        {
            if (File.Exists(AppPaths.ActivityLogPath))
            {
                File.WriteAllText(AppPaths.ActivityLogPath, "");
            }
            LoadActivityLogs();
            StatusMessage = LocalizationManager.L("activity_log_cleared_success");
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Clear activity logs error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        StatusMessage = LocalizationManager.L("checking_updates");
        IsSuccess = false;

        try
        {
            var (latestTag, htmlUrl) = await _updateService.GetLatestReleaseAsync();
            string cleanLatest = latestTag.TrimStart('v', 'V');

            if (Version.TryParse(cleanLatest, out var latestVersion) && 
                Version.TryParse(AppVersion.TrimStart('v', 'V'), out var currentVersion))
            {
                if (latestVersion > currentVersion)
                {
                    StatusMessage = string.Format(LocalizationManager.L("update_available"), latestTag, AppVersion);
                    IsSuccess = true;

                    bool goToDownload = await _dialogService.ShowConfirmAsync(
                        LocalizationManager.L("update_title"), 
                        string.Format(LocalizationManager.L("update_available"), latestTag, AppVersion)
                    );

                    if (goToDownload)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = htmlUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    StatusMessage = string.Format(LocalizationManager.L("up_to_date"), AppVersion);
                    IsSuccess = true;
                    await _dialogService.ShowMessageAsync(
                        LocalizationManager.L("info"), 
                        string.Format(LocalizationManager.L("up_to_date"), AppVersion)
                    );
                }
            }
            else
            {
                throw new FormatException("Version string parsing failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Check updates error", ex);
            StatusMessage = string.Format(LocalizationManager.L("update_error"), ex.Message);
            IsSuccess = false;
            await _dialogService.ShowMessageAsync(
                LocalizationManager.L("error"), 
                string.Format(LocalizationManager.L("update_error"), ex.Message)
            );
        }
    }
}
