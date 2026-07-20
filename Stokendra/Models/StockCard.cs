using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokendra.Models;

/// <summary>
/// Stock card domain model. Represents both parent (grouping) and child (movement) cards.
/// </summary>
public partial class StockCard : ObservableObject
{
    /// <summary>Primary key.</summary>
    [ObservableProperty]
    private int _id;

    /// <summary>Stock name.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _name = string.Empty;

    /// <summary>Unique stock code (uppercase).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _code = string.Empty;

    /// <summary>Description text.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Minimum stock threshold. Warning is shown when stock falls below this value.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLowStock))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private int _minStock;

    /// <summary>Calculated current stock quantity (entry - exit).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLowStock))]
    [NotifyPropertyChangedFor(nameof(IsDepleted))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private double _currentStock;

    /// <summary>Total entry quantity.</summary>
    [ObservableProperty]
    private double _totalEntry;

    /// <summary>Total exit quantity.</summary>
    [ObservableProperty]
    private double _totalExit;

    /// <summary>Category tag.</summary>
    [ObservableProperty]
    private string _category = string.Empty;

    /// <summary>Measurement unit (default: Pcs).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private string _unit = "Adet";

    /// <summary>
    /// Card type string representation ("Child" or "Parent").
    /// Use <see cref="CardTypeEnum"/> for type-safe access.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardTypeEnum))]
    [NotifyPropertyChangedFor(nameof(CardTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(IsParentCard))]
    [NotifyPropertyChangedFor(nameof(IsLowStock))]
    [NotifyPropertyChangedFor(nameof(IsDepleted))]
    private string _cardType = "Child";

    /// <summary>Parent card ID. Valid only for Child cards.</summary>
    [ObservableProperty]
    private int? _parentId;

    /// <summary>Parent card name (populated via JOIN).</summary>
    [ObservableProperty]
    private string _parentName = string.Empty;

    /// <summary>Localized display name for card type.</summary>
    public string CardTypeDisplay => CardTypeEnum switch
    {
        Models.CardType.Parent => LocalizationManager.L("card_type_parent"),
        Models.CardType.Child => LocalizationManager.L("card_type_child"),
        _ => CardType
    };

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
        IsDepleted ? "#EF4444" : // Red 500
        CurrentStock > MinStock ? "#10B981" : // Emerald 500
        CurrentStock < MinStock ? "#EF4444" : // Red 500
        "#D97706"; // Amber 600

    /// <summary>Text label for stock status.</summary>
    public string StatusLabel => $"{CurrentStock} {Unit}";

    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}
