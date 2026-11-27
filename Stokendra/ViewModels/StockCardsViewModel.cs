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
    [ObservableProperty] private string    _searchText = "";
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
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
    public BulkObservableCollection<StockCard> SelectedCards { get; } = new();

    private const int PageSize = 50;
    private int _totalCount;
    private bool _isLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _totalPages = 1;

    public bool CanPrevPage => CurrentPage > 1;
    public bool CanNextPage => CurrentPage < TotalPages;

    // For Debounce
    private CancellationTokenSource? _filterCts;

    public StockCardsViewModel(
        IStockCardRepository stockCards,
        IMovementRepository movements,
        IDepartmentRepository departments,
        IDialogService dialogService)
    {
        _stockCards = stockCards;
        _movements = movements;
        _departments = departments;
        _dialogService = dialogService;

        DataEvents.DataChanged += OnDataChanged;

        SelectedCards.CollectionChanged += (s, e) => {
            IsMultipleSelected = SelectedCards.Count >= 2;
            OnPropertyChanged(nameof(IsCardSelected));
            OnPropertyChanged(nameof(IsSingleCardSelected));
            if (_isLoaded)
                UpdateStatusText();
        };
    }

    private void OnDataChanged(DataDomain domain)
    {
        if (domain is DataDomain.StockCards or DataDomain.Movements)
        {
            _isLoaded = false;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;
        await LoadAsync();
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
            string? term = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            _totalCount = await _stockCards.GetCountAsync(term, null, token);
            TotalPages = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var data = await _stockCards.GetPagedAsync(CurrentPage, PageSize, term, null, token);
            token.ThrowIfCancellationRequested();

            Cards.ReplaceAll(data);
            _isLoaded = true;
            UpdateStatusText();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) 
        { 
            AppLogger.LogError("StockCards load error", ex);
            StatusText = LocalizationManager.L("error"); 
        }
        finally
        {
            if (token == _loadingCts?.Token)
                IsLoading = false;
        }
    }

    private void UpdateStatusText()
    {
        string pageInfo = LocalizationManager.L("total_records_page", _totalCount, CurrentPage, TotalPages);
        if (SelectedCards.Count > 0)
        {
            string selectedPart = LocalizationManager.L("records_info", _totalCount, SelectedCards.Count).Split('•').LastOrDefault()?.Trim() ?? "";
            StatusText = string.IsNullOrEmpty(selectedPart) ? pageInfo : $"{pageInfo}  •  {selectedPart}";
        }
        else
        {
            StatusText = pageInfo;
        }
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
            await Task.Delay(120, token);
            CurrentPage = 1;
            await LoadAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.LogError("Search filter error", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    public async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrevPage))]
    public async Task PrevPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadAsync();
        }
    }

    [RelayCommand]
    public void ClearSearch() => SearchText = "";

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public async Task AddCardAsync()
    {
        if (!EnsureCanEditForSession()) return;
        var vm = new AddStockCardViewModel(_stockCards);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _stockCards.AddAsync(vm.Result);
                DataEvents.NotifyChanged(DataDomain.StockCards);
                await LoadAsync();
                StatusText = LocalizationManager.L("card_added_success", vm.Result.Name);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Add stock card error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task EditCardAsync()
    {
        if (!EnsureCanEditForSession()) return;
        var cardToEdit = SelectedCard ?? SelectedCards.FirstOrDefault();
        if (cardToEdit == null) return;

        var vm = new AddStockCardViewModel(_stockCards, cardToEdit);
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _stockCards.UpdateAsync(vm.Result);
                DataEvents.NotifyChanged(DataDomain.StockCards);
                await LoadAsync();
                StatusText = LocalizationManager.L("card_updated_success", vm.Result.Name);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Edit stock card error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task DeleteCardAsync()
    {
        if (!EnsureCanEditForSession()) return;
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
                await _stockCards.DeleteBulkAsync(SelectedCards.Select(c => c.Id).ToList());
                DataEvents.NotifyChanged(DataDomain.StockCards);
                await LoadAsync();
                StatusText = LocalizationManager.L("bulk_delete_success", count);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Bulk delete cards error", ex);
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
                DataEvents.NotifyChanged(DataDomain.StockCards);
                await LoadAsync();
                StatusText = LocalizationManager.L("bulk_delete_success", 1);
            }
            catch (Exception ex) 
            { 
                AppLogger.LogError("Delete card error", ex);
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
        var vm = new BulkMovementViewModel(movements, depts, _stockCards, _dialogService);
        
        if (await _dialogService.ShowDialogAsync(vm))
        {
            DataEvents.NotifyChanged(DataDomain.Movements);
            await LoadAsync();
        }
    }

    [RelayCommand]
    public async Task ImportExcelAsync()
    {
        if (!EnsureCanEditForSession()) return;
        string? path = await _dialogService.OpenFileAsync(LocalizationManager.L("import_excel"), "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            int count = 0;
            await Task.Run(() => {
                string defaultUnit = LocalizationManager.L("default_unit");
                var mappings = new Dictionary<string, string[]>
                {
                    { "code", new[] { "code", "kod", "kodno", "stok kodu", "stokkodu" } },
                    { "name", new[] { "name", "stok adı", "stok adi", "ad", "adi" } },
                    { "category", new[] { "category", "kategori" } },
                    { "unit", new[] { "unit", "birim" } },
                    { "minStock", new[] { "minstock", "minstok", "min stok", "minimum stok", "min" } },
                    { "description", new[] { "description", "açıklama", "aciklama" } }
                };
                
                var fallback = new Dictionary<string, int>
                {
                    { "code", 1 }, { "name", 2 }, { "category", 5 },
                    { "minStock", 7 }, { "unit", 8 }
                };

                var cardsToImport = ExcelImportHelper.ImportData(path, mappings, fallback, (row, col) => 
                {
                    string code = col["code"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["code"])) : "";
                    string name = col["name"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["name"])) : "";

                    if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name)) return null;

                    return new StockCard
                    {
                        Code = code,
                        Name = name,
                        Category = col["category"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["category"])) : "",
                        Unit = col["unit"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["unit"]), defaultUnit) : defaultUnit,
                        MinStock = col["minStock"] > 0 ? ExcelImportHelper.ParseCellInt(row.Cell(col["minStock"]), 0) : 0,
                        CardType = "Child",
                        Description = col["description"] > 0 ? ExcelImportHelper.ParseCellString(row.Cell(col["description"])) : ""
                    };
                });

                if (cardsToImport.Count > 0)
                {
                    _stockCards.AddBulk(cardsToImport);
                    count = cardsToImport.Count;
                }
            });
            DataEvents.NotifyChanged(DataDomain.StockCards);
            await LoadAsync();
            StatusText = LocalizationManager.L("import_success", count);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("import_success", count));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Stock cards import error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("import_error", ex.Message));
        }
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
            _dialogService);
            
        await _dialogService.ShowDialogAsync(vm);
        if (vm.HasChanges)
        {
            await LoadAsync();
        }
    }

    private async Task<List<StockCard>> FetchAllMatchingCardsAsync()
    {
        if (_totalCount <= Cards.Count && Cards.Count > 0)
            return Cards.ToList();

        string? term = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
        var all = new List<StockCard>();
        const int batchSize = 5000;
        int page = 1;
        while (true)
        {
            var batch = await _stockCards.GetPagedAsync(page, batchSize, term, null);
            if (batch == null || batch.Count == 0) break;
            all.AddRange(batch);
            if (batch.Count < batchSize) break;
            page++;
        }
        return all.Count > 0 ? all : Cards.ToList();
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        if (_totalCount == 0 && Cards.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
            return;
        }

        string baseName = LocalizationManager.L("export_fn_cards");
        string fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync(LocalizationManager.L("export_excel"), fileName, "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            var snapshot = await FetchAllMatchingCardsAsync();
            if (snapshot.Count == 0) return;
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
                ExcelService.ExportToExcel(path, "StockCards", headers, snapshot, k => new object?[] {
                    k.Code, k.Name, k.CardTypeDisplay, k.ParentName, k.Category, k.CurrentStock, k.MinStock, k.Unit
                });
            });
            StatusText = LocalizationManager.L("export_success", path);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("export_success", path));
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("Excel export error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; 
        }
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        if (_totalCount == 0 && Cards.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
            return;
        }

        try
        {
            StatusText = LocalizationManager.L("preparing_print");
            var snapshot = await FetchAllMatchingCardsAsync();
            if (snapshot.Count == 0) return;
            string title = LocalizationManager.L("stock_cards");
            string dateInfo = LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            string footer = LocalizationManager.L("total_records_page", snapshot.Count, 1, 1);

            string html = await Task.Run(() =>
            {
                var sbHeaders = new System.Text.StringBuilder();
                sbHeaders.Append($"<th style='width: 10%;'>{LocalizationManager.L("code_no")}</th>");
                sbHeaders.Append($"<th style='width: 30%; text-align: left; padding-left: 8px;'>{LocalizationManager.L("stock_name")}</th>");
                sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("card_type")}</th>");
                sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("category")}</th>");
                sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("current_stock")}</th>");
                sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("min_stock")}</th>");

                var sbBody = new System.Text.StringBuilder(snapshot.Count * 160);
                foreach (var c in snapshot)
                {
                    sbBody.Append("<tr>");
                    sbBody.Append($"<td class='num'>{PrintTemplateBuilder.Escape(c.Code)}</td>");
                    sbBody.Append($"<td style='text-align: left; padding-left: 8px;'>{PrintTemplateBuilder.Escape(c.Name)}</td>");
                    sbBody.Append($"<td class='num'>{PrintTemplateBuilder.Escape(c.CardTypeDisplay)}</td>");
                    sbBody.Append($"<td class='num'>{PrintTemplateBuilder.Escape(c.Category)}</td>");
                    sbBody.Append($"<td class='num'>{(c.IsParentCard ? "-" : c.CurrentStock.ToString())}</td>");
                    sbBody.Append($"<td class='num'>{c.MinStock}</td>");
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
        catch (Exception ex)
        {
            AppLogger.LogError("Cards print error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("export_error")}: {ex.Message}");
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _isLoaded = false;
    }
}
