namespace StokTakip.Models;

/// <summary>
/// Kullanıcı notu veri modeli.
/// </summary>
public class Not
{
    /// <summary>Birincil anahtar.</summary>
    public int Id { get; set; }

    /// <summary>Not tarihi ve saati.</summary>
    public DateTime Tarih { get; set; } = DateTime.Now;

    /// <summary>Not başlığı (zorunlu).</summary>
    public string Baslik { get; set; } = "";

    /// <summary>Not içeriği.</summary>
    public string Icerik { get; set; } = "";

    /// <inheritdoc/>
    public override string ToString() => $"[{Tarih:dd.MM.yyyy}] {Baslik}";
}
