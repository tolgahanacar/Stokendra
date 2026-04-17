namespace StokTakip.Models;

public class Not
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; } = DateTime.Now;
    public string Baslik { get; set; } = "";
    public string Icerik { get; set; } = "";
}
