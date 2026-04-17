namespace StokTakip.Models;

public class Birim
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public override string ToString() => Ad;
}
