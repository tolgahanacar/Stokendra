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

public partial class StockCardsViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private string    _searchText = "";
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool      _isLoading;
    [ObservableProperty] private string    _statusText = "";
    [ObservableProperty] private StockCard? _selectedCard;

    public bool IsNotLoading => !IsLoading;

    public bool IsCardSelected => SelectedCard != null || SelectedCards.Count > 0;
    public bool IsSingleCardSelected => (SelectedCard != null && SelectedCards.Count <= 1) || SelectedCards.Count == 1;
    [ObservableProperty] private bool _isMultipleSelected;
    
    partial void OnSelectedCardChanged(StockCard? value)
    {
        OnPropertyChanged(nameof(IsCardSelected));
        OnPropertyChanged(nameof(IsSingleCardSelected));
    }

    public BulkObservableCollection<StockCard> Cards { get; } = new();
    public ObservableCollection<StockCard> SelectedCards { get; } = new();

    private List<StockCard> _allCards = new();

    // For Debounce
    private CancellationTokenSource? _filterCts;

    public StockCardsViewModel(
        IStockCardRepository stockCards,
        IMovementRepository movements,
        IDepartmentRepository departments,
        IDialogService dialogService,
        ILogger logger)
    {
        _stockCards = stockCards;
        _movements = movements;
        _departments = departments;
        _dialogService = dialogService;
        _logger = logger;
        
        SelectedCards.CollectionChanged += (s, e) => {
            IsMultipleSelected = SelectedCards.Count >= 2;
            OnPropertyChanged(nameof(IsCardSelected));
            OnPropertyChanged(nameof(IsSingleCardSelected));
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
            _logger.LogError("Load error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}";
        }
    }

    private CancellationTokenSource? _loadingCts;

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task LoadAsync()
    {
        _loadingCts?.Cancel();
        _loadingCts?.Dispose();
        _loadingCts = new CancellationTokenSource();
        var token = _loadingCts.Token;

        IsLoading = true;
        try
        {
            _allCards = await _stockCards.GetAllAsync(token);
            ApplySearch();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) 
        { 
            _logger.LogError("StockCards load error", ex);
            StatusText = LocalizationManager.L("error"); 
        }
        finally { IsLoading = false; }
    }

    partial void OnSearchTextChanged(string value) => ScheduleFilter();

    private async void ScheduleFilter()
    {
        _filterCts?.Cancel();
        _filterCts?.Dispose();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;
        try
        {
            await Task.Delay(250, token);
            ApplySearch();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError("Search filter error", ex);
        }
    }

    private void ApplySearch()
    {
        var term = SearchText.Trim().ToTurkishLower();
        var data = string.IsNullOrEmpty(term)
            ? _allCards
            : _allCards.Where(k =>
                k.Name.ToTurkishLower().Contains(term) ||
                k.Code.ToTurkishLower().Contains(term) ||
                k.Category.ToTurkishLower().Contains(term)).ToList();

        Cards.Clear();
        Cards.AddRange(data);
        StatusText = LocalizationManager.L("records_info", data.Count, 0).Split('•')[0].Trim();
    }

    [RelayCommand]
    public void ClearSearch() => SearchText = "";

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public async Task AddCardAsync()
    {
        var vm = new AddStockCardViewModel(_stockCards);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _stockCards.AddAsync(vm.Result);
                await LoadAsync();
                StatusText = LocalizationManager.L("card_added_success", vm.Result.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError("Add stock card error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task EditCardAsync()
    {
        var cardToEdit = SelectedCard ?? SelectedCards.FirstOrDefault();
        if (cardToEdit == null) return;

        var vm = new AddStockCardViewModel(_stockCards, cardToEdit);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _stockCards.UpdateAsync(vm.Result);
                await LoadAsync();
                StatusText = LocalizationManager.L("card_updated_success", vm.Result.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError("Edit stock card error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task DeleteCardAsync()
    {
        if (SelectedCards.Count >= 2)
        {
            int count = SelectedCards.Count;
            bool confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_bulk_delete_title"), 
                LocalizationManager.L("confirm_bulk_delete", count)
            );
            
            if (!confirm) return;

            try
            {
                foreach (var k in SelectedCards.ToList())
                    await _stockCards.DeleteAsync(k.Id);
                await LoadAsync();
                StatusText = LocalizationManager.L("bulk_delete_success", count);
            }
            catch (Exception ex)
            {
                _logger.LogError("Bulk delete cards error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
        else
        {
            var cardToDelete = SelectedCard ?? SelectedCards.FirstOrDefault();
            if (cardToDelete == null) return;
            
            bool confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_delete_title"), 
                LocalizationManager.L("confirm_delete", $"{cardToDelete.Code} - {cardToDelete.Name}")
            );
            
            if (!confirm) return;

            try
            {
                await _stockCards.DeleteAsync(cardToDelete.Id);
                await LoadAsync();
                StatusText = LocalizationManager.L("bulk_delete_success", 1);
            }
            catch (Exception ex) 
            { 
                _logger.LogError("Delete card error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task BulkDeleteAsync() => await DeleteCardAsync();

    [RelayCommand]
    public async Task BulkEntryAsync()
    {
        var movements = _movements;
        var depts = _departments;
        var vm = new BulkMovementViewModel(movements, depts, _stockCards, _dialogService, _logger);
        
        if (await _dialogService.ShowDialogAsync(vm))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    public async Task ImportExcelAsync()
    {
        string? path = await _dialogService.OpenFileAsync(LocalizationManager.L("import_excel"), "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            int count = 0;
            await Task.Run(() => {
                using var workbook = new ClosedXML.Excel.XLWorkbook(path);
                var worksheet = workbook.Worksheet(1);
                
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null) return;

                int colCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                
                // Dynamically find column indexes (1-based) supporting both languages
                int colCode = 0, colName = 0, colCategory = 0, colUnit = 0;
                int colMinStock = 0, colDescription = 0;

                for (int i = 1; i <= colCount; i++)
                {
                    string header = firstRow.Cell(i).GetValue<string>().Trim().ToTurkishLower();
                    if (header == "code" || header == "kod" || header == "kodno" || header == "stok kodu" || header == "stokkodu")
                        colCode = i;
                    else if (header == "name" || header == "stok adı" || header == "stok adi" || header == "ad" || header == "adi")
                        colName = i;
                    else if (header == "category" || header == "kategori")
                        colCategory = i;
                    else if (header == "unit" || header == "birim")
                        colUnit = i;

                    else if (header == "minstock" || header == "minstok" || header == "min stok" || header == "minimum stok" || header == "min")
                        colMinStock = i;
                    else if (header == "description" || header == "açıklama" || header == "aciklama")
                        colDescription = i;
                }

                // Fallback to absolute column layout if matching headers are not found
                if (colCode == 0 && colName == 0)
                {
                    colCode = 1;
                    colName = 2;
                    colCategory = 3;
                    colUnit = 4;
                    colMinStock = 5;
                    colDescription = 6;
                }

                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<ClosedXML.Excel.IXLRangeRow>();
                var cardsToImport = new List<StockCard>();

                foreach (var row in rows)
                {
                    string code = colCode > 0 ? (row.Cell(colCode).GetValue<string>() ?? "").Trim() : "";
                    string name = colName > 0 ? (row.Cell(colName).GetValue<string>() ?? "").Trim() : "";

                    if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name)) continue;

                    var k = new StockCard
                    {
                        Code = code,
                        Name = name,
                        Category = colCategory > 0 ? (row.Cell(colCategory).GetValue<string>() ?? "").Trim() : "",
                        Unit = colUnit > 0 ? (row.Cell(colUnit).GetValue<string>() ?? "Adet").Trim() : "Adet",
                        MinStock = colMinStock > 0 ? (row.Cell(colMinStock).TryGetValue<int>(out int valMin) ? valMin : 0) : 0,
                        CardType = "Child",
                        Description = colDescription > 0 ? (row.Cell(colDescription).GetValue<string>() ?? "").Trim() : ""
                    };

                    if (!string.IsNullOrEmpty(k.Code) && !string.IsNullOrEmpty(k.Name))
                    {
                        cardsToImport.Add(k);
                    }
                }

                if (cardsToImport.Count > 0)
                {
                    _stockCards.AddBulk(cardsToImport);
                    count = cardsToImport.Count;
                }
            });
            await LoadAsync();
            StatusText = LocalizationManager.L("import_success", count);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("import_success", count));
        }
        catch (Exception ex)
        {
            _logger.LogError("Stock cards import error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("import_error")}: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DownloadSampleAsync()
    {
        string? path = await _dialogService.SaveFileAsync(LocalizationManager.L("sample_file"), "stock_card_sample.xlsx", "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            await Task.Run(() => {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var ws = workbook.Worksheets.Add("StockCards");
                string[] headers = { "Code", "Name", "Category", "Unit", "MinStock", "Description" };
                for (int i = 0; i < headers.Length; i++) { ws.Cell(1, i + 1).Value = headers[i]; ws.Cell(1, i + 1).Style.Font.Bold = true; }
                
                ws.Cell(2, 1).Value = "100.001";
                ws.Cell(2, 2).Value = "Sample Product";
                ws.Cell(2, 3).Value = "Office";
                ws.Cell(2, 4).Value = "Pcs";
                ws.Cell(2, 5).Value = 5;
                
                ws.Columns().AdjustToContents();
                workbook.SaveAs(path);
            });
            StatusText = LocalizationManager.L("sample_file_created", path);
        }
        catch (Exception ex) { await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), ex.Message); }
    }

    [RelayCommand]
    public async Task ShowDetailAsync()
    {
        if (SelectedCard == null) return;
        
        var movements = _movements;
        var depts = _departments;
        
        var vm = new StockCardDetailViewModel(
            SelectedCard.Id, 
            _stockCards, 
            movements, 
            depts, 
            _dialogService, 
            _logger);
            
        await _dialogService.ShowDialogAsync(vm);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string fileName = $"Stock_Cards_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync(LocalizationManager.L("export_excel"), fileName, "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            await Task.Run(() => {
                var headers = new[] { 
                    LocalizationManager.L("code_no"), 
                    LocalizationManager.L("stock_name"), 
                    LocalizationManager.L("card_type"), 
                    LocalizationManager.L("parent_card"), 
                    LocalizationManager.L("category"), 
                    LocalizationManager.L("current_stock"), 
                    LocalizationManager.L("min_stock"), 
                    LocalizationManager.L("unit") 
                };
                ExcelService.ExportToExcel(path, "StockCards", headers, Cards, k => new object?[] {
                    k.Code, k.Name, k.CardTypeDisplay, k.ParentName, k.Category, k.CurrentStock, k.MinStock, k.Unit
                });
            });
            StatusText = LocalizationManager.L("export_success", path);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("export_success", path));
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Excel export error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; 
        }
    }
}
