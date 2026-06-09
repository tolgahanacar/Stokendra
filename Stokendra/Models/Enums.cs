namespace Stokendra.Models;

/// <summary>Defines the type of stock movement (Entry / Exit / Blank).</summary>
public enum MovementType
{
    /// <summary>Stock entry</summary>
    Entry,
    /// <summary>Stock exit</summary>
    Exit,
    /// <summary>Blank/zero movement</summary>
    Blank
}

/// <summary>Defines the stock card hierarchy type (Parent / Child).</summary>
public enum CardType
{
    /// <summary>Child card — can accept stock movements</summary>
    Child,
    /// <summary>Parent card — for grouping purposes only</summary>
    Parent
}
