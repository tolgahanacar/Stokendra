using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Infrastructure;

namespace StokTakip.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DashboardViewModel      _dashboard;
    private readonly StockCardsViewModel     _stockCards;
    private readonly StockMovementsViewModel _movements;
    private readonly ServicesViewModel       _services;
    private readonly NotesViewModel          _notes;
    private readonly SettingsViewModel       _settings;
    private readonly StocksViewModel         _stocks;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private string _activeMenu = "dashboard";
    [ObservableProperty] private string _currentUser = "";

    public MainViewModel(
        DashboardViewModel      dashboard,
        StockCardsViewModel     stockCards,
        StockMovementsViewModel movements,
        ServicesViewModel       services,
        NotesViewModel          notes,
        SettingsViewModel       settings,
        StocksViewModel         stocks)
    {
        _dashboard  = dashboard;
        _stockCards = stockCards;
        _movements  = movements;
        _services   = services;
        _notes      = notes;
        _settings   = settings;
        _stocks     = stocks;

        CurrentUser = AppServices.Current.Session?.Username ?? "admin";
        NavigateToDashboard();
    }

    [RelayCommand] public void NavigateToDashboard()  { CurrentPage = _dashboard;  ActiveMenu = "dashboard";  }
    [RelayCommand] public void NavigateToStockCards()  { CurrentPage = _stockCards; ActiveMenu = "stockcards"; }
    [RelayCommand] public void NavigateToMovements()   { CurrentPage = _movements;  ActiveMenu = "movements";  }
    [RelayCommand] public void NavigateToServices()    { CurrentPage = _services;   ActiveMenu = "services";   }
    [RelayCommand] public void NavigateToNotes()       { CurrentPage = _notes;      ActiveMenu = "notes";      }
    [RelayCommand] public void NavigateToSettings()    { CurrentPage = _settings;   ActiveMenu = "settings";   }
    [RelayCommand] public void NavigateToStocks()      { CurrentPage = _stocks;     ActiveMenu = "stocks";     }
}
