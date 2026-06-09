namespace Stokendra.Models;

/// <summary>
/// Stock card domain model. Represents both parent (grouping) and child (movement) cards.
/// </summary>
public class StockCard
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Stock name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Unique stock code (uppercase).</summary>
    public string Code { get; set; } = "";

    /// <summary>Description text.</summary>
    public string Description { get; set; } = "";

    /// <summary>Minimum stock threshold. Warning is shown when stock falls below this value.</summary>
    public int MinStock { get; set; }

    /// <summary>Calculated current stock quantity (entry - exit).</summary>
    public double CurrentStock { get; set; }

    /// <summary>Total entry quantity.</summary>
    public double TotalEntry { get; set; }

    /// <summary>Total exit quantity.</summary>
    public double TotalExit { get; set; }

    /// <summary>Category tag.</summary>
    public string Category { get; set; } = "";

    /// <summary>Measurement unit (default: Pcs).</summary>
    public string Unit { get; set; } = "Adet";

    /// <summary>Warehouse/shelf location.</summary>
    public string Location { get; set; } = "";

    /// <summary>Supplier name.</summary>
    public string Supplier { get; set; } = "";

    /// <summary>Barcode value.</summary>
    public string Barcode { get; set; } = "";

    /// <summary>Unit price.</summary>
    public double UnitPrice { get; set; }

    /// <summary>
    /// Card type string representation ("Child" or "Parent").
    /// Use <see cref="CardTypeEnum"/> for type-safe access.
    /// </summary>
    public string CardType { get; set; } = "Child";

    /// <summary>Parent card ID. Valid only for Child cards.</summary>
    public int? ParentId { get; set; }

    /// <summary>Parent card name (populated via JOIN).</summary>
    public string ParentName { get; set; } = "";

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
        CurrentStock > MinStock ? "#10B981" : // Emerald 500 (Okunabilir Yeşil)
        CurrentStock < MinStock ? "#EF4444" : // Red 500 (Okunabilir Kırmızı)
        "#D97706"; // Amber 600 (Sarı/Turuncu)

    /// <summary>Text label for stock status.</summary>
    public string StatusLabel => $"{CurrentStock} {Unit}";

    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}
