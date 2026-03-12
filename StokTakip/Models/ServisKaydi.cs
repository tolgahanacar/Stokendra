namespace StokTakip.Models;

public class ServisKaydi
{
    public int Id { get; set; }
    public string CihazAdi { get; set; } = "";
    public string SeriNumarasi { get; set; } = "";
    public string Firma { get; set; } = "";
    public DateTime BakimTarihi { get; set; }
    public string Sorun { get; set; } = "";
    public string Sonuc { get; set; } = "";
}
