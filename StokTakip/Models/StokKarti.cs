namespace StokTakip.Models;

public class StokKarti
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string KodNo { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public int MinStok { get; set; }
    public double MevcutStok { get; set; }
    public string Kategori { get; set; } = "";
    public string Birim { get; set; } = "Adet";
    public string Konum { get; set; } = "";
    public string Tedarikci { get; set; } = "";
    public string Barkod { get; set; } = "";
    public double BirimFiyat { get; set; }

    // Üst/Alt Kart
    public string KartTipi { get; set; } = "Alt";
    public int? UstKartId { get; set; }
    public string UstKartAd { get; set; } = "";

    public override string ToString() => $"{Ad} ({KodNo})";
}
