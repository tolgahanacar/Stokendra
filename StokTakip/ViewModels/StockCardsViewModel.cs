using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

public partial class StockCardsViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;

    [ObservableProperty] private string    _searchText = "";
    [ObservableProperty] private bool      _isLoading;
    [ObservableProperty] private string    _statusText = "";
    [ObservableProperty] private StokKarti? _selectedCard;

    public bool IsCardSelected => SelectedCard != null;
    partial void OnSelectedCardChanged(StokKarti? value) => OnPropertyChanged(nameof(IsCardSelected));

    public Func<AddStockCardViewModel, Task<bool>>? ShowDialogAction { get; set; }
    public Func<string, string, Task<string?>>? SaveFileAction { get; set; }

    public ObservableCollection<StokKarti> Cards { get; } = new();
    private List<StokKarti> _allCards = new();

    public StockCardsViewModel(IStockCardRepository stockCards)
    {
        _stockCards = stockCards;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _allCards = await _stockCards.GetAllAsync();
            ApplySearch();
        }
        catch (Exception ex) { Console.WriteLine($"StockCards load error: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        var term = SearchText.Trim().ToLowerInvariant();
        var data = string.IsNullOrEmpty(term)
            ? _allCards
            : _allCards.Where(k =>
                k.Ad.ToLowerInvariant().Contains(term) ||
                k.KodNo.ToLowerInvariant().Contains(term) ||
                k.Kategori.ToLowerInvariant().Contains(term)).ToList();

        Cards.Clear();
        foreach (var k in data) Cards.Add(k);
        StatusText = $"{data.Count} kart";
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = "";
    }

    [RelayCommand]
    public async Task DeleteCardAsync()
    {
        if (SelectedCard == null) return;
        try
        {
            await Task.Run(() => _stockCards.Delete(SelectedCard.Id));
            await LoadAsync();
        }
        catch (Exception ex) { Console.WriteLine($"Delete card error: {ex.Message}"); }
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public async Task AddCardAsync()
    {
        if (ShowDialogAction == null) return;
        var vm = new AddStockCardViewModel(_stockCards);
        if (await ShowDialogAction(vm) && vm.Result != null)
        {
            await Task.Run(() => _stockCards.Add(vm.Result));
            await LoadAsync();
            StatusText = "Yeni kart eklendi.";
        }
    }

    [RelayCommand]
    public async Task EditCardAsync()
    {
        if (SelectedCard == null || ShowDialogAction == null) return;
        var vm = new AddStockCardViewModel(_stockCards, SelectedCard);
        if (await ShowDialogAction(vm) && vm.Result != null)
        {
            await Task.Run(() => _stockCards.Update(vm.Result));
            await LoadAsync();
            StatusText = "Kart güncellendi.";
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        if (SaveFileAction == null) return;
        string fileName = $"Stok_Kartlari_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await SaveFileAction(fileName, "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = "Excel oluşturuluyor...";
            await Task.Run(() => {
                var headers = new[] { "Kod", "Ad", "Tip", "Üst Kart", "Kategori", "Mevcut", "Min", "Birim", "Konum" };
                Infrastructure.ExcelService.ExportToExcel(path, "Stok Kartları", headers, Cards, k => new object?[] {
                    k.KodNo, k.Ad, k.KartTipi, k.UstKartAd, k.Kategori, k.MevcutStok, k.MinStok, k.Birim, k.Konum
                });
            });
            StatusText = "Excel başarıyla kaydedildi.";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }
}
