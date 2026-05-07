using StokTakip.Models;
using System.Globalization;

namespace StokTakip.Infrastructure;

/// <summary>
/// Stok hareketi görüntüleme ve parse işlemleri için merkezi yardımcı.
/// "[G]", "[Ç]", "[B]" format mantığı tek yerde tanımlıdır.
/// </summary>
public static class MovementFormatter
{
    // ── Görüntüleme sabitleri ──────────────────────────────────────────────

    /// <summary>Giriş hareketi etiketi.</summary>
    public const string EntryTag = "[G]";

    /// <summary>Çıkış hareketi etiketi.</summary>
    public const string ExitTag = "[Ç]";

    /// <summary>Boş hareket etiketi.</summary>
    public const string EmptyTag = "[B]";

    // ── Biçimlendirme ──────────────────────────────────────────────────────

    /// <summary>
    /// Hareketi görüntüleme formatına dönüştürür: "5[G]", "3[Ç]", "0[B]"
    /// </summary>
    public static string Format(StokHareketi hareket)
        => Format(hareket.Tur, hareket.Miktar);

    /// <summary>
    /// Hareket türü ve miktarı görüntüleme formatına dönüştürür.
    /// </summary>
    public static string Format(string tur, double miktar)
    {
        string tag = tur switch
        {
            nameof(HareketTuru.Giris) => EntryTag,
            nameof(HareketTuru.Cikis) => ExitTag,
            _ => EmptyTag
        };
        return $"{UIHelper.FormatMiktar(miktar)}{tag}";
    }

    /// <summary>
    /// Görüntüleme formatındaki string'den hareket türünü belirler.
    /// </summary>
    public static HareketTuru ParseTur(string formatted)
    {
        if (formatted.Contains(ExitTag)) return HareketTuru.Cikis;
        if (formatted.Contains(EmptyTag)) return HareketTuru.Bos;
        return HareketTuru.Giris;
    }

    // ── Parse ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Excel import için hücre değerini parse eder.
    /// Örnek: "5[G]" → (Giris, 5.0), "3[Ç]" → (Cikis, 3.0), "0[B]" → (Bos, 0.0)
    /// </summary>
    /// <param name="cellValue">Excel hücre değeri.</param>
    /// <returns>Parse sonucu.</returns>
    public static ParseResult ParseCell(string cellValue)
    {
        if (string.IsNullOrWhiteSpace(cellValue))
            return ParseResult.Failure("Boş hücre");

        HareketTuru tur = ParseTur(cellValue);

        if (tur == HareketTuru.Bos)
            return ParseResult.Success(HareketTuru.Bos, 0);

        // Tag'leri temizle
        string raw = cellValue
            .Replace(EntryTag, "")
            .Replace(ExitTag, "")
            .Replace(EmptyTag, "")
            .Replace(',', '.')
            .Trim();

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double miktar)
            && miktar > 0)
            return ParseResult.Success(tur, miktar);

        return ParseResult.Failure($"Geçersiz miktar: '{cellValue}'");
    }

    /// <summary>
    /// Hareket türüne göre grid hücre rengini döndürür.
    /// </summary>
    public static Color GetColor(string tur) => tur switch
    {
        nameof(HareketTuru.Giris) => UIHelper.AccentGreen,
        nameof(HareketTuru.Cikis) => UIHelper.StokWarning,
        _ => UIHelper.TextSecondary
    };

    /// <summary>
    /// Hareket türüne göre print rengi döndürür.
    /// </summary>
    public static Color GetPrintColor(string tur) => tur switch
    {
        nameof(HareketTuru.Giris) => Color.DarkGreen,
        nameof(HareketTuru.Cikis) => Color.DarkRed,
        _ => Color.Gray
    };

    // ── İç tipler ─────────────────────────────────────────────────────────

    /// <summary>Parse işlemi sonucu.</summary>
    public sealed class ParseResult
    {
        /// <summary>Parse başarılı mı?</summary>
        public bool IsSuccess { get; private init; }

        /// <summary>Hareket türü.</summary>
        public HareketTuru Tur { get; private init; }

        /// <summary>Miktar.</summary>
        public double Miktar { get; private init; }

        /// <summary>Hata mesajı (başarısız ise).</summary>
        public string? Error { get; private init; }

        private ParseResult() { }

        internal static ParseResult Success(HareketTuru tur, double miktar)
            => new() { IsSuccess = true, Tur = tur, Miktar = miktar };

        internal static ParseResult Failure(string error)
            => new() { IsSuccess = false, Error = error };
    }
}
