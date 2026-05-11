namespace StokTakip.Models;

/// <summary>
/// Kullanıcı notu veri modeli.
/// </summary>
public class Not
{
    /// <summary>Birincil anahtar.</summary>
    public int Id { get; set; }

    /// <summary>Not tarihi ve saati (Legacy).</summary>
    public DateTime Tarih { get; set; } = DateTime.Now;

    /// <summary>Oluşturma tarihi.</summary>
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

    /// <summary>Son güncelleme tarihi.</summary>
    public DateTime GuncellenmeTarihi { get; set; } = DateTime.Now;

    /// <summary>Not başlığı (zorunlu).</summary>
    public string Baslik { get; set; } = "";

    /// <summary>Not içeriği.</summary>
    public string Icerik { get; set; } = "";

    /// <inheritdoc/>
    public override string ToString() => $"[{Tarih:dd.MM.yyyy}] {Baslik}";
}
