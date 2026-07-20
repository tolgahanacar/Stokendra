using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokendra.Models;

/// <summary>
/// Stock movement domain model. Represents entry, exit, or blank movements.
/// </summary>
public partial class StockMovement : ObservableObject
{
    /// <summary>Primary key.</summary>
    [ObservableProperty]
    private int _id;

    /// <summary>Related stock card ID.</summary>
    [ObservableProperty]
    private int _stockCardId;

    /// <summary>Stock card name (populated via JOIN).</summary>
    [ObservableProperty]
    private string _stockCardName = string.Empty;

    /// <summary>Stock card code (populated via JOIN).</summary>
    [ObservableProperty]
    private string _stockCardCode = string.Empty;

    /// <summary>
    /// Movement type string representation ("Entry", "Exit", "Blank").
    /// Use <see cref="TypeEnum"/> for type-safe access.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeEnum))]
    [NotifyPropertyChangedFor(nameof(Impact))]
    [NotifyPropertyChangedFor(nameof(IsEntry))]
    [NotifyPropertyChangedFor(nameof(IsExit))]
    [NotifyPropertyChangedFor(nameof(DisplayType))]
    [NotifyPropertyChangedFor(nameof(DisplayColor))]
    private string _type = "Entry";

    /// <summary>Movement quantity. Must be 0 for Blank movements.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Impact))]
    private double _quantity;

    /// <summary>Recipient or sender person name.</summary>
    [ObservableProperty]
    private string _recipient = string.Empty;

    /// <summary>Department name.</summary>
    [ObservableProperty]
    private string _department = string.Empty;

    /// <summary>Movement date and time.</summary>
    [ObservableProperty]
    private DateTime _date = DateTime.Now;

    /// <summary>Description text.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Gets or sets the movement type as an enum.
    /// Synchronized with the <see cref="Type"/> string field.
    /// </summary>
    public MovementType TypeEnum
    {
        get => Type.ToMovementType();
        set => Type = value.ToDbString();
    }

    /// <summary>Calculates the impact of this movement on stock level (+entry, -exit, 0 blank).</summary>
    public double Impact => TypeEnum switch
    {
        MovementType.Entry => Quantity,
        MovementType.Exit => -Quantity,
        _ => 0
    };

    /// <summary>Returns true if this is an entry movement.</summary>
    public bool IsEntry => TypeEnum == MovementType.Entry;

    /// <summary>Returns true if this is an exit movement.</summary>
    public bool IsExit => TypeEnum == MovementType.Exit;

    /// <summary>UI representation of the movement type symbol.</summary>
    public string DisplayType => TypeEnum switch
    {
        MovementType.Entry => LocalizationManager.L("entry_symbol"),
        MovementType.Exit => LocalizationManager.L("exit_symbol"),
        _ => "—"
    };

    /// <summary>Color code for UI display based on type.</summary>
    public string DisplayColor => TypeEnum switch
    {
        MovementType.Entry => "#10B981", // Green
        MovementType.Exit => "#EF4444",   // Red
        _ => "#94A3B8"                    // Gray
    };

    /// <summary>Creates a shallow copy of the stock movement.</summary>
    public StockMovement Clone() => new StockMovement
    {
        Id = Id,
        StockCardId = StockCardId,
        StockCardName = StockCardName,
        StockCardCode = StockCardCode,
        Type = Type,
        Quantity = Quantity,
        Recipient = Recipient,
        Department = Department,
        Date = Date,
        Description = Description
    };
}
