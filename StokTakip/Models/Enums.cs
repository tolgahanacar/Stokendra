namespace StokTakip.Models;

/// <summary>Stok hareket türlerini tanımlar (Giriş / Çıkış / Boş).</summary>
public enum HareketTuru
{
    /// <summary>Stok girişi</summary>
    Giris,
    /// <summary>Stok çıkışı</summary>
    Cikis,
    /// <summary>Boş hareket (miktar sıfır)</summary>
    Bos
}

/// <summary>Stok kartı hiyerarşi tipi (Üst / Alt).</summary>
public enum KartTipi
{
    /// <summary>Alt kart — stok hareketi alabilir</summary>
    Alt,
    /// <summary>Üst kart — sadece gruplama amaçlı</summary>
    Ust
}
