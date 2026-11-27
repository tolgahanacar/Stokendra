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

public partial class BulkMovementItemViewModel : ObservableObject
{
    public StockCard Card { get; }
    
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double? _quantity;

    public BulkMovementItemViewModel(StockCard card)
    {
        Card = card;
    }
}

public partial class BulkMovementViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IStockCardRepository _stockCards;
    private readonly IDialogService _dialogService;
    [ObservableProperty] private int _selectedTypeIndex = 1; // 0: Entry, 1: Exit
    [ObservableProperty] private string _selectedDepartment = "";
    [ObservableProperty] private string _deliveredTo = "";
    [ObservableProperty] private DateTime? _date = DateTime.Now;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    public bool CanSave => !IsSaving;
    private System.Threading.CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value) => ScheduleSearch();

    private async void ScheduleSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(120, token);
            UpdateVisibleItems();
        }
        catch (OperationCanceledException) { }
    }

    public ObservableCollection<string> Departments { get; } = new();
    public BulkObservableCollection<BulkMovementItemViewModel> Items { get; } = new();
    
    private List<BulkMovementItemViewModel> _allItemViewModels = new();

    public BulkMovementViewModel(
        IMovementRepository movements,
        IDepartmentRepository departments,
        IStockCardRepository stockCards,
        IDialogService dialogService)
    {
        _movements = movements;
        _departments = departments;
        _stockCards = stockCards;
        _dialogService = dialogService;
        AsyncHelper.RunSafe(
            () => InitializeAsync(),
            ex => AppLogger.LogError("BulkMovement initialize failed", ex));
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var depts = await _departments.GetAllAsync();
            foreach (var d in depts) Departments.Add(d);
            SelectedDepartment = depts.FirstOrDefault() ?? "";

            var cards = await _stockCards.GetChildCardsAsync();
            _allItemViewModels = cards.Select(c => new BulkMovementItemViewModel(c)).ToList();
            
            UpdateVisibleItems();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Bulk movement init error", ex);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void UpdateVisibleItems()
    {
        var term = SearchText.Trim().ToTurkishLower();
        var filtered = string.IsNullOrWhiteSpace(term) 
            ? _allItemViewModels 
            : _allItemViewModels.Where(i => i.Card.MatchesSearch(term)).ToList();

        Items.ReplaceAll(filtered);
    }

    [RelayCommand]
    public void SelectAll() { foreach (var i in Items) i.IsSelected = true; }

    [RelayCommand]
    public void DeselectAll() { foreach (var i in Items) i.IsSelected = false; }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task SaveAsync()
    {
        if (IsSaving || !EnsureCanEditForSession()) return;

        var selectedChecked = _allItemViewModels.Where(i => i.IsSelected).ToList();
        var missingQtyCount = selectedChecked.Count(i => !i.Quantity.HasValue || i.Quantity <= 0);
        if (missingQtyCount > 0)
        {
            await _dialogService.ShowMessageAsync(
                LocalizationManager.L("warning"),
                LocalizationManager.L("bulk_checked_missing_quantity", missingQtyCount));
            return;
        }

        var selected = selectedChecked.Where(i => i.Quantity.HasValue && i.Quantity > 0).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("bulk_no_save"));
            return;
        }

        IsSaving = true;
        try
        {
            foreach (var item in selected)
            {
                double q = item.Quantity!.Value;
                if (double.IsNaN(q) || double.IsInfinity(q) || q <= 0 || q > 1_000_000_000)
                {
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("invalid_quantity"));
                    return;
                }
            }

            string tur = SelectedTypeIndex == 0 ? "Entry" : "Exit";
            if (tur == "Exit")
            {
                foreach (var grp in selected.GroupBy(i => i.Card.Id))
                {
                    var card = grp.First().Card;
                    double totalExit = grp.Sum(x => x.Quantity!.Value);
                    if (card.CurrentStock - totalExit < -1e-9)
                    {
                        await _dialogService.ShowMessageAsync(
                            LocalizationManager.L("warning"),
                            $"⚠️ {LocalizationManager.L("stock_would_go_negative")} ({card.Name}: {card.CurrentStock})");
                        return;
                    }
                }
            }

            var baseDate = (Date.HasValue && Date.Value > DateTime.MinValue.AddDays(2) && Date.Value < DateTime.MaxValue.AddDays(-2))
                ? Date.Value.Date
                : DateTime.Today;
            var targetDate = baseDate.Add(DateTime.Now.TimeOfDay);
            var movements = selected.Select(i => new StockMovement
            {
                StockCardId = i.Card.Id,
                Type = tur,
                Quantity = i.Quantity!.Value,
                Department = SelectedDepartment ?? "",
                Recipient = DeliveredTo ?? "",
                Date = targetDate,
                Description = LocalizationManager.L("bulk_operation_description")
            }).ToList();

            await _movements.AddBulkAsync(movements);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("bulk_save_success", movements.Count));
            CloseAction?.Invoke(true);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Bulk movement save error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
