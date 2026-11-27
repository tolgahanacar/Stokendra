using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stokendra.Infrastructure;

namespace Stokendra.ViewModels;

public partial class StocksViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly IDialogService _dialogService;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public bool IsNotLoading => !IsLoading;

    public BulkObservableCollection<StockCard> Stocks { get; } = new();
    private const int PageSize = 50;
    private int _totalCount;
    private int _lowStockCount;
    private int _depletedCount;
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

    public StocksViewModel(IStockCardRepository stockCards, IDialogService dialogService)
    {
        _stockCards = stockCards;
        _dialogService = dialogService;

        DataEvents.DataChanged += OnDataChanged;
    }

    private void OnDataChanged(DataDomain domain)
    {
        if (domain == DataDomain.StockCards || domain == DataDomain.Movements)
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
            int threshold = AppServices.TryGetCurrent(out var app) ? app!.Settings.LowStockThreshold : 3;

            var summary = await _stockCards.GetChildStockSummaryAsync(term, threshold, token);
            _totalCount = summary.TotalCount;
            _lowStockCount = summary.LowStockCount;
            _depletedCount = summary.DepletedCount;

            TotalPages = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var data = await _stockCards.GetPagedAsync(CurrentPage, PageSize, term, "Child", token);
            token.ThrowIfCancellationRequested();

            Stocks.ReplaceAll(data);
            _isLoaded = true;
            StatusText = $"{LocalizationManager.L("total_records_page", _totalCount, CurrentPage, TotalPages)} | ⚠ {_lowStockCount} {LocalizationManager.L("low_stock")} | ❌ {_depletedCount} {LocalizationManager.L("depleted")}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; }
        finally
        {
            if (token == _loadingCts?.Token)
                IsLoading = false;
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
            AppLogger.LogError("Filter schedule error", ex);
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

    private async Task<List<StockCard>> FetchAllMatchingStocksAsync()
    {
        if (_totalCount <= Stocks.Count && Stocks.Count > 0)
            return Stocks.ToList();

        string? term = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
        var all = new List<StockCard>();
        const int batchSize = 5000;
        int page = 1;
        while (true)
        {
            var batch = await _stockCards.GetPagedAsync(page, batchSize, term, "Child");
            if (batch == null || batch.Count == 0) break;
            all.AddRange(batch);
            if (batch.Count < batchSize) break;
            page++;
        }
        return all.Count > 0 ? all : Stocks.ToList();
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearSearch() => SearchText = "";

    [RelayCommand]
    public async Task Export()
    {
        if (_totalCount == 0 && Stocks.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
            return;
        }

        string baseName = LocalizationManager.L("export_fn_stocks");
        string fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        var path = await _dialogService.SaveFileAsync(LocalizationManager.L("export_excel"), fileName, "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            var snapshot = await FetchAllMatchingStocksAsync();
            if (snapshot.Count == 0) return;
            await Task.Run(() => 
            {
                string[] headers = { 
                    LocalizationManager.L("code_no"), 
                    LocalizationManager.L("stock_name"), 
                    LocalizationManager.L("category"), 
                    LocalizationManager.L("current_stock"), 
                    LocalizationManager.L("min_stock"), 
                    LocalizationManager.L("unit") 
                };
                ExcelService.ExportToExcel(path, "StockStatus", headers, snapshot, s => new object?[]
                {
                    s.Code,
                    s.Name,
                    s.Category,
                    s.CurrentStock,
                    s.MinStock,
                    s.Unit
                });
            });

            StatusText = LocalizationManager.L("export_success", path);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("export_success", path));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Stocks export error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("export_error")}: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task Print()
    {
        if (_totalCount == 0 && Stocks.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
            return;
        }

        try
        {
            StatusText = LocalizationManager.L("preparing_print");
            var snapshot = await FetchAllMatchingStocksAsync();
            if (snapshot.Count == 0)
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
                return;
            }
            string title = LocalizationManager.L("stock_status_report");
            string dateInfo = LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));

            string html = await Task.Run(() =>
            {
                double grandTotalIn = snapshot.Sum(s => s.TotalEntry);
                double grandTotalOut = snapshot.Sum(s => s.TotalExit);
                double grandTotalResult = grandTotalIn - grandTotalOut;

                var sbHeaders = new System.Text.StringBuilder();
                sbHeaders.Append($"<th style='width: 18%;'>{LocalizationManager.L("code_no")}</th>");
                sbHeaders.Append($"<th style='width: 34%; text-align: left; padding-left: 8px;'>{LocalizationManager.L("stock_name")}</th>");
                sbHeaders.Append($"<th style='width: 16%;'>{LocalizationManager.L("entry")}</th>");
                sbHeaders.Append($"<th style='width: 16%;'>{LocalizationManager.L("exit")}</th>");
                sbHeaders.Append($"<th style='width: 16%;'>{LocalizationManager.L("current_stock")}</th>");

                var sbBody = new System.Text.StringBuilder(snapshot.Count * 160);
                foreach (var s in snapshot)
                {
                    sbBody.Append("<tr>");
                    sbBody.Append($"<td class='num'>{PrintTemplateBuilder.Escape(s.Code)}</td>");
                    sbBody.Append($"<td style='text-align: left; padding-left: 8px;'>{PrintTemplateBuilder.Escape(s.Name)}</td>");
                    sbBody.Append($"<td class='num'>{s.TotalEntry}</td>");
                    sbBody.Append($"<td class='num'>{s.TotalExit}</td>");
                    sbBody.Append($"<td class='num bold'>{s.CurrentStock}</td>");
                    sbBody.Append("</tr>");
                }

                // Grand Total Row
                sbBody.Append("<tr class='footer-row'>");
                sbBody.Append($"<td colspan='2' style='text-align: right; padding-right: 15px;'>{LocalizationManager.L("grand_total")}</td>");
                sbBody.Append($"<td class='num'>{grandTotalIn}</td>");
                sbBody.Append($"<td class='num'>{grandTotalOut}</td>");
                sbBody.Append($"<td class='num'>{grandTotalResult}</td>");
                sbBody.Append("</tr>");

                return PrintTemplateBuilder.BuildReportHtml(
                    title: title,
                    headerTitle: title.ToUpper(),
                    dateInfo: dateInfo,
                    tableHeadersHtml: sbHeaders.ToString(),
                    tableBodyHtml: sbBody.ToString(),
                    footerHtml: ""
                );
            });
            
            var previewVm = new PrintPreviewViewModel(title, html);
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = LocalizationManager.L("printing_finished");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Stocks print error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("export_error")}: {ex.Message}");
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _isLoaded = false;
    }
}
