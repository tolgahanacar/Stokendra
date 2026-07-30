using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class StockMovementsViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IStockCardRepository _stockCards;
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    // Filters
    [ObservableProperty] private DateTime _startDate = new DateTime(2024, 1, 1);
    [ObservableProperty] private DateTime _endDate   = DateTime.Today;
    [ObservableProperty] private string   _searchText = "";
    [ObservableProperty] private int      _selectedStockIndex = 0;
    [ObservableProperty] private int      _selectedTypeIndex  = 0;
    
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _searchCts;

    // State
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private StockMovement? _selectedMovement;

    public bool IsNotLoading => !IsLoading;

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    private const int PageSize = 30;

    public bool IsMovementSelected => SelectedMovement != null || SelectedMovements.Count > 0;
    [ObservableProperty] private bool _isMultipleSelected;

    partial void OnSelectedMovementChanged(StockMovement? value) => OnPropertyChanged(nameof(IsMovementSelected));

    public BulkObservableCollection<StockMovement> Movements { get; } = new();
    public ObservableCollection<StockMovement> SelectedMovements { get; } = new();

    public ObservableCollection<string>       StockItems { get; } = new();
    public ObservableCollection<string>       TypeItems  { get; } = new();

    private List<StockCard>   _allCards     = new();

    public StockMovementsViewModel(
        IMovementRepository movements,
        IStockCardRepository stockCards,
        IDepartmentRepository departments,
        IDialogService dialogService)
    {
        _movements   = movements;
        _stockCards  = stockCards;
        _departments = departments;
        _dialogService = dialogService;
        SelectedMovements.CollectionChanged += (s, e) => {
            IsMultipleSelected = SelectedMovements.Count >= 2;
            OnPropertyChanged(nameof(IsMovementSelected));
        };

        // Populate localized type filter choices
        TypeItems.Add(LocalizationManager.L("all"));
        TypeItems.Add(LocalizationManager.L("entry"));
        TypeItems.Add(LocalizationManager.L("exit"));
        TypeItems.Add(LocalizationManager.L("type_empty"));

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
            AppLogger.LogError("Init error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}";
        }
    }

    private async Task InitAsync()
    {
        _allCards = await _stockCards.GetChildCardsAsync();
        StockItems.Clear();
        StockItems.Add(LocalizationManager.L("all"));
        foreach (var k in _allCards) StockItems.Add($"{k.Code} - {k.Name}");
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
            string? type = SelectedTypeIndex switch { 1 => "Entry", 2 => "Exit", 3 => "Blank", _ => null };
            string? search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

            var totalCount = await _movements.GetCountAsync(stockCardId: cardId, startDate: StartDate, endDate: EndDate.AddDays(1), department: null, movementType: type, category: null, searchTerm: search, cancellationToken: token);
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var data = await _movements.GetPagedAsync(CurrentPage, PageSize, stockCardId: cardId, startDate: StartDate, endDate: EndDate.AddDays(1), department: null, movementType: type, category: null, searchTerm: search, cancellationToken: token);

            token.ThrowIfCancellationRequested();

            Movements.Clear();
            Movements.AddRange(data);
            StatusText = LocalizationManager.L("total_movements_page", totalCount, CurrentPage, TotalPages);
        }
        catch (OperationCanceledException) 
        { 
            // Ignored, operation was intentionally cancelled
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("LoadMovements error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; 
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void ClearFilters()
    {
        _searchCts?.Cancel();
        _cts?.Cancel();
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

    partial void OnSearchTextChanged(string value) => ScheduleSearch();

    private async void ScheduleSearch()
    {
        _cts?.Cancel();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(300, token);
            CurrentPage = 1;
            await LoadMovementsAsync();
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    public async Task DeleteMovementAsync()
    {
        if (SelectedMovements.Count >= 2)
        {
            int count = SelectedMovements.Count;
            bool confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_bulk_delete_title"), 
                LocalizationManager.L("confirm_bulk_movement_delete", count)
            );
            if (!confirm) return;

            try
            {
                var ids = SelectedMovements.Select(m => m.Id).ToList();
                await _movements.DeleteBulkAsync(ids);
                await LoadMovementsAsync();
                StatusText = LocalizationManager.L("bulk_movement_delete_success", count);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Bulk delete error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
        else
        {
            var itemToDelete = SelectedMovement ?? SelectedMovements.FirstOrDefault();
            if (itemToDelete == null) return;
            
            bool confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_delete_title"), 
                LocalizationManager.L("confirm_movement_delete")
            );
            if (!confirm) return;

            try
            {
                await _movements.DeleteAsync(itemToDelete.Id);
                await LoadMovementsAsync();
                StatusText = LocalizationManager.L("bulk_movement_delete_success", 1);
            }
            catch (Exception ex) 
            { 
                AppLogger.LogError("Delete error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task DeleteBulkAsync() => await DeleteMovementAsync();

    [RelayCommand]
    public async Task AddMovementAsync()
    {
        var vm = new AddMovementViewModel(_stockCards, _departments);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _movements.AddAsync(vm.Result);
                await LoadMovementsAsync();
                StatusText = LocalizationManager.L("movement_saved_success");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Add movement error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task EditMovementAsync()
    {
        if (SelectedMovements.Count == 0) return;
        
        if (SelectedMovements.Count == 1)
        {
            var target = SelectedMovements[0];
            var vm = new AddMovementViewModel(_stockCards, _departments, target);
            if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
            {
                try
                {
                    await _movements.UpdateAsync(vm.Result);
                    await LoadMovementsAsync();
                    StatusText = LocalizationManager.L("movement_saved_success");
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Edit movement error", ex);
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
                }
            }
        }
        else
        {
            var vm = new BulkEditMovementsViewModel(SelectedMovements.ToList(), _movements, _departments, _dialogService);
            if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
            {
                await LoadMovementsAsync();
                StatusText = LocalizationManager.L("movement_saved_success");
            }
        }
    }

    [RelayCommand]
    public async Task BulkMovementAsync()
    {
        var vm = new BulkMovementViewModel(_movements, _departments, _stockCards, _dialogService);
        if (await _dialogService.ShowDialogAsync(vm))
        {
            await LoadMovementsAsync();
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string baseName = LocalizationManager.L("export_fn_movements");
        string fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync(LocalizationManager.L("export_excel"), fileName, "Excel File (*.xlsx)|*.xlsx");
        
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            await Task.Run(() => 
            {
                var headers = new[] { 
                    LocalizationManager.L("date"), 
                    LocalizationManager.L("code_no"), 
                    LocalizationManager.L("stock_name"), 
                    LocalizationManager.L("type"), 
                    LocalizationManager.L("quantity"), 
                    LocalizationManager.L("department"), 
                    LocalizationManager.L("delivered_to"), 
                    LocalizationManager.L("description") 
                };
                ExcelService.ExportToExcel(path, "StockMovements", headers, Movements, h => new object?[]
                {
                    h.Date.ToString("dd.MM.yyyy HH:mm"),
                    h.StockCardCode,
                    h.StockCardName,
                    h.DisplayType,
                    h.Quantity,
                    h.Department,
                    h.Recipient,
                    h.Description
                });
            });
            StatusText = LocalizationManager.L("export_success", path);
        }
        catch (Exception ex) { StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task ImportXlsxAsync()
    {
        string? path = await _dialogService.OpenFileAsync(LocalizationManager.L("import_excel"), "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            int count = 0;
            await Task.Run(async () => 
            {
                var mappings = new Dictionary<string, string[]>
                {
                    { "code", new[] { "stok kodu", "kod", "stokkodu", "kodno", "code" } },
                    { "recipient", new[] { "teslim edilen/alınan", "teslim edilen/alinan", "teslim edilen / alınan", "teslim edilen / alinan", "teslim edilen", "teslim alan", "personel", "kisi", "kişi", "recipient", "teslim", "alinan", "alınan", "delivered to", "delivered_to" } },
                    { "type", new[] { "tür", "tur", "işlem", "islem", "tip", "islem tipi", "type", "giriş", "giris", "çıkış", "cikis" } },
                    { "quantity", new[] { "miktar", "adet", "sayı", "sayi", "quantity" } },
                    { "department", new[] { "departman", "bölüm", "bolum", "department" } },
                    { "date", new[] { "tarih", "işlem tarihi", "islem tarihi", "date" } },
                    { "description", new[] { "açıklama", "aciklama", "not", "description" } }
                };

                var fallback = new Dictionary<string, int>
                {
                    { "date", 1 }, { "code", 2 }, { "type", 4 },
                    { "quantity", 5 }, { "department", 6 }, { "recipient", 7 }, { "description", 8 }
                };

                var movementsToImport = ExcelImportHelper.ImportData(path, mappings, fallback, (row, col) => 
                {
                    string code = col["code"] > 0 ? (row.Cell(col["code"]).GetValue<string>() ?? "").Trim() : "";
                    if (string.IsNullOrEmpty(code)) return null;

                    var card = _allCards.FirstOrDefault(c => c.Code == code);
                    if (card == null) return null;

                    string typeRaw = col["type"] > 0 ? (row.Cell(col["type"]).GetValue<string>() ?? "").Trim() : "";
                    string type = typeRaw.Contains("[G]") || typeRaw.ToTurkishLower().Contains("giri") || typeRaw.ToTurkishLower().Contains("giris") || typeRaw.ToTurkishLower().Contains("entry") ? "Entry" : "Exit";

                    double qty = col["quantity"] > 0 ? (row.Cell(col["quantity"]).TryGetValue<double>(out double m) ? m : 1.0) : 1.0;
                    string department = col["department"] > 0 ? (row.Cell(col["department"]).GetValue<string>() ?? "").Trim() : "";
                    string recipient = col["recipient"] > 0 ? (row.Cell(col["recipient"]).GetValue<string>() ?? "").Trim() : "";
                    
                    DateTime date = DateTime.Now;
                    if (col["date"] > 0)
                    {
                        var cellVal = row.Cell(col["date"]).GetValue<string>();
                        if (DateTime.TryParse(cellVal, out var dt)) date = dt;
                    }
                    
                    string description = col["description"] > 0 ? (row.Cell(col["description"]).GetValue<string>() ?? "").Trim() : "";

                    return new StockMovement
                    {
                        StockCardId = card.Id,
                        Type = type,
                        Quantity = qty,
                        Department = department,
                        Recipient = recipient,
                        Date = date,
                        Description = description
                    };
                });

                if (movementsToImport.Count > 0)
                {
                    await _movements.AddBulkAsync(movementsToImport);
                    count = movementsToImport.Count;
                }
            });
            await LoadMovementsAsync();
            StatusText = LocalizationManager.L("import_success", count);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("import_success", count));
        }
        catch (Exception ex) { 
            AppLogger.LogError("Import error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("import_error")}: {ex.Message}");
        }
    }



    [RelayCommand]
    public async Task PrintAsync()
    {
        try
        {
            StatusText = LocalizationManager.L("preparing_print");
            var sbHeaders = new System.Text.StringBuilder();
            sbHeaders.Append($"<th style='width: 8%;'>{LocalizationManager.L("code_no")}</th>");
            sbHeaders.Append($"<th style='width: 32%; text-align: left;'>{LocalizationManager.L("stock_name")}</th>");
            sbHeaders.Append($"<th style='width: 7%;'>{LocalizationManager.L("quantity")}</th>");
            sbHeaders.Append($"<th style='width: 6%;'>{LocalizationManager.L("type")}</th>");
            sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("department")}</th>");
            sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("delivered_to")}</th>");
            sbHeaders.Append($"<th style='width: 17%;'>{LocalizationManager.L("date")}</th>");
            
            var sbBody = new System.Text.StringBuilder();
            foreach (var h in Movements)
            {
                string clr = h.IsEntry ? "green" : "red";
                sbBody.Append("<tr>");
                sbBody.Append($"<td class='num'>{h.StockCardCode}</td>");
                sbBody.Append($"<td>{h.StockCardName}</td>");
                sbBody.Append($"<td class='num bold {clr}'>{h.Quantity}</td>");
                sbBody.Append($"<td class='num bold {clr}'>{h.DisplayType}</td>");
                sbBody.Append($"<td>{h.Department}</td>");
                sbBody.Append($"<td>{h.Recipient}</td>");
                sbBody.Append($"<td>{h.Date:dd.MM.yyyy HH:mm}</td>");
                sbBody.Append("</tr>");
            }
            
            string html = PrintTemplateBuilder.BuildReportHtml(
                title: LocalizationManager.L("movements_report"),
                headerTitle: LocalizationManager.L("movements_report").ToUpper(),
                dateInfo: LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")),
                tableHeadersHtml: sbHeaders.ToString(),
                tableBodyHtml: sbBody.ToString(),
                footerHtml: LocalizationManager.L("total_records_page", Movements.Count, 1, 1)
            );
            
            var previewVm = new PrintPreviewViewModel(LocalizationManager.L("movements_report"), html);
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = LocalizationManager.L("printing_finished");
        }
        catch (Exception ex) { StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; }
    }
}
