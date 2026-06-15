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
    private readonly ILogger _logger;

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

    public bool IsMovementSelected => SelectedMovement != null;
    [ObservableProperty] private bool _isMultipleSelected;

    partial void OnSelectedMovementChanged(StockMovement? value) => OnPropertyChanged(nameof(IsMovementSelected));

    public ObservableCollection<StockMovement> Movements { get; } = new();
    public ObservableCollection<StockMovement> SelectedMovements { get; } = new();

    public ObservableCollection<string>       StockItems { get; } = new();
    public ObservableCollection<string>       TypeItems  { get; } = new();

    private List<StockCard>   _allCards     = new();

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
            _logger.LogError("Init error", ex);
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
            foreach (var h in data) Movements.Add(h);
            StatusText = LocalizationManager.L("total_movements_page", totalCount, CurrentPage, TotalPages);
        }
        catch (OperationCanceledException) 
        { 
            // Ignored, operation was intentionally cancelled
        }
        catch (Exception ex) 
        { 
            _logger.LogError("LoadMovements error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; 
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
        if (SelectedMovement == null) return;
        
        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("confirm_delete_title"), LocalizationManager.L("confirm_movement_delete"));
        if (!confirm) return;

        try
        {
            await Task.Run(() => _movements.Delete(SelectedMovement.Id));
            await LoadMovementsAsync();
            StatusText = LocalizationManager.L("bulk_movement_delete_success", 1);
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Delete error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DeleteBulkAsync()
    {
        if (SelectedMovements.Count < 2) return;

        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("confirm_bulk_delete_title"), LocalizationManager.L("confirm_bulk_movement_delete", SelectedMovements.Count));
        if (!confirm) return;

        try
        {
            var ids = SelectedMovements.Select(m => m.Id).ToList();
            await Task.Run(() => _movements.DeleteBulk(ids));
            await LoadMovementsAsync();
            StatusText = LocalizationManager.L("bulk_movement_delete_success", ids.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk delete error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
        }
    }

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
                StatusText = LocalizationManager.L("save_settings");
            }
            catch (Exception ex)
            {
                _logger.LogError("Add movement error", ex);
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
                    await Task.Run(() => _movements.Update(vm.Result));
                    await LoadMovementsAsync();
                    StatusText = LocalizationManager.L("save_settings");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Edit movement error", ex);
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
                }
            }
        }
        else
        {
            var vm = new BulkEditMovementsViewModel(SelectedMovements.ToList(), _movements, _departments, _dialogService, _logger);
            if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
            {
                await LoadMovementsAsync();
                StatusText = LocalizationManager.L("save_settings");
            }
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
        string fileName = $"Stock_Movements_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
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
                using var workbook = new ClosedXML.Excel.XLWorkbook(path);
                var worksheet = workbook.Worksheet(1);
                
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null) return;

                int colCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                
                // Find column indexes dynamically
                int colCode = 0, colRecipient = 0, colType = 0, colQuantity = 0, colDepartment = 0, colDate = 0, colDescription = 0;

                for (int i = 1; i <= colCount; i++)
                {
                    string header = firstRow.Cell(i).GetValue<string>().Trim().ToTurkishLower();
                    if (header == "stok kodu" || header == "kod" || header == "stokkodu" || header == "kodno" || header == "code")
                        colCode = i;
                    else if (header == "teslim edilen" || header == "teslim alan" || header == "personel" || header == "kisi" || header == "kişi" || header == "recipient")
                        colRecipient = i;
                    else if (header == "tür" || header == "tur" || header == "işlem" || header == "islem" || header == "tip" || header == "islem tipi" || header == "type")
                        colType = i;
                    else if (header == "miktar" || header == "adet" || header == "sayı" || header == "sayi" || header == "quantity")
                        colQuantity = i;
                    else if (header == "departman" || header == "bölüm" || header == "bolum" || header == "department")
                        colDepartment = i;
                    else if (header == "tarih" || header == "işlem tarihi" || header == "islem tarihi" || header == "date")
                        colDate = i;
                    else if (header == "açıklama" || header == "aciklama" || header == "not" || header == "description")
                        colDescription = i;
                }

                // Fallback to absolute layout
                if (colCode == 0 && colType == 0)
                {
                    colCode = 1;
                    colRecipient = 3;
                    colType = 4;
                    colQuantity = 5;
                    colDepartment = 6;
                    colDate = 7;
                    colDescription = 8;
                }

                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<ClosedXML.Excel.IXLRangeRow>();
                var movementsToImport = new List<StockMovement>();
                
                foreach (var row in rows)
                {
                    string code = colCode > 0 ? (row.Cell(colCode).GetValue<string>() ?? "").Trim() : "";
                    if (string.IsNullOrEmpty(code)) continue;

                    var card = _allCards.FirstOrDefault(c => c.Code == code);
                    if (card == null) continue;

                    string typeRaw = colType > 0 ? (row.Cell(colType).GetValue<string>() ?? "").Trim() : "";
                    string type = typeRaw.Contains("[G]") || typeRaw.ToTurkishLower().Contains("giri") || typeRaw.ToTurkishLower().Contains("giris") || typeRaw.ToLower().Contains("entry") ? "Entry" : "Exit";

                    double qty = colQuantity > 0 ? (row.Cell(colQuantity).TryGetValue<double>(out double m) ? m : 1.0) : 1.0;
                    string department = colDepartment > 0 ? (row.Cell(colDepartment).GetValue<string>() ?? "").Trim() : "";
                    string recipient = colRecipient > 0 ? (row.Cell(colRecipient).GetValue<string>() ?? "").Trim() : "";
                    
                    DateTime date = DateTime.Now;
                    if (colDate > 0)
                    {
                        var cellVal = row.Cell(colDate).GetValue<string>();
                        if (DateTime.TryParse(cellVal, out var dt))
                        {
                            date = dt;
                        }
                    }
                    
                    string description = colDescription > 0 ? (row.Cell(colDescription).GetValue<string>() ?? "").Trim() : "";

                    movementsToImport.Add(new StockMovement
                    {
                        StockCardId = card.Id,
                        Type = type,
                        Quantity = qty,
                        Department = department,
                        Recipient = recipient,
                        Date = date,
                        Description = description
                    });
                }

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
            _logger.LogError("Import error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("import_error")}: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ExportSampleAsync()
    {
        string? path = await _dialogService.SaveFileAsync(LocalizationManager.L("sample_file"), "stock_movement_sample.xlsx", "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            await Task.Run(() => 
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var ws = workbook.AddWorksheet("Sample");
                string[] headers = { 
                    LocalizationManager.L("code_no"), 
                    LocalizationManager.L("stock_name") + " (Optional)", 
                    LocalizationManager.L("delivered_to"), 
                    LocalizationManager.L("type"), 
                    LocalizationManager.L("quantity"), 
                    LocalizationManager.L("department"), 
                    LocalizationManager.L("date") + " (dd.MM.yyyy)", 
                    LocalizationManager.L("description") 
                };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(30, 37, 52);
                    ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                }
                
                // Sample Entry row
                ws.Cell(2, 1).Value = "001";
                ws.Cell(2, 2).Value = "Sample Item";
                ws.Cell(2, 3).Value = "John Doe";
                ws.Cell(2, 4).Value = $"[{LocalizationManager.L("entry_symbol")}] " + LocalizationManager.L("entry");
                ws.Cell(2, 5).Value = 10;
                ws.Cell(2, 6).Value = "IT";
                ws.Cell(2, 7).Value = DateTime.Now.ToString("dd.MM.yyyy");
                ws.Cell(2, 8).Value = "New purchase";

                // Sample Exit row
                ws.Cell(3, 1).Value = "001";
                ws.Cell(3, 2).Value = "Sample Item";
                ws.Cell(3, 3).Value = "Alice Smith";
                ws.Cell(3, 4).Value = $"[{LocalizationManager.L("exit_symbol")}] " + LocalizationManager.L("exit");
                ws.Cell(3, 5).Value = 3;
                ws.Cell(3, 6).Value = "Accounting";
                ws.Cell(3, 7).Value = DateTime.Now.ToString("dd.MM.yyyy");
                ws.Cell(3, 8).Value = "Department request";
                
                ws.Columns().AdjustToContents();
                workbook.SaveAs(path);
            });
            StatusText = LocalizationManager.L("sample_file_created", path);
        }
        catch (Exception ex) { await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), ex.Message); }
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        try
        {
            StatusText = "Preparing print...";
            var sb = new System.Text.StringBuilder();
            sb.Append($"<html><head><meta charset='utf-8'><title>{LocalizationManager.L("movements_report")}</title>");
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
            sb.Append($"<h1>{LocalizationManager.L("movements_report").ToUpper()}</h1>");
            sb.Append($"<div class='date-box'>{LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm"))}</div>");
            sb.Append("</div>");

            sb.Append("<table><thead><tr>");
            sb.Append($"<th style='width: 8%;'>{LocalizationManager.L("code_no")}</th>");
            sb.Append($"<th style='width: 32%; text-align: left;'>{LocalizationManager.L("stock_name")}</th>");
            sb.Append($"<th style='width: 7%;'>{LocalizationManager.L("quantity")}</th>");
            sb.Append($"<th style='width: 6%;'>{LocalizationManager.L("type")}</th>");
            sb.Append($"<th style='width: 15%;'>{LocalizationManager.L("department")}</th>");
            sb.Append($"<th style='width: 15%;'>{LocalizationManager.L("delivered_to")}</th>");
            sb.Append($"<th style='width: 17%;'>{LocalizationManager.L("date")}</th>");
            sb.Append("</tr></thead><tbody>");
            
            foreach (var h in Movements)
            {
                string clr = h.IsEntry ? "green" : "red";
                sb.Append("<tr>");
                sb.Append($"<td class='num'>{h.StockCardCode}</td>");
                sb.Append($"<td>{h.StockCardName}</td>");
                sb.Append($"<td class='num bold {clr}'>{h.Quantity}</td>");
                sb.Append($"<td class='num bold {clr}'>{h.DisplayType}</td>");
                sb.Append($"<td>{h.Department}</td>");
                sb.Append($"<td>{h.Recipient}</td>");
                sb.Append($"<td>{h.Date:dd.MM.yyyy HH:mm}</td>");
                sb.Append("</tr>");
            }
            
            sb.Append("</tbody></table>");
            sb.Append($"<div class='footer'>{LocalizationManager.L("total_records_page", Movements.Count, 1, 1)}</div>");
            sb.Append("</body></html>");
            
            var previewVm = new PrintPreviewViewModel(LocalizationManager.L("movements_report"), sb.ToString());
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = "Printing finished.";
        }
        catch (Exception ex) { StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; }
    }
}
