using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// <see cref="IMovementRepository"/> implementasyonu için integration testler.
/// </summary>
public class MovementRepositoryTests : IDisposable
{
    private readonly TestDatabaseFactory _factory;
    private readonly IMovementRepository _movRepo;
    private readonly IStockCardRepository _cardRepo;
    private readonly IDepartmentRepository _deptRepo;
    private int _testCardId;

    public MovementRepositoryTests()
    {
        _factory = new TestDatabaseFactory();
        var db = _factory.Create();
        _movRepo = db;
        _cardRepo = db;
        _deptRepo = db;

        // Test için departman ve stok kartı oluştur
        _deptRepo.Add("Test Departman");
        var kart = new StokKarti { Ad = "Test Stok", KodNo = "TS001", KartTipi = "Alt" };
        _cardRepo.Add(kart);
        _testCardId = kart.Id;
    }

    public void Dispose() => _factory.Dispose();

    private StokHareketi MakeEntry(double miktar = 10) => new()
    {
        StokKartId = _testCardId,
        Tur = "Giris",
        Miktar = miktar,
        Departman = "Test Departman",
        Tarih = DateTime.Now
    };

    private StokHareketi MakeExit(double miktar = 5) => new()
    {
        StokKartId = _testCardId,
        Tur = "Cikis",
        Miktar = miktar,
        Departman = "Test Departman",
        Tarih = DateTime.Now
    };

    // ── Add ───────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ValidEntry_IncreasesStock()
    {
        _movRepo.Add(MakeEntry(20));

        var card = _cardRepo.GetById(_testCardId);
        Assert.Equal(20, card?.MevcutStok);
    }

    [Fact]
    public void Add_ValidExit_DecreasesStock()
    {
        _movRepo.Add(MakeEntry(20));
        _movRepo.Add(MakeExit(8));

        var card = _cardRepo.GetById(_testCardId);
        Assert.Equal(12, card?.MevcutStok);
    }

    [Fact]
    public void Add_ExitWithoutSufficientStock_ThrowsInvalidOperationException()
    {
        _movRepo.Add(MakeEntry(5));

        Assert.Throws<InvalidOperationException>(() => _movRepo.Add(MakeExit(10)));
    }

    [Fact]
    public void Add_ZeroQuantityEntry_ThrowsInvalidOperationException()
    {
        var hareket = MakeEntry(0);
        Assert.Throws<InvalidOperationException>(() => _movRepo.Add(hareket));
    }

    [Fact]
    public void Add_NegativeQuantity_ThrowsInvalidOperationException()
    {
        var hareket = MakeEntry(-5);
        Assert.Throws<InvalidOperationException>(() => _movRepo.Add(hareket));
    }

    [Fact]
    public void Add_ToParentCard_ThrowsInvalidOperationException()
    {
        var ustKart = new StokKarti { Ad = "Üst", KodNo = "UST_MOV", KartTipi = "Ust" };
        _cardRepo.Add(ustKart);

        var hareket = new StokHareketi
        {
            StokKartId = ustKart.Id,
            Tur = "Giris",
            Miktar = 10,
            Departman = "Test Departman"
        };

        Assert.Throws<InvalidOperationException>(() => _movRepo.Add(hareket));
    }

    // ── AddBulk ───────────────────────────────────────────────────────────

    [Fact]
    public void AddBulk_MultipleEntries_AllPersisted()
    {
        var hareketler = new[]
        {
            MakeEntry(10),
            MakeEntry(20),
            MakeEntry(30)
        };

        _movRepo.AddBulk(hareketler);

        var result = _movRepo.GetAll(stockCardId: _testCardId);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void AddBulk_EmptyList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _movRepo.AddBulk(Array.Empty<StokHareketi>()));
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_FilterByStockCardId_ReturnsOnlyMatchingMovements()
    {
        var kart2 = new StokKarti { Ad = "Stok 2", KodNo = "TS002", KartTipi = "Alt" };
        _cardRepo.Add(kart2);

        _movRepo.Add(MakeEntry(10));
        _movRepo.Add(new StokHareketi
        {
            StokKartId = kart2.Id,
            Tur = "Giris",
            Miktar = 5,
            Departman = "Test Departman"
        });

        var result = _movRepo.GetAll(stockCardId: _testCardId);
        Assert.All(result, h => Assert.Equal(_testCardId, h.StokKartId));
    }

    [Fact]
    public void GetAll_FilterByMovementType_ReturnsOnlyMatchingType()
    {
        _movRepo.Add(MakeEntry(10));
        _movRepo.Add(MakeExit(3));

        var entries = _movRepo.GetAll(movementType: "Giris");
        Assert.All(entries, h => Assert.Equal("Giris", h.Tur));
    }

