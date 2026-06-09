using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
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
    [ObservableProperty] private double _quantity = 1;
    [ObservableProperty] private string _deliveredTo = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private DateTimeOffset _date = DateTimeOffset.Now;
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _errorMessage = "";

    public ObservableCollection<StockCard> AllCards { get; } = new();
    public ObservableCollection<string> AllDepartments { get; } = new();

    public AddMovementViewModel(
        IStockCardRepository stockCards,
        IDepartmentRepository departments,
        StockMovement? movement = null)
    {
        _stockCards = stockCards;
        _departments = departments;
        _editingMovement = movement;

        if (movement != null)
        {
            Title = LocalizationManager.L("edit_movement");
            SelectedTypeIndex = movement.Type switch { "Entry" => 0, "Exit" => 1, "Blank" => 2, _ => 0 };
            Quantity = movement.Quantity;
            DeliveredTo = movement.Recipient;
            Department = movement.Department;
            Date = new DateTimeOffset(movement.Date);
            Description = movement.Description;
        }
        else
        {
            Title = LocalizationManager.L("new_movement");
        }

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var cards = await _stockCards.GetChildCardsAsync();
        foreach (var c in cards) AllCards.Add(c);

        if (_editingMovement != null)
        {
            SelectedCard = AllCards.FirstOrDefault(c => c.Id == _editingMovement.StockCardId);
        }

        var depts = await _departments.GetAllAsync();
        foreach (var d in depts) AllDepartments.Add(d);
        
        if (_editingMovement == null)
        {
            // Default Values
            SelectedTypeIndex = 1; // Exit (Çıkış)
            SelectedCard = AllCards.FirstOrDefault();
            Department = AllDepartments.Count > 0 ? AllDepartments[0] : "";
        }
    }

    public StockMovement? Result { get; private set; }

    [RelayCommand]
    private void Save()
    {
        if (SelectedCard == null) return;
        
        // Negative Stock Validation
        string tur = SelectedTypeIndex switch { 0 => "Entry", 1 => "Exit", 2 => "Blank", _ => "Entry" };
        if (tur == "Exit" && Quantity > SelectedCard.CurrentStock)
        {
            ErrorMessage = $"⚠️ {LocalizationManager.L("stock_would_go_negative")} ({LocalizationManager.L("current_stock")}: {SelectedCard.CurrentStock})";
            return;
        }

        Result = new StockMovement
        {
            Id = _editingMovement?.Id ?? 0,
            StockCardId = SelectedCard.Id,
            Type = tur,
            Quantity = Quantity,
            Recipient = DeliveredTo,
            Department = Department,
            Date = Date.Date.Add(DateTime.Now.TimeOfDay),
            Description = Description,
            StockCardName = SelectedCard.Name,
            StockCardCode = SelectedCard.Code
        };
        
        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
