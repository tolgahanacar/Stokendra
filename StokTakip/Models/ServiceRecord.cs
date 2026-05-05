namespace StokTakip.Models;

/// <summary>
/// Cihaz bakım/servis kaydı veri modeli.
/// </summary>
public class ServisKaydi
{
    /// <summary>Birincil anahtar.</summary>
    public int Id { get; set; }

    /// <summary>Cihaz adı (zorunlu).</summary>
    public string CihazAdi { get; set; } = "";

    /// <summary>Cihaz seri numarası.</summary>
    public string SeriNumarasi { get; set; } = "";

    /// <summary>Servis firması adı.</summary>
    public string Firma { get; set; } = "";

    /// <summary>Bakım/servis tarihi.</summary>
    public DateTime BakimTarihi { get; set; }

    /// <summary>Tespit edilen sorun açıklaması.</summary>
    public string Sorun { get; set; } = "";

    /// <summary>Yapılan işlem ve sonuç açıklaması.</summary>
    public string Sonuc { get; set; } = "";

    /// <inheritdoc/>
    public override string ToString() => $"{CihazAdi} ({SeriNumarasi}) - {BakimTarihi:dd.MM.yyyy}";
}
