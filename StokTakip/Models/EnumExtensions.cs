namespace StokTakip.Models;

/// <summary>
/// Enum türleri için string dönüşüm ve yardımcı metodlar.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// <see cref="HareketTuru"/> enum'ını veritabanı string değerine dönüştürür.
    /// </summary>
    public static string ToDbString(this HareketTuru tur) => tur switch
    {
        HareketTuru.Giris => "Giris",
        HareketTuru.Cikis => "Cikis",
        HareketTuru.Bos => "Bos",
        _ => "Bos"
    };

    /// <summary>
    /// String değeri <see cref="HareketTuru"/> enum'ına dönüştürür.
    /// Geçersiz değer için <see cref="HareketTuru.Bos"/> döner.
    /// </summary>
    public static HareketTuru ToHareketTuru(this string? value) => value?.Trim() switch
    {
        "Giris" => HareketTuru.Giris,
        "Cikis" => HareketTuru.Cikis,
        "Bos" => HareketTuru.Bos,
        _ => HareketTuru.Bos
    };

    /// <summary>
    /// <see cref="KartTipi"/> enum'ını veritabanı string değerine dönüştürür.
    /// </summary>
    public static string ToDbString(this KartTipi tip) => tip switch
    {
        KartTipi.Ust => "Ust",
        KartTipi.Alt => "Alt",
        _ => "Alt"
    };

    /// <summary>
    /// String değeri <see cref="KartTipi"/> enum'ına dönüştürür.
    /// Geçersiz değer için <see cref="KartTipi.Alt"/> döner.
    /// </summary>
    public static KartTipi ToKartTipi(this string? value) => value?.Trim() switch
    {
        "Ust" => KartTipi.Ust,
        "Alt" => KartTipi.Alt,
        _ => KartTipi.Alt
    };

    /// <summary>
    /// Türkçe metni Türkçe büyük harfe dönüştürür (ı→I, i→İ, ş→Ş vb.).
    /// </summary>
    public static string ToUpperTr(this string text)
    {
        return text.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
    }

    /// <summary>
    /// Türkçe metni Türkçe küçük harfe dönüştürür (I→ı, İ→i, Ş→ş vb.).
    /// </summary>
    public static string ToLowerTr(this string text)
    {
        return text.ToLower(new System.Globalization.CultureInfo("tr-TR"));
    }
}
