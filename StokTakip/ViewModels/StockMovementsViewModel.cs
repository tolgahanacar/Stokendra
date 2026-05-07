using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

public partial class StockMovementsViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IStockCardRepository _stockCards;
    private readonly IDepartmentRepository _departments;

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
    partial void OnSelectedMovementChanged(StokHareketi? value) => OnPropertyChanged(nameof(IsMovementSelected));

    public Func<AddMovementViewModel, Task<bool>>? ShowDialogAction { get; set; }
    public Func<string, string, Task<string?>>? SaveFileAction { get; set; }
    public Func<string, Task<string?>>? OpenFileAction { get; set; }

    public ObservableCollection<StokHareketi> Movements { get; } = new();
    public ObservableCollection<string>       StockItems { get; } = new();
    public ObservableCollection<string>       TypeItems  { get; } = new() { "Tümü", "Giriş", "Çıkış", "Boş" };

    private List<StokKarti>   _allCards     = new();
    private List<StokHareketi> _allMovements = new();

    public StockMovementsViewModel(
        IMovementRepository movements,
        IStockCardRepository stockCards,
        IDepartmentRepository departments)
    {
        _movements   = movements;
        _stockCards  = stockCards;
        _departments = departments;
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
        catch (Exception ex) { Console.WriteLine($"Movement load error: {ex.Message}"); }
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
        try
        {
            await Task.Run(() => _movements.Delete(SelectedMovement.Id));
            await LoadMovementsAsync();
        }
        catch (Exception ex) { Console.WriteLine($"Delete error: {ex.Message}"); }
    }

    [RelayCommand]
    public async Task AddMovementAsync()
    {
        if (ShowDialogAction == null) return;
        
        var vm = new AddMovementViewModel(_stockCards, _departments);
        if (await ShowDialogAction(vm) && vm.Result != null)
        {
            await Task.Run(() => _movements.Add(vm.Result));
            await LoadMovementsAsync();
            StatusText = "Hareket başarıyla eklendi.";
        }
    }

    [RelayCommand]
    public async Task EditMovementAsync()
    {
        if (SelectedMovement == null || ShowDialogAction == null) return;
        
        var vm = new AddMovementViewModel(_stockCards, _departments, SelectedMovement);
        if (await ShowDialogAction(vm) && vm.Result != null)
        {
            await Task.Run(() => _movements.Update(vm.Result));
            await LoadMovementsAsync();
            StatusText = "Hareket güncellendi.";
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        if (SaveFileAction == null) return;

        string fileName = $"Stok_Hareketleri_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await SaveFileAction(fileName, "Excel Dosyası (*.xlsx)|*.xlsx");
        
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
                    h.Tur,
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
    public async Task ImportAsync()
    {
        if (OpenFileAction == null) return;
        
        string? path = await OpenFileAction("Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = "Excel dosyası okunuyor...";
            await Task.Run(async () => 
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(path);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Header'ı geç

                var movementsToImport = new List<StokHareketi>();
                foreach (var row in rows)
                {
                    string kod = row.Cell(1).GetValue<string>();
                    var card = _allCards.FirstOrDefault(c => c.KodNo == kod);
                    if (card == null) continue;

                    movementsToImport.Add(new StokHareketi
                    {
                        StokKartId = card.Id,
                        Tur = row.Cell(4).GetValue<string>().Contains("Giris") ? "Giris" : "Cikis",
                        Miktar = row.Cell(5).GetValue<double>(),
                        Departman = row.Cell(6).GetValue<string>(),
                        TeslimEdilen = row.Cell(7).GetValue<string>(),
                        Tarih = DateTime.TryParse(row.Cell(8).GetValue<string>(), out var dt) ? dt : DateTime.Now,
                        Aciklama = row.Cell(9).GetValue<string>()
                    });
                }

                if (movementsToImport.Count > 0)
                {
                    await _movements.AddBulkAsync(movementsToImport);
                }
            });
            await LoadMovementsAsync();
            StatusText = "İçe aktarım başarıyla tamamlandı.";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        try
        {
            StatusText = "Yazdırma hazırlanıyor...";
            string tempPath = Path.Combine(Path.GetTempPath(), $"Stok_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.html");
            
            var sb = new System.Text.StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'><title>Stok Hareket Raporu</title>");
            sb.Append("<style>");
            sb.Append("@page { size: landscape; margin: 1cm; } ");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 10px; color: #333; } ");
            sb.Append("table { width: 100%; border-collapse: collapse; margin-top: 20px; table-layout: fixed; } ");
            sb.Append("th, td { border: 1px solid #999; padding: 10px; text-align: left; font-size: 12px; word-wrap: break-word; } ");
            sb.Append("th { background: #f0f0f0; font-weight: bold; } ");
            sb.Append(".header { text-align: center; border-bottom: 2px solid #2563EB; padding-bottom: 10px; margin-bottom: 20px; } ");
            sb.Append(".header h1 { margin: 0; color: #2563EB; font-size: 24px; } ");
            sb.Append(".footer { margin-top: 30px; font-size: 10px; text-align: right; color: #777; } ");
            sb.Append("</style>");
            sb.Append("</head><body>");
            
            sb.Append("<div class='header'>");
            sb.Append("<h1>STOK HAREKET RAPORU</h1>");
            sb.Append($"<p>Oluşturma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.Append("</div>");

            sb.Append("<table><thead><tr>");
            sb.Append("<th style='width: 35%;'>Stok Adı</th>");
            sb.Append("<th style='width: 7%; text-align: center;'>Miktar</th>");
            sb.Append("<th style='width: 8%; text-align: center;'>Tür</th>");
            sb.Append("<th style='width: 18%;'>Departman</th>");
            sb.Append("<th style='width: 17%;'>Teslim Alan</th>");
            sb.Append("<th style='width: 15%;'>Tarih</th>");
            sb.Append("</tr></thead><tbody>");
            
            foreach (var h in Movements)
            {
                string turText = h.Tur switch { "Giris" => "Giriş", "Cikis" => "Çıkış", _ => "-" };
                sb.Append("<tr>");
                sb.Append($"<td>{h.StokKartAd}</td>");
                sb.Append($"<td style='text-align: center;'>{h.Miktar}</td>");
                sb.Append($"<td style='text-align: center;'>{turText}</td>");
                sb.Append($"<td>{h.Departman}</td>");
                sb.Append($"<td>{h.TeslimEdilen}</td>");
                sb.Append($"<td>{h.Tarih:dd.MM.yyyy HH:mm}</td>");
                sb.Append("</tr>");
            }
            
            sb.Append("</tbody></table>");
            sb.Append($"<div class='footer'>Stokendra - {DateTime.Now.Year} | Toplam Kayıt: {Movements.Count}</div>");
            sb.Append("</body></html>");
            
            await File.WriteAllTextAsync(tempPath, sb.ToString());
            
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            
            StatusText = "Rapor tarayıcıda açıldı. Yazdırmak için Ctrl+P kullanın (Yatay Seçmeyi Unutmayın).";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }
}
