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

    [ObservableProperty] private string      _searchText  = "";
    [ObservableProperty] private DateTime    _startDate   = new DateTime(2000, 1, 1);
    [ObservableProperty] private DateTime    _endDate     = DateTime.Today;
    [ObservableProperty] private bool        _isLoading;
    [ObservableProperty] private string      _statusText  = "";
    [ObservableProperty] private ServisKaydi? _selectedRecord;

    public bool IsRecordSelected => SelectedRecord != null;
    partial void OnSelectedRecordChanged(ServisKaydi? value) => OnPropertyChanged(nameof(IsRecordSelected));

    public ObservableCollection<ServisKaydi> Records { get; } = new();

    public ServicesViewModel(IServiceRecordRepository services, IDialogService dialogService, ILogger logger)
    {
        _services = services;
        _dialogService = dialogService;
        _logger = logger;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var term = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var data = await _services.GetAllAsync(StartDate, EndDate.AddDays(1), term);

            Records.Clear();
            foreach (var r in data) Records.Add(r);
            StatusText = $"{data.Count} kayıt listeleniyor";
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Services load error", ex);
            StatusText = "Yükleme hatası.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearFilters()
    {
        SearchText = "";
        StartDate  = new DateTime(2000, 1, 1);
        EndDate    = DateTime.Today;
        _ = LoadAsync();
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
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1);
                var toImport = new List<ServisKaydi>();
                foreach (var row in rows)
                {
                    toImport.Add(new ServisKaydi {
                        BakimTarihi = DateTime.TryParse(row.Cell(1).GetValue<string>(), out var dt) ? dt : DateTime.Now,
                        CihazAdi = row.Cell(2).GetValue<string>(),
                        SeriNumarasi = row.Cell(3).GetValue<string>(),
                        Firma = row.Cell(4).GetValue<string>(),
                        Sorun = row.Cell(5).GetValue<string>(),
                        Sonuc = row.Cell(6).GetValue<string>()
                    });
                }
                if (toImport.Count > 0) await _services.AddBulkAsync(toImport);
            });
            await LoadAsync();
            StatusText = "İçeri aktarım tamamlandı.";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();
}
