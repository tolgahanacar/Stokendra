using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class StockCardDetailViewModel : ViewModelBase
{
    private readonly int _cardId;
    private readonly IStockCardRepository _stockCards;
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddMovement))]
    private StockCard? _card;
    [ObservableProperty] private double _totalEntryLast30;
    [ObservableProperty] private double _totalExitLast30;
    [ObservableProperty] private bool _isLoading;

    public bool HasChanges { get; private set; }
    public bool CanAddMovement => CanEdit && Card != null && Card.CardTypeEnum != CardType.Parent;

    public override void RefreshSession()
    {
        base.RefreshSession();
        OnPropertyChanged(nameof(CanAddMovement));
    }

    public BulkObservableCollection<StockMovement> Movements { get; } = new();

    public StockCardDetailViewModel(
        int cardId,
        IStockCardRepository stockCards,
        IMovementRepository movements,
        IDepartmentRepository departments,
        IDialogService dialogService)
    {
        _cardId = cardId;
        _stockCards = stockCards;
        _movements = movements;
        _departments = departments;
        _dialogService = dialogService;
        AsyncHelper.RunSafe(
            () => LoadDataAsync(),
            ex => AppLogger.LogError("StockCardDetail load failed", ex));
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            Card = await _stockCards.GetByIdAsync(_cardId);
            if (Card == null) return;

            var all = await _movements.GetAllAsync(stockCardId: _cardId);
            Movements.ReplaceAll(all.OrderByDescending(h => h.Date));

            var son30 = all.Where(h => h.Date >= DateTime.Now.AddDays(-30)).ToList();
            TotalEntryLast30 = son30.Where(h => h.IsEntry).Sum(h => h.Quantity);
            TotalExitLast30  = son30.Where(h => h.IsExit).Sum(h => h.Quantity);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Load detail error", ex);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task AddMovementAsync()
    {
        if (!EnsureCanEditForSession()) return;
        if (Card == null || Card.CardTypeEnum == CardType.Parent) return;
        var vm = new AddMovementViewModel(_stockCards, _departments, preSelectedCard: Card);
        vm.SelectedCard = Card; // Auto-select the current card
        
        if (await _dialogService.ShowDialogAsync(vm) && vm.Result != null)
        {
            try
            {
                await _movements.AddAsync(vm.Result);
                HasChanges = true;
                DataEvents.NotifyChanged(DataDomain.Movements);
                DataEvents.NotifyChanged(DataDomain.StockCards);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Add movement from detail error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        if (Card == null) return;

        try
        {
            var cardSnapshot = Card;
            var movementsSnapshot = Movements.ToList();
            double entryLast30 = TotalEntryLast30;
            double exitLast30 = TotalExitLast30;
            string reportTitle = LocalizationManager.L("detail_report");

            string html = await Task.Run(() =>
            {
                var sb = new System.Text.StringBuilder(1024 + movementsSnapshot.Count * 160);
                sb.Append($"<html><head><meta charset='utf-8'><title>{reportTitle}</title>");
                sb.Append("<style>");
                sb.Append("body { font-family: 'Segoe UI', Arial, sans-serif; padding: 20px; line-height: 1.4; } ");
                sb.Append(".header { border-bottom: 2px solid #2563EB; padding-bottom: 10px; margin-bottom: 20px; } ");
                sb.Append(".info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 20px; } ");
                sb.Append(".stat-box { background: #f8fafc; padding: 15px; border-radius: 8px; border: 1px solid #e2e8f0; } ");
                sb.Append("table { width: 100%; border-collapse: collapse; font-size: 12px; } ");
                sb.Append("th, td { border: 1px solid #cbd5e1; padding: 8px; text-align: left; } ");
                sb.Append("th { background: #f1f5f9; font-weight: bold; } ");
                sb.Append(".green { color: #16a34a; font-weight: bold; } ");
                sb.Append(".red { color: #dc2626; font-weight: bold; } ");
                sb.Append("</style>");
                sb.Append("<script>window.onload = function() { window.print(); }</script>");
                sb.Append("</head><body>");

                sb.Append("<div class='header'>");
                sb.Append($"<h1>{PrintTemplateBuilder.Escape(cardSnapshot.Name)}</h1>");
                sb.Append($"<p>{LocalizationManager.L("code_no")}: <b>{PrintTemplateBuilder.Escape(cardSnapshot.Code)}</b> | {LocalizationManager.L("category")}: <b>{PrintTemplateBuilder.Escape(cardSnapshot.Category)}</b> | {LocalizationManager.L("date")}: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
                sb.Append("</div>");

                sb.Append("<div class='info-grid'>");
                sb.Append($"<div class='stat-box'><b>{LocalizationManager.L("current_stock")}:</b><br/><span style='font-size: 24px;'>{cardSnapshot.CurrentStock} {PrintTemplateBuilder.Escape(cardSnapshot.Unit)}</span></div>");
                sb.Append($"<div class='stat-box'><b>{LocalizationManager.L("last_30_days")}:</b><br/><span class='green'>▲ {LocalizationManager.L("entry")}: {entryLast30}</span><br/><span class='red'>▼ {LocalizationManager.L("exit")}: {exitLast30}</span></div>");
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                sb.Append($"<th>{LocalizationManager.L("date")}</th><th>{LocalizationManager.L("type")}</th><th>{LocalizationManager.L("quantity")}</th><th>{LocalizationManager.L("department")}</th><th>{LocalizationManager.L("delivered_to")}</th><th>{LocalizationManager.L("description")}</th>");
                sb.Append("</tr></thead><tbody>");

                foreach (var h in movementsSnapshot)
                {
                    string clr = h.IsEntry ? "green" : "red";
                    sb.Append("<tr>");
                    sb.Append($"<td>{h.Date:dd.MM.yyyy HH:mm}</td>");
                    sb.Append($"<td class='{clr}'>{PrintTemplateBuilder.Escape(h.DisplayType)}</td>");
                    sb.Append($"<td class='{clr}'>{h.Quantity}</td>");
                    sb.Append($"<td>{PrintTemplateBuilder.Escape(h.Department)}</td>");
                    sb.Append($"<td>{PrintTemplateBuilder.Escape(h.Recipient)}</td>");
                    sb.Append($"<td>{PrintTemplateBuilder.Escape(h.Description)}</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</tbody></table>");
                sb.Append("</body></html>");
                return sb.ToString();
            });

            var previewVm = new PrintPreviewViewModel(reportTitle, html);
            await _dialogService.ShowDialogAsync(previewVm);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Print detail error", ex);
        }
    }

    [RelayCommand]
    public void Close() => CloseAction?.Invoke(true);

    public Action<bool>? CloseAction { get; set; }
}
