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

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private int _minStock = 0;
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _unit = "Adet";
    [ObservableProperty] private string _location = "";
    [ObservableProperty] private string _supplier = "";
    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private double _unitPrice = 0;
    [ObservableProperty] private int _selectedTypeIndex = 0; // 0: Child, 1: Parent
    [ObservableProperty] private StockCard? _selectedParent;

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
            Location = card.Location;
            Supplier = card.Supplier;
            Barcode = card.Barcode;
            UnitPrice = card.UnitPrice;
            SelectedTypeIndex = card.CardType == "Parent" ? 1 : 0;
        }
        else
        {
            Title = LocalizationManager.L("new_stock_card");
        }

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var all = await _stockCards.GetAllAsync();
        var parents = all.Where(k => k.CardType == "Parent" && k.Id != _editingCard?.Id).ToList();
        
        ParentCards.Clear();
        foreach (var p in parents) ParentCards.Add(p);

        if (_editingCard?.ParentId != null)
        {
            SelectedParent = ParentCards.FirstOrDefault(p => p.Id == _editingCard.ParentId);
        }
    }

    public StockCard? Result { get; private set; }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        Result = new StockCard
        {
            Id = _editingCard?.Id ?? 0,
            Name = Name,
            Code = Code,
            Description = Description,
            MinStock = MinStock,
            Category = Category,
            Unit = Unit,
            Location = Location,
            Supplier = Supplier,
            Barcode = Barcode,
            UnitPrice = UnitPrice,
            CardType = SelectedTypeIndex == 1 ? "Parent" : "Child",
            ParentId = SelectedTypeIndex == 0 ? SelectedParent?.Id : null
        };

        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
