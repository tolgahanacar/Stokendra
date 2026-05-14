using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace StokTakip.ViewModels;

public partial class BulkMovementItemViewModel : ObservableObject
{
    public StokKarti Card { get; }
    
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double? _quantity;

    public BulkMovementItemViewModel(StokKarti card)
    {
        Card = card;
    }
}

public partial class BulkMovementViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IStockCardRepository _stockCards;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private int _selectedTypeIndex = 1; // 0: Giriş, 1: Çıkış
    [ObservableProperty] private string _selectedDepartment = "";
    [ObservableProperty] private string _deliveredTo = "";
    [ObservableProperty] private DateTime? _date = DateTime.Now;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLoading;

    partial void OnSearchTextChanged(string value) => UpdateVisibleItems();

    public ObservableCollection<string> Departments { get; } = new();
    public ObservableCollection<BulkMovementItemViewModel> Items { get; } = new();
    
    private List<BulkMovementItemViewModel> _allItemViewModels = new();

    public BulkMovementViewModel(
        IMovementRepository movements,
        IDepartmentRepository departments,
        IStockCardRepository stockCards,
        IDialogService dialogService,
        ILogger logger)
    {
        _movements = movements;
        _departments = departments;
        _stockCards = stockCards;
        _dialogService = dialogService;
        _logger = logger;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var depts = await _departments.GetAllAsync();
            foreach (var d in depts) Departments.Add(d);
            SelectedDepartment = depts.FirstOrDefault() ?? "";

            var cards = await _stockCards.GetChildCardsAsync();
            _allItemViewModels = cards.Select(c => new BulkMovementItemViewModel(c)).ToList();
            
            UpdateVisibleItems();
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk movement init error", ex);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void UpdateVisibleItems()
    {
        var term = SearchText.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrWhiteSpace(term) 
            ? _allItemViewModels 
            : _allItemViewModels.Where(i => i.Card.Ad.ToLowerInvariant().Contains(term) || i.Card.KodNo.ToLowerInvariant().Contains(term)).ToList();

        // Toplu güncelleme: önce temizle, sonra tek seferde ekle
        // Büyük listelerde her Add bir CollectionChanged tetikler.
        // Avalonia'da bu pattern kabul edilebilir; gerçek virtualization DataGrid ile sağlanır.
        Items.Clear();
        foreach (var i in filtered) Items.Add(i);
    }

    [RelayCommand]
    public void SelectAll() { foreach (var i in Items) i.IsSelected = true; }

    [RelayCommand]
    public void DeselectAll() { foreach (var i in Items) i.IsSelected = false; }

    [RelayCommand]
    public async Task SaveAsync()
    {
        var selected = _allItemViewModels.Where(i => i.IsSelected && i.Quantity.HasValue && i.Quantity > 0).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowMessageAsync("Bilgi", "Seçili ve miktarı girilmiş kayıt bulunamadı.");
            return;
        }

        try
        {
            string tur = SelectedTypeIndex == 0 ? "Giris" : "Cikis";
            var targetDate = (Date ?? DateTime.Now).Date.Add(DateTime.Now.TimeOfDay);
            var movements = selected.Select(i => new StokHareketi
            {
                StokKartId = i.Card.Id,
                Tur = tur,
                Miktar = i.Quantity!.Value,
                Departman = SelectedDepartment,
                TeslimEdilen = DeliveredTo,
                Tarih = targetDate,
                Aciklama = "Toplu İşlem"
            }).ToList();

            await _movements.AddBulkAsync(movements);
            await _dialogService.ShowMessageAsync("Başarılı", $"{movements.Count} adet hareket kaydedildi.");
            CloseAction?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk movement save error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Kayıt sırasında hata oluştu: {ex.Message}");
        }
    }

    [RelayCommand]
    public void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
