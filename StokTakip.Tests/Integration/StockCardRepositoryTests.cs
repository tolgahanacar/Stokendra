using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// StockCardRepository — CRUD, stok hesaplama, hiyerarşi testleri.
/// Her test izole bir SQLite DB kullanır.
/// </summary>
public class StockCardRepositoryTests : IDisposable
{
    private readonly TestDb _db;
    public StockCardRepositoryTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    // ── CRUD ──────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ValidCard_AssignsPositiveId()
    {
        var kart = new StokKarti { Ad = "Toner", KodNo = "001", KartTipi = "Alt" };
        _db.StockCards.Add(kart);
        Assert.True(kart.Id > 0);
    }

    [Fact]
    public void Add_ThenGetById_ReturnsCorrectData()
    {
        var kart = new StokKarti
        {
            Ad = "Yazıcı Toner", KodNo = "002", KartTipi = "Alt",
            Kategori = "Toner", Birim = "Adet", MinStok = 5,
            Konum = "Raf A1", Tedarikci = "ABC Ltd", BirimFiyat = 150.0
        };
        _db.StockCards.Add(kart);

        var result = _db.StockCards.GetById(kart.Id);

        Assert.NotNull(result);
        Assert.Equal("Yazıcı Toner", result.Ad);
        Assert.Equal("002", result.KodNo);
        Assert.Equal("Toner", result.Kategori);
        Assert.Equal(5, result.MinStok);
        Assert.Equal(150.0, result.BirimFiyat);
    }

    [Fact]
    public void GetById_NonExistent_ReturnsNull()
    {
        Assert.Null(_db.StockCards.GetById(99999));
    }

    [Fact]
    public void Update_ChangesName_Persisted()
    {
        var kart = new StokKarti { Ad = "Eski Ad", KodNo = "003", KartTipi = "Alt" };
        _db.StockCards.Add(kart);

        kart.Ad = "Yeni Ad";
        _db.StockCards.Update(kart);

        Assert.Equal("Yeni Ad", _db.StockCards.GetById(kart.Id)?.Ad);
    }

    [Fact]
    public void Delete_ExistingCard_RemovedFromDb()
    {
        var kart = new StokKarti { Ad = "Silinecek", KodNo = "004", KartTipi = "Alt" };
        _db.StockCards.Add(kart);

        _db.StockCards.Delete(kart.Id);

        Assert.Null(_db.StockCards.GetById(kart.Id));
    }

    [Fact]
    public void GetAll_ReturnsAllCards()
    {
        _db.StockCards.Add(new StokKarti { Ad = "A", KodNo = "A01", KartTipi = "Alt" });
        _db.StockCards.Add(new StokKarti { Ad = "B", KodNo = "B01", KartTipi = "Alt" });

        var all = _db.StockCards.GetAll();
        Assert.Equal(2, all.Count);
    }

    // ── Stok hesaplama ────────────────────────────────────────────────────

    [Fact]
    public void MevcutStok_AfterEntry_EqualsEntryAmount()
    {
        int id = _db.CreateCard("Kağıt", "K001");
        _db.AddEntry(id, 100);

        Assert.Equal(100, _db.GetStock(id));
    }

    [Fact]
    public void MevcutStok_AfterEntryAndExit_CorrectBalance()
    {
        int id = _db.CreateCard("Kalem", "K002");
        _db.AddEntry(id, 50);
        _db.AddExit(id, 20);

        Assert.Equal(30, _db.GetStock(id));
    }

    [Fact]
    public void MevcutStok_MultipleEntriesAndExits_CorrectBalance()
    {
        int id = _db.CreateCard("Zımba", "Z001");
        _db.AddEntry(id, 100);
        _db.AddEntry(id, 50);
        _db.AddExit(id, 30);
        _db.AddExit(id, 20);

        // 100 + 50 - 30 - 20 = 100
        Assert.Equal(100, _db.GetStock(id));
    }

    [Fact]
    public void MevcutStok_NoMovements_IsZero()
    {
        int id = _db.CreateCard("Boş Kart", "B001");
        Assert.Equal(0, _db.GetStock(id));
    }

    [Fact]
    public void ToplamGiris_ToplamCikis_CorrectlyCalculated()
    {
        int id = _db.CreateCard("Dosya", "D001");
        _db.AddEntry(id, 80);
        _db.AddEntry(id, 20);
        _db.AddExit(id, 15);

        var kart = _db.StockCards.GetById(id)!;
        Assert.Equal(100, kart.ToplamGiris);
        Assert.Equal(15, kart.ToplamCikis);
        Assert.Equal(85, kart.MevcutStok);
    }

