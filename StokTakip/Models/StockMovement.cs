namespace StokTakip.Models;

/// <summary>
/// Stok hareketi veri modeli. Giriş, çıkış veya boş hareket kaydı.
/// </summary>
public class StokHareketi
{
    /// <summary>Birincil anahtar.</summary>
    public int Id { get; set; }

    /// <summary>İlişkili stok kartı ID'si.</summary>
    public int StokKartId { get; set; }

    /// <summary>Stok kartı adı (JOIN ile doldurulur).</summary>
    public string StokKartAd { get; set; } = "";

    /// <summary>Stok kartı kodu (JOIN ile doldurulur).</summary>
    public string StokKartKodNo { get; set; } = "";

    /// <summary>
    /// Hareket türü string değeri (veritabanı formatı: "Giris", "Cikis", "Bos").
    /// Tip güvenli erişim için <see cref="TurEnum"/> kullanın.
    /// </summary>
    public string Tur { get; set; } = "Giris";

    /// <summary>Hareket miktarı. Bos hareketlerde 0 olmalıdır.</summary>
    public double Miktar { get; set; }

    /// <summary>Teslim edilen/alınan kişi adı.</summary>
    public string TeslimEdilen { get; set; } = "";

    /// <summary>Departman adı.</summary>
    public string Departman { get; set; } = "";

    /// <summary>Hareket tarihi ve saati.</summary>
    public DateTime Tarih { get; set; } = DateTime.Now;

    /// <summary>Açıklama metni.</summary>
    public string Aciklama { get; set; } = "";

    /// <summary>
    /// Hareket türünü enum olarak döndürür veya ayarlar.
    /// <see cref="Tur"/> string alanıyla senkronize çalışır.
    /// </summary>
    public HareketTuru TurEnum
    {
        get => Tur.ToHareketTuru();
        set => Tur = value.ToDbString();
    }

    /// <summary>Bu hareketin stok üzerindeki etkisini hesaplar (+giriş, -çıkış, 0 boş).</summary>
    public double Impact => TurEnum switch
    {
        HareketTuru.Giris => Miktar,
        HareketTuru.Cikis => -Miktar,
        _ => 0
    };

    /// <summary>Giriş hareketi mi?</summary>
    public bool IsEntry => TurEnum == HareketTuru.Giris;

    /// <summary>Çıkış hareketi mi?</summary>
    public bool IsExit => TurEnum == HareketTuru.Cikis;
}
