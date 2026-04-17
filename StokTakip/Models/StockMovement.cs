namespace StokTakip.Models;

public class StokHareketi
{
    public int Id { get; set; }
    public int StokKartId { get; set; }
    public string StokKartAd { get; set; } = "";
    public string StokKartKodNo { get; set; } = "";
    public string Tur { get; set; } = "Giris";
    public double Miktar { get; set; }
    public string TeslimEdilen { get; set; } = "";
    public string Departman { get; set; } = "";
    public DateTime Tarih { get; set; } = DateTime.Now;
    public string Aciklama { get; set; } = "";
}
