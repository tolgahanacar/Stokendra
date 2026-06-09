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

public partial class ServicesViewModel : ViewModelBase
{
    private readonly IServiceRecordRepository _services;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    private System.Threading.CancellationTokenSource? _cts;
    private System.Threading.CancellationTokenSource? _searchCts;

    [ObservableProperty] private string      _searchText  = "";
    [ObservableProperty] private DateTime    _startDate   = new DateTime(2024, 1, 1);
    [ObservableProperty] private DateTime    _endDate     = DateTime.Today;
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool        _isLoading;
    [ObservableProperty] private string      _statusText  = "";
    [ObservableProperty] private ServisKaydi? _selectedRecord;
    
    public bool IsNotLoading => !IsLoading;

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    private const int PageSize = 20;

    public bool IsRecordSelected => SelectedRecord != null;
    partial void OnSelectedRecordChanged(ServisKaydi? value) => OnPropertyChanged(nameof(IsRecordSelected));

    public ObservableCollection<ServisKaydi> Records { get; } = new();

    public ServicesViewModel(IServiceRecordRepository services, IDialogService dialogService, ILogger logger)
    {
        _services = services;
        _dialogService = dialogService;
        _logger = logger;
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
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new System.Threading.CancellationTokenSource();
        var token = _cts.Token;

        IsLoading = true;
        try
        {
            var term = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            
            var totalCount = await _services.GetCountAsync(StartDate, EndDate.AddDays(1), term, token);
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var data = await _services.GetPagedAsync(CurrentPage, PageSize, StartDate, EndDate.AddDays(1), term, token);

            token.ThrowIfCancellationRequested();

            Records.Clear();
            foreach (var r in data) Records.Add(r);
            StatusText = $"{totalCount} kayıttan {Records.Count} tanesi listeleniyor (Sayfa {CurrentPage}/{TotalPages})";
        }
        catch (OperationCanceledException)
        {
            // Ignored
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Services load error", ex);
            StatusText = "Yükleme hatası.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearFilters()
    {
        SearchText = "";
        StartDate  = new DateTime(2000, 1, 1);
        EndDate    = DateTime.Today;
        CurrentPage = 1;
        _ = LoadAsync();
    }

    [RelayCommand] public async Task NextPageAsync() { if (CurrentPage < TotalPages) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] public async Task PrevPageAsync() { if (CurrentPage > 1) { CurrentPage--; await LoadAsync(); } }

    partial void OnSearchTextChanged(string value) => ScheduleSearch();

    private async void ScheduleSearch()
    {
        _cts?.Cancel();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(300, token);
            CurrentPage = 1;
            await LoadAsync();
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedRecord == null) return;
        
        bool confirm = await _dialogService.ShowConfirmAsync("Silme Onayı", 
            $"{SelectedRecord.CihazAdi} kaydını silmek istediğinize emin misiniz?");
            
        if (!confirm) return;

        try
        {
            await _services.DeleteAsync(SelectedRecord.Id);
            await LoadAsync();
            StatusText = "Kayıt silindi.";
        }
        catch (Exception ex) { await _dialogService.ShowMessageAsync("Hata", $"Silme hatası: {ex.Message}"); }
    }

    [RelayCommand]
    public async Task AddAsync()
    {
        var vm = new AddServiceViewModel();
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            await _services.AddAsync(vm.Result);
            await LoadAsync();
            StatusText = "Servis kaydı eklendi.";
        }
    }

