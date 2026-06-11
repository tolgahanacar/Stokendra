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
    private readonly ILogger _logger;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public bool IsNotLoading => !IsLoading;

    public ObservableCollection<StockCard> Stocks { get; } = new();
    private List<StockCard> _allStocks = new();

    // For Debounce
    private CancellationTokenSource? _filterCts;

    public StocksViewModel(IStockCardRepository stockCards, IDialogService dialogService, ILogger logger)
    {
        _stockCards = stockCards;
        _dialogService = dialogService;
        _logger = logger;
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

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _allStocks = await _stockCards.GetChildCardsAsync();
            ApplyFilter();
        }
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
            await Task.Delay(250, token);
            ApplyFilter();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError("Filter schedule error", ex);
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
        foreach (var k in data) Stocks.Add(k);
        
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

        var path = await _dialogService.SaveFileAsync(LocalizationManager.L("export_excel"), "Stock_Status_Report.xlsx", "Excel File (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            string[] headers = { 
                LocalizationManager.L("code_no"), 
                LocalizationManager.L("stock_name"), 
                LocalizationManager.L("category"), 
                LocalizationManager.L("current_stock"), 
                LocalizationManager.L("min_stock"), 
                LocalizationManager.L("unit"), 
                LocalizationManager.L("location") 
            };
            ExcelService.ExportToExcel(path, "StockStatus", headers, Stocks, s => new object?[]
            {
                s.Code,
                s.Name,
                s.Category,
                s.CurrentStock,
                s.MinStock,
                s.Unit,
                s.Location
            });

            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("export_success", path));
        }
        catch (Exception ex)
        {
            _logger.LogError("Stocks export error", ex);
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
            StatusText = "Preparing print...";
            
            double grandTotalIn = Stocks.Sum(s => s.TotalEntry);
            double grandTotalOut = Stocks.Sum(s => s.TotalExit);
            double grandTotalResult = grandTotalIn - grandTotalOut;

            var sb = new System.Text.StringBuilder();
            sb.Append($"<html><head><meta charset='utf-8'><title>{LocalizationManager.L("detail_report")}</title>");
            sb.Append("<style>");
            sb.Append("@page { size: portrait; margin: 0.5cm; } ");
            sb.Append("body { font-family: 'Segoe UI', Arial, sans-serif; padding: 10px; color: #1a1a1a; line-height: 1.2; } ");
            sb.Append(".top-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #1e293b; padding-bottom: 8px; margin-bottom: 15px; } ");
            sb.Append(".top-header h1 { margin: 0; color: #1e293b; font-size: 20px; font-weight: 800; } ");
            sb.Append(".date-box { text-align: right; font-size: 11px; color: #4b5563; } ");
            sb.Append("table { width: 100%; border-collapse: collapse; font-size: 11px; table-layout: fixed; } ");
            sb.Append("th, td { border: 1px solid #666; padding: 6px 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; } ");
            sb.Append("th { background: #f1f5f9; font-weight: bold; text-align: center; font-size: 10px; text-transform: uppercase; } ");
            sb.Append(".num { text-align: center; } ");
            sb.Append(".bold { font-weight: bold; } ");
            sb.Append(".footer-row { background: #f8fafc; font-weight: 800; } ");
            sb.Append(".footer-row td { border-top: 2px solid #1e293b; font-size: 12px; } ");
            sb.Append("@media print { body { -webkit-print-color-adjust: exact; } table { page-break-inside: auto; } tr { page-break-inside: avoid; page-break-after: auto; } } ");
            sb.Append("</style>");
            sb.Append("<script>window.onload = function() { window.print(); }</script>");
            sb.Append("</head><body>");
            
            sb.Append("<div class='top-header'>");
            sb.Append($"<h1>{LocalizationManager.L("detail_report").ToUpper()}</h1>");
            sb.Append($"<div class='date-box'>{LocalizationManager.L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm"))}</div>");
            sb.Append("</div>");

            sb.Append("<table><thead><tr>");
            sb.Append($"<th style='width: 15%;'>{LocalizationManager.L("code_no")}</th>");
            sb.Append($"<th style='width: 45%; text-align: left; padding-left: 8px;'>{LocalizationManager.L("stock_name")}</th>");
            sb.Append($"<th style='width: 12%;'>{LocalizationManager.L("entry")}</th>");
            sb.Append($"<th style='width: 12%;'>{LocalizationManager.L("exit")}</th>");
            sb.Append($"<th style='width: 16%;'>{LocalizationManager.L("current_stock")}</th>");
            sb.Append("</tr></thead><tbody>");
            
            foreach (var s in Stocks)
            {
                sb.Append("<tr>");
                sb.Append($"<td class='num'>{s.Code}</td>");
                sb.Append($"<td style='text-align: left; padding-left: 8px;'>{s.Name}</td>");
                sb.Append($"<td class='num'>{s.TotalEntry}</td>");
                sb.Append($"<td class='num'>{s.TotalExit}</td>");
                sb.Append($"<td class='num bold'>{s.CurrentStock}</td>");
                sb.Append("</tr>");
            }
            
            // Grand Total Row
            sb.Append("<tr class='footer-row'>");
            sb.Append($"<td colspan='2' style='text-align: right; padding-right: 15px;'>{LocalizationManager.L("grand_total")}</td>");
            sb.Append($"<td class='num'>{grandTotalIn}</td>");
            sb.Append($"<td class='num'>{grandTotalOut}</td>");
            sb.Append($"<td class='num'>{grandTotalResult}</td>");
            sb.Append("</tr>");

            sb.Append("</tbody></table>");
            sb.Append("</body></html>");
            
            var previewVm = new PrintPreviewViewModel(LocalizationManager.L("detail_report"), sb.ToString());
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = "Printing finished.";
        }
        catch (Exception ex)
        {
            _logger.LogError("Stocks print error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("export_error")}: {ex.Message}");
        }
    }
}
