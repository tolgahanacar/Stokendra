using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Infrastructure;
using Stokendra.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Stokendra.Views;
using System;

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

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private string _activeMenu = "dashboard";
    [ObservableProperty] private string _currentUser = "";
    public string CurrentRole => AppServices.Current.Session?.Role == "admin" ? "Yönetici" : "Kullanıcı";

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
        IBackupService          backupService)
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

        CurrentUser = AppServices.Current.Session?.Username ?? "admin";
        NavigateToDashboard();
        
        // Günlük yedekleme kontrolü (Arka planda)
        SafeCheckAutoBackupAsync();
    }

    private async void SafeCheckAutoBackupAsync()
    {
        try
        {
            await _backupService.CheckAutoBackupAsync();
        }
        catch (System.Exception ex)
        {
            AppLogger.LogError("Auto backup check failed.", ex);
        }
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
}