    [RelayCommand]
    public async Task EditAsync()
    {
        if (SelectedRecord == null) return;
        var vm = new AddServiceViewModel(SelectedRecord);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            await _services.UpdateAsync(vm.Result);
            await LoadAsync();
            StatusText = "Servis kaydı güncellendi.";
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string fileName = $"Servis_Kayitlari_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync("Excel Kaydet", fileName, "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = "Excel oluşturuluyor...";
            await Task.Run(() => {
                var headers = new[] { "Tarih", "Cihaz Adı", "Seri No", "Firma", "Sorun", "Sonuç" };
                Infrastructure.ExcelService.ExportToExcel(path, "Servis Kayıtları", headers, Records, s => new object?[] {
                    s.BakimTarihi.ToString("dd.MM.yyyy"), s.CihazAdi, s.SeriNumarasi, s.Firma, s.Sorun, s.Sonuc
                });
            });
            StatusText = "Excel başarıyla kaydedildi.";
            await _dialogService.ShowMessageAsync("Başarılı", "Excel dosyası başarıyla kaydedildi.");
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Excel export error", ex);
            StatusText = $"Hata: {ex.Message}"; 
        }
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        string? path = await _dialogService.OpenFileAsync("Excel Seç", "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = "Excel okunuyor...";
            await Task.Run(async () => {
                using var workbook = new ClosedXML.Excel.XLWorkbook(path);
                var worksheet = workbook.Worksheet(1);
                
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null) return;

                int colCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                // Başlıklara göre sütun indekslerini bul (1-based)
                int colTarih = 0, colCihaz = 0, colSeri = 0, colFirma = 0, colSorun = 0, colSonuc = 0;

                for (int i = 1; i <= colCount; i++)
                {
                    string header = firstRow.Cell(i).GetValue<string>().Trim().ToTurkishLower();
                    if (header == "bakım tarihi" || header == "bakim tarihi" || header == "tarih" || header == "bakimtarihi")
                        colTarih = i;
                    else if (header == "cihaz adı" || header == "cihaz adi" || header == "cihaz" || header == "cihazadi")
                        colCihaz = i;
                    else if (header == "seri numarası" || header == "seri numarasi" || header == "seri no" || header == "serino")
                        colSeri = i;
                    else if (header == "firma" || header == "şirket" || header == "sirket")
                        colFirma = i;
                    else if (header == "sorun" || header == "arıza" || header == "ariza")
                        colSorun = i;
                    else if (header == "sonuç" || header == "sonuc" || header == "durum")
                        colSonuc = i;
                }

                // Eşleşme yoksa yedek plan (fallback)
                if (colTarih == 0 && colCihaz == 0)
                {
                    colTarih = 1;
                    colCihaz = 2;
                    colSeri = 3;
                    colFirma = 4;
                    colSorun = 5;
                    colSonuc = 6;
                }

                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<ClosedXML.Excel.IXLRangeRow>();
                var toImport = new List<ServisKaydi>();
                
                foreach (var row in rows)
                {
                    string cihaz = colCihaz > 0 ? (row.Cell(colCihaz).GetValue<string>() ?? "").Trim() : "";
                    if (string.IsNullOrEmpty(cihaz)) continue;

                    DateTime tarih = DateTime.Now;
                    if (colTarih > 0)
                    {
                        var cellVal = row.Cell(colTarih).GetValue<string>();
                        if (DateTime.TryParse(cellVal, out var dt))
                        {
                            tarih = dt;
                        }
                    }

                    string seri = colSeri > 0 ? (row.Cell(colSeri).GetValue<string>() ?? "").Trim() : "";
                    string firma = colFirma > 0 ? (row.Cell(colFirma).GetValue<string>() ?? "").Trim() : "";
                    string sorun = colSorun > 0 ? (row.Cell(colSorun).GetValue<string>() ?? "").Trim() : "";
                    string sonuc = colSonuc > 0 ? (row.Cell(colSonuc).GetValue<string>() ?? "").Trim() : "";

                    toImport.Add(new ServisKaydi {
                        BakimTarihi = tarih,
                        CihazAdi = cihaz,
                        SeriNumarasi = seri,
                        Firma = firma,
                        Sorun = sorun,
                        Sonuc = sonuc
                    });
                }
                
                if (toImport.Count > 0) await _services.AddBulkAsync(toImport);
            });
            await LoadAsync();
            StatusText = "İçeri aktarım tamamlandı.";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public async Task PrintAsync()
    {
        try
        {
            if (Records.Count == 0) return;
            StatusText = "Yazdırma hazırlanıyor...";
            
            var sb = new System.Text.StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'><title>Servis Kayıt Raporu</title>");
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
            sb.Append(".footer { margin-top: 20px; font-size: 10px; text-align: right; color: #94a3b8; } ");
            sb.Append("</style>");
            sb.Append("<script>window.onload = function() { window.print(); }</script>");
            sb.Append("</head><body>");
            
            sb.Append("<div class='top-header'>");
            sb.Append("<h1>SERVİS KAYIT RAPORU</h1>");
            sb.Append($"<div class='date-box'>Rapor Tarihi:<br/><b>{DateTime.Now:dd.MM.yyyy HH:mm}</b></div>");
            sb.Append("</div>");

            sb.Append("<table><thead><tr>");
            sb.Append("<th style='width: 15%;'>Tarih</th>");
            sb.Append("<th style='width: 25%;'>Cihaz Adı</th>");
            sb.Append("<th style='width: 20%;'>Seri No</th>");
            sb.Append("<th style='width: 20%;'>Firma</th>");
            sb.Append("<th style='width: 20%;'>Sonuç</th>");
            sb.Append("</tr></thead><tbody>");
            
            foreach (var r in Records)
            {
                sb.Append("<tr>");
                sb.Append($"<td class='num'>{r.BakimTarihi:dd.MM.yyyy}</td>");
                sb.Append($"<td>{r.CihazAdi}</td>");
                sb.Append($"<td>{r.SeriNumarasi}</td>");
                sb.Append($"<td>{r.Firma}</td>");
                sb.Append($"<td>{r.Sonuc}</td>");
                sb.Append("</tr>");
            }
            
            sb.Append("</tbody></table>");
            sb.Append($"<div class='footer'>Toplam {Records.Count} kayıt listelenmiştir.</div>");
            sb.Append("</body></html>");
            
            var previewVm = new PrintPreviewViewModel("Servis Kayıt Raporu", sb.ToString());
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = "Yazdırma tamamlandı.";
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Print error", ex);
            StatusText = $"Hata: {ex.Message}"; 
        }
    }
}
