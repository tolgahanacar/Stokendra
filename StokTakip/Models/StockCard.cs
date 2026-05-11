namespace StokTakip.Models;

/// <summary>
/// Stok kartı veri modeli. Hem üst (gruplama) hem de alt (hareket alabilen) kartları temsil eder.
/// </summary>
public class StokKarti
{
    /// <summary>Birincil anahtar.</summary>
    public int Id { get; set; }

    /// <summary>Stok adı.</summary>
    public string Ad { get; set; } = "";

    /// <summary>Benzersiz stok kodu (büyük harf).</summary>
    public string KodNo { get; set; } = "";

    /// <summary>Açıklama metni.</summary>
    public string Aciklama { get; set; } = "";

    /// <summary>Minimum stok eşiği. Bu değerin altına düşünce uyarı verilir.</summary>
    public int MinStok { get; set; }

    /// <summary>Hesaplanmış mevcut stok miktarı (giriş - çıkış).</summary>
    public double MevcutStok { get; set; }

    /// <summary>Toplam giriş miktarı.</summary>
    public double ToplamGiris { get; set; }

    /// <summary>Toplam çıkış miktarı.</summary>
    public double ToplamCikis { get; set; }

    /// <summary>Kategori etiketi.</summary>
    public string Kategori { get; set; } = "";

    /// <summary>Ölçü birimi (varsayılan: Adet).</summary>
    public string Birim { get; set; } = "Adet";

    /// <summary>Depo/raf konumu.</summary>
    public string Konum { get; set; } = "";

    /// <summary>Tedarikçi adı.</summary>
    public string Tedarikci { get; set; } = "";

    /// <summary>Barkod değeri.</summary>
    public string Barkod { get; set; } = "";

    /// <summary>Birim fiyat.</summary>
    public double BirimFiyat { get; set; }

    /// <summary>
    /// Kart tipi string değeri (veritabanı formatı: "Alt" veya "Ust").
    /// Tip güvenli erişim için <see cref="KartTipiEnum"/> kullanın.
    /// </summary>
    public string KartTipi { get; set; } = "Alt";

    /// <summary>Üst kart ID'si. Sadece Alt kartlar için geçerlidir.</summary>
    public int? UstKartId { get; set; }

    /// <summary>Üst kart adı (JOIN ile doldurulur).</summary>
    public string UstKartAd { get; set; } = "";

    /// <summary>
    /// Kart tipini enum olarak döndürür veya ayarlar.
    /// <see cref="KartTipi"/> string alanıyla senkronize çalışır.
    /// </summary>
    public KartTipi KartTipiEnum
    {
        get => KartTipi.ToKartTipi();
        set => KartTipi = value.ToDbString();
    }

    /// <summary>Bu kartın üst (gruplama) kartı olup olmadığını döndürür.</summary>
    public bool IsParentCard => KartTipiEnum == Models.KartTipi.Ust;

    /// <summary>Mevcut stok minimum eşiğin altında mı?</summary>
    public bool IsLowStock => !IsParentCard && MevcutStok > 0 && MevcutStok <= (MinStok > 0 ? MinStok : 3);

    /// <summary>Stok tükenmiş mi?</summary>
    public bool IsDepleted => !IsParentCard && MevcutStok <= 0;

    /// <summary>UI gösterimi için formatlanmış ad.</summary>
    public string DisplayName => $"{KodNo} - {Ad}";

    /// <summary>Stok durumu için renk kodu.</summary>
    public string StatusColor => 
        MevcutStok > MinStok ? "#10B981" : // Emerald 500 (Okunabilir Yeşil)
        MevcutStok < MinStok ? "#EF4444" : // Red 500 (Okunabilir Kırmızı)
        "#D97706"; // Amber 600 (Beyaz yazıyla okunabilir Sarı/Turuncu)

    /// <summary>Stok durumu için metin.</summary>
    public string StatusLabel => $"{MevcutStok} {Birim}";

    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}