    // ── IsLowStock / IsDepleted ───────────────────────────────────────────

    [Fact]
    public void IsLowStock_WhenBelowMinStok_IsTrue()
    {
        int id = _db.CreateCard("Toner", "T001", minStok: 10);
        _db.AddEntry(id, 5); // 5 < minStok(10)

        var kart = _db.StockCards.GetById(id)!;
        Assert.True(kart.IsLowStock);
        Assert.False(kart.IsDepleted);
    }

    [Fact]
    public void IsLowStock_WhenAboveMinStok_IsFalse()
    {
        int id = _db.CreateCard("Toner", "T002", minStok: 5);
        _db.AddEntry(id, 20); // 20 > minStok(5)

        var kart = _db.StockCards.GetById(id)!;
        Assert.False(kart.IsLowStock);
    }

    [Fact]
    public void IsDepleted_WhenStockIsZero_IsTrue()
    {
        int id = _db.CreateCard("Toner", "T003");
        // Hareket yok → MevcutStok = 0

        var kart = _db.StockCards.GetById(id)!;
        Assert.True(kart.IsDepleted);
    }

    [Fact]
    public void IsDepleted_WhenStockIsPositive_IsFalse()
    {
        int id = _db.CreateCard("Toner", "T004");
        _db.AddEntry(id, 1);

        var kart = _db.StockCards.GetById(id)!;
        Assert.False(kart.IsDepleted);
    }

    [Fact]
    public void IsLowStock_DefaultThreshold3_WhenStockIs2_IsTrue()
    {
        // MinStok = 0 → eşik 3 olarak kabul edilir
        int id = _db.CreateCard("Toner", "T005", minStok: 0);
        _db.AddEntry(id, 2); // 2 <= 3

        var kart = _db.StockCards.GetById(id)!;
        Assert.True(kart.IsLowStock);
    }

    // ── Hiyerarşi ─────────────────────────────────────────────────────────

    [Fact]
    public void GetParentCards_ReturnsOnlyUstCards()
    {
        _db.StockCards.Add(new StokKarti { Ad = "Üst", KodNo = "UST01", KartTipi = "Ust" });
        _db.StockCards.Add(new StokKarti { Ad = "Alt", KodNo = "ALT01", KartTipi = "Alt" });

        var parents = _db.StockCards.GetParentCards();
        Assert.All(parents, k => Assert.Equal("Ust", k.KartTipi));
    }

    [Fact]
    public void GetChildCards_ReturnsOnlyAltCards()
    {
        _db.StockCards.Add(new StokKarti { Ad = "Üst", KodNo = "UST02", KartTipi = "Ust" });
        _db.StockCards.Add(new StokKarti { Ad = "Alt", KodNo = "ALT02", KartTipi = "Alt" });

        var children = _db.StockCards.GetChildCards();
        Assert.All(children, k => Assert.Equal("Alt", k.KartTipi));
    }

    [Fact]
    public void GetChildCards_FilterByParentId_ReturnsOnlyChildren()
    {
        var ust = new StokKarti { Ad = "Üst", KodNo = "UST03", KartTipi = "Ust" };
        _db.StockCards.Add(ust);

        var alt1 = new StokKarti { Ad = "Alt1", KodNo = "ALT03", KartTipi = "Alt", UstKartId = ust.Id };
        var alt2 = new StokKarti { Ad = "Alt2", KodNo = "ALT04", KartTipi = "Alt" }; // bağımsız
        _db.StockCards.Add(alt1);
        _db.StockCards.Add(alt2);

        var children = _db.StockCards.GetChildCards(ust.Id);
        Assert.Single(children);
        Assert.Equal("ALT03", children[0].KodNo);
    }

    // ── GetNextCode ───────────────────────────────────────────────────────

    [Fact]
    public void GetNextCode_EmptyDb_Returns001()
    {
        Assert.Equal("001", _db.StockCards.GetNextCode());
    }

    [Fact]
    public void GetNextCode_AfterAdding005_Returns006()
    {
        _db.StockCards.Add(new StokKarti { Ad = "X", KodNo = "005", KartTipi = "Alt" });
        Assert.Equal("006", _db.StockCards.GetNextCode());
    }

    // ── Async ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidCard_AssignsId()
    {
        var kart = new StokKarti { Ad = "Async", KodNo = "AS01", KartTipi = "Alt" };
        await _db.StockCards.AddAsync(kart);
        Assert.True(kart.Id > 0);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCards()
    {
        _db.StockCards.Add(new StokKarti { Ad = "X", KodNo = "X01", KartTipi = "Alt" });
        var result = await _db.StockCards.GetAllAsync();
        Assert.NotEmpty(result);
    }
}
