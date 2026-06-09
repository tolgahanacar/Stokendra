using System;

namespace Stokendra.Models;

/// <summary>
/// Stock movement domain model. Represents entry, exit, or blank movements.
/// </summary>
public class StockMovement
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Related stock card ID.</summary>
    public int StockCardId { get; set; }

    /// <summary>Stock card name (populated via JOIN).</summary>
    public string StockCardName { get; set; } = "";

    /// <summary>Stock card code (populated via JOIN).</summary>
    public string StockCardCode { get; set; } = "";

    /// <summary>
    /// Movement type string representation ("Entry", "Exit", "Blank").
    /// Use <see cref="TypeEnum"/> for type-safe access.
    /// </summary>
    public string Type { get; set; } = "Entry";

    /// <summary>Movement quantity. Must be 0 for Blank movements.</summary>
    public double Quantity { get; set; }

    /// <summary>Recipient or sender person name.</summary>
    public string Recipient { get; set; } = "";

    /// <summary>Department name.</summary>
    public string Department { get; set; } = "";

    /// <summary>Movement date and time.</summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>Description text.</summary>
    public string Description { get; set; } = "";

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
}
