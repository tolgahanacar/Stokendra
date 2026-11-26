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

public record StockCardReportItem(int? Id, string DisplayName)
{
    public static StockCardReportItem All => new(null, LocalizationManager.L("all"));
    public override string ToString() => DisplayName;
}

public partial class ReportsViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IStockCardRepository _stockCards;
    private readonly IDialogService _dialogService;
    [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private string _selectedDepartment = LocalizationManager.L("all");
    [ObservableProperty] private string _selectedCategory = LocalizationManager.L("all");
    [ObservableProperty] private string _selectedUser = LocalizationManager.L("all");
    [ObservableProperty] private StockCardReportItem _selectedStockCard = StockCardReportItem.All;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private double _totalConsumption;
    [ObservableProperty] private int _uniqueItemCount;
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<string> Departments { get; } = new() { LocalizationManager.L("all") };
    public ObservableCollection<string> Categories { get; } = new() { LocalizationManager.L("all") };
    public ObservableCollection<string> Users { get; } = new() { LocalizationManager.L("all") };
    public ObservableCollection<StockCardReportItem> StockCards { get; } = new() { StockCardReportItem.All };
    [ObservableProperty] private List<StockMovement> _reportRows = new();

    public List<(string Name, double Total)> ChartData { get; private set; } = new();

    private bool _isLoaded;
    private bool _filtersDirty = true;

    public ReportsViewModel(
        IMovementRepository movements,
        IDepartmentRepository departments,
        IStockCardRepository stockCards,
        IDialogService dialogService)
    {
        _movements = movements;
        _departments = departments;
        _stockCards = stockCards;
        _dialogService = dialogService;
        DataEvents.DataChanged += OnDataChanged;
        AsyncHelper.RunSafe(
            () => EnsureLoadedAsync(),
            ex => AppLogger.LogError("Reports initialize failed", ex));
    }

    private void OnDataChanged(DataDomain domain)
    {
        if (domain is DataDomain.Movements or DataDomain.StockCards or DataDomain.Departments)
        {
            if (domain is DataDomain.StockCards or DataDomain.Departments or DataDomain.Movements)
                _filtersDirty = true;
            _isLoaded = false;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_filtersDirty || !_isLoaded)
        {
            await InitializeAsync();
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _filtersDirty = true;
        _isLoaded = false;
    }

    private async Task InitializeAsync()
    {
        try
        {
            string prevDept = SelectedDepartment;
            string prevCat = SelectedCategory;
            string prevUser = SelectedUser;
            int? prevCardId = SelectedStockCard?.Id;
            string allLabel = LocalizationManager.L("all");

            var depts = await _departments.GetAllAsync();
            var cards = await _stockCards.GetChildCardsAsync();
            var cats = cards.Select(c => c.Category).Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k).ToList();
            var sortedCards = cards.OrderBy(c => c.Code).ThenBy(c => c.Name).ToList();
            var users = _movements.GetDeliveredPersons();

            Departments.Clear();
            Departments.Add(allLabel);
            foreach (var d in depts) Departments.Add(d);

            Categories.Clear();
            Categories.Add(allLabel);
            foreach (var c in cats) Categories.Add(c!);

            StockCards.Clear();
            var allCardItem = StockCardReportItem.All;
            StockCards.Add(allCardItem);
            foreach (var c in sortedCards)
            {
                string display = !string.IsNullOrWhiteSpace(c.Code) ? $"{c.Code} - {c.Name}" : c.Name;
                StockCards.Add(new StockCardReportItem(c.Id, display));
            }

            Users.Clear();
            Users.Add(allLabel);
            foreach (var u in users) Users.Add(u);

            SelectedDepartment = Departments.FirstOrDefault(d => string.Equals(d, prevDept, StringComparison.OrdinalIgnoreCase)) ?? allLabel;
            SelectedCategory = Categories.FirstOrDefault(c => string.Equals(c, prevCat, StringComparison.OrdinalIgnoreCase)) ?? allLabel;
            SelectedUser = Users.FirstOrDefault(u => string.Equals(u, prevUser, StringComparison.OrdinalIgnoreCase)) ?? allLabel;
            SelectedStockCard = prevCardId.HasValue
                ? StockCards.FirstOrDefault(sc => sc.Id == prevCardId.Value) ?? allCardItem
                : allCardItem;

            _filtersDirty = false;
            await GenerateReport();
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Reports initialization error", ex);
        }
    }

    private System.Threading.CancellationTokenSource? _cts;

    [RelayCommand]
    public async Task GenerateReport()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new System.Threading.CancellationTokenSource();
        var token = _cts.Token;

        IsLoading = true;
        try
        {
            string? dept = SelectedDepartment == LocalizationManager.L("all") ? null : SelectedDepartment;
            string? cat = SelectedCategory == LocalizationManager.L("all") ? null : SelectedCategory;
            string? user = SelectedUser == LocalizationManager.L("all") ? null : SelectedUser;
            int? cardId = SelectedStockCard?.Id;

            // Büyük veri setlerinde bellek baskısını önlemek için sayfalı yükleme
            const int batchSize = 5000;
            int page = 1;
            var allMovements = new List<StockMovement>();

            while (true)
            {
                token.ThrowIfCancellationRequested();
                DateTime safeEnd = EndDate.Date >= DateTime.MaxValue.Date ? DateTime.MaxValue : EndDate.Date.AddDays(1);
                var batch = await _movements.GetPagedAsync(
                    page, batchSize,
                    stockCardId: cardId,
                    startDate: StartDate.Date,
                    endDate: safeEnd,
                    department: dept,
                    movementType: "Exit",
                    category: cat,
                    searchTerm: null,
                    recipient: user,
                    cancellationToken: token);

                if (batch.Count == 0) break;
                allMovements.AddRange(batch);
                if (batch.Count < batchSize) break;
                page++;
            }

            token.ThrowIfCancellationRequested();

            var rowsList = new List<StockMovement>();
            double total = 0;
            var uniqueIds = new HashSet<int>();
            var totalsGrouped = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in allMovements)
            {
                rowsList.Add(m);
                total += m.Quantity;
                uniqueIds.Add(m.StockCardId);
                
                string groupKey;
                if (cardId.HasValue)
                {
                    // Belirli bir stok kartı seçildiyse grafiği Birim/Departman (veya Departman seçiliyse Teslim Alan) kırılımında grupla
                    if (!string.IsNullOrEmpty(dept))
                        groupKey = !string.IsNullOrWhiteSpace(m.Recipient) ? m.Recipient : LocalizationManager.L("all");
                    else
                        groupKey = !string.IsNullOrWhiteSpace(m.Department) ? m.Department : LocalizationManager.L("all");
                }
                else
                {
                    groupKey = !string.IsNullOrWhiteSpace(m.StockCardCode)
                        ? $"{m.StockCardCode} - {m.StockCardName}"
                        : m.StockCardName;
                }

                if (totalsGrouped.ContainsKey(groupKey))
                    totalsGrouped[groupKey] += m.Quantity;
                else
                    totalsGrouped[groupKey] = m.Quantity;
            }

            ReportRows = rowsList;

            TotalConsumption = total;
            UniqueItemCount = uniqueIds.Count;
            StatusText = string.Format(LocalizationManager.L("records_found"), allMovements.Count);

            ChartData = totalsGrouped.OrderByDescending(x => x.Value).Take(10)
                .Select(x => (x.Key, x.Value)).ToList();
            
            OnPropertyChanged(nameof(ChartData));
        }
        catch (OperationCanceledException)
        {
            // Ignored, operation was intentionally cancelled
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Report generation error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("report_generation_failed"), ex.Message));
        }
        finally
        {
            if (token == _cts?.Token)
                IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task Export()
    {
        if (ReportRows.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("no_data_to_export"));
            return;
        }

        var snapshot = ReportRows.ToList();
        if (snapshot.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
            return;
        }

        string baseName = LocalizationManager.L("export_fn_reports");
        string fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        var path = await _dialogService.SaveFileAsync(LocalizationManager.L("save_report_title"), fileName, LocalizationManager.L("svc_excel_filter"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            await Task.Run(() => 
            {
                string[] headers = { 
                    LocalizationManager.L("date"), 
                    LocalizationManager.L("code_no"),
                    LocalizationManager.L("stock_card_header"), 
                    LocalizationManager.L("quantity"), 
                    LocalizationManager.L("delivered_to_header"), 
                    LocalizationManager.L("department"), 
                    LocalizationManager.L("description") 
                };
                ExcelService.ExportToExcel(path, "TuketimRaporu", headers, snapshot, r => new object?[]
                {
                    r.Date.ToString("dd.MM.yyyy HH:mm"),
                    r.StockCardCode,
                    r.StockCardName,
                    r.Quantity,
                    r.Recipient,
                    r.Department,
                    r.Description
                });
            });

            StatusText = LocalizationManager.L("export_success", path);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("success"), LocalizationManager.L("report_saved_success"));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Report export error", ex);
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
            
            var sbHeaders = new System.Text.StringBuilder();
            sbHeaders.Append($"<th>{LocalizationManager.L("date")}</th>");
            sbHeaders.Append($"<th>{LocalizationManager.L("code_no")}</th>");
            sbHeaders.Append($"<th>{LocalizationManager.L("stock_name")}</th>");
            sbHeaders.Append($"<th>{LocalizationManager.L("quantity")}</th>");
            sbHeaders.Append($"<th>{LocalizationManager.L("department")}</th>");
            sbHeaders.Append($"<th>{LocalizationManager.L("delivered_to_header")}</th>");
            
            var sbBody = new System.Text.StringBuilder();
            foreach (var r in ReportRows)
            {
                sbBody.Append("<tr>");
                sbBody.Append($"<td>{r.Date:dd.MM.yyyy HH:mm}</td>");
                sbBody.Append($"<td>{PrintTemplateBuilder.Escape(r.StockCardCode)}</td>");
                sbBody.Append($"<td>{PrintTemplateBuilder.Escape(r.StockCardName)}</td>");
                sbBody.Append($"<td>{r.Quantity}</td>");
                sbBody.Append($"<td>{PrintTemplateBuilder.Escape(r.Department)}</td>");
                sbBody.Append($"<td>{PrintTemplateBuilder.Escape(r.Recipient)}</td>");
                sbBody.Append("</tr>");
            }
            
            string summaryHtml = "<div class='summary'>" +
                $"<div class='summary-item'><div class='summary-value'>{TotalConsumption}</div><div class='summary-label'>{LocalizationManager.L("total_consumption")}</div></div>" +
                $"<div class='summary-item'><div class='summary-value'>{UniqueItemCount}</div><div class='summary-label'>{LocalizationManager.L("unique_items")}</div></div>" +
                $"<div class='summary-item'><div class='summary-value'>{ReportRows.Count}</div><div class='summary-label'>{LocalizationManager.L("transaction_count")}</div></div>" +
                "</div>";
            
            string html = PrintTemplateBuilder.BuildReportHtml(
                title: LocalizationManager.L("consumption_report_title"),
                headerTitle: LocalizationManager.L("consumption_report_header"),
                dateInfo: LocalizationManager.L("report_date_label") + $"<br/><b>{DateTime.Now:dd.MM.yyyy HH:mm}</b><br/>{StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}",
                tableHeadersHtml: sbHeaders.ToString(),
                tableBodyHtml: sbBody.ToString(),
                footerHtml: string.Format(
                    LocalizationManager.L("printed_by"),
                    DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                    PrintTemplateBuilder.Escape(AppServices.TryGetCurrent(out var app) ? (app?.Session?.Username ?? "admin") : "admin")),
                additionalSummaryHtml: summaryHtml
            );
            
            var previewVm = new PrintPreviewViewModel(LocalizationManager.L("consumption_report_title"), html);
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = LocalizationManager.L("print_done_or_cancelled");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Report print error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("print_window_open_failed"), ex.Message));
        }
    }

    [RelayCommand]
    public async Task ClearFiltersAsync()
    {
        StartDate = DateTime.Today.AddDays(-30);
        EndDate = DateTime.Today;
        SelectedDepartment = LocalizationManager.L("all");
        SelectedCategory = LocalizationManager.L("all");
        SelectedUser = LocalizationManager.L("all");
        SelectedStockCard = StockCardReportItem.All;
        await GenerateReport();
    }
}
