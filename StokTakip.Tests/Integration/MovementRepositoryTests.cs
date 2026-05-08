using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// MovementRepository — CRUD, stok etkisi, filtreler, bulk işlemler.
/// </summary>
public class MovementRepositoryTests : IDisposable
{
    private readonly TestDb _db;
    private readonly int _cardId;

    public MovementRepositoryTests()
    {
        _db = new TestDb();
        _cardId = _db.CreateCard("Test Stok", "TS001");
    }

    public void Dispose() => _db.Dispose();

    // ── Giriş / Çıkış etkisi ─────────────────────────────────────────────

    [Fact]
    public void Add_Entry_IncreasesStock()
    {
        _db.AddEntry(_cardId, 50);
        Assert.Equal(50, _db.GetStock(_cardId));
    }

    [Fact]
    public void Add_Exit_DecreasesStock()
    {
        _db.AddEntry(_cardId, 50);
        _db.AddExit(_cardId, 20);
        Assert.Equal(30, _db.GetStock(_cardId));
    }

    [Fact]
    public void Add_MultipleMovements_CorrectFinalStock()
    {
        _db.AddEntry(_cardId, 100);
        _db.AddEntry(_cardId, 50);
        _db.AddExit(_cardId, 30);
        _db.AddExit(_cardId, 40);
        // 100 + 50 - 30 - 40 = 80
        Assert.Equal(80, _db.GetStock(_cardId));
    }

    [Fact]
    public void Add_ExactlyDepletes_StockIsZero()
    {
        _db.AddEntry(_cardId, 10);
        _db.AddExit(_cardId, 10);
        Assert.Equal(0, _db.GetStock(_cardId));
    }

    // ── Validasyon ────────────────────────────────────────────────────────

    [Fact]
    public void Add_ExitMoreThanStock_ThrowsInvalidOperationException()
    {
        _db.AddEntry(_cardId, 10);
        Assert.Throws<InvalidOperationException>(() => _db.AddExit(_cardId, 11));
    }

