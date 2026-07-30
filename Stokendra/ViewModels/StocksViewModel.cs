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
    private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public bool IsNotLoading => !IsLoading;

    public BulkObservableCollection<StockCard> Stocks { get; } = new();
    private List<StockCard> _allStocks = new();

    // For Debounce
    private CancellationTokenSource? _filterCts;

    public StocksViewModel(IStockCardRepository stockCards, IDialogService dialogService)
    {
        _stockCards = stockCards;
        _dialogService = dialogService;
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
            _allStocks = await _stockCards.GetChildCardsAsync(null, token);
            ApplyFilter();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; }
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
            // 250ms debounce
            await Task.Delay(300, token);
            ApplyFilter();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.LogError("Filter schedule error", ex);
        }
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim().ToTurkishLower();
        var data = string.IsNullOrEmpty(term)
            ? _allStocks
            : _allStocks.Where(k =>
                k.Name.ToTurkishLower().Contains(term) ||
                k.Code.ToTurkishLower().Contains(term) ||
                k.Category.ToTurkishLower().Contains(term)).ToList();

        Stocks.Clear();
        Stocks.AddRange(data);
        
        int lowStockCount = data.Count(k => k.CurrentStock <= k.MinStock && k.CurrentStock > 0);
        int depletedCount = data.Count(k => k.CurrentStock <= 0);
        
        // Localized status text
        StatusText = $"{LocalizationManager.L("records_info", data.Count, 0).Split('•')[0].Trim()} | ⚠ {lowStockCount} {LocalizationManager.L("low_stock")} | ❌ {depletedCount} {LocalizationManager.L("depleted")}";
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearSearch() => SearchText = "";

    [RelayCommand]
    public async Task Export()
    {
        if (Stocks.Count == 0)
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
            string[] headers = { 
                LocalizationManager.L("code_no"), 
                LocalizationManager.L("stock_name"), 
                LocalizationManager.L("category"), 
                LocalizationManager.L("current_stock"), 
                LocalizationManager.L("min_stock"), 
                LocalizationManager.L("unit") 
            };
            ExcelService.ExportToExcel(path, "StockStatus", headers, Stocks, s => new object?[]
            {
                s.Code,
                s.Name,
                s.Category,
                s.CurrentStock,
                s.MinStock,
                s.Unit
            });

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
        if (Stocks.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
            return;
        }

        try
        {
            StatusText = LocalizationManager.L("preparing_print");
            
            double grandTotalIn = Stocks.Sum(s => s.TotalEntry);
            double grandTotalOut = Stocks.Sum(s => s.TotalExit);
            double grandTotalResult = grandTotalIn - grandTotalOut;

            var sbHeaders = new System.Text.StringBuilder();
            sbHeaders.Append($"<th style='width: 15%;'>{LocalizationManager.L("code_no")}</th>");
            sbHeaders.Append($"<th style='width: 45%; text-align: left; padding-left: 8px;'>{LocalizationManager.L("stock_name")}</th>");
            sbHeaders.Append($"<th style='width: 12%;'>{LocalizationManager.L("entry")}</th>");
            sbHeaders.Append($"<th style='width: 12%;'>{LocalizationManager.L("exit")}</th>");
            sbHeaders.Append($"<th style='width: 16%;'>{LocalizationManager.L("current_stock")}</th>");
            
            var sbBody = new System.Text.StringBuilder();
            foreach (var s in Stocks)
            {
                sbBody.Append("<tr>");
                sbBody.Append($"<td class='num'>{s.Code}</td>");
                sbBody.Append($"<td style='text-align: left; padding-left: 8px;'>{s.Name}</td>");
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
            
            string html = PrintTemplateBuilder.BuildReportHtml(
                title: LocalizationManager.L("detail_report"),
                headerTitle: LocalizationManager.L("detail_report").ToUpper(),
                dateInfo: LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")),
                tableHeadersHtml: sbHeaders.ToString(),
                tableBodyHtml: sbBody.ToString(),
                footerHtml: ""
            );
            
            var previewVm = new PrintPreviewViewModel(LocalizationManager.L("detail_report"), html);
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = LocalizationManager.L("printing_finished");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Stocks print error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("export_error")}: {ex.Message}");
        }
    }
}
