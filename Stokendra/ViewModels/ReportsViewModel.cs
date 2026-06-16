using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IStockCardRepository _stockCards;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private string _selectedDepartment = LocalizationManager.L("all");
    [ObservableProperty] private string _selectedCategory = LocalizationManager.L("all");
    [ObservableProperty] private string _selectedUser = LocalizationManager.L("all");
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private double _totalConsumption;
    [ObservableProperty] private int _uniqueItemCount;
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<string> Departments { get; } = new() { LocalizationManager.L("all") };
    public ObservableCollection<string> Categories { get; } = new() { LocalizationManager.L("all") };
    public ObservableCollection<string> Users { get; } = new() { LocalizationManager.L("all") };
    public ObservableCollection<StockMovement> ReportRows { get; } = new();

    public List<(string Name, double Total)> ChartData { get; private set; } = new();

    public ReportsViewModel(
        IMovementRepository movements,
        IDepartmentRepository departments,
        IStockCardRepository stockCards,
        IDialogService dialogService,
        ILogger logger)
    {
        _movements = movements;
        _departments = departments;
        _stockCards = stockCards;
        _dialogService = dialogService;
        _logger = logger;
        
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try 
        {
            var depts = await _departments.GetAllAsync();
            foreach (var d in depts) Departments.Add(d);

            var cards = await _stockCards.GetChildCardsAsync();
            var cats = cards.Select(c => c.Category).Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k);
            foreach (var c in cats) Categories.Add(c!);

            var users = _movements.GetDeliveredPersons();
            foreach (var u in users) Users.Add(u);

            await GenerateReport();
        }
        catch (Exception ex)
        {
            _logger.LogError("Reports initialization error", ex);
        }
    }

    [RelayCommand]
    public async Task GenerateReport()
    {
        IsLoading = true;
        try
        {
            string? dept = SelectedDepartment == LocalizationManager.L("all") ? null : SelectedDepartment;
            string? cat = SelectedCategory == LocalizationManager.L("all") ? null : SelectedCategory;
            string? user = SelectedUser == LocalizationManager.L("all") ? null : SelectedUser;

            // Büyük veri setlerinde bellek baskısını önlemek için sayfalı yükleme
            const int batchSize = 5000;
            int page = 1;
            var allMovements = new List<StockMovement>();

            while (true)
            {
                var batch = await _movements.GetPagedAsync(
                    page, batchSize,
                    stockCardId: null,
                    startDate: StartDate,
                    endDate: EndDate.AddDays(1),
                    department: dept,
                    movementType: "Exit",
                    category: cat,
                    searchTerm: null,
                    recipient: user);

                if (batch.Count == 0) break;
                allMovements.AddRange(batch);
                if (batch.Count < batchSize) break;
                page++;
            }

            ReportRows.Clear();
            double total = 0;
            var uniqueIds = new HashSet<int>();
            var totalsByCard = new Dictionary<string, double>();

            foreach (var m in allMovements)
            {
                ReportRows.Add(m);
                total += m.Quantity;
                uniqueIds.Add(m.StockCardId);
                
                if (totalsByCard.ContainsKey(m.StockCardName))
                    totalsByCard[m.StockCardName] += m.Quantity;
                else
                    totalsByCard[m.StockCardName] = m.Quantity;
            }

            TotalConsumption = total;
            UniqueItemCount = uniqueIds.Count;
            StatusText = string.Format(LocalizationManager.L("records_found"), allMovements.Count);

            ChartData = totalsByCard.OrderByDescending(x => x.Value).Take(10)
                .Select(x => (x.Key, x.Value)).ToList();
            
            OnPropertyChanged(nameof(ChartData));
        }
        catch (Exception ex)
        {
            _logger.LogError("Report generation error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("report_generation_failed"), ex.Message));
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task Export()
    {
        if (ReportRows.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("no_data_to_export"));
            return;
        }

        var path = await _dialogService.SaveFileAsync(LocalizationManager.L("save_report_title"), "Stok_Tuketim_Raporu.xlsx", LocalizationManager.L("svc_excel_filter"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            string[] headers = { 
                LocalizationManager.L("date"), 
                LocalizationManager.L("stock_card_header"), 
                LocalizationManager.L("quantity"), 
                LocalizationManager.L("delivered_to_header"), 
                LocalizationManager.L("department"), 
                LocalizationManager.L("description") 
            };
            ExcelService.ExportToExcel(path, "TuketimRaporu", headers, ReportRows, r => new object?[]
            {
                r.Date.ToString("dd.MM.yyyy HH:mm"),
                r.StockCardName,
                r.Quantity,
                r.Recipient,
                r.Department,
                r.Description
            });

            await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("report_saved_success"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Report export error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("report_save_failed"), ex.Message));
        }
    }

    [RelayCommand]
    public async Task Print()
    {
        if (ReportRows.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("no_data_to_print"));
            return;
        }

        try
        {
            StatusText = LocalizationManager.L("reports_print_preparing");
            string tempPath = Path.Combine(Path.GetTempPath(), $"Stok_Tuketim_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.html");
            
            var sb = new System.Text.StringBuilder();
            sb.Append($"<html><head><meta charset='utf-8'><title>{LocalizationManager.L("consumption_report_title")}</title>");
            sb.Append("<style>");
            sb.Append("@page { size: portrait; margin: 1cm; } ");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; color: #333; } ");
            sb.Append(".top-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 3px solid #2563EB; padding-bottom: 10px; margin-bottom: 25px; } ");
            sb.Append(".top-header h1 { margin: 0; color: #2563EB; font-size: 28px; } ");
            sb.Append(".date-box { text-align: right; font-size: 12px; color: #64748b; } ");
            sb.Append("table { width: 100%; border-collapse: collapse; margin-top: 20px; } ");
            sb.Append("th, td { border: 1px solid #ccc; padding: 12px; text-align: left; font-size: 13px; } ");
            sb.Append("th { background: #f8f9fa; font-weight: bold; color: #2563EB; } ");
            sb.Append(".summary { display: flex; justify-content: space-between; margin-bottom: 20px; background: #f1f5f9; padding: 15px; border-radius: 8px; } ");
            sb.Append(".summary-item { text-align: center; flex: 1; } ");
            sb.Append(".summary-value { font-size: 20px; font-weight: bold; color: #1e293b; } ");
            sb.Append(".summary-label { font-size: 11px; color: #64748b; text-transform: uppercase; } ");
            sb.Append(".footer { margin-top: 40px; font-size: 11px; text-align: right; color: #94a3b8; border-top: 1px solid #e2e8f0; padding-top: 10px; } ");
            sb.Append("</style>");
            sb.Append("<script>window.onload = function() { window.print(); }</script>");
            sb.Append("</head><body>");
            
            sb.Append("<div class='top-header'>");
            sb.Append($"<h1>{LocalizationManager.L("consumption_report_header")}</h1>");
            sb.Append($"<div class='date-box'>{LocalizationManager.L("report_date_label")}<br/><b>{DateTime.Now:dd.MM.yyyy HH:mm}</b><br/>{StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}</div>");
            sb.Append("</div>");

            sb.Append("<div class='summary'>");
            sb.Append($"<div class='summary-item'><div class='summary-value'>{TotalConsumption}</div><div class='summary-label'>{LocalizationManager.L("total_consumption")}</div></div>");
            sb.Append($"<div class='summary-item'><div class='summary-value'>{UniqueItemCount}</div><div class='summary-label'>{LocalizationManager.L("unique_items")}</div></div>");
            sb.Append($"<div class='summary-item'><div class='summary-value'>{ReportRows.Count}</div><div class='summary-label'>{LocalizationManager.L("transaction_count")}</div></div>");
            sb.Append("</div>");

            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LocalizationManager.L("date")}</th>");
            sb.Append($"<th>{LocalizationManager.L("stock_name")}</th>");
            sb.Append($"<th>{LocalizationManager.L("quantity")}</th>");
            sb.Append($"<th>{LocalizationManager.L("department")}</th>");
            sb.Append($"<th>{LocalizationManager.L("delivered_to_header")}</th>");
            sb.Append("</tr></thead><tbody>");
            
            foreach (var r in ReportRows)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{r.Date:dd.MM.yyyy HH:mm}</td>");
                sb.Append($"<td>{r.StockCardName}</td>");
                sb.Append($"<td>{r.Quantity}</td>");
                sb.Append($"<td>{r.Department}</td>");
                sb.Append($"<td>{r.Recipient}</td>");
                sb.Append("</tr>");
            }
            
            sb.Append("</tbody></table>");
            sb.Append($"<div class='footer'>{string.Format(LocalizationManager.L("printed_by"), DateTime.Now.ToString("dd.MM.yyyy HH:mm"), AppServices.Current.Session?.Username ?? "admin")}</div>");
            sb.Append("</body></html>");
            
            var previewVm = new PrintPreviewViewModel(LocalizationManager.L("consumption_report_title"), sb.ToString());
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = LocalizationManager.L("print_done_or_cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError("Report print error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("print_window_open_failed"), ex.Message));
        }
    }

    [RelayCommand]
    public void ClearFilters()
    {
        StartDate = DateTime.Today.AddDays(-30);
        EndDate = DateTime.Today;
        SelectedDepartment = LocalizationManager.L("all");
        SelectedCategory = LocalizationManager.L("all");
        SelectedUser = LocalizationManager.L("all");
        _ = GenerateReport();
    }
}
