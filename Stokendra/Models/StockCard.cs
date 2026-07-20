using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokendra.Models;

/// <summary>
/// Stock card domain model. Represents both parent (grouping) and child (movement) cards.
/// </summary>
public class StockCard : ObservableObject
{
    private int _id;
    private string _name = "";
    private string _code = "";
    private string _description = "";
    private int _minStock;
    private double _currentStock;
    private double _totalEntry;
    private double _totalExit;
    private string _category = "";
    private string _unit = "Adet";
    private string _cardType = "Child";
    private int? _parentId;
    private string _parentName = "";

    /// <summary>Primary key.</summary>
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>Stock name.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Unique stock code (uppercase).</summary>
    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    /// <summary>Description text.</summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>Minimum stock threshold. Warning is shown when stock falls below this value.</summary>
    public int MinStock
    {
        get => _minStock;
        set => SetProperty(ref _minStock, value);
    }

    /// <summary>Calculated current stock quantity (entry - exit).</summary>
    public double CurrentStock
    {
        get => _currentStock;
        set
        {
            if (SetProperty(ref _currentStock, value))
            {
                OnPropertyChanged(nameof(IsLowStock));
                OnPropertyChanged(nameof(IsDepleted));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusLabel));
            }
        }
    }

    /// <summary>Total entry quantity.</summary>
    public double TotalEntry
    {
        get => _totalEntry;
        set => SetProperty(ref _totalEntry, value);
    }

    /// <summary>Total exit quantity.</summary>
    public double TotalExit
    {
        get => _totalExit;
        set => SetProperty(ref _totalExit, value);
    }

    /// <summary>Category tag.</summary>
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    /// <summary>Measurement unit (default: Pcs).</summary>
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }



    /// <summary>
    /// Card type string representation ("Child" or "Parent").
    /// Use <see cref="CardTypeEnum"/> for type-safe access.
    /// </summary>
    public string CardType
    {
        get => _cardType;
        set
        {
            if (SetProperty(ref _cardType, value))
            {
                OnPropertyChanged(nameof(CardTypeEnum));
                OnPropertyChanged(nameof(CardTypeDisplay));
                OnPropertyChanged(nameof(IsParentCard));
                OnPropertyChanged(nameof(IsLowStock));
                OnPropertyChanged(nameof(IsDepleted));
            }
        }
    }

    /// <summary>Localized display name for card type.</summary>
    public string CardTypeDisplay => CardTypeEnum switch
    {
        Models.CardType.Parent => LocalizationManager.L("card_type_parent"),
        Models.CardType.Child => LocalizationManager.L("card_type_child"),
        _ => CardType
    };

    /// <summary>Parent card ID. Valid only for Child cards.</summary>
    public int? ParentId
    {
        get => _parentId;
        set => SetProperty(ref _parentId, value);
    }

    /// <summary>Parent card name (populated via JOIN).</summary>
    public string ParentName
    {
        get => _parentName;
        set => SetProperty(ref _parentName, value);
    }

    /// <summary>
    /// Gets or sets the card type as an enum.
    /// Synchronized with the <see cref="CardType"/> string field.
    /// </summary>
    public CardType CardTypeEnum
    {
        get => CardType.ToCardType();
        set => CardType = value.ToDbString();
    }

    /// <summary>Returns true if this card is a parent (grouping) card.</summary>
    public bool IsParentCard => CardTypeEnum == Models.CardType.Parent;

    /// <summary>Is current stock below minimum threshold?</summary>
    public bool IsLowStock => !IsParentCard && CurrentStock > 0 && CurrentStock <= (MinStock > 0 ? MinStock : 3);

    /// <summary>Is stock completely depleted?</summary>
    public bool IsDepleted => !IsParentCard && CurrentStock <= 0;

    /// <summary>Formatted name for UI display.</summary>
    public string DisplayName => $"{Code} - {Name}";

    /// <summary>Color code for stock status.</summary>
    public string StatusColor => 
        IsDepleted ? "#EF4444" : // Red 500 (Okunabilir Kırmızı)
        CurrentStock > MinStock ? "#10B981" : // Emerald 500 (Okunabilir Yeşil)
        CurrentStock < MinStock ? "#EF4444" : // Red 500 (Okunabilir Kırmızı)
        "#D97706"; // Amber 600 (Sarı/Turuncu)

    /// <summary>Text label for stock status.</summary>
    public string StatusLabel => $"{CurrentStock} {Unit}";

    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}
