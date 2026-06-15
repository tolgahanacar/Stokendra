using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Stokendra.Models;

/// <summary>
/// Stock movement domain model. Represents entry, exit, or blank movements.
/// </summary>
public class StockMovement : ObservableObject
{
    private int _id;
    private int _stockCardId;
    private string _stockCardName = "";
    private string _stockCardCode = "";
    private string _type = "Entry";
    private double _quantity;
    private string _recipient = "";
    private string _department = "";
    private DateTime _date = DateTime.Now;
    private string _description = "";

    /// <summary>Primary key.</summary>
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>Related stock card ID.</summary>
    public int StockCardId
    {
        get => _stockCardId;
        set => SetProperty(ref _stockCardId, value);
    }

    /// <summary>Stock card name (populated via JOIN).</summary>
    public string StockCardName
    {
        get => _stockCardName;
        set => SetProperty(ref _stockCardName, value);
    }

    /// <summary>Stock card code (populated via JOIN).</summary>
    public string StockCardCode
    {
        get => _stockCardCode;
        set => SetProperty(ref _stockCardCode, value);
    }

    /// <summary>
    /// Movement type string representation ("Entry", "Exit", "Blank").
    /// Use <see cref="TypeEnum"/> for type-safe access.
    /// </summary>
    public string Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeEnum));
                OnPropertyChanged(nameof(Impact));
                OnPropertyChanged(nameof(IsEntry));
                OnPropertyChanged(nameof(IsExit));
                OnPropertyChanged(nameof(DisplayType));
                OnPropertyChanged(nameof(DisplayColor));
            }
        }
    }

    /// <summary>Movement quantity. Must be 0 for Blank movements.</summary>
    public double Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(Impact));
            }
        }
    }

    /// <summary>Recipient or sender person name.</summary>
    public string Recipient
    {
        get => _recipient;
        set => SetProperty(ref _recipient, value);
    }

    /// <summary>Department name.</summary>
    public string Department
    {
        get => _department;
        set => SetProperty(ref _department, value);
    }

    /// <summary>Movement date and time.</summary>
    public DateTime Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    /// <summary>Description text.</summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

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
        MovementType.Exit => "#EF4444",  // Red
        _ => "#94A3B8"                   // Gray
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
