using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Infrastructure;
using Stokendra.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Stokendra.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DashboardViewModel      _dashboard;
    private readonly StockCardsViewModel     _stockCards;
    private readonly StockMovementsViewModel _movements;
    private readonly ServicesViewModel       _services;
    private readonly NotesViewModel          _notes;
    private readonly SettingsViewModel       _settings;
    private readonly StocksViewModel         _stocks;
    private readonly DepartmentsViewModel    _departments;
    private readonly ReportsViewModel        _reports;
    private readonly GuideViewModel          _guide;
    private readonly UsersViewModel          _users;
    private readonly IBackupService          _backupService;
    private readonly IUpdateService          _updateService;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private string _activeMenu = "dashboard";
    [ObservableProperty] private string _currentUser = "";
    public string CurrentRole => AppServices.Current.Session?.Role == "admin" ? LocalizationManager.L("role_admin") : LocalizationManager.L("role_user");
    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "7.3.9"}";

    // ── Güncelleme Banner Durumu ──────────────────────────────────────────
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private bool _updateAvailable;

    [ObservableProperty] private string _updateTagName = "";
    [ObservableProperty] private string _updateMessage = "";
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private int _downloadProgress;
    [ObservableProperty] private string _downloadStatusText = "";
    [ObservableProperty] private bool _downloadComplete;
    [ObservableProperty] private bool _updateDismissed;

    /// <summary>Banner'ın görünür olup olmayacağı.</summary>
    public bool HasUpdate => UpdateAvailable && !UpdateDismissed;

    private UpdateInfo? _pendingUpdate;
    private string? _downloadedInstallerPath;
    private CancellationTokenSource? _downloadCts;

    public MainViewModel(
        DashboardViewModel      dashboard,
        StockCardsViewModel     stockCards,
        StockMovementsViewModel movements,
        ServicesViewModel       services,
        NotesViewModel          notes,
        SettingsViewModel       settings,
        StocksViewModel         stocks,
        DepartmentsViewModel    departments,
        ReportsViewModel        reports,
        GuideViewModel          guide,
        UsersViewModel          users,
        IBackupService          backupService,
        IUpdateService          updateService)
    {
        _dashboard  = dashboard;
        _stockCards = stockCards;
        _movements  = movements;
        _services   = services;
        _notes      = notes;
        _settings   = settings;
        _stocks     = stocks;
        _departments = departments;
        _reports     = reports;
        _guide       = guide;
        _users       = users;
        _backupService = backupService;
        _updateService = updateService;

        CurrentUser = AppServices.Current.Session?.Username ?? "admin";
        NavigateToDashboard();
        
        // Günlük yedekleme kontrolü (Arka planda)
        AsyncHelper.RunSafe(
            () => _backupService.CheckAutoBackupAsync(),
            ex => AppLogger.LogError("Auto backup check failed.", ex));

        // Uygulama açılışında sessiz güncelleme kontrolü
        AsyncHelper.RunSafe(
            () => CheckForUpdateSilentlyAsync(),
            ex => AppLogger.LogError("Silent update check failed.", ex));
    }

    [RelayCommand] public void NavigateToDashboard()  { CurrentPage = _dashboard;  ActiveMenu = "dashboard";  }
    [RelayCommand] public void NavigateToStockCards()  { CurrentPage = _stockCards; ActiveMenu = "stockcards"; }
    [RelayCommand] public void NavigateToMovements()   { CurrentPage = _movements;  ActiveMenu = "movements";  }
    [RelayCommand] public void NavigateToServices()    { CurrentPage = _services;   ActiveMenu = "services";   }
    [RelayCommand] public void NavigateToNotes()       { CurrentPage = _notes;      ActiveMenu = "notes";      }
    [RelayCommand] public void NavigateToSettings()    { CurrentPage = _settings;   ActiveMenu = "settings";   }
    [RelayCommand] public void NavigateToStocks()      { CurrentPage = _stocks;     ActiveMenu = "stocks";     }
    [RelayCommand] public void NavigateToDepartments() { CurrentPage = _departments; ActiveMenu = "departments"; }
    [RelayCommand] public void NavigateToReports()     { CurrentPage = _reports;     ActiveMenu = "reports";     }
    [RelayCommand] public void NavigateToGuide()       { CurrentPage = _guide;       ActiveMenu = "guide";       }
    [RelayCommand] public void NavigateToUsers()       { CurrentPage = _users;       ActiveMenu = "users";       }

    public new void RefreshSession()
    {
        CurrentUser = AppServices.Current.Session?.Username ?? "admin";
        OnPropertyChanged(nameof(CurrentUser));
        OnPropertyChanged(nameof(CurrentRole));
        
        _dashboard.RefreshSession();
        _stockCards.RefreshSession();
        _movements.RefreshSession();
        _services.RefreshSession();
        _notes.RefreshSession();
        _settings.RefreshSession();
        _stocks.RefreshSession();
        _departments.RefreshSession();
        _reports.RefreshSession();
        _guide.RefreshSession();
        _users.RefreshSession();

        NavigateToDashboard();
    }

    [RelayCommand]
    public void Logout()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppServices.Current.EndSession();

            var currentWindow = desktop.MainWindow;

            var loginVm = ServiceContainer.GetService<LoginViewModel>();
            var loginView = new LoginView { DataContext = loginVm };

            void OnLoginSuccessful(object? sender, EventArgs e)
            {
                loginVm.LoginSuccessful -= OnLoginSuccessful;
                Dispatcher.UIThread.Post(() =>
                {
                    RefreshSession();

                    var mainWindow = new MainWindow { DataContext = this };
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    loginView.Close();
                });
            }

            loginVm.LoginSuccessful += OnLoginSuccessful;

            desktop.MainWindow = loginView;
            loginView.Show();
            currentWindow?.Close();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Güncelleme Sistemi ────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Uygulama açılışında arka planda sessizce güncelleme kontrolü yapar.
    /// Güncelleme bulunursa banner gösterir.
    /// </summary>
    private async Task CheckForUpdateSilentlyAsync()
    {
        try
        {
            // Kısa bir gecikme — UI'ın tam yüklenmesini bekle
            await Task.Delay(2000);

            var updateInfo = await _updateService.CheckForUpdateAsync();
            if (updateInfo == null) return;

            string currentVersionStr = System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version?.ToString(3) ?? "0.0.0";

            if (!Version.TryParse(currentVersionStr, out var currentVersion)) return;

            if (updateInfo.Version > currentVersion)
            {
                _pendingUpdate = updateInfo;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateTagName = updateInfo.TagName;
                    UpdateMessage = string.Format(
                        LocalizationManager.L("update_banner_msg"),
                        updateInfo.TagName,
                        $"v{currentVersionStr}");
                    UpdateAvailable = true;
                    UpdateDismissed = false;
                    OnPropertyChanged(nameof(HasUpdate));
                });
            }
        }
        catch (Exception ex)
        {
            // Sessiz kontrol — hata durumunda kullanıcıya bir şey gösterme
            AppLogger.LogError("Silent update check failed.", ex);
        }
    }

    /// <summary>
    /// Güncelleme banner'ındaki "İndir ve Kur" butonuna basıldığında çalışır.
    /// Installer'ı indirir, ardından kurulumu başlatır ve uygulamayı kapatır.
    /// </summary>
    [RelayCommand]
    public async Task DownloadAndInstallUpdateAsync()
    {
        if (_pendingUpdate == null) return;

        // İndirilebilir installer yoksa tarayıcıda aç
        if (string.IsNullOrEmpty(_pendingUpdate.InstallerDownloadUrl))
        {
            OpenInBrowser(_pendingUpdate.HtmlUrl);
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadComplete = false;
        DownloadStatusText = LocalizationManager.L("update_downloading");

        _downloadCts = new CancellationTokenSource();

        try
        {
            string fileName = _pendingUpdate.InstallerFileName ?? $"Stokendra_Setup_{_pendingUpdate.TagName}.exe";
            string downloadsDir = Path.Combine(AppPaths.ApplicationDataDirectory, "updates");
            Directory.CreateDirectory(downloadsDir);
            string destPath = Path.Combine(downloadsDir, fileName);

            var progress = new Progress<int>(percent =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DownloadProgress = percent;
                    string sizeText = _pendingUpdate.InstallerSize > 0
                        ? $" ({FormatBytes(_pendingUpdate.InstallerSize * percent / 100)} / {FormatBytes(_pendingUpdate.InstallerSize)})"
                        : "";
                    DownloadStatusText = $"{LocalizationManager.L("update_downloading")} %{percent}{sizeText}";
                });
            });

            await _updateService.DownloadInstallerAsync(
                _pendingUpdate.InstallerDownloadUrl, destPath, progress, _downloadCts.Token);

            _downloadedInstallerPath = destPath;
            DownloadComplete = true;
            DownloadProgress = 100;
            DownloadStatusText = LocalizationManager.L("update_download_complete");

            // Otomatik olarak kurulumu başlat
            await Task.Delay(500); // Kullanıcı durumu görsün
            LaunchInstallerAndExit();
        }
        catch (OperationCanceledException)
        {
            DownloadStatusText = LocalizationManager.L("update_download_cancelled");
            IsDownloading = false;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Update download failed.", ex);
            DownloadStatusText = string.Format(LocalizationManager.L("update_download_error"), ex.Message);
            IsDownloading = false;
        }
    }

    /// <summary>
    /// İndirmeyi iptal eder.
    /// </summary>
    [RelayCommand]
    public void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    /// <summary>
    /// Güncelleme banner'ını kapatır ("Sonra hatırlat" / "X" butonu).
    /// </summary>
    [RelayCommand]
    public void DismissUpdate()
    {
        UpdateDismissed = true;
        OnPropertyChanged(nameof(HasUpdate));
    }

    /// <summary>
    /// Release sayfasını varsayılan tarayıcıda açar.
    /// </summary>
    [RelayCommand]
    public void OpenReleasePage()
    {
        if (_pendingUpdate != null)
            OpenInBrowser(_pendingUpdate.HtmlUrl);
    }

    /// <summary>
    /// İndirilen installer'ı çalıştırır ve uygulamayı kapatır.
    /// </summary>
    private void LaunchInstallerAndExit()
    {
        if (string.IsNullOrEmpty(_downloadedInstallerPath) || !File.Exists(_downloadedInstallerPath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _downloadedInstallerPath,
                UseShellExecute = true
            });

            // Uygulamayı kapat
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to launch installer.", ex);
            DownloadStatusText = string.Format(LocalizationManager.L("update_launch_error"), ex.Message);
        }
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to open URL.", ex);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:N1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:N1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:N0} KB";
        return $"{bytes} B";
    }
}
