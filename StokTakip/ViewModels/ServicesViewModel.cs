using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

public partial class ServicesViewModel : ViewModelBase
{
    private readonly IServiceRecordRepository _services;

    [ObservableProperty] private string      _searchText  = "";
    [ObservableProperty] private DateTime    _startDate   = new DateTime(2010, 1, 1);
    [ObservableProperty] private DateTime    _endDate     = DateTime.Today;
    [ObservableProperty] private bool        _isLoading;
    [ObservableProperty] private string      _statusText  = "";
    [ObservableProperty] private ServisKaydi? _selectedRecord;

    public bool IsRecordSelected => SelectedRecord != null;
    partial void OnSelectedRecordChanged(ServisKaydi? value) => OnPropertyChanged(nameof(IsRecordSelected));

    public Func<AddServiceViewModel, Task<bool>>? ShowDialogAction { get; set; }
    public Func<string, string, Task<string?>>? SaveFileAction { get; set; }
    public Func<string, Task<string?>>? OpenFileAction { get; set; }

    public ObservableCollection<ServisKaydi> Records { get; } = new();

    public ServicesViewModel(IServiceRecordRepository services)
    {
        _services = services;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var term = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var data = await Task.Run(() =>
                _services.GetAll(StartDate, EndDate.AddDays(1), term));

            Records.Clear();
            foreach (var r in data) Records.Add(r);
            StatusText = $"{data.Count} kayıt";
        }
        catch (Exception ex) { Console.WriteLine($"Services load error: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearFilters()
    {
        SearchText = "";
        StartDate  = new DateTime(2020, 1, 1);
        EndDate    = DateTime.Today;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedRecord == null) return;
        try
        {
            await Task.Run(() => _services.Delete(SelectedRecord.Id));
            await LoadAsync();
        }
        catch (Exception ex) { Console.WriteLine($"Delete service error: {ex.Message}"); }
    }

    [RelayCommand]
    public async Task AddAsync()
    {
        if (ShowDialogAction == null) return;
        var vm = new AddServiceViewModel();
        if (await ShowDialogAction(vm) && vm.Result != null)
        {
            await Task.Run(() => _services.Add(vm.Result));
            await LoadAsync();
            StatusText = "Servis kaydı eklendi.";
        }
    }

    [RelayCommand]
    public async Task EditAsync()
    {
        if (SelectedRecord == null || ShowDialogAction == null) return;
        var vm = new AddServiceViewModel(SelectedRecord);
        if (await ShowDialogAction(vm) && vm.Result != null)
        {
            await Task.Run(() => _services.Update(vm.Result));
            await LoadAsync();
            StatusText = "Servis kaydı güncellendi.";
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        if (SaveFileAction == null) return;
        string fileName = $"Servis_Kayitlari_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await SaveFileAction(fileName, "Excel Dosyası (*.xlsx)|*.xlsx");
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
            StatusText = "Excel okunuyor...";
            await Task.Run(() => {
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
                if (toImport.Count > 0) _services.AddBulk(toImport);
            });
            await LoadAsync();
            StatusText = "İçeri aktarım tamamlandı.";
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();
}
