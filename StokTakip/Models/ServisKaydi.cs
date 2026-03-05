namespace StokTakip.Models;

public class ServisKaydi
{
    public int Id { get; set; }
    public string CihazAdi { get; set; } = "";
    public string SeriNumarasi { get; set; } = "";
    public string Firma { get; set; } = "";
    public DateTime BakimTarihi { get; set; }
    public string Aciklama { get; set; } = "";
}
