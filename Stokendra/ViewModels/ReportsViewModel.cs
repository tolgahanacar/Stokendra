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
    [ObservableProperty] private DateTime? _startDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _endDate = DateTime.Today;
    [ObservableProperty] private string _selectedDepartment = LocalizationManager.L("all");
    [ObservableProperty] private string _selectedCategory = LocalizationManager.L("all");
    [ObservableProperty] private string _selectedUser = LocalizationManager.L("all");
    [ObservableProperty] private StockCardReportItem _selectedStockCard = StockCardReportItem.All;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private double _totalConsumption;
    [ObservableProperty] private int _uniqueItemCount;
    [ObservableProperty] private string _statusText = "";

    private const int PageSize = 100;
    private DateTime _activeStartDate;
    private DateTime _activeEndDate;
    private string? _activeDept;
    private string? _activeCat;
    private string? _activeUser;
    private int? _activeCardId;
    private int _totalRowCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _totalPages = 1;

    public bool CanPrevPage => CurrentPage > 1;
    public bool CanNextPage => CurrentPage < TotalPages;

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

            var depts = await _departments.GetAllAsync() ?? new List<string>();
            var cardsLookup = await _stockCards.GetCardLookupsAsync("Child");
            var cards = (cardsLookup != null && cardsLookup.Count > 0) ? cardsLookup : (await _stockCards.GetChildCardsAsync() ?? new List<StockCard>());
            var cats = cards.Select(c => c.Category).Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k).ToList();
            var sortedCards = cards.OrderBy(c => c.Code).ThenBy(c => c.Name).ToList();
            var users = await _movements.GetDeliveredPersonsAsync() ?? _movements.GetDeliveredPersons();

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
            string allLabel = LocalizationManager.L("all");
            _activeDept = SelectedDepartment == allLabel ? null : SelectedDepartment;
            _activeCat = SelectedCategory == allLabel ? null : SelectedCategory;
            _activeUser = SelectedUser == allLabel ? null : SelectedUser;
            _activeCardId = SelectedStockCard?.Id;

            _activeStartDate = (StartDate ?? new DateTime(2024, 1, 1)).Date;
            DateTime endVal = (EndDate ?? DateTime.Today).Date;
            _activeEndDate = endVal >= DateTime.MaxValue.Date ? DateTime.MaxValue : endVal.AddDays(1);
            CurrentPage = 1;

            var summaryTask = _movements.GetConsumptionSummaryAsync(
                stockCardId: _activeCardId,
                startDate: _activeStartDate,
                endDate: _activeEndDate,
                department: _activeDept,
                movementType: "Exit",
                category: _activeCat,
                searchTerm: null,
                recipient: _activeUser,
                fallbackLabel: allLabel,
                topN: 10,
                cancellationToken: token);

            var pageTask = _movements.GetPagedAsync(
                CurrentPage, PageSize,
                stockCardId: _activeCardId,
                startDate: _activeStartDate,
                endDate: _activeEndDate,
                department: _activeDept,
                movementType: "Exit",
                category: _activeCat,
                searchTerm: null,
                recipient: _activeUser,
                cancellationToken: token);

            await Task.WhenAll(summaryTask, pageTask);
            token.ThrowIfCancellationRequested();

            var summary = await summaryTask;
            var pageRows = await pageTask ?? new List<StockMovement>();

            if (summary != null)
            {
                _totalRowCount = summary.TotalRowCount;
                TotalConsumption = summary.TotalConsumption;
                UniqueItemCount = summary.UniqueItemCount;
                ChartData = summary.ChartData;
            }
            else
            {
                _totalRowCount = pageRows.Count;
                TotalConsumption = pageRows.Sum(m => m.Quantity);
                UniqueItemCount = pageRows.Select(m => m.StockCardId).Distinct().Count();
                ChartData = BuildFallbackChartData(pageRows, _activeCardId, _activeDept, allLabel);
            }

            TotalPages = Math.Max(1, (int)Math.Ceiling(_totalRowCount / (double)PageSize));
            ReportRows = pageRows;
            StatusText = LocalizationManager.L("total_records_page", _totalRowCount, CurrentPage, TotalPages);
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

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    public async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadPageAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrevPage))]
    public async Task PrevPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadPageAsync();
        }
    }

    private async Task LoadPageAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new System.Threading.CancellationTokenSource();
        var token = _cts.Token;

        IsLoading = true;
        try
        {
            var pageRows = await _movements.GetPagedAsync(
                CurrentPage, PageSize,
                stockCardId: _activeCardId,
                startDate: _activeStartDate,
                endDate: _activeEndDate,
                department: _activeDept,
                movementType: "Exit",
                category: _activeCat,
                searchTerm: null,
                recipient: _activeUser,
                cancellationToken: token);

            token.ThrowIfCancellationRequested();
            ReportRows = pageRows ?? new List<StockMovement>();
            StatusText = LocalizationManager.L("total_records_page", _totalRowCount, CurrentPage, TotalPages);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.LogError("Report page load error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}";
        }
        finally
        {
            if (token == _cts?.Token)
                IsLoading = false;
        }
    }

    private static List<(string Name, double Total)> BuildFallbackChartData(
        List<StockMovement> movements, int? cardId, string? dept, string fallbackLabel)
    {
        var totalsGrouped = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in movements)
        {
            string groupKey = cardId.HasValue
                ? (!string.IsNullOrEmpty(dept)
                    ? (!string.IsNullOrWhiteSpace(m.Recipient) ? m.Recipient : fallbackLabel)
                    : (!string.IsNullOrWhiteSpace(m.Department) ? m.Department : fallbackLabel))
                : (!string.IsNullOrWhiteSpace(m.StockCardCode) ? $"{m.StockCardCode} - {m.StockCardName}" : m.StockCardName);

            totalsGrouped[groupKey] = totalsGrouped.TryGetValue(groupKey, out double cur) ? cur + m.Quantity : m.Quantity;
        }
        return totalsGrouped.OrderByDescending(x => x.Value).Take(10).Select(x => (x.Key, x.Value)).ToList();
    }

    private async Task<List<StockMovement>> FetchAllFilteredMovementsAsync()
    {
        if (_totalRowCount <= ReportRows.Count && ReportRows.Count > 0)
            return ReportRows.ToList();

        var all = new List<StockMovement>();
        DateTime start = _activeStartDate != default ? _activeStartDate : (StartDate ?? new DateTime(2024, 1, 1)).Date;
        DateTime endVal = (EndDate ?? DateTime.Today).Date;
        DateTime end = _activeEndDate != default ? _activeEndDate : (endVal >= DateTime.MaxValue.Date ? DateTime.MaxValue : endVal.AddDays(1));
        const int batchSize = 5000;
        int page = 1;
        while (true)
        {
            var batch = await _movements.GetPagedAsync(
                page, batchSize,
                stockCardId: _activeCardId,
                startDate: start,
                endDate: end,
                department: _activeDept,
                movementType: "Exit",
                category: _activeCat,
                searchTerm: null,
                recipient: _activeUser);
            if (batch == null || batch.Count == 0) break;
            all.AddRange(batch);
            if (batch.Count < batchSize) break;
            page++;
        }
        return all.Count > 0 ? all : ReportRows.ToList();
    }

    [RelayCommand]
    public async Task Export()
    {
        if (_totalRowCount == 0 && ReportRows.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("no_data_to_export"));
            return;
        }

        string baseName = LocalizationManager.L("export_fn_reports");
        string fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        var path = await _dialogService.SaveFileAsync(LocalizationManager.L("save_report_title"), fileName, LocalizationManager.L("svc_excel_filter"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            var snapshot = await FetchAllFilteredMovementsAsync();
            if (snapshot.Count == 0)
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
                return;
            }

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
        if (_totalRowCount == 0 && ReportRows.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("no_data_to_print"));
            return;
        }

        try
        {
            StatusText = LocalizationManager.L("reports_print_preparing");
            var rowsSnapshot = await FetchAllFilteredMovementsAsync();
            double totalConsumption = TotalConsumption;
            int uniqueItemCount = UniqueItemCount;
            string dateRange = $"{(StartDate ?? new DateTime(2024, 1, 1)):dd.MM.yyyy} - {(EndDate ?? DateTime.Today):dd.MM.yyyy}";
            string printedUser = PrintTemplateBuilder.Escape(AppServices.TryGetCurrent(out var app) ? (app?.Session?.Username ?? "admin") : "admin");
            string title = LocalizationManager.L("consumption_report_title");
            string headerTitle = LocalizationManager.L("consumption_report_header");

            string html = await Task.Run(() =>
            {
                var sbHeaders = new System.Text.StringBuilder();
                sbHeaders.Append($"<th>{LocalizationManager.L("date")}</th>");
                sbHeaders.Append($"<th>{LocalizationManager.L("code_no")}</th>");
                sbHeaders.Append($"<th>{LocalizationManager.L("stock_name")}</th>");
                sbHeaders.Append($"<th>{LocalizationManager.L("quantity")}</th>");
                sbHeaders.Append($"<th>{LocalizationManager.L("department")}</th>");
                sbHeaders.Append($"<th>{LocalizationManager.L("delivered_to_header")}</th>");

                var sbBody = new System.Text.StringBuilder(rowsSnapshot.Count * 160);
                foreach (var r in rowsSnapshot)
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
                    $"<div class='summary-item'><div class='summary-value'>{totalConsumption}</div><div class='summary-label'>{LocalizationManager.L("total_consumption")}</div></div>" +
                    $"<div class='summary-item'><div class='summary-value'>{uniqueItemCount}</div><div class='summary-label'>{LocalizationManager.L("unique_items")}</div></div>" +
                    $"<div class='summary-item'><div class='summary-value'>{rowsSnapshot.Count}</div><div class='summary-label'>{LocalizationManager.L("transaction_count")}</div></div>" +
                    "</div>";

                return PrintTemplateBuilder.BuildReportHtml(
                    title: title,
                    headerTitle: headerTitle,
                    dateInfo: LocalizationManager.L("report_date_label") + $"<br/><b>{DateTime.Now:dd.MM.yyyy HH:mm}</b><br/>{dateRange}",
                    tableHeadersHtml: sbHeaders.ToString(),
                    tableBodyHtml: sbBody.ToString(),
                    footerHtml: string.Format(
                        LocalizationManager.L("printed_by"),
                        DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                        printedUser),
                    additionalSummaryHtml: summaryHtml
                );
            });
            
            var previewVm = new PrintPreviewViewModel(title, html);
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
