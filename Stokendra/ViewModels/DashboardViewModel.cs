using CommunityToolkit.Mvvm.ComponentModel;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IReportRepository _reports;
    private readonly IMovementRepository _movements;
    private readonly IStockCardRepository _stockCards;
    private readonly System.Threading.SemaphoreSlim _loadLock = new(1, 1);

    [ObservableProperty] private int _totalCards;
    [ObservableProperty] private string _totalStock = "0";
    [ObservableProperty] private int _lowStock;
    [ObservableProperty] private int _depletedStock;
    [ObservableProperty] private int _totalMovements;
    [ObservableProperty] private int _todayMovements;
    [ObservableProperty] private bool _isLoading = true;
    private bool _hasLoadedOnce;
    private bool _isDirty;

    public BulkObservableCollection<LowStockItem> LowStockItems { get; } = new();

    public DashboardViewModel(
        IReportRepository reports,
        IMovementRepository movements,
        IStockCardRepository stockCards)
    {
        _reports = reports;
        _movements = movements;
        _stockCards = stockCards;

        DataEvents.DataChanged += OnDataChanged;
        // Loaded lazily by MainViewModel.NavigateToDashboard()
    }

    private void OnDataChanged(DataDomain domain)
    {
        if (domain == DataDomain.StockCards || domain == DataDomain.Movements)
        {
            _isDirty = true;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_hasLoadedOnce && !_isDirty) return;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (!await _loadLock.WaitAsync(0)) return;
        if (!_hasLoadedOnce) IsLoading = true;
        try
        {
            _isDirty = false;
            int lowStockThreshold = AppServices.TryGetCurrent(out var app) ? app!.Settings.LowStockThreshold : 3;
            var stats = await _reports.GetDashboardStatsAsync(lowStockThreshold);
            TotalCards     = stats.TotalCards;
            TotalStock     = FormatNumber(stats.TotalStock);
            LowStock       = stats.LowStock;
            DepletedStock  = stats.DepletedStock;
            TotalMovements = stats.TotalMovements;
            TodayMovements = stats.TodayMovements;

            // Get top 20 low stock items directly from DB
            var cards = await _stockCards.GetLowStockCardsAsync(20, lowStockThreshold);
            LowStockItems.ReplaceAll(cards.Select(k => new LowStockItem
            {
                Name       = k.Name,
                Code       = k.Code,
                Current    = FormatNumber(k.CurrentStock),
                MinStock   = k.MinStock.ToString(),
                Status     = k.CurrentStock <= 0 ? LocalizationManager.L("depleted") : LocalizationManager.L("low"),
                IsDepleted = k.CurrentStock <= 0
            }));

            _hasLoadedOnce = true;
        }
        catch (Exception ex)
        {
            _isDirty = true;
            AppLogger.LogError("Dashboard load error", ex);
        }
        finally
        {
            IsLoading = false;
            _loadLock.Release();
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _hasLoadedOnce = false;
        _isDirty = true;
        AsyncHelper.RunSafe(
            () => LoadAsync(),
            ex => AppLogger.LogError("Dashboard refresh failed", ex));
    }

    private static string FormatNumber(double v)
        => v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("N2");
}

public class LowStockItem
{
    public string Name       { get; set; } = "";
    public string Code       { get; set; } = "";
    public string Current    { get; set; } = "";
    public string MinStock   { get; set; } = "";
    public string Status     { get; set; } = "";
    public bool   IsDepleted { get; set; }
}
