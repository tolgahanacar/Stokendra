using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace StokTakip.ViewModels;

public partial class StocksViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<StokKarti> Stocks { get; } = new();
    private List<StokKarti> _allStocks = new();

    public StocksViewModel(IStockCardRepository stockCards)
    {
        _stockCards = stockCards;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _stockCards.GetAllAsync();
            // Sadece Alt kartları (hareket görebilen) ve stok takibi yapılanları göster
            _allStocks = all.Where(k => k.KartTipi == "Alt").ToList();
            ApplyFilter();
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = SearchText.Trim().ToLowerInvariant();
        var data = string.IsNullOrEmpty(term)
            ? _allStocks
            : _allStocks.Where(k =>
                k.Ad.ToLowerInvariant().Contains(term) ||
                k.KodNo.ToLowerInvariant().Contains(term) ||
                k.Kategori.ToLowerInvariant().Contains(term)).ToList();

        Stocks.Clear();
        foreach (var k in data) Stocks.Add(k);
        
        int lowStockCount = data.Count(k => k.MevcutStok <= k.MinStok && k.MevcutStok > 0);
        int depletedCount = data.Count(k => k.MevcutStok <= 0);
        
        StatusText = $"{data.Count} Ürün | ⚠ {lowStockCount} Düşük Stok | ❌ {depletedCount} Tükenmiş";
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearSearch() => SearchText = "";
}
