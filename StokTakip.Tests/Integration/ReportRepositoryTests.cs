using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// <see cref="IReportRepository"/> implementasyonu için integration testler.
/// Dashboard istatistikleri ve stok raporu doğruluğunu test eder.
/// </summary>
public class ReportRepositoryTests : IDisposable
{
    private readonly TestDatabaseFactory _factory;
    private readonly IReportRepository _reportRepo;
    private readonly IStockCardRepository _cardRepo;
    private readonly IMovementRepository _movRepo;
    private readonly IDepartmentRepository _deptRepo;

    public ReportRepositoryTests()
    {
        _factory = new TestDatabaseFactory();
        var db = _factory.Create();
        _reportRepo = db;
        _cardRepo = db;
        _movRepo = db;
        _deptRepo = db;

        _deptRepo.Add("Genel");
    }

    public void Dispose() => _factory.Dispose();

    private int AddCard(string ad, string kod)
    {
        var kart = new StokKarti { Ad = ad, KodNo = kod, KartTipi = "Alt" };
        _cardRepo.Add(kart);
        return kart.Id;
    }

    private void AddEntry(int cardId, double miktar)
    {
        _movRepo.Add(new StokHareketi
        {
            StokKartId = cardId,
            Tur = "Giris",
            Miktar = miktar,
            Departman = "Genel",
            Tarih = DateTime.Now
        });
    }

    private void AddExit(int cardId, double miktar)
    {
        _movRepo.Add(new StokHareketi
        {
            StokKartId = cardId,
            Tur = "Cikis",
            Miktar = miktar,
            Departman = "Genel",
            Tarih = DateTime.Now
        });
    }

    // ── DashboardStats ────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardStatsAsync_EmptyDatabase_ReturnsZeros()
    {
        var stats = await _reportRepo.GetDashboardStatsAsync();

        Assert.Equal(0, stats.TotalCards);
        Assert.Equal(0, stats.TotalStock);
        Assert.Equal(0, stats.TotalMovements);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_WithCards_ReturnsTotalCards()
    {
        AddCard("Ürün A", "R001");
        AddCard("Ürün B", "R002");

        var stats = await _reportRepo.GetDashboardStatsAsync();

        Assert.Equal(2, stats.TotalCards);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_WithMovements_ReturnsTotalStock()
    {
        int id = AddCard("Ürün", "R003");
        AddEntry(id, 50);
        AddExit(id, 10);

        var stats = await _reportRepo.GetDashboardStatsAsync();

        Assert.Equal(40, stats.TotalStock);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_LowStockCard_CountedInLowStock()
    {
        var kart = new StokKarti { Ad = "Düşük Stok", KodNo = "R004", KartTipi = "Alt", MinStok = 10 };
        _cardRepo.Add(kart);
        AddEntry(kart.Id, 5); // MinStok=10, Mevcut=5 → düşük stok

        var stats = await _reportRepo.GetDashboardStatsAsync();

        Assert.True(stats.LowStock >= 1);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_DepletedCard_CountedInDepleted()
    {
        int id = AddCard("Tükenmiş", "R005");
        // Hareket yok → stok = 0 → tükenmiş

        var stats = await _reportRepo.GetDashboardStatsAsync();

        Assert.True(stats.DepletedStock >= 1);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_TodayMovements_CountedCorrectly()
    {
        int id = AddCard("Bugün", "R006");
        AddEntry(id, 10);

        var stats = await _reportRepo.GetDashboardStatsAsync();

        Assert.True(stats.TodayMovements >= 1);
    }

    // ── StockReport ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetStockReportAsync_ReturnsOnlyChildCards()
    {
        _cardRepo.Add(new StokKarti { Ad = "Üst", KodNo = "UST_R", KartTipi = "Ust" });
        AddCard("Alt", "ALT_R");

        var report = await _reportRepo.GetStockReportAsync();

        Assert.All(report, r => Assert.NotEqual("UST_R", r.Code));
    }

    [Fact]
    public async Task GetStockReportAsync_CalculatesCorrectTotals()
    {
        int id = AddCard("Rapor Ürün", "RP001");
        AddEntry(id, 100);
        AddExit(id, 30);

        var report = await _reportRepo.GetStockReportAsync();
        var row = report.FirstOrDefault(r => r.Code == "RP001");

        Assert.NotNull(row);
        Assert.Equal(100, row.TotalEntry);
        Assert.Equal(30, row.TotalExit);
        Assert.Equal(70, row.Current);
    }

    // ── ExportSqlBackup ───────────────────────────────────────────────────

    [Fact]
    public void ExportSqlBackup_CreatesNonEmptyFile()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_backup_{Guid.NewGuid():N}.sql");
        try
        {
            _reportRepo.ExportSqlBackup(tempFile);

            Assert.True(File.Exists(tempFile));
            Assert.True(new FileInfo(tempFile).Length > 0);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportSqlBackup_ContainsTableDefinitions()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_backup_{Guid.NewGuid():N}.sql");
        try
        {
            _reportRepo.ExportSqlBackup(tempFile);
            string content = File.ReadAllText(tempFile);

            Assert.Contains("StokKartlari", content);
            Assert.Contains("StokHareketleri", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
