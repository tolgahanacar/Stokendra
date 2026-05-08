using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// ReportRepository — dashboard istatistikleri, stok raporu.
/// </summary>
public class ReportRepositoryTests : IDisposable
{
    private readonly TestDb _db;
    public ReportRepositoryTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    // ── Dashboard istatistikleri ──────────────────────────────────────────

    [Fact]
    public async Task GetDashboardStats_EmptyDb_AllZero()
    {
        var stats = await _db.Reports.GetDashboardStatsAsync();

        Assert.Equal(0, stats.TotalCards);
        Assert.Equal(0, stats.TotalStock);
        Assert.Equal(0, stats.TotalMovements);
        Assert.Equal(0, stats.TodayMovements);
    }

    [Fact]
    public async Task GetDashboardStats_TotalCards_CountsOnlyAltCards()
    {
        _db.StockCards.Add(new StokTakip.Models.StokKarti { Ad = "Üst", KodNo = "U01", KartTipi = "Ust" });
        _db.CreateCard("Alt1", "A01");
        _db.CreateCard("Alt2", "A02");

        var stats = await _db.Reports.GetDashboardStatsAsync();
        Assert.Equal(2, stats.TotalCards); // Sadece Alt kartlar
    }

    [Fact]
    public async Task GetDashboardStats_TotalStock_SumOfAllEntryMinusExit()
    {
        int id1 = _db.CreateCard("Stok1", "S01");
        int id2 = _db.CreateCard("Stok2", "S02");

        _db.AddEntry(id1, 100);
        _db.AddExit(id1, 30);
        _db.AddEntry(id2, 50);

        var stats = await _db.Reports.GetDashboardStatsAsync();
        // (100-30) + 50 = 120
        Assert.Equal(120, stats.TotalStock);
    }

    [Fact]
    public async Task GetDashboardStats_TotalMovements_CountsAllMovements()
    {
        int id = _db.CreateCard("Stok", "S01");
        _db.AddEntry(id, 10);
        _db.AddEntry(id, 20);
        _db.AddExit(id, 5);

        var stats = await _db.Reports.GetDashboardStatsAsync();
        Assert.Equal(3, stats.TotalMovements);
    }

    [Fact]
    public async Task GetDashboardStats_TodayMovements_CountsOnlyToday()
    {
        int id = _db.CreateCard("Stok", "S01");
        _db.AddEntry(id, 10, tarih: DateTime.Today);
        _db.AddEntry(id, 20, tarih: DateTime.Today.AddDays(-1)); // dün

        var stats = await _db.Reports.GetDashboardStatsAsync();
        Assert.Equal(1, stats.TodayMovements);
    }

    [Fact]
    public async Task GetDashboardStats_DepletedStock_CountsZeroOrNegativeStock()
    {
        int id1 = _db.CreateCard("Tükenen", "T01");
        int id2 = _db.CreateCard("Dolu", "T02");

        // id1: hareket yok → stok 0 → tükendi
        _db.AddEntry(id2, 10); // id2 dolu

        var stats = await _db.Reports.GetDashboardStatsAsync();
        // id1 stok 0 → tükendi sayılır
        Assert.True(stats.DepletedStock >= 1);
    }

    [Fact]
    public async Task GetDashboardStats_LowStock_CountsBelowMinStok()
    {
        int id = _db.CreateCard("Düşük", "D01", minStok: 10);
        _db.AddEntry(id, 5); // 5 < minStok(10)

        var stats = await _db.Reports.GetDashboardStatsAsync();
        Assert.True(stats.LowStock >= 1);
    }

    // ── Stok raporu ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetStockReport_ReturnsOnlyAltCards()
    {
        _db.StockCards.Add(new StokTakip.Models.StokKarti { Ad = "Üst", KodNo = "U01", KartTipi = "Ust" });
        _db.CreateCard("Alt", "A01");

        var report = await _db.Reports.GetStockReportAsync();
        Assert.All(report, r => Assert.NotEqual("U01", r.Code));
    }

    [Fact]
    public async Task GetStockReport_CorrectTotals()
    {
        int id = _db.CreateCard("Rapor Stok", "R01");
        _db.AddEntry(id, 100);
        _db.AddEntry(id, 50);
        _db.AddExit(id, 30);

        var report = await _db.Reports.GetStockReportAsync();
        var row = report.First(r => r.Code == "R01");

        Assert.Equal(150, row.TotalEntry);
        Assert.Equal(30, row.TotalExit);
        Assert.Equal(120, row.Current);
    }

    [Fact]
    public async Task GetStockReport_OrderedByCode()
    {
        _db.CreateCard("Z Stok", "Z01");
        _db.CreateCard("A Stok", "A01");

        var report = await _db.Reports.GetStockReportAsync();
        Assert.Equal("A01", report[0].Code);
        Assert.Equal("Z01", report[1].Code);
    }
}