    [Fact]
    public void Add_ExitOnEmptyStock_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _db.AddExit(_cardId, 1));
    }

    [Fact]
    public void Add_ZeroQuantity_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi
            {
                StokKartId = _cardId, Tur = "Giris", Miktar = 0, Departman = "Test"
            }));
    }

    [Fact]
    public void Add_NegativeQuantity_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi
            {
                StokKartId = _cardId, Tur = "Giris", Miktar = -5, Departman = "Test"
            }));
    }

    [Fact]
    public void Add_ToParentCard_ThrowsInvalidOperationException()
    {
        var ust = new StokKarti { Ad = "Üst", KodNo = "UST_M", KartTipi = "Ust" };
        _db.StockCards.Add(ust);

        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi
            {
                StokKartId = ust.Id, Tur = "Giris", Miktar = 10, Departman = "Test"
            }));
    }

    [Fact]
    public void Add_InvalidMovementType_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi
            {
                StokKartId = _cardId, Tur = "YANLIS", Miktar = 10, Departman = "Test"
            }));
    }

    // ── Bos hareket ───────────────────────────────────────────────────────

    [Fact]
    public void Add_BosMovement_DoesNotChangeStock()
    {
        _db.AddEntry(_cardId, 10);
        _db.Movements.Add(new StokHareketi
        {
            StokKartId = _cardId, Tur = "Bos", Miktar = 0, Departman = "Test"
        });
        Assert.Equal(10, _db.GetStock(_cardId));
    }

    // ── GetAll filtreleri ─────────────────────────────────────────────────

    [Fact]
    public void GetAll_FilterByCardId_ReturnsOnlyThatCard()
    {
        int card2 = _db.CreateCard("Stok 2", "TS002");
        _db.AddEntry(_cardId, 10);
        _db.AddEntry(card2, 20);

        var result = _db.Movements.GetAll(stockCardId: _cardId);
        Assert.All(result, h => Assert.Equal(_cardId, h.StokKartId));
    }

    [Fact]
    public void GetAll_FilterByType_ReturnsOnlyThatType()
    {
        _db.AddEntry(_cardId, 20);
        _db.AddExit(_cardId, 5);

        var entries = _db.Movements.GetAll(movementType: "Giris");
        Assert.All(entries, h => Assert.Equal("Giris", h.Tur));

        var exits = _db.Movements.GetAll(movementType: "Cikis");
        Assert.All(exits, h => Assert.Equal("Cikis", h.Tur));
    }

    [Fact]
    public void GetAll_FilterByDateRange_ReturnsOnlyInRange()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var tomorrow  = DateTime.Today.AddDays(1);

        _db.AddEntry(_cardId, 10, tarih: DateTime.Today);

        var result = _db.Movements.GetAll(startDate: yesterday, endDate: tomorrow);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GetAll_FilterByDepartment_ReturnsOnlyThatDept()
    {
        _db.AddEntry(_cardId, 10, dept: "Bilgi İşlem");
        _db.AddEntry(_cardId, 5,  dept: "Muhasebe");

        var result = _db.Movements.GetAll(department: "Bilgi İşlem");
        Assert.All(result, h => Assert.Equal("Bilgi İşlem", h.Departman));
    }

    [Fact]
    public void GetAll_NoFilter_ReturnsAllMovements()
    {
        _db.AddEntry(_cardId, 10);
        _db.AddEntry(_cardId, 20);
        _db.AddExit(_cardId, 5);

        var result = _db.Movements.GetAll();
        Assert.Equal(3, result.Count);
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_Entry_StockDecreases()
    {
        _db.AddEntry(_cardId, 30);
        var movements = _db.Movements.GetAll(stockCardId: _cardId);
        int entryId = movements[0].Id;

        _db.Movements.Delete(entryId);

        Assert.Equal(0, _db.GetStock(_cardId));
    }

    [Fact]
    public void Delete_EntryWhenExitExists_ThrowsInvalidOperationException()
    {
        // Giriş 10, çıkış 10 → stok 0
        // Girişi silmek stoku -10 yapar → hata
        _db.AddEntry(_cardId, 10);
        _db.AddExit(_cardId, 10);

        var entry = _db.Movements.GetAll(stockCardId: _cardId, movementType: "Giris").Single();
        Assert.Throws<InvalidOperationException>(() => _db.Movements.Delete(entry.Id));
    }

    // ── DeleteBulk ────────────────────────────────────────────────────────

    [Fact]
    public void DeleteBulk_AllEntries_StockBecomesZero()
    {
        _db.AddEntry(_cardId, 10);
        _db.AddEntry(_cardId, 20);
        var ids = _db.Movements.GetAll(stockCardId: _cardId).Select(h => h.Id).ToList();

        _db.Movements.DeleteBulk(ids);

        Assert.Equal(0, _db.GetStock(_cardId));
        Assert.Empty(_db.Movements.GetAll(stockCardId: _cardId));
    }

    [Fact]
    public void DeleteBulk_WouldCauseNegative_ThrowsInvalidOperationException()
    {
        _db.AddEntry(_cardId, 10);
        _db.AddExit(_cardId, 5);

        // Sadece girişi silmek → stok -5 olur
        var entryId = _db.Movements.GetAll(stockCardId: _cardId, movementType: "Giris")
                                   .Select(h => h.Id).ToList();

        Assert.Throws<InvalidOperationException>(() => _db.Movements.DeleteBulk(entryId));
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ChangeDescription_Persisted()
    {
        _db.AddEntry(_cardId, 10);
        var h = _db.Movements.GetAll(stockCardId: _cardId).Single();
        h.Aciklama = "Güncellendi";

        _db.Movements.Update(h);

        var updated = _db.Movements.GetAll(stockCardId: _cardId).Single();
        Assert.Equal("Güncellendi", updated.Aciklama);
    }

    [Fact]
    public void Update_IncreaseMiktar_StockIncreases()
    {
        _db.AddEntry(_cardId, 10);
        var h = _db.Movements.GetAll(stockCardId: _cardId).Single();
        h.Miktar = 20;

        _db.Movements.Update(h);

        Assert.Equal(20, _db.GetStock(_cardId));
    }

    [Fact]
    public void Update_DecreaseMiktar_WouldCauseNegative_ThrowsInvalidOperationException()
    {
        _db.AddEntry(_cardId, 10);
        _db.AddExit(_cardId, 8);

        // Girişi 5'e düşürmek → stok 5-8 = -3 olur
        var entry = _db.Movements.GetAll(stockCardId: _cardId, movementType: "Giris").Single();
        entry.Miktar = 5;

        Assert.Throws<InvalidOperationException>(() => _db.Movements.Update(entry));
    }

    // ── AddBulk ───────────────────────────────────────────────────────────

    [Fact]
    public void AddBulk_ThreeEntries_AllPersisted()
    {
        var list = new[]
        {
            new StokHareketi { StokKartId = _cardId, Tur = "Giris", Miktar = 10, Departman = "D" },
            new StokHareketi { StokKartId = _cardId, Tur = "Giris", Miktar = 20, Departman = "D" },
            new StokHareketi { StokKartId = _cardId, Tur = "Giris", Miktar = 30, Departman = "D" },
        };

        _db.Movements.AddBulk(list);

        Assert.Equal(60, _db.GetStock(_cardId));
        Assert.Equal(3, _db.Movements.GetAll(stockCardId: _cardId).Count);
    }

    [Fact]
    public void AddBulk_EmptyList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.AddBulk(Array.Empty<StokHareketi>()));
    }

    // ── GetLast7DaysSummary ───────────────────────────────────────────────

    [Fact]
    public async Task GetLast7DaysSummaryAsync_AlwaysReturns7Items()
    {
        var result = await _db.Movements.GetLast7DaysSummaryAsync();
        Assert.Equal(7, result.Count);
    }

    [Fact]
    public async Task GetLast7DaysSummaryAsync_TodayEntry_ReflectedInSummary()
    {
        _db.AddEntry(_cardId, 25, tarih: DateTime.Today);

        var result = await _db.Movements.GetLast7DaysSummaryAsync();
        var today = result.First(r => r.Date.Date == DateTime.Today);

        Assert.Equal(25, today.Entry);
        Assert.Equal(0, today.Exit);
    }

    [Fact]
    public async Task GetLast7DaysSummaryAsync_TodayExit_ReflectedInSummary()
    {
        _db.AddEntry(_cardId, 50, tarih: DateTime.Today);
        _db.AddExit(_cardId, 15, tarih: DateTime.Today);

        var result = await _db.Movements.GetLast7DaysSummaryAsync();
        var today = result.First(r => r.Date.Date == DateTime.Today);

        Assert.Equal(50, today.Entry);
        Assert.Equal(15, today.Exit);
    }

    // ── GetDeliveredPersons ───────────────────────────────────────────────

    [Fact]
    public void GetDeliveredPersons_ReturnsDistinctNames()
    {
        _db.AddEntry(_cardId, 30);
        _db.AddExit(_cardId, 5,  teslim: "Ahmet");
        _db.AddExit(_cardId, 5,  teslim: "Mehmet");
        _db.AddExit(_cardId, 5,  teslim: "Ahmet"); // tekrar

        var persons = _db.Movements.GetDeliveredPersons();
        Assert.Equal(2, persons.Count);
        Assert.Contains("Ahmet", persons);
        Assert.Contains("Mehmet", persons);
    }

    [Fact]
    public void GetDeliveredPersons_EmptyTeslim_NotIncluded()
    {
        _db.AddEntry(_cardId, 10, teslim: "");

        var persons = _db.Movements.GetDeliveredPersons();
        Assert.Empty(persons);
    }
}
