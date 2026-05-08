using StokTakip.Models;

namespace StokTakip.Tests.Unit;

/// <summary>
/// <see cref="StokKarti"/> model özellikleri için unit testler.
/// </summary>
public class StokKartiModelTests
{
    [Fact]
    public void KartTipiEnum_Get_ReturnsCorrectEnum()
    {
        var kart = new StokKarti { KartTipi = "Ust" };
        Assert.Equal(KartTipi.Ust, kart.KartTipiEnum);
    }

    [Fact]
    public void KartTipiEnum_Set_UpdatesStringProperty()
    {
        var kart = new StokKarti();
        kart.KartTipiEnum = KartTipi.Ust;
        Assert.Equal("Ust", kart.KartTipi);
    }

    [Fact]
    public void IsParentCard_WhenUst_ReturnsTrue()
    {
        var kart = new StokKarti { KartTipi = "Ust" };
        Assert.True(kart.IsParentCard);
    }

    [Fact]
    public void IsParentCard_WhenAlt_ReturnsFalse()
    {
        var kart = new StokKarti { KartTipi = "Alt" };
        Assert.False(kart.IsParentCard);
    }

    [Theory]
    [InlineData(2, 5, true)]   // stok var, min altında
    [InlineData(5, 5, true)]   // stok eşit min
    [InlineData(6, 5, false)]  // stok min üstünde
    [InlineData(0, 5, false)]  // tükenmiş — IsLowStock false, IsDepleted true
    public void IsLowStock_ReturnsExpected(double mevcutStok, int minStok, bool expected)
    {
        var kart = new StokKarti { KartTipi = "Alt", MevcutStok = mevcutStok, MinStok = minStok };
        Assert.Equal(expected, kart.IsLowStock);
    }

    [Fact]
    public void IsLowStock_WhenParentCard_ReturnsFalse()
    {
        var kart = new StokKarti { KartTipi = "Ust", MevcutStok = 1, MinStok = 10 };
        Assert.False(kart.IsLowStock);
    }

    [Fact]
    public void IsDepleted_WhenZeroStock_ReturnsTrue()
    {
        var kart = new StokKarti { KartTipi = "Alt", MevcutStok = 0 };
        Assert.True(kart.IsDepleted);
    }

    [Fact]
    public void IsDepleted_WhenNegativeStock_ReturnsTrue()
    {
        var kart = new StokKarti { KartTipi = "Alt", MevcutStok = -1 };
        Assert.True(kart.IsDepleted);
    }

    [Fact]
    public void IsDepleted_WhenParentCard_ReturnsFalse()
    {
        var kart = new StokKarti { KartTipi = "Ust", MevcutStok = 0 };
        Assert.False(kart.IsDepleted);
    }

    [Fact]
    public void ToString_ReturnsAdAndKodNo()
    {
        var kart = new StokKarti { Ad = "Kalem", KodNo = "001" };
        Assert.Equal("001 - Kalem", kart.ToString());
    }
}