    [Fact]
    public void GetAll_FilterByDateRange_ReturnsOnlyInRange()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var tomorrow = DateTime.Today.AddDays(1);

        _movRepo.Add(MakeEntry(10));

        var result = _movRepo.GetAll(startDate: yesterday, endDate: tomorrow);
        Assert.NotEmpty(result);
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_ExistingMovement_RemovesFromDatabase()
    {
        _movRepo.Add(MakeEntry(10));
        var movements = _movRepo.GetAll(stockCardId: _testCardId);
        int id = movements[0].Id;

        _movRepo.Delete(id);

        var after = _movRepo.GetAll(stockCardId: _testCardId);
        Assert.DoesNotContain(after, h => h.Id == id);
    }

    [Fact]
    public void Delete_WouldCauseNegativeStock_ThrowsInvalidOperationException()
    {
        _movRepo.Add(MakeEntry(10));
        _movRepo.Add(MakeExit(10));

        // Girişi silmek stoku negatife düşürür
        var entries = _movRepo.GetAll(stockCardId: _testCardId, movementType: "Giris");
        Assert.Throws<InvalidOperationException>(() => _movRepo.Delete(entries[0].Id));
    }

    // ── DeleteBulk ────────────────────────────────────────────────────────

    [Fact]
    public void DeleteBulk_ValidIds_RemovesAll()
    {
        _movRepo.Add(MakeEntry(10));
        _movRepo.Add(MakeEntry(20));
        var movements = _movRepo.GetAll(stockCardId: _testCardId);
        var ids = movements.Select(h => h.Id).ToList();

        _movRepo.DeleteBulk(ids);

        var after = _movRepo.GetAll(stockCardId: _testCardId);
        Assert.Empty(after);
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ExistingMovement_ChangesArePersisted()
    {
        _movRepo.Add(MakeEntry(10));
        var movements = _movRepo.GetAll(stockCardId: _testCardId);
        var hareket = movements[0];

        hareket.Aciklama = "Güncellenmiş açıklama";
        _movRepo.Update(hareket);

        var updated = _movRepo.GetAll(stockCardId: _testCardId);
        Assert.Equal("Güncellenmiş açıklama", updated[0].Aciklama);
    }

    // ── GetLast7DaysSummary ───────────────────────────────────────────────

    [Fact]
    public async Task GetLast7DaysSummaryAsync_Returns7Days()
    {
        var result = await _movRepo.GetLast7DaysSummaryAsync();
        Assert.Equal(7, result.Count);
    }

    [Fact]
    public async Task GetLast7DaysSummaryAsync_TodayEntryReflected()
    {
        _movRepo.Add(MakeEntry(15));

        var result = await _movRepo.GetLast7DaysSummaryAsync();
        var today = result.FirstOrDefault(r => r.Date.Date == DateTime.Today);

        Assert.Equal(15, today.Entry);
    }

    // ── GetDeliveredPersons ───────────────────────────────────────────────

    [Fact]
    public void GetDeliveredPersons_ReturnsDistinctNames()
    {
        var h1 = MakeExit(2); h1.TeslimEdilen = "Ahmet";
        var h2 = MakeExit(2); h2.TeslimEdilen = "Mehmet";
        var h3 = MakeExit(2); h3.TeslimEdilen = "Ahmet"; // tekrar

        _movRepo.Add(MakeEntry(20));
        _movRepo.Add(h1);
        _movRepo.Add(h2);
        _movRepo.Add(h3);

        var persons = _movRepo.GetDeliveredPersons();
        Assert.Equal(2, persons.Count);
        Assert.Contains("Ahmet", persons);
        Assert.Contains("Mehmet", persons);
    }
    [Fact]
    public void Update_EntryWithDependentExit_AllowsSameCardUpdate()
    {
        _movRepo.Add(MakeEntry(10));
        _movRepo.Add(MakeExit(5));

        var entry = _movRepo.GetAll(stockCardId: _testCardId, movementType: "Giris").Single();
        entry.Miktar = 12;

        _movRepo.Update(entry);

        var card = _cardRepo.GetById(_testCardId);
        Assert.Equal(7, card?.MevcutStok);
    }

    [Fact]
    public void Update_MoveEntryToDifferentCard_WhenOldCardWouldGoNegative_ThrowsInvalidOperationException()
    {
        var kart2 = new StokKarti { Ad = "Stok 2", KodNo = "TS002_MOVE", KartTipi = "Alt" };
        _cardRepo.Add(kart2);

        _movRepo.Add(MakeEntry(10));
        _movRepo.Add(MakeExit(5));

        var entry = _movRepo.GetAll(stockCardId: _testCardId, movementType: "Giris").Single();
        entry.StokKartId = kart2.Id;

        Assert.Throws<InvalidOperationException>(() => _movRepo.Update(entry));
    }
}
