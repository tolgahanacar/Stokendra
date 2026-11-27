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

public partial class AddMovementViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly IDepartmentRepository _departments;
    private readonly StockMovement? _editingMovement;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private StockCard? _selectedCard;
    [ObservableProperty] private int _selectedTypeIndex = 0; // 0: Entry, 1: Exit, 2: Blank
    [ObservableProperty] private double? _quantity = 1;
    [ObservableProperty] private string _deliveredTo = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private DateTimeOffset _date = DateTimeOffset.Now;
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    public bool CanSave => !IsSaving;

    private readonly StockCard? _preSelectedCard;
    private Task? _initTask;

    public ObservableCollection<StockCard> AllCards { get; } = new();
    public ObservableCollection<string> AllDepartments { get; } = new();

    public AddMovementViewModel(
        IStockCardRepository stockCards,
        IDepartmentRepository departments,
        StockMovement? movement = null,
        StockCard? preSelectedCard = null)
    {
        _stockCards = stockCards;
        _departments = departments;
        _editingMovement = movement;
        _preSelectedCard = preSelectedCard;

        if (movement != null)
        {
            Title = LocalizationManager.L("edit_movement");
            SelectedTypeIndex = movement.Type switch
            {
                "Entry" or "Giris" or "Giriş" => 0,
                "Exit" or "Cikis" or "Çıkış"   => 1,
                "Blank" or "Bos" or "Boş"     => 2,
                _ => 0
            };
            Quantity = movement.Quantity;
            DeliveredTo = movement.Recipient;
            Department = movement.Department;
            Date = SafeDateTimeOffset(movement.Date);
            Description = movement.Description;
        }
        else
        {
            Title = LocalizationManager.L("new_movement");
            if (preSelectedCard != null && preSelectedCard.CardTypeEnum != CardType.Parent)
            {
                SelectedCard = preSelectedCard;
            }
        }

        _initTask = InitAsync();
        AsyncHelper.RunSafe(
            () => _initTask,
            ex => AppLogger.LogError("AddMovement init failed", ex));
    }

    private static DateTimeOffset SafeDateTimeOffset(DateTime dt)
    {
        if (dt <= DateTime.MinValue.AddDays(2) || dt >= DateTime.MaxValue.AddDays(-2))
            return DateTimeOffset.Now;

        try { return new DateTimeOffset(dt); }
        catch { return DateTimeOffset.Now; }
    }

    private async Task InitAsync()
    {
        try
        {
            var cards = await _stockCards.GetChildCardsAsync();
            foreach (var c in cards) AllCards.Add(c);

            if (_editingMovement != null)
            {
                SelectedCard = AllCards.FirstOrDefault(c => c.Id == _editingMovement.StockCardId);
            }

            var depts = await _departments.GetAllAsync();
            foreach (var d in depts) AllDepartments.Add(d);

            if (_editingMovement != null &&
                !string.IsNullOrWhiteSpace(_editingMovement.Department) &&
                !AllDepartments.Any(d => string.Equals(d, _editingMovement.Department, StringComparison.OrdinalIgnoreCase)))
            {
                AllDepartments.Insert(0, _editingMovement.Department);
            }
            
            if (_editingMovement == null)
            {
                // Default Values
                SelectedTypeIndex = 1; // Exit (Çıkış)
                var validPreSelected = _preSelectedCard?.CardTypeEnum != CardType.Parent ? _preSelectedCard : null;
                int? targetCardId = validPreSelected?.Id ?? SelectedCard?.Id;
                if (targetCardId.HasValue)
                {
                    SelectedCard = AllCards.FirstOrDefault(c => c.Id == targetCardId.Value) ?? validPreSelected ?? AllCards.FirstOrDefault();
                }
                else
                {
                    SelectedCard = AllCards.FirstOrDefault();
                }
                Department = AllDepartments.Count > 0 ? AllDepartments[0] : "";
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("AddMovementViewModel init error", ex);
        }
    }

    public StockMovement? Result { get; private set; }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (IsSaving) return;
        IsSaving = true;
        try
        {
            ErrorMessage = "";

            if (_initTask is { IsCompleted: false })
            {
                await _initTask;
            }

            if (SelectedCard == null)
            {
                ErrorMessage = $"⚠️ {LocalizationManager.L("select_stock_card")}";
                return;
            }

            if (SelectedCard.CardTypeEnum == CardType.Parent)
            {
                ErrorMessage = $"⚠️ {LocalizationManager.L("parent_card_movement_forbidden")}";
                return;
            }

            if (!Quantity.HasValue)
            {
                ErrorMessage = $"⚠️ {LocalizationManager.L("quantity_cannot_be_zero")}";
                return;
            }

            double qtyVal = Quantity.Value;
            if (double.IsNaN(qtyVal) || double.IsInfinity(qtyVal) || qtyVal < 0 || qtyVal > 1_000_000_000)
            {
                ErrorMessage = $"⚠️ {LocalizationManager.L("invalid_quantity")}";
                return;
            }
            
            string tur = SelectedTypeIndex switch { 0 => "Entry", 1 => "Exit", 2 => "Blank", _ => "Entry" };

            if (qtyVal <= 0 && tur != "Blank")
            {
                ErrorMessage = LocalizationManager.L("quantity_cannot_be_zero");
                return;
            }
            
            // Mathematically correct negative stock validation
            double stockWithoutOld = SelectedCard.CurrentStock;
            if (_editingMovement != null)
            {
                if (SelectedCard.Id == _editingMovement.StockCardId)
                {
                    if (_editingMovement.TypeEnum == MovementType.Exit)
                        stockWithoutOld += _editingMovement.Quantity;
                    else if (_editingMovement.TypeEnum == MovementType.Entry)
                        stockWithoutOld -= _editingMovement.Quantity;
                }
                else
                {
                    var originalCard = AllCards.FirstOrDefault(c => c.Id == _editingMovement.StockCardId);
                    if (originalCard != null)
                    {
                        double originalCardStockWithoutOld = originalCard.CurrentStock;
                        if (_editingMovement.TypeEnum == MovementType.Entry)
                        {
                            originalCardStockWithoutOld -= _editingMovement.Quantity;
                            if (originalCardStockWithoutOld < -1e-9)
                            {
                                ErrorMessage = $"⚠️ {LocalizationManager.L("stock_would_go_negative")} ({originalCard.Name}: {originalCard.CurrentStock})";
                                return;
                            }
                        }
                    }
                }
            }

            double newImpact = tur switch { "Entry" => qtyVal, "Exit" => -qtyVal, _ => 0 };
            if (stockWithoutOld + newImpact < -1e-9)
            {
                ErrorMessage = $"⚠️ {LocalizationManager.L("stock_would_go_negative")} ({LocalizationManager.L("current_stock")}: {SelectedCard.CurrentStock})";
                return;
            }

            Result = new StockMovement
            {
                Id = _editingMovement?.Id ?? 0,
                StockCardId = SelectedCard.Id,
                Type = tur,
                Quantity = qtyVal,
                Recipient = DeliveredTo ?? "",
                Department = Department ?? "",
                Date = Date.Date.Add(_editingMovement != null ? _editingMovement.Date.TimeOfDay : DateTime.Now.TimeOfDay),
                Description = Description ?? "",
                StockCardName = SelectedCard.Name,
                StockCardCode = SelectedCard.Code
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
