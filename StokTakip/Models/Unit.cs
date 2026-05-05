namespace StokTakip.Models;

/// <summary>
/// Ölçü birimi veri modeli.
/// </summary>
public class Birim
{
    /// <summary>Birincil anahtar.</summary>
    public int Id { get; set; }

    /// <summary>Birim adı (örn. Adet, Kg, Lt).</summary>
    public string Ad { get; set; } = "";

    /// <inheritdoc/>
    public override string ToString() => Ad;
}
