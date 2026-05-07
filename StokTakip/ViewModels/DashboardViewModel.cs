using CommunityToolkit.Mvvm.ComponentModel;
using StokTakip.Data.Interfaces;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

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
            var stats = await _reports.GetDashboardStatsAsync();
            TotalCards     = stats.TotalCards;
            TotalStock     = FormatNumber(stats.TotalStock);
            LowStock       = stats.LowStock;
            DepletedStock  = stats.DepletedStock;
            TotalMovements = stats.TotalMovements;
            TodayMovements = stats.TodayMovements;

            // Düşük stok listesi
            var cards = await _stockCards.GetChildCardsAsync();
            LowStockItems.Clear();
            foreach (var k in cards
                .Where(c => c.MevcutStok <= (c.MinStok > 0 ? c.MinStok : 3))
                .OrderBy(c => c.MevcutStok)
                .Take(20))
            {
                LowStockItems.Add(new LowStockItem
                {
                    Ad       = k.Ad,
                    KodNo    = k.KodNo,
                    Mevcut   = FormatNumber(k.MevcutStok),
                    MinStok  = k.MinStok.ToString(),
                    Durum    = k.MevcutStok <= 0 ? "Tükendi" : "Düşük",
                    IsDepleted = k.MevcutStok <= 0
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

    private static string FormatNumber(double v)
        => v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("N2");
}

public class LowStockItem
{
    public string Ad        { get; set; } = "";
    public string KodNo     { get; set; } = "";
    public string Mevcut    { get; set; } = "";
    public string MinStok   { get; set; } = "";
    public string Durum     { get; set; } = "";
    public bool   IsDepleted { get; set; }
}
