using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class ServicesViewModel : ViewModelBase
{
    private readonly IServiceRecordRepository _services;
    private readonly IDialogService _dialogService;
    private System.Threading.CancellationTokenSource? _cts;
    private System.Threading.CancellationTokenSource? _searchCts;

    [ObservableProperty] private string      _searchText  = "";
    [ObservableProperty] private DateTime    _startDate   = new DateTime(2024, 1, 1);
    [ObservableProperty] private DateTime    _endDate     = DateTime.Today;
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool        _isLoading;
    [ObservableProperty] private string      _statusText  = "";
    [ObservableProperty] private ServiceRecord? _selectedRecord;
    
    public bool IsNotLoading => !IsLoading;

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    private const int PageSize = 20;

    public bool IsRecordSelected => SelectedRecord != null || SelectedRecords.Count > 0;
    partial void OnSelectedRecordChanged(ServiceRecord? value) => OnPropertyChanged(nameof(IsRecordSelected));

    public BulkObservableCollection<ServiceRecord> Records { get; } = new();
    public ObservableCollection<ServiceRecord> SelectedRecords { get; } = new();

    public ServicesViewModel(IServiceRecordRepository services, IDialogService dialogService)
    {
        _services = services;
        _dialogService = dialogService;
        SelectedRecords.CollectionChanged += (s, e) => {
            OnPropertyChanged(nameof(IsRecordSelected));
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
            AppLogger.LogError("Load error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}";
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
            Records.AddRange(data);
            StatusText = string.Format(LocalizationManager.L("svc_status_listing"), totalCount, Records.Count, CurrentPage, TotalPages);
        }
        catch (OperationCanceledException)
        {
            // Ignored
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("Services load error", ex);
            StatusText = LocalizationManager.L("svc_load_error");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearFilters()
    {
        _searchCts?.Cancel();
        _cts?.Cancel();
        SearchText  = "";
        StartDate   = new DateTime(2024, 1, 1);
        EndDate     = DateTime.Today;
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
        if (SelectedRecords.Count >= 2)
        {
            int count = SelectedRecords.Count;
            bool confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_bulk_delete_title"), 
                LocalizationManager.L("confirm_bulk_service_delete", count)
            );
            if (!confirm) return;

            try
            {
                await _services.DeleteBulkAsync(SelectedRecords.Select(r => r.Id).ToList());
                await LoadAsync();
                StatusText = LocalizationManager.L("svc_record_deleted");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Bulk delete service error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("svc_delete_error"), ex.Message));
            }
        }
        else
        {
            var itemToDelete = SelectedRecord ?? SelectedRecords.FirstOrDefault();
            if (itemToDelete == null) return;
            
            bool confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_delete_title"), 
                LocalizationManager.L("confirm_service_delete")
            );
                
            if (!confirm) return;

            try
            {
                await _services.DeleteAsync(itemToDelete.Id);
                await LoadAsync();
                StatusText = LocalizationManager.L("svc_record_deleted");
            }
            catch (Exception ex) 
            { 
                AppLogger.LogError("Delete service error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("svc_delete_error"), ex.Message)); 
            }
        }
    }

    [RelayCommand]
    public async Task AddAsync()
    {
        var vm = new AddServiceViewModel();
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _services.AddAsync(vm.Result);
                await LoadAsync();
                StatusText = LocalizationManager.L("svc_record_added");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Add service error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task EditAsync()
    {
        if (SelectedRecords.Count >= 2)
        {
            var vm = new BulkEditServicesViewModel(SelectedRecords.ToList(), _services, _dialogService);
            if (await _dialogService.ShowDialogAsync(vm) == true)
            {
                await LoadAsync();
                StatusText = LocalizationManager.L("bulk_edit_success", SelectedRecords.Count);
            }
        }
        else
        {
            var itemToEdit = SelectedRecord ?? SelectedRecords.FirstOrDefault();
            if (itemToEdit == null) return;
            var vm = new AddServiceViewModel(itemToEdit);
            if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
            {
                try
                {
                    await _services.UpdateAsync(vm.Result);
                    await LoadAsync();
                    StatusText = LocalizationManager.L("svc_record_updated");
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Edit service error", ex);
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
                }
            }
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string baseName = LocalizationManager.L("export_fn_services");
        string fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync(LocalizationManager.L("svc_excel_save_title"), fileName, LocalizationManager.L("svc_excel_filter"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("svc_excel_saving");
            await Task.Run(() => {
                var headers = new[] { 
                    LocalizationManager.L("date"), 
                    LocalizationManager.L("device_name"), 
                    LocalizationManager.L("serial_number"), 
                    LocalizationManager.L("company"), 
                    LocalizationManager.L("problem"), 
                    LocalizationManager.L("result") 
                };
                Infrastructure.ExcelService.ExportToExcel(path, LocalizationManager.L("services"), headers, Records, s => new object?[] {
                    s.ServiceDate.ToString("dd.MM.yyyy"), s.DeviceName, s.SerialNumber, s.Company, s.Issue, s.Result
                });
            });
            StatusText = LocalizationManager.L("svc_excel_saved");
            await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("svc_excel_saved_dialog"));
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("Excel export error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; 
        }
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        string? path = await _dialogService.OpenFileAsync(LocalizationManager.L("svc_excel_select_title"), LocalizationManager.L("svc_excel_filter"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("svc_import_reading");
            await Task.Run(async () => {
                var mappings = new Dictionary<string, string[]>
                {
                    { "date", new[] { "bakım tarihi", "bakim tarihi", "tarih", "bakimtarihi", "date", "service date", "service_date" } },
                    { "device", new[] { "cihaz adı", "cihaz adi", "cihaz", "cihazadi", "device name", "device_name" } },
                    { "serial", new[] { "seri numarası", "seri numarasi", "seri no", "serino", "serial number", "serial_number" } },
                    { "company", new[] { "firma", "şirket", "sirket", "company" } },
                    { "issue", new[] { "sorun", "arıza", "ariza", "problem", "issue" } },
                    { "result", new[] { "sonuç", "sonuc", "durum", "result" } }
                };

                var fallback = new Dictionary<string, int>
                {
                    { "date", 1 }, { "device", 2 }, { "serial", 3 },
                    { "company", 4 }, { "issue", 5 }, { "result", 6 }
                };

                var toImport = Stokendra.Infrastructure.ExcelImportHelper.ImportData(path, mappings, fallback, (row, col) => 
                {
                    string cihaz = col["device"] > 0 ? (row.Cell(col["device"]).GetValue<string>() ?? "").Trim() : "";
                    if (string.IsNullOrEmpty(cihaz)) return null;

                    DateTime tarih = DateTime.Now;
                    if (col["date"] > 0)
                    {
                        var cellVal = row.Cell(col["date"]).GetValue<string>();
                        if (DateTime.TryParse(cellVal, out var dt)) tarih = dt;
                    }

                    string seri = col["serial"] > 0 ? (row.Cell(col["serial"]).GetValue<string>() ?? "").Trim() : "";
                    string firma = col["company"] > 0 ? (row.Cell(col["company"]).GetValue<string>() ?? "").Trim() : "";
                    string sorun = col["issue"] > 0 ? (row.Cell(col["issue"]).GetValue<string>() ?? "").Trim() : "";
                    string sonuc = col["result"] > 0 ? (row.Cell(col["result"]).GetValue<string>() ?? "").Trim() : "";

                    return new ServiceRecord {
                        DeviceName = cihaz,
                        ServiceDate = tarih,
                        SerialNumber = seri,
                        Company = firma,
                        Issue = sorun,
                        Result = sonuc
                    };
                });
                
                if (toImport.Count > 0) await _services.AddBulkAsync(toImport);
            });
            await LoadAsync();
            StatusText = LocalizationManager.L("svc_import_done");
        }
        catch (Exception ex) { StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; }
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public async Task PrintAsync()
    {
        try
        {
            if (Records.Count == 0) return;
            StatusText = LocalizationManager.L("svc_print_preparing");
            var sbHeaders = new System.Text.StringBuilder();
            sbHeaders.Append($"<th style='width: 12%;'>{LocalizationManager.L("maintenance_date")}</th>");
            sbHeaders.Append($"<th style='width: 23%; text-align: left;'>{LocalizationManager.L("device_name")}</th>");
            sbHeaders.Append($"<th style='width: 20%; text-align: left;'>{LocalizationManager.L("serial_number")}</th>");
            sbHeaders.Append($"<th style='width: 15%; text-align: left;'>{LocalizationManager.L("company")}</th>");
            sbHeaders.Append($"<th style='width: 15%; text-align: left;'>{LocalizationManager.L("problem")}</th>");
            sbHeaders.Append($"<th style='width: 15%; text-align: left;'>{LocalizationManager.L("result")}</th>");
            
            var sbBody = new System.Text.StringBuilder();
            foreach (var r in Records)
            {
                sbBody.Append("<tr>");
                sbBody.Append($"<td class='num'>{r.ServiceDate:dd.MM.yyyy}</td>");
                sbBody.Append($"<td style='text-align: left;'>{r.DeviceName}</td>");
                sbBody.Append($"<td style='text-align: left;'>{r.SerialNumber}</td>");
                sbBody.Append($"<td style='text-align: left;'>{r.Company}</td>");
                sbBody.Append($"<td style='text-align: left;'>{r.Issue}</td>");
                sbBody.Append($"<td style='text-align: left;'>{r.Result}</td>");
                sbBody.Append("</tr>");
            }
            
            string html = PrintTemplateBuilder.BuildReportHtml(
                title: LocalizationManager.L("service_records"),
                headerTitle: LocalizationManager.L("service_records").ToUpper(),
                dateInfo: LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")),
                tableHeadersHtml: sbHeaders.ToString(),
                tableBodyHtml: sbBody.ToString(),
                footerHtml: LocalizationManager.L("total_records_page", Records.Count, 1, 1)
            );
            
            var previewVm = new PrintPreviewViewModel(LocalizationManager.L("service_records"), html);
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = LocalizationManager.L("svc_print_done");
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("Print error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; 
        }
    }
}
