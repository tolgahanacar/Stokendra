using StokTakip.Models;

namespace StokTakip.Tests.Unit;

/// <summary>
/// <see cref="EnumExtensions"/> için unit testler.
/// Enum ↔ string dönüşümlerinin doğruluğunu doğrular.
/// </summary>
public class EnumExtensionsTests
{
    // ── HareketTuru → string ───────────────────────────────────────────────

    [Theory]
    [InlineData(HareketTuru.Giris, "Giris")]
    [InlineData(HareketTuru.Cikis, "Cikis")]
    [InlineData(HareketTuru.Bos, "Bos")]
    public void ToDbString_HareketTuru_ReturnsCorrectString(HareketTuru tur, string expected)
    {
        Assert.Equal(expected, tur.ToDbString());
    }

    // ── string → HareketTuru ───────────────────────────────────────────────

    [Theory]
    [InlineData("Giris", HareketTuru.Giris)]
    [InlineData("Cikis", HareketTuru.Cikis)]
    [InlineData("Bos", HareketTuru.Bos)]
    public void ToHareketTuru_ValidString_ReturnsCorrectEnum(string value, HareketTuru expected)
    {
        Assert.Equal(expected, value.ToHareketTuru());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("GIRIS")]  // büyük harf — case-sensitive
    public void ToHareketTuru_InvalidString_ReturnsBos(string? value)
    {
        Assert.Equal(HareketTuru.Bos, value.ToHareketTuru());
    }

    // ── KartTipi → string ─────────────────────────────────────────────────

    [Theory]
    [InlineData(KartTipi.Alt, "Alt")]
    [InlineData(KartTipi.Ust, "Ust")]
    public void ToDbString_KartTipi_ReturnsCorrectString(KartTipi tip, string expected)
    {
        Assert.Equal(expected, tip.ToDbString());
    }

    // ── string → KartTipi ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Alt", KartTipi.Alt)]
    [InlineData("Ust", KartTipi.Ust)]
    public void ToKartTipi_ValidString_ReturnsCorrectEnum(string value, KartTipi expected)
    {
        Assert.Equal(expected, value.ToKartTipi());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void ToKartTipi_InvalidString_ReturnsAlt(string? value)
    {
        Assert.Equal(KartTipi.Alt, value.ToKartTipi());
    }

    // ── Türkçe büyük/küçük harf ───────────────────────────────────────────

    [Fact]
    public void ToUpperTr_ConvertsICorrectly()
    {
        Assert.Equal("İSTANBUL", "istanbul".ToUpperTr());
    }

    [Fact]
    public void ToUpperTr_ConvertsDottedI()
    {
        Assert.Equal("İ", "i".ToUpperTr());
    }

    [Fact]
    public void ToLowerTr_ConvertsCapitalICorrectly()
    {
        Assert.Equal("ı", "I".ToLowerTr());
    }
}
