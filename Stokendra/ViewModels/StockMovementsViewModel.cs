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
    [ObservableProperty] private DateTime? _startDate = new DateTime(2024, 1, 1);
    [ObservableProperty] private DateTime? _endDate   = DateTime.Today;
    [ObservableProperty] private string   _searchText = "";
    [ObservableProperty] private int      _selectedStockIndex = 0;
    [ObservableProperty] private int      _selectedTypeIndex  = 0;
    
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _searchCts;

    // State
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    [NotifyCanExecuteChangedFor(nameof(LoadMovementsCommand))]
    private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private StockMovement? _selectedMovement;

    public bool IsNotLoading => !IsLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _totalPages = 1;

    public bool CanPrevPage => CurrentPage > 1;
    public bool CanNextPage => CurrentPage < TotalPages;
    private const int PageSize = 30;

    public bool IsMovementSelected => SelectedMovement != null || SelectedMovements.Count > 0;
    [ObservableProperty] private bool _isMultipleSelected;

    partial void OnSelectedMovementChanged(StockMovement? value) => OnPropertyChanged(nameof(IsMovementSelected));

    public BulkObservableCollection<StockMovement> Movements { get; } = new();
    public BulkObservableCollection<StockMovement> SelectedMovements { get; } = new();

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

        DataEvents.DataChanged += OnDataChanged;
    }

    private bool _isLoaded;
    private bool _cardsDirty = true;

    private void OnDataChanged(DataDomain domain)
    {
        if (domain is DataDomain.Movements or DataDomain.Departments)
        {
            _isLoaded = false;
        }
        else if (domain == DataDomain.StockCards)
        {
            _cardsDirty = true;
            _isLoaded = false;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_cardsDirty)
        {
            _cardsDirty = false;
            await ReloadCardsAsync();
        }
        if (_isLoaded) return;
        await LoadMovementsAsync();
    }

    private async Task ReloadCardsAsync()
    {
        try
        {
            int previousIndex = SelectedStockIndex;
            int? previousCardId = (previousIndex > 0 && previousIndex - 1 < _allCards.Count)
                ? _allCards[previousIndex - 1].Id
                : null;

            var lookup = await _stockCards.GetCardLookupsAsync("Child");
            _allCards = (lookup != null && lookup.Count > 0) ? lookup : (await _stockCards.GetChildCardsAsync() ?? new List<StockCard>());
            StockItems.Clear();
            StockItems.Add(LocalizationManager.L("all"));
            foreach (var k in _allCards) StockItems.Add($"{k.Code} - {k.Name}");

            int restoredIndex = previousCardId.HasValue
                ? _allCards.FindIndex(c => c.Id == previousCardId.Value)
                : -1;
            SelectedStockIndex = restoredIndex >= 0 ? restoredIndex + 1 : 0;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("ReloadCards error", ex);
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
            int? cardId = (SelectedStockIndex > 0 && SelectedStockIndex - 1 < _allCards.Count) ? _allCards[SelectedStockIndex - 1].Id : null;
            string? type = SelectedTypeIndex switch { 1 => "Entry", 2 => "Exit", 3 => "Blank", _ => null };
            string? search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

            DateTime safeStart = (StartDate ?? new DateTime(2024, 1, 1)).Date;
            DateTime endVal = (EndDate ?? DateTime.Today).Date;
            DateTime safeEnd = endVal >= DateTime.MaxValue.Date ? DateTime.MaxValue : endVal.AddDays(1);
            var totalCount = await _movements.GetCountAsync(stockCardId: cardId, startDate: safeStart, endDate: safeEnd, department: null, movementType: type, category: null, searchTerm: search, cancellationToken: token);
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var data = await _movements.GetPagedAsync(CurrentPage, PageSize, stockCardId: cardId, startDate: safeStart, endDate: safeEnd, department: null, movementType: type, category: null, searchTerm: search, cancellationToken: token);

            token.ThrowIfCancellationRequested();

            Movements.ReplaceAll(data);
            _isLoaded = true;
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
        finally
        {
            if (token == _cts?.Token)
                IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ClearFiltersAsync()
    {
        _cts?.Cancel();
        StartDate          = new DateTime(2024, 1, 1);
        EndDate            = DateTime.Today;
        SearchText         = "";
        _searchCts?.Cancel();
        SelectedStockIndex = 0;
        SelectedTypeIndex  = 0;
        CurrentPage        = 1;
        await LoadMovementsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))] public async Task NextPageAsync() { if (CurrentPage < TotalPages) { CurrentPage++; await LoadMovementsAsync(); } }
    [RelayCommand(CanExecute = nameof(CanPrevPage))] public async Task PrevPageAsync() { if (CurrentPage > 1) { CurrentPage--; await LoadMovementsAsync(); } }

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
            await Task.Delay(120, token);
            CurrentPage = 1;
            await LoadMovementsAsync();
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.LogError("StockMovements schedule search error", ex);
        }
    }

    [RelayCommand]
    public async Task DeleteMovementAsync()
    {
        if (!EnsureCanEditForSession()) return;
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
                DataEvents.NotifyChanged(DataDomain.Movements);
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
                DataEvents.NotifyChanged(DataDomain.Movements);
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
        if (!EnsureCanEditForSession()) return;
        var vm = new AddMovementViewModel(_stockCards, _departments);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _movements.AddAsync(vm.Result);
                DataEvents.NotifyChanged(DataDomain.Movements);
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
        if (!EnsureCanEditForSession()) return;
        var selectedList = SelectedMovements.Count > 0
            ? SelectedMovements.ToList()
            : (SelectedMovement != null ? new List<StockMovement> { SelectedMovement } : new List<StockMovement>());
        if (selectedList.Count == 0) return;
        
        if (selectedList.Count == 1)
        {
            var target = selectedList[0];
            var vm = new AddMovementViewModel(_stockCards, _departments, target);
            if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
            {
                try
                {
                    await _movements.UpdateAsync(vm.Result);
                    DataEvents.NotifyChanged(DataDomain.Movements);
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
            var vm = new BulkEditMovementsViewModel(selectedList, _movements, _departments, _dialogService);
            if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
            {
                DataEvents.NotifyChanged(DataDomain.Movements);
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
            DataEvents.NotifyChanged(DataDomain.Movements);
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
            int? cardId = SelectedStockIndex > 0 && SelectedStockIndex - 1 < _allCards.Count ? _allCards[SelectedStockIndex - 1].Id : null;
            string? type = SelectedTypeIndex switch { 1 => "Entry", 2 => "Exit", 3 => "Blank", _ => null };

            DateTime safeStart = (StartDate ?? new DateTime(2024, 1, 1)).Date;
            DateTime endVal = (EndDate ?? DateTime.Today).Date;
            DateTime safeEnd = endVal >= DateTime.MaxValue.Date ? DateTime.MaxValue : endVal.AddDays(1);
            string? search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var recordsToExport = await _movements.GetPagedAsync(1, 50000, cardId, safeStart, safeEnd, null, type, null, search);
            if (recordsToExport.Count == 0)
            {
                recordsToExport = Movements.ToList();
            }

            if (recordsToExport.Count == 0)
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
                StatusText = "";
                return;
            }

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
                ExcelService.ExportToExcel(path, "StockMovements", headers, recordsToExport, h => new object?[]
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
        if (!EnsureCanEditForSession()) return;
        string? path = await _dialogService.OpenFileAsync(LocalizationManager.L("import_excel"), "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            if (_cardsDirty || _allCards.Count == 0)
            {
                await ReloadCardsAsync();
                _cardsDirty = false;
            }
            var cardsSnapshot = _allCards.ToList();
            var cardByCode = cardsSnapshot
                .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
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
                    string code = col["code"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["code"])) : "";
                    if (string.IsNullOrEmpty(code)) return null;

                    if (!cardByCode.TryGetValue(code, out var card)) return null;

                    string typeRaw = col["type"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["type"])) : "";
                    string lowerType = typeRaw.ToTurkishLower();
                    string type;
                    if (typeRaw.Contains("[G]") || typeRaw.Contains("[E]") || lowerType.Contains("giri") || lowerType.Contains("giris") || lowerType.Contains("entry"))
                        type = "Entry";
                    else if (typeRaw.Contains("[B]") || lowerType.Contains("boş") || lowerType.Contains("bos") || lowerType.Contains("blank"))
                        type = "Blank";
                    else
                        type = "Exit";

                    double qty = col["quantity"] > 0 ? ExcelImportHelper.ParseCellDouble(row.Cell(col["quantity"]), 1.0) : 1.0;
                    if (double.IsNaN(qty) || double.IsInfinity(qty) || qty < 0 || qty > 1_000_000_000 || (type != "Blank" && qty <= 0))
                        return null;
                    string department = col["department"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["department"])) : "";
                    string recipient = col["recipient"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["recipient"])) : "";
                    
                    DateTime date = col["date"] > 0 
                        ? ExcelImportHelper.ParseCellDate(row.Cell(col["date"])) 
                        : DateTime.Now;
                    
                    string description = col["description"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["description"])) : "";

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
            if (count > 0) DataEvents.NotifyChanged(DataDomain.Movements);
            await LoadMovementsAsync();
            StatusText = LocalizationManager.L("import_success", count);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("import_success", count));
        }
        catch (Exception ex) { 
            AppLogger.LogError("Import error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("import_error", ex.Message));
        }
    }



    [RelayCommand]
    public async Task PrintAsync()
    {
        try
        {
            int? cardId = SelectedStockIndex > 0 && SelectedStockIndex - 1 < _allCards.Count ? _allCards[SelectedStockIndex - 1].Id : null;
            string? type = SelectedTypeIndex switch { 1 => "Entry", 2 => "Exit", 3 => "Blank", _ => null };
            string? search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            DateTime safeStart = (StartDate ?? new DateTime(2024, 1, 1)).Date;
            DateTime endVal = (EndDate ?? DateTime.Today).Date;
            DateTime safeEnd = endVal >= DateTime.MaxValue.Date ? DateTime.MaxValue : endVal.AddDays(1);

            var snapshot = await _movements.GetPagedAsync(1, 50000, cardId, safeStart, safeEnd, null, type, null, search);
            if (snapshot.Count == 0)
            {
                snapshot = Movements.ToList();
            }

            if (snapshot.Count == 0)
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
                return;
            }

            StatusText = LocalizationManager.L("preparing_print");
            string title = LocalizationManager.L("movements_report");
            string dateInfo = LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            string footer = LocalizationManager.L("total_records_page", snapshot.Count, 1, 1);

            string html = await Task.Run(() =>
            {
                var sbHeaders = new System.Text.StringBuilder();
                sbHeaders.Append($"<th style='width: 8%;'>{LocalizationManager.L("code_no")}</th>");
                sbHeaders.Append($"<th style='width: 32%; text-align: left;'>{LocalizationManager.L("stock_name")}</th>");
                sbHeaders.Append($"<th style='width: 7%;'>{LocalizationManager.L("quantity")}</th>");
                sbHeaders.Append($"<th style='width: 6%;'>{LocalizationManager.L("type")}</th>");
                sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("department")}</th>");
                sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("delivered_to")}</th>");
                sbHeaders.Append($"<th style='width: 17%;'>{LocalizationManager.L("date")}</th>");

                var sbBody = new System.Text.StringBuilder(snapshot.Count * 180);
                foreach (var h in snapshot)
                {
                    string clr = h.IsEntry ? "green" : "red";
                    sbBody.Append("<tr>");
                    sbBody.Append($"<td class='num'>{PrintTemplateBuilder.Escape(h.StockCardCode)}</td>");
                    sbBody.Append($"<td>{PrintTemplateBuilder.Escape(h.StockCardName)}</td>");
                    sbBody.Append($"<td class='num bold {clr}'>{h.Quantity}</td>");
                    sbBody.Append($"<td class='num bold {clr}'>{PrintTemplateBuilder.Escape(h.DisplayType)}</td>");
                    sbBody.Append($"<td>{PrintTemplateBuilder.Escape(h.Department)}</td>");
                    sbBody.Append($"<td>{PrintTemplateBuilder.Escape(h.Recipient)}</td>");
                    sbBody.Append($"<td>{h.Date:dd.MM.yyyy HH:mm}</td>");
                    sbBody.Append("</tr>");
                }

                return PrintTemplateBuilder.BuildReportHtml(
                    title: title,
                    headerTitle: title.ToUpper(),
                    dateInfo: dateInfo,
                    tableHeadersHtml: sbHeaders.ToString(),
                    tableBodyHtml: sbBody.ToString(),
                    footerHtml: footer
                );
            });
            
            var previewVm = new PrintPreviewViewModel(title, html);
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = LocalizationManager.L("printing_finished");
        }
        catch (Exception ex) { StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _isLoaded = false;
        _cardsDirty = true;
    }
}
