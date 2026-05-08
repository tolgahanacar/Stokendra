using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Infrastructure;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

public partial class StockMovementsViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IStockCardRepository _stockCards;
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    // Filtreler
    [ObservableProperty] private DateTime _startDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime _endDate   = DateTime.Today;
    [ObservableProperty] private string   _searchText = "";
    [ObservableProperty] private int      _selectedStockIndex = 0;
    [ObservableProperty] private int      _selectedTypeIndex  = 0;

    // Durum
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private StokHareketi? _selectedMovement;

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

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        _allCards = await _stockCards.GetChildCardsAsync();
        StockItems.Clear();
        StockItems.Add("Tümü");
        foreach (var k in _allCards) StockItems.Add($"{k.KodNo} - {k.Ad}");
        await LoadMovementsAsync();
    }

    [RelayCommand]
    public async Task LoadMovementsAsync()
    {
        IsLoading = true;
        try
        {
            int? cardId = SelectedStockIndex > 0 ? _allCards[SelectedStockIndex - 1].Id : null;
            string? tur = SelectedTypeIndex switch
            {
                1 => "Giris",
                2 => "Cikis",
                3 => "Bos",
                _ => null
            };

            _allMovements = await _movements.GetAllAsync(
                stockCardId: cardId,
                startDate: StartDate,
                endDate: EndDate.AddDays(1),
                movementType: tur);

            ApplySearch();
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Movement load error", ex);
            StatusText = $"Hata: {ex.Message}"; 
        }
        finally { IsLoading = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        var term = SearchText.Trim().ToLowerInvariant();
        var data = string.IsNullOrEmpty(term)
            ? _allMovements
            : _allMovements.Where(h =>
                h.StokKartAd.ToLowerInvariant().Contains(term) ||
                h.StokKartKodNo.ToLowerInvariant().Contains(term) ||
                h.TeslimEdilen.ToLowerInvariant().Contains(term) ||
                h.Departman.ToLowerInvariant().Contains(term)).ToList();

        Movements.Clear();
        foreach (var h in data) Movements.Add(h);
        StatusText = $"{data.Count} hareket";
    }

    [RelayCommand]
    public void ClearFilters()
    {
        StartDate          = DateTime.Today.AddMonths(-1);
        EndDate            = DateTime.Today;
        SearchText         = "";
        SelectedStockIndex = 0;
        SelectedTypeIndex  = 0;
        _ = LoadMovementsAsync();
    }

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
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); 

                var movementsToImport = new List<StokHareketi>();
                foreach (var row in rows)
                {
                    string kod = row.Cell(1).GetValue<string>();
                    var card = _allCards.FirstOrDefault(c => c.KodNo == kod);
                    if (card == null) continue;

                    string turRaw = row.Cell(4).GetValue<string>();
                    string tur = turRaw.Contains("[G]") || turRaw.ToLower().Contains("giris") ? "Giris" : "Cikis";

                    movementsToImport.Add(new StokHareketi
                    {
                        StokKartId = card.Id,
                        Tur = tur,
                        Miktar = row.Cell(5).GetValue<double>(),
                        Departman = row.Cell(6).GetValue<string>(),
                        TeslimEdilen = row.Cell(3).GetValue<string>(),
                        Tarih = DateTime.TryParse(row.Cell(7).GetValue<string>(), out var dt) ? dt : DateTime.Now,
                        Aciklama = row.Cell(8).GetValue<string>()
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
                var ws = workbook.Worksheet(1);
                string[] headers = { "Stok Kodu", "Stok Adı (Opsiyonel)", "Teslim Edilen", "Tür ([G] Giriş / [Ç] Çıkış)", "Miktar", "Departman", "Tarih (dd.MM.yyyy)", "Açıklama" };
                for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                
                ws.Cell(2, 1).Value = "001";
                ws.Cell(2, 4).Value = "[G] Giriş";
                ws.Cell(2, 5).Value = 10;
                ws.Cell(2, 7).Value = DateTime.Now.ToString("dd.MM.yyyy");
                
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
