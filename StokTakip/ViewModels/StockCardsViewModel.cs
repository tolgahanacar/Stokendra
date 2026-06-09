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

    // Debounce için
    private CancellationTokenSource? _filterCts;

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

    partial void OnSearchTextChanged(string value) => ScheduleFilter();

    private async void ScheduleFilter()
    {
        _filterCts?.Cancel();
        _filterCts?.Dispose();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;
        try
        {
            await Task.Delay(250, token);
            ApplySearch();
        }
        catch (OperationCanceledException) { }
    }

    private void ApplySearch()
    {
        var term = SearchText.Trim().ToTurkishLower();
        var data = string.IsNullOrEmpty(term)
            ? _allCards
            : _allCards.Where(k =>
                k.Ad.ToTurkishLower().Contains(term) ||
                k.KodNo.ToTurkishLower().Contains(term) ||
                k.Kategori.ToTurkishLower().Contains(term)).ToList();

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
                
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null) return;

                int colCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                
                // Sütun indekslerini başlığa göre dinamik bul (1-based)
                int colKodNo = 0, colAd = 0, colKategori = 0, colBirim = 0, colKonum = 0;
                int colTedarikci = 0, colBarkod = 0, colBirimFiyat = 0, colMinStok = 0, colAciklama = 0;

                for (int i = 1; i <= colCount; i++)
                {
                    string header = firstRow.Cell(i).GetValue<string>().Trim().ToTurkishLower();
                    if (header == "kodno" || header == "kod" || header == "stok kodu" || header == "stokkodu")
                        colKodNo = i;
                    else if (header == "stok adı" || header == "stok adi" || header == "ad" || header == "adi")
                        colAd = i;
                    else if (header == "kategori")
                        colKategori = i;
                    else if (header == "birim")
                        colBirim = i;
                    else if (header == "konum")
                        colKonum = i;
                    else if (header == "tedarikçi" || header == "tedarikci")
                        colTedarikci = i;
                    else if (header == "barkod")
                        colBarkod = i;
                    else if (header == "birimfiyat" || header == "birim fiyat" || header == "fiyat" || header == "fiyati")
                        colBirimFiyat = i;
                    else if (header == "minstok" || header == "min stok" || header == "minimum stok" || header == "min")
                        colMinStok = i;
                    else if (header == "açıklama" || header == "aciklama")
                        colAciklama = i;
                }

                // Eşleşme bulunamadıysa klasik sıralamayı yedek plan (fallback) olarak kullan
                if (colKodNo == 0 && colAd == 0)
                {
                    colKodNo = 1;
                    colAd = 2;
                    colKategori = 3;
                    colBirim = 4;
                    colKonum = 5;
                    colTedarikci = 6;
                    colBarkod = 7;
                    colBirimFiyat = 8;
                    colMinStok = 9;
                    colAciklama = 10;
                }

                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<ClosedXML.Excel.IXLRangeRow>();

                foreach (var row in rows)
                {
                    string kodNo = colKodNo > 0 ? (row.Cell(colKodNo).GetValue<string>() ?? "").Trim() : "";
                    string ad = colAd > 0 ? (row.Cell(colAd).GetValue<string>() ?? "").Trim() : "";

                    if (string.IsNullOrEmpty(kodNo) && string.IsNullOrEmpty(ad)) continue;

                    var k = new StokKarti
                    {
                        KodNo = kodNo,
                        Ad = ad,
                        Kategori = colKategori > 0 ? (row.Cell(colKategori).GetValue<string>() ?? "").Trim() : "",
                        Birim = colBirim > 0 ? (row.Cell(colBirim).GetValue<string>() ?? "Adet").Trim() : "Adet",
                        Konum = colKonum > 0 ? (row.Cell(colKonum).GetValue<string>() ?? "").Trim() : "",
                        Tedarikci = colTedarikci > 0 ? (row.Cell(colTedarikci).GetValue<string>() ?? "").Trim() : "",
                        Barkod = colBarkod > 0 ? (row.Cell(colBarkod).GetValue<string>() ?? "").Trim() : "",
                        BirimFiyat = colBirimFiyat > 0 ? (row.Cell(colBirimFiyat).TryGetValue<double>(out double valFiyat) ? valFiyat : 0.0) : 0.0,
                        MinStok = colMinStok > 0 ? (row.Cell(colMinStok).TryGetValue<int>(out int valMin) ? valMin : 0) : 0,
                        KartTipi = "Alt",
                        Aciklama = colAciklama > 0 ? (row.Cell(colAciklama).GetValue<string>() ?? "").Trim() : ""
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
