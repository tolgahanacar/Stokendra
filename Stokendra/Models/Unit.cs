namespace Stokendra.Models;

/// <summary>
/// Measurement unit domain model.
/// </summary>
public class Unit
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Unit name (e.g. Pcs, Kg, Lt).</summary>
    public string Name { get; set; } = "";

    /// <inheritdoc/>
    public override string ToString() => Name;
}
