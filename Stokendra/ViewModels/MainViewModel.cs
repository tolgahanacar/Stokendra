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
    private readonly FeedbackViewModel       _feedback;
    private readonly IBackupService          _backupService;
    private readonly IUpdateService          _updateService;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private string _activeMenu = "dashboard";
    [ObservableProperty] private string _currentUser = "";
    public string CurrentRole => AppServices.Current.Session?.Role == "admin" ? LocalizationManager.L("role_admin") : LocalizationManager.L("role_user");
    public string AppVersion => AppInfo.VersionString;

    // ── Güncelleme Banner Durumu ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private bool _updateAvailable;

    [ObservableProperty] private string _updateTagName = "";
    [ObservableProperty] private string _updateMessage = "";
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private int _downloadProgress;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDownloadStatus))]
    private string _downloadStatusText = "";
    public bool HasDownloadStatus => !string.IsNullOrEmpty(DownloadStatusText);
    [ObservableProperty] private bool _downloadComplete;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private bool _updateDismissed;

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
        FeedbackViewModel       feedback,
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
        _feedback    = feedback;
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

    [RelayCommand] public void NavigateToDashboard()  { if (CurrentPage == _dashboard) return; CurrentPage = _dashboard;  ActiveMenu = "dashboard";  AsyncHelper.RunSafe(() => _dashboard.EnsureLoadedAsync(), ex => AppLogger.LogError("Dashboard nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToStockCards()  { if (CurrentPage == _stockCards) return; CurrentPage = _stockCards; ActiveMenu = "stockcards"; AsyncHelper.RunSafe(() => _stockCards.EnsureLoadedAsync(), ex => AppLogger.LogError("StockCards nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToMovements()   { if (CurrentPage == _movements) return; CurrentPage = _movements;  ActiveMenu = "movements";  AsyncHelper.RunSafe(() => _movements.EnsureLoadedAsync(), ex => AppLogger.LogError("Movements nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToServices()    { if (CurrentPage == _services) return; CurrentPage = _services;   ActiveMenu = "services";   AsyncHelper.RunSafe(() => _services.EnsureLoadedAsync(), ex => AppLogger.LogError("Services nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToNotes()       { if (CurrentPage == _notes) return; CurrentPage = _notes;      ActiveMenu = "notes";      AsyncHelper.RunSafe(() => _notes.EnsureLoadedAsync(), ex => AppLogger.LogError("Notes nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToSettings()    { if (CurrentPage == _settings) return; CurrentPage = _settings;   ActiveMenu = "settings";   }
    [RelayCommand] public void NavigateToStocks()      { if (CurrentPage == _stocks) return; CurrentPage = _stocks;     ActiveMenu = "stocks";     AsyncHelper.RunSafe(() => _stocks.EnsureLoadedAsync(), ex => AppLogger.LogError("Stocks nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToDepartments() { if (CurrentPage == _departments) return; CurrentPage = _departments; ActiveMenu = "departments"; AsyncHelper.RunSafe(() => _departments.EnsureLoadedAsync(), ex => AppLogger.LogError("Departments nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToReports()     { if (CurrentPage == _reports) return; CurrentPage = _reports;     ActiveMenu = "reports";     AsyncHelper.RunSafe(() => _reports.EnsureLoadedAsync(), ex => AppLogger.LogError("Reports nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToGuide()       { if (CurrentPage == _guide) return; CurrentPage = _guide;       ActiveMenu = "guide";       }
    [RelayCommand] public void NavigateToUsers()       { if (CurrentPage == _users) return; CurrentPage = _users;       ActiveMenu = "users";       AsyncHelper.RunSafe(() => _users.EnsureLoadedAsync(), ex => AppLogger.LogError("Users nav refresh failed", ex)); }
    [RelayCommand] public void NavigateToFeedback()    { if (CurrentPage == _feedback) return; CurrentPage = _feedback; ActiveMenu = "feedback";    _feedback.RefreshDiagnostics(); }

    public override void RefreshSession()
    {
        base.RefreshSession();
        CurrentUser = AppServices.TryGetCurrent(out var app) ? (app?.Session?.Username ?? "admin") : "admin";
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
        _feedback.RefreshSession();

        if (CurrentPage == _dashboard)
        {
            AsyncHelper.RunSafe(() => _dashboard.EnsureLoadedAsync(), ex => AppLogger.LogError("Dashboard refresh failed", ex));
        }
        else
        {
            NavigateToDashboard();
        }
    }

    [RelayCommand]
    public void Logout()
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = null;

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

            var currentVersion = AppInfo.Version;
            string currentVersionStr = AppInfo.RawVersion;

            if (updateInfo.Version > currentVersion)
            {
                _pendingUpdate = updateInfo;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateTagName = updateInfo.TagName;
                    UpdateMessage = string.Format(
                        LocalizationManager.L("update_banner_msg"),
                        updateInfo.TagName,
                        AppInfo.VersionString);
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
        if (IsDownloading || _pendingUpdate == null) return;

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
            string rawName = _pendingUpdate.InstallerFileName ?? $"Stokendra_Setup_{_pendingUpdate.TagName}.exe";
            string fileName = Path.GetFileName(rawName);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "Stokendra_Setup.exe";
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
            IsDownloading = false;
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
