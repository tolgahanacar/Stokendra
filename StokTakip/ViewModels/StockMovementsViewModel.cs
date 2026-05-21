using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Infrastructure;
using System.Collections.ObjectModel;
using System.Threading;

namespace StokTakip.ViewModels;

public partial class StockMovementsViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IStockCardRepository _stockCards;
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    // Filtreler
    [ObservableProperty] private DateTime _startDate = new DateTime(2024, 1, 1);
    [ObservableProperty] private DateTime _endDate   = DateTime.Today;
    [ObservableProperty] private string   _searchText = "";
    [ObservableProperty] private int      _selectedStockIndex = 0;
    [ObservableProperty] private int      _selectedTypeIndex  = 0;
    
    private CancellationTokenSource? _cts;

    // Durum
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private StokHareketi? _selectedMovement;

    public bool IsNotLoading => !IsLoading;

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    private const int PageSize = 30;

    public bool IsMovementSelected => SelectedMovement != null;
    [ObservableProperty] private bool _isMultipleSelected;

    partial void OnSelectedMovementChanged(StokHareketi? value) => OnPropertyChanged(nameof(IsMovementSelected));

    public ObservableCollection<StokHareketi> Movements { get; } = new();
    public ObservableCollection<StokHareketi> SelectedMovements { get; } = new();

    public ObservableCollection<string>       StockItems { get; } = new();
    public ObservableCollection<string>       TypeItems  { get; } = new() { "Tümü", "Giriş", "Çıkış", "Boş" };

    private List<StokKarti>   _allCards     = new();
    private List<StokHareketi> _allMovements = new();

    public StockMovementsViewModel(
        IMovementRepository movements,
        IStockCardRepository stockCards,
        IDepartmentRepository departments,
        IDialogService dialogService,
        ILogger logger)
    {
        _movements   = movements;
        _stockCards  = stockCards;
        _departments = departments;
        _dialogService = dialogService;
        _logger = logger;
        
        SelectedMovements.CollectionChanged += (s, e) => {
            IsMultipleSelected = SelectedMovements.Count >= 2;
        };

        SafeInitAsync();
    }

    private async void SafeInitAsync()
    {
        try
        {
            await InitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Init error", ex);
            StatusText = $"Başlangıç Hatası: {ex.Message}";
        }
    }

    private async Task InitAsync()
    {
        _allCards = await _stockCards.GetChildCardsAsync();
        StockItems.Clear();
        StockItems.Add("Tümü");
        foreach (var k in _allCards) StockItems.Add($"{k.KodNo} - {k.Ad}");
        await LoadMovementsAsync();
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task LoadMovementsAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsLoading = true;
        try
        {
            int? cardId = SelectedStockIndex > 0 ? _allCards[SelectedStockIndex - 1].Id : null;
            string? type = SelectedTypeIndex switch { 1 => "Giris", 2 => "Cikis", 3 => "Bos", _ => null };
            string? search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

            var totalCount = await _movements.GetCountAsync(stockCardId: cardId, startDate: StartDate, endDate: EndDate.AddDays(1), department: null, movementType: type, category: null, searchTerm: search, cancellationToken: token);
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var data = await _movements.GetPagedAsync(CurrentPage, PageSize, stockCardId: cardId, startDate: StartDate, endDate: EndDate.AddDays(1), department: null, movementType: type, category: null, searchTerm: search, cancellationToken: token);

            token.ThrowIfCancellationRequested();

            Movements.Clear();
            foreach (var h in data) Movements.Add(h);
            StatusText = $"{totalCount} hareketten {Movements.Count} tanesi listeleniyor (Sayfa {CurrentPage}/{TotalPages})";
        }
        catch (OperationCanceledException) 
        { 
            // Ignored, operation was intentionally cancelled
        }
        catch (Exception ex) 
        { 
            _logger.LogError("LoadMovements error", ex);
            StatusText = $"Hata: {ex.Message}"; 
        }
        finally { IsLoading = false; }
    }


    [RelayCommand]
    public void ClearFilters()
    {
        StartDate          = new DateTime(2024, 1, 1);
        EndDate            = DateTime.Today;
        SearchText         = "";
        SelectedStockIndex = 0;
        SelectedTypeIndex  = 0;
        CurrentPage        = 1;
        _ = LoadMovementsAsync();
    }

    [RelayCommand] public async Task NextPageAsync() { if (CurrentPage < TotalPages) { CurrentPage++; await LoadMovementsAsync(); } }
    [RelayCommand] public async Task PrevPageAsync() { if (CurrentPage > 1) { CurrentPage--; await LoadMovementsAsync(); } }

    partial void OnSearchTextChanged(string value) => _ = LoadMovementsAsync();

    [RelayCommand]
    public async Task DeleteMovementAsync()
    {
        if (SelectedMovement == null) return;
        
        bool confirm = await _dialogService.ShowConfirmAsync("Silme Onayı", "Bu hareketi silmek istediğinize emin misiniz?");
        if (!confirm) return;

        try
        {
            await Task.Run(() => _movements.Delete(SelectedMovement.Id));
            await LoadMovementsAsync();
            StatusText = "Hareket silindi.";
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Delete error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Silme işlemi başarısız: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DeleteBulkAsync()
    {
        if (SelectedMovements.Count < 2) return;

        bool confirm = await _dialogService.ShowConfirmAsync("Toplu Silme", $"{SelectedMovements.Count} adet hareketi silmek istediğinize emin misiniz?");
        if (!confirm) return;

        try
        {
            var ids = SelectedMovements.Select(m => m.Id).ToList();
            await Task.Run(() => _movements.DeleteBulk(ids));
            await LoadMovementsAsync();
            StatusText = $"{ids.Count} adet hareket silindi.";
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk delete error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Toplu silme başarısız: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task AddMovementAsync()
    {
        var vm = new AddMovementViewModel(_stockCards, _departments);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            await _movements.AddAsync(vm.Result);
            await LoadMovementsAsync();
            StatusText = "Hareket başarıyla eklendi.";
        }
    }

    [RelayCommand]
    public async Task EditMovementAsync()
    {
        if (SelectedMovement == null) return;
        
        var vm = new AddMovementViewModel(_stockCards, _departments, SelectedMovement);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            await Task.Run(() => _movements.Update(vm.Result));
            await LoadMovementsAsync();
            StatusText = "Hareket güncellendi.";
        }
    }

    [RelayCommand]
    public async Task BulkMovementAsync()
    {
        var vm = new BulkMovementViewModel(_movements, _departments, _stockCards, _dialogService, _logger);
        if (await _dialogService.ShowDialogAsync(vm))
        {
            await LoadMovementsAsync();
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string fileName = $"Stok_Hareketleri_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync("Excel'e Aktar", fileName, "Excel Dosyası (*.xlsx)|*.xlsx");
        
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = "Excel dosyası oluşturuluyor...";
            await Task.Run(() => 
            {
                var headers = new[] { "Tarih", "Kod", "Stok Adı", "Tür", "Miktar", "Departman", "Teslim Edilen", "Açıklama" };
                Infrastructure.ExcelService.ExportToExcel(path, "Stok Hareketleri", headers, Movements, h => new object?[]
                {
                    h.Tarih.ToString("dd.MM.yyyy HH:mm"),
                    h.StokKartKodNo,
                    h.StokKartAd,
                    h.DisplayTur,
                    h.Miktar,
                    h.Departman,
                    h.TeslimEdilen,
                    h.Aciklama
                });
            });
            StatusText = "Excel başarıyla kaydedildi.";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task ImportXlsxAsync()
    {
        string? path = await _dialogService.OpenFileAsync("Excel'den Aktar", "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = "Excel dosyası okunuyor...";
            int count = 0;
            await Task.Run(async () => 
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(path);
                var worksheet = workbook.Worksheet(1);
                
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null) return;

                int colCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                
                // Başlıklara göre sütun indekslerini bul (1-based)
                int colKod = 0, colTeslimEdilen = 0, colTur = 0, colMiktar = 0, colDepartman = 0, colTarih = 0, colAciklama = 0;

                for (int i = 1; i <= colCount; i++)
                {
                    string header = firstRow.Cell(i).GetValue<string>().Trim().ToLowerInvariant();
                    if (header == "stok kodu" || header == "kod" || header == "stokkodu" || header == "kodno" || header == "stokkodu")
                        colKod = i;
                    else if (header == "teslim edilen" || header == "teslim edilen" || header == "teslim alan" || header == "teslim edılen" || header == "personel" || header == "kisi" || header == "kişi")
                        colTeslimEdilen = i;
                    else if (header == "tür" || header == "tur" || header == "işlem" || header == "islem" || header == "tip" || header == "islem tipi")
                        colTur = i;
                    else if (header == "miktar" || header == "adet" || header == "sayı" || header == "sayi")
                        colMiktar = i;
                    else if (header == "departman" || header == "bölüm" || header == "bolum")
                        colDepartman = i;
                    else if (header == "tarih" || header == "işlem tarihi" || header == "islem tarihi")
                        colTarih = i;
                    else if (header == "açıklama" || header == "aciklama" || header == "not")
                        colAciklama = i;
                }

                // Eşleşme yoksa yedek plan (fallback - klasik yedek/şablon yapısı)
                if (colKod == 0 && colTur == 0)
                {
                    colKod = 1;
                    colTeslimEdilen = 3;
                    colTur = 4;
                    colMiktar = 5;
                    colDepartman = 6;
                    colTarih = 7;
                    colAciklama = 8;
                }

                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<ClosedXML.Excel.IXLRangeRow>();
                var movementsToImport = new List<StokHareketi>();
                
                foreach (var row in rows)
                {
                    string kod = colKod > 0 ? (row.Cell(colKod).GetValue<string>() ?? "").Trim() : "";
                    if (string.IsNullOrEmpty(kod)) continue;

                    var card = _allCards.FirstOrDefault(c => c.KodNo == kod);
                    if (card == null) continue;

                    string turRaw = colTur > 0 ? (row.Cell(colTur).GetValue<string>() ?? "").Trim() : "";
                    string tur = turRaw.Contains("[G]") || turRaw.ToLowerInvariant().Contains("giri") || turRaw.ToLowerInvariant().Contains("giris") ? "Giris" : "Cikis";

                    double miktar = colMiktar > 0 ? (row.Cell(colMiktar).TryGetValue<double>(out double m) ? m : 1.0) : 1.0;
                    string departman = colDepartman > 0 ? (row.Cell(colDepartman).GetValue<string>() ?? "").Trim() : "";
                    string teslimEdilen = colTeslimEdilen > 0 ? (row.Cell(colTeslimEdilen).GetValue<string>() ?? "").Trim() : "";
                    
                    DateTime tarih = DateTime.Now;
                    if (colTarih > 0)
                    {
                        var cellVal = row.Cell(colTarih).GetValue<string>();
                        if (DateTime.TryParse(cellVal, out var dt))
                        {
                            tarih = dt;
                        }
                    }
                    
                    string aciklama = colAciklama > 0 ? (row.Cell(colAciklama).GetValue<string>() ?? "").Trim() : "";

                    movementsToImport.Add(new StokHareketi
                    {
                        StokKartId = card.Id,
                        Tur = tur,
                        Miktar = miktar,
                        Departman = departman,
                        TeslimEdilen = teslimEdilen,
                        Tarih = tarih,
                        Aciklama = aciklama
                    });
                }

                if (movementsToImport.Count > 0)
                {
                    await _movements.AddBulkAsync(movementsToImport);
                    count = movementsToImport.Count;
                }
            });
            await LoadMovementsAsync();
            StatusText = $"{count} adet hareket içe aktarıldı.";
            await _dialogService.ShowMessageAsync("Başarılı", $"{count} adet hareket başarıyla içe aktarıldı.");
        }
        catch (Exception ex) { 
            _logger.LogError("Import error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"İçe aktarım başarısız: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ExportSampleAsync()
    {
        string? path = await _dialogService.SaveFileAsync("Örnek Dosyayı Kaydet", "stok_hareket_ornek.xlsx", "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            await Task.Run(() => 
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var ws = workbook.AddWorksheet("Örnek");
                string[] headers = { "Stok Kodu", "Stok Adı (Opsiyonel)", "Teslim Edilen", "Tür ([G] Giriş / [Ç] Çıkış)", "Miktar", "Departman", "Tarih (dd.MM.yyyy)", "Açıklama" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(30, 37, 52);
                    ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                }
                
                // Örnek Giriş satırı
                ws.Cell(2, 1).Value = "001";
                ws.Cell(2, 2).Value = "Örnek Malzeme";
                ws.Cell(2, 3).Value = "Ahmet Yılmaz";
                ws.Cell(2, 4).Value = "[G] Giriş";
                ws.Cell(2, 5).Value = 10;
                ws.Cell(2, 6).Value = "Bilgi İşlem";
                ws.Cell(2, 7).Value = DateTime.Now.ToString("dd.MM.yyyy");
                ws.Cell(2, 8).Value = "Yeni alım";

                // Örnek Çıkış satırı
                ws.Cell(3, 1).Value = "001";
                ws.Cell(3, 2).Value = "Örnek Malzeme";
                ws.Cell(3, 3).Value = "Mehmet Demir";
                ws.Cell(3, 4).Value = "[Ç] Çıkış";
                ws.Cell(3, 5).Value = 3;
                ws.Cell(3, 6).Value = "Muhasebe";
                ws.Cell(3, 7).Value = DateTime.Now.ToString("dd.MM.yyyy");
                ws.Cell(3, 8).Value = "Departman talebi";
                
                ws.Columns().AdjustToContents();
                workbook.SaveAs(path);
            });
            StatusText = "Örnek dosya oluşturuldu.";
        }
        catch (Exception ex) { await _dialogService.ShowMessageAsync("Hata", ex.Message); }
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        try
        {
            StatusText = "Yazdırma hazırlanıyor...";
            var sb = new System.Text.StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'><title>Stok Hareket Raporu</title>");
            sb.Append("<style>");
            sb.Append("@page { size: landscape; margin: 0.5cm; } ");
            sb.Append("body { font-family: 'Segoe UI', Arial, sans-serif; padding: 10px; color: #1a1a1a; line-height: 1.2; } ");
            sb.Append(".top-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #2563EB; padding-bottom: 8px; margin-bottom: 15px; } ");
            sb.Append(".top-header h1 { margin: 0; color: #2563EB; font-size: 20px; font-weight: 800; } ");
            sb.Append(".date-box { text-align: right; font-size: 11px; color: #4b5563; } ");
            sb.Append("table { width: 100%; border-collapse: collapse; font-size: 11px; table-layout: fixed; } ");
            sb.Append("th, td { border: 1px solid #666; padding: 6px 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; } ");
            sb.Append("th { background: #f1f5f9; font-weight: bold; text-align: center; } ");
            sb.Append(".num { text-align: center; } ");
            sb.Append(".bold { font-weight: bold; } ");
            sb.Append(".green { color: #10B981; } ");
            sb.Append(".red { color: #EF4444; } ");
            sb.Append(".footer { margin-top: 20px; font-size: 10px; text-align: right; color: #94a3b8; } ");
            sb.Append("</style>");
            sb.Append("<script>window.onload = function() { window.print(); }</script>");
            sb.Append("</head><body>");
            
            sb.Append("<div class='top-header'>");
            sb.Append("<h1>STOK HAREKET RAPORU</h1>");
            sb.Append($"<div class='date-box'>Rapor Tarihi:<br/><b>{DateTime.Now:dd.MM.yyyy HH:mm}</b></div>");
            sb.Append("</div>");

            sb.Append("<table><thead><tr>");
            sb.Append("<th style='width: 8%;'>Kod</th>");
            sb.Append("<th style='width: 32%; text-align: left;'>Stok Adı</th>");
            sb.Append("<th style='width: 7%;'>Miktar</th>");
            sb.Append("<th style='width: 6%;'>Tür</th>");
            sb.Append("<th style='width: 15%;'>Departman</th>");
            sb.Append("<th style='width: 15%;'>Teslim Alan</th>");
            sb.Append("<th style='width: 17%;'>Tarih</th>");
            sb.Append("</tr></thead><tbody>");
            
            foreach (var h in Movements)
            {
                string clr = h.Tur == "Giris" ? "green" : "red";
                sb.Append("<tr>");
                sb.Append($"<td class='num'>{h.StokKartKodNo}</td>");
                sb.Append($"<td>{h.StokKartAd}</td>");
                sb.Append($"<td class='num bold {clr}'>{h.Miktar}</td>");
                sb.Append($"<td class='num bold {clr}'>{h.DisplayTur}</td>");
                sb.Append($"<td>{h.Departman}</td>");
                sb.Append($"<td>{h.TeslimEdilen}</td>");
                sb.Append($"<td>{h.Tarih:dd.MM.yyyy HH:mm}</td>");
                sb.Append("</tr>");
            }
            
            sb.Append("</tbody></table>");
            sb.Append($"<div class='footer'>Toplam {Movements.Count} kayıt listelenmiştir.</div>");
            sb.Append("</body></html>");
            
            var previewVm = new PrintPreviewViewModel("Stok Hareket Raporu", sb.ToString());
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = "Yazdırma tamamlandı.";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }
}
