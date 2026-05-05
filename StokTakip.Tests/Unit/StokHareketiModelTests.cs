using StokTakip.Models;

namespace StokTakip.Tests.Unit;

/// <summary>
/// <see cref="StokHareketi"/> model özellikleri için unit testler.
/// </summary>
public class StokHareketiModelTests
{
    [Fact]
    public void TurEnum_Get_ReturnsCorrectEnum()
    {
        var h = new StokHareketi { Tur = "Cikis" };
        Assert.Equal(HareketTuru.Cikis, h.TurEnum);
    }

    [Fact]
    public void TurEnum_Set_UpdatesStringProperty()
    {
        var h = new StokHareketi();
        h.TurEnum = HareketTuru.Cikis;
        Assert.Equal("Cikis", h.Tur);
    }

    [Theory]
    [InlineData("Giris", 10, 10)]    // giriş → pozitif etki
    [InlineData("Cikis", 10, -10)]   // çıkış → negatif etki
    [InlineData("Bos", 5, 0)]        // boş → sıfır etki
    public void Impact_ReturnsCorrectValue(string tur, double miktar, double expectedImpact)
    {
        var h = new StokHareketi { Tur = tur, Miktar = miktar };
        Assert.Equal(expectedImpact, h.Impact);
    }

    [Fact]
    public void IsEntry_WhenGiris_ReturnsTrue()
    {
        var h = new StokHareketi { Tur = "Giris" };
        Assert.True(h.IsEntry);
        Assert.False(h.IsExit);
    }

    [Fact]
    public void IsExit_WhenCikis_ReturnsTrue()
    {
        var h = new StokHareketi { Tur = "Cikis" };
        Assert.True(h.IsExit);
        Assert.False(h.IsEntry);
    }

    [Fact]
    public void DefaultTur_IsGiris()
    {
        var h = new StokHareketi();
        Assert.Equal("Giris", h.Tur);
        Assert.Equal(HareketTuru.Giris, h.TurEnum);
    }

    [Fact]
    public void DefaultTarih_IsApproximatelyNow()
    {
        var before = DateTime.Now.AddSeconds(-1);
        var h = new StokHareketi();
        var after = DateTime.Now.AddSeconds(1);
        Assert.InRange(h.Tarih, before, after);
    }
}
