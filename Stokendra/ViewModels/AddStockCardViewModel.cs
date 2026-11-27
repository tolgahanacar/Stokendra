using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class AddStockCardViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly StockCard? _editingCard;
    private List<StockCard> _allCards = new();
    private Task? _initTask;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private int? _minStock = 0;
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _unit = LocalizationManager.L("default_unit");
    [ObservableProperty] private int _selectedTypeIndex = 0; // 0: Child, 1: Parent
    [ObservableProperty] private StockCard? _selectedParent;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    public bool CanSave => !IsSaving;

    public bool IsChildCard => SelectedTypeIndex == 0;
    partial void OnSelectedTypeIndexChanged(int value) => OnPropertyChanged(nameof(IsChildCard));

    public ObservableCollection<StockCard> ParentCards { get; } = new();

    public AddStockCardViewModel(IStockCardRepository stockCards, StockCard? card = null)
    {
        _stockCards = stockCards;
        _editingCard = card;

        if (card != null)
        {
            Title = LocalizationManager.L("edit_stock_card");
            Name = card.Name;
            Code = card.Code;
            Description = card.Description;
            MinStock = card.MinStock;
            Category = card.Category;
            Unit = card.Unit;
            SelectedTypeIndex = card.CardType == "Parent" ? 1 : 0;
        }
        else
        {
            Title = LocalizationManager.L("new_stock_card");
        }

        _initTask = InitAsync();
        Infrastructure.AsyncHelper.RunSafe(() => _initTask, ex => AppLogger.LogError("AddStockCardViewModel init failed", ex));
    }

    private async Task InitAsync()
    {
        try
        {
            if (_editingCard == null && string.IsNullOrWhiteSpace(Code))
            {
                string? nextCode = await _stockCards.GetNextCodeAsync();
                Code = !string.IsNullOrEmpty(nextCode) ? nextCode : _stockCards.GetNextCode();
            }

            var lookupCards = await _stockCards.GetCardLookupsAsync();
            _allCards = (lookupCards != null && lookupCards.Count > 0) ? lookupCards : await _stockCards.GetAllAsync();
            var parents = _allCards.Where(k => k.CardType == "Parent" && k.Id != _editingCard?.Id).ToList();
            
            ParentCards.Clear();
            foreach (var p in parents) ParentCards.Add(p);

            if (_editingCard?.ParentId != null)
            {
                SelectedParent = ParentCards.FirstOrDefault(p => p.Id == _editingCard.ParentId);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("AddStockCardViewModel init error", ex);
        }
    }

    public StockCard? Result { get; private set; }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (IsSaving) return;
        IsSaving = true;
        try
        {
            ErrorMessage = "";

            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = LocalizationManager.L("name_required");
                return;
            }

            if (string.IsNullOrWhiteSpace(Code))
            {
                ErrorMessage = LocalizationManager.L("code_required");
                return;
            }

            if (_initTask != null)
            {
                await _initTask;
            }

            var cleanedCode = Code.Trim().ToUpperInvariant();
            var duplicateExists = _allCards.Any(k => 
                k.Code.Equals(cleanedCode, System.StringComparison.OrdinalIgnoreCase) && 
                k.Id != (_editingCard?.Id ?? 0));

            if (duplicateExists)
            {
                ErrorMessage = LocalizationManager.L("stock_code_exists");
                return;
            }

            Result = new StockCard
            {
                Id = _editingCard?.Id ?? 0,
                Name = Name.Trim(),
                Code = cleanedCode,
                Description = Description ?? "",
                MinStock = Math.Max(0, MinStock ?? 0),
                Category = Category ?? "",
                Unit = string.IsNullOrWhiteSpace(Unit) ? LocalizationManager.L("default_unit") : Unit.Trim(),
                CardType = SelectedTypeIndex == 1 ? "Parent" : "Child",
                ParentId = SelectedTypeIndex == 0 ? SelectedParent?.Id : null
            };

            CloseAction?.Invoke(true);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
