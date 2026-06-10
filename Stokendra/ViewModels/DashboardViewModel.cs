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

    [ObservableProperty] private int _totalCards;
    [ObservableProperty] private string _totalStock = "0";
    [ObservableProperty] private int _lowStock;
    [ObservableProperty] private int _depletedStock;
    [ObservableProperty] private int _totalMovements;
    [ObservableProperty] private int _todayMovements;
    [ObservableProperty] private bool _isLoading = true;

    public ObservableCollection<LowStockItem> LowStockItems { get; } = new();

    public DashboardViewModel(
        IReportRepository reports,
        IMovementRepository movements,
        IStockCardRepository stockCards)
    {
        _reports = reports;
        _movements = movements;
        _stockCards = stockCards;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            int lowStockThreshold = 0;
            var stats = await _reports.GetDashboardStatsAsync(lowStockThreshold);
            TotalCards     = stats.TotalCards;
            TotalStock     = FormatNumber(stats.TotalStock);
            LowStock       = stats.LowStock;
            DepletedStock  = stats.DepletedStock;
            TotalMovements = stats.TotalMovements;
            TodayMovements = stats.TodayMovements;

            // Get top 20 low stock items directly from DB
            var cards = await _stockCards.GetLowStockCardsAsync(20, lowStockThreshold);
            LowStockItems.Clear();
            foreach (var k in cards)
            {
                LowStockItems.Add(new LowStockItem
                {
                    Name       = k.Name,
                    Code       = k.Code,
                    Current    = FormatNumber(k.CurrentStock),
                    MinStock   = k.MinStock.ToString(),
                    Status     = k.CurrentStock <= 0 ? LocalizationManager.L("depleted") : LocalizationManager.L("low"),
                    IsDepleted = k.CurrentStock <= 0
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dashboard load error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _ = LoadAsync();
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
