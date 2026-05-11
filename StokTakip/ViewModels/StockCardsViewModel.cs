using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace StokTakip.ViewModels;

public partial class StockCardsViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private string    _searchText = "";
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool      _isLoading;
    [ObservableProperty] private string    _statusText = "";
    [ObservableProperty] private StokKarti? _selectedCard;

    public bool IsNotLoading => !IsLoading;

    public bool IsCardSelected => SelectedCard != null;
    [ObservableProperty] private bool _isMultipleSelected;
    
    partial void OnSelectedCardChanged(StokKarti? value) => OnPropertyChanged(nameof(IsCardSelected));

    public ObservableCollection<StokKarti> Cards { get; } = new();
    public ObservableCollection<StokKarti> SelectedCards { get; } = new();

    private List<StokKarti> _allCards = new();

    public StockCardsViewModel(
        IStockCardRepository stockCards,
        IMovementRepository movements,
        IDepartmentRepository departments,
        IDialogService dialogService,
        ILogger logger)
    {
        _stockCards = stockCards;
        _movements = movements;
        _departments = departments;
        _dialogService = dialogService;
        _logger = logger;
        
        SelectedCards.CollectionChanged += (s, e) => {
            IsMultipleSelected = SelectedCards.Count >= 2;
        };
        
        SafeLoadAsync();
    }

    private async void SafeLoadAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Load error", ex);
            StatusText = $"Hata: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _allCards = await _stockCards.GetAllAsync();
            ApplySearch();
        }
        catch (Exception ex) 
        { 
            _logger.LogError("StockCards load error", ex);
            StatusText = "Yükleme hatası."; 
        }
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
        StatusText = $"{data.Count} kart listeleniyor";
    }

    [RelayCommand]
    public void ClearSearch() => SearchText = "";

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public async Task DeleteCardAsync()
    {
        if (SelectedCard == null) return;
        
        bool confirm = await _dialogService.ShowConfirmAsync("Silme Onayı", 
            $"{SelectedCard.KodNo} - {SelectedCard.Ad} kartını ve bu karta bağlı tüm hareketleri silmek istediğinize emin misiniz?");
        
        if (!confirm) return;

        try
        {
            await Task.Run(() => _stockCards.Delete(SelectedCard.Id));
            await LoadAsync();
            StatusText = "Kart ve bağlı hareketler silindi.";
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Delete card error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Silme işlemi başarısız: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task AddCardAsync()
    {
        var vm = new AddStockCardViewModel(_stockCards);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            await Task.Run(() => _stockCards.Add(vm.Result));
            await LoadAsync();
            StatusText = "Yeni kart eklendi.";
        }
    }

    [RelayCommand]
    public async Task EditCardAsync()
    {
        if (SelectedCard == null) return;
        var vm = new AddStockCardViewModel(_stockCards, SelectedCard);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            await Task.Run(() => _stockCards.Update(vm.Result));
            await LoadAsync();
            StatusText = "Kart güncellendi.";
        }
    }

    [RelayCommand]
    public async Task BulkDeleteAsync()
    {
        if (SelectedCards.Count < 2) return;

        bool confirm = await _dialogService.ShowConfirmAsync("Toplu Silme", 
            $"{SelectedCards.Count} adet kartı ve bunlara bağlı tüm hareketleri silmek istediğinize emin misiniz?");
        
        if (!confirm) return;

        try
        {
            await Task.Run(() => {
                foreach (var k in SelectedCards.ToList())
                    _stockCards.Delete(k.Id);
            });
            await LoadAsync();
            StatusText = "Seçili kartlar silindi.";
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk delete cards error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"İşlem başarısız: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task BulkEntryAsync()
    {
        // Global toplu giriş viewmodel'ini kullanıyoruz
        var movements = _movements;
        var depts = _departments;
        var vm = new BulkMovementViewModel(movements, depts, _stockCards, _dialogService, _logger);
        
        if (await _dialogService.ShowDialogAsync(vm))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    public async Task ImportExcelAsync()
    {
        string? path = await _dialogService.OpenFileAsync("Excel'den İçe Aktar", "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = "Veriler aktarılıyor...";
            int count = 0;
            await Task.Run(() => {
                using var workbook = new ClosedXML.Excel.XLWorkbook(path);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    var k = new StokKarti
                    {
                        KodNo = row.Cell(1).GetValue<string>().Trim(),
                        Ad = row.Cell(2).GetValue<string>().Trim(),
                        Kategori = row.Cell(3).GetValue<string>().Trim(),
                        Birim = row.Cell(4).GetValue<string>().Trim() ?? "Adet",
                        Konum = row.Cell(5).GetValue<string>().Trim(),
                        Tedarikci = row.Cell(6).GetValue<string>().Trim(),
                        Barkod = row.Cell(7).GetValue<string>().Trim(),
                        BirimFiyat = row.Cell(8).GetValue<double>(),
                        MinStok = row.Cell(9).GetValue<int>(),
                        KartTipi = "Alt",
                        Aciklama = row.Cell(10).GetValue<string>().Trim()
                    };

                    if (!string.IsNullOrEmpty(k.KodNo) && !string.IsNullOrEmpty(k.Ad))
                    {
                        _stockCards.Add(k);
                        count++;
                    }
                }
            });
            await LoadAsync();
            StatusText = $"{count} kart içe aktarıldı.";
            await _dialogService.ShowMessageAsync("Başarılı", $"{count} adet stok kartı başarıyla eklendi.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Stock cards import error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"İçe aktarım başarısız: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DownloadSampleAsync()
    {
        string? path = await _dialogService.SaveFileAsync("Örnek Dosyayı Kaydet", "stok_karti_ornek.xlsx", "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            await Task.Run(() => {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var ws = workbook.Worksheets.Add("StokKartlari");
                string[] headers = { "KodNo", "Stok Adı", "Kategori", "Birim", "Konum", "Tedarikçi", "Barkod", "BirimFiyat", "MinStok", "Açıklama" };
                for (int i = 0; i < headers.Length; i++) { ws.Cell(1, i + 1).Value = headers[i]; ws.Cell(1, i + 1).Style.Font.Bold = true; }
                
                ws.Cell(2, 1).Value = "100.001";
                ws.Cell(2, 2).Value = "Örnek Malzeme";
                ws.Cell(2, 3).Value = "Kırtasiye";
                ws.Cell(2, 4).Value = "Adet";
                ws.Cell(2, 9).Value = 5;
                
                ws.Columns().AdjustToContents();
                workbook.SaveAs(path);
            });
            StatusText = "Örnek dosya kaydedildi.";
        }
        catch (Exception ex) { await _dialogService.ShowMessageAsync("Hata", ex.Message); }
    }

    [RelayCommand]
    public async Task ShowDetailAsync()
    {
        if (SelectedCard == null) return;
        
        var movements = _movements;
        var depts = _departments;
        
        var vm = new StockCardDetailViewModel(
            SelectedCard.Id, 
            _stockCards, 
            movements, 
            depts, 
            _dialogService, 
            _logger);
            
        await _dialogService.ShowDialogAsync(vm);
        await LoadAsync(); // Detayda hareket eklendiyse mevcut stoğu güncelle
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string fileName = $"Stok_Kartlari_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync("Excel'e Aktar", fileName, "Excel Dosyası (*.xlsx)|*.xlsx");
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
            await _dialogService.ShowMessageAsync("Başarılı", "Veriler Excel dosyasına aktarıldı.");
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Excel export error", ex);
            StatusText = $"Hata: {ex.Message}"; 
        }
    }
}
