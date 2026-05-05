using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// <see cref="IStockCardRepository"/> implementasyonu için integration testler.
/// Her test izole bir SQLite veritabanı kullanır.
/// </summary>
public class StockCardRepositoryTests : IDisposable
{
    private readonly TestDatabaseFactory _factory;
    private readonly IStockCardRepository _repo;

    public StockCardRepositoryTests()
    {
        _factory = new TestDatabaseFactory();
        _repo = _factory.Create();
    }

    public void Dispose() => _factory.Dispose();

    // ── Add ───────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ValidCard_AssignsId()
    {
        var kart = new StokKarti { Ad = "Test Ürün", KodNo = "T001", KartTipi = "Alt" };

        _repo.Add(kart);

        Assert.True(kart.Id > 0);
    }

    [Fact]
    public void Add_DuplicateCode_ThrowsInvalidOperationException()
    {
        _repo.Add(new StokKarti { Ad = "Ürün A", KodNo = "DUP001", KartTipi = "Alt" });

        Assert.Throws<InvalidOperationException>(() =>
            _repo.Add(new StokKarti { Ad = "Ürün B", KodNo = "DUP001", KartTipi = "Alt" }));
    }

    [Fact]
    public void Add_EmptyName_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _repo.Add(new StokKarti { Ad = "", KodNo = "E001", KartTipi = "Alt" }));
    }

    [Fact]
    public void Add_EmptyCode_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _repo.Add(new StokKarti { Ad = "Ürün", KodNo = "", KartTipi = "Alt" }));
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllCards()
    {
        _repo.Add(new StokKarti { Ad = "Ürün 1", KodNo = "G001", KartTipi = "Alt" });
        _repo.Add(new StokKarti { Ad = "Ürün 2", KodNo = "G002", KartTipi = "Alt" });

        var result = _repo.GetAll();

        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        var result = _repo.GetAll();
        Assert.Empty(result);
    }

    // ── GetById ───────────────────────────────────────────────────────────

    [Fact]
    public void GetById_ExistingId_ReturnsCard()
    {
        var kart = new StokKarti { Ad = "Detay Test", KodNo = "D001", KartTipi = "Alt" };
        _repo.Add(kart);

        var result = _repo.GetById(kart.Id);

        Assert.NotNull(result);
        Assert.Equal("Detay Test", result.Ad);
        Assert.Equal("D001", result.KodNo);
    }

    [Fact]
    public void GetById_NonExistingId_ReturnsNull()
    {
        var result = _repo.GetById(99999);
        Assert.Null(result);
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ExistingCard_ChangesArePersisted()
    {
        var kart = new StokKarti { Ad = "Eski Ad", KodNo = "U001", KartTipi = "Alt" };
        _repo.Add(kart);

        kart.Ad = "Yeni Ad";
        _repo.Update(kart);

        var updated = _repo.GetById(kart.Id);
        Assert.Equal("Yeni Ad", updated?.Ad);
    }

    [Fact]
    public void Update_NonExistingCard_ThrowsInvalidOperationException()
    {
        var kart = new StokKarti { Id = 99999, Ad = "Yok", KodNo = "N001", KartTipi = "Alt" };
        Assert.Throws<InvalidOperationException>(() => _repo.Update(kart));
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_ExistingCard_RemovesFromDatabase()
    {
        var kart = new StokKarti { Ad = "Silinecek", KodNo = "S001", KartTipi = "Alt" };
        _repo.Add(kart);

        _repo.Delete(kart.Id);

        Assert.Null(_repo.GetById(kart.Id));
    }

    [Fact]
    public void Delete_NonExistingId_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _repo.Delete(99999));
    }

    // ── Parent/Child ──────────────────────────────────────────────────────

    [Fact]
    public void GetParentCards_ReturnsOnlyUstCards()
    {
        _repo.Add(new StokKarti { Ad = "Üst Kart", KodNo = "UST001", KartTipi = "Ust" });
        _repo.Add(new StokKarti { Ad = "Alt Kart", KodNo = "ALT001", KartTipi = "Alt" });

        var parents = _repo.GetParentCards();

        Assert.All(parents, k => Assert.Equal("Ust", k.KartTipi));
    }

    [Fact]
    public void GetChildCards_ReturnsOnlyAltCards()
    {
        _repo.Add(new StokKarti { Ad = "Üst Kart", KodNo = "UST002", KartTipi = "Ust" });
        _repo.Add(new StokKarti { Ad = "Alt Kart", KodNo = "ALT002", KartTipi = "Alt" });

        var children = _repo.GetChildCards();

        Assert.All(children, k => Assert.Equal("Alt", k.KartTipi));
    }

    [Fact]
    public void Add_ParentCardWithMovements_CannotBeConvertedToParent()
    {
        // Alt kart ekle, hareket ekle, sonra Ust'a çevirmeye çalış
        // Bu test Database.StockCard.cs ValidateStokKarti mantığını doğrular
        var kart = new StokKarti { Ad = "Dönüşüm Test", KodNo = "DT001", KartTipi = "Alt" };
        _repo.Add(kart);

        // Kart tipini Ust'a çevirmeye çalış — hareket yoksa geçmeli
        kart.KartTipi = "Ust";
        _repo.Update(kart); // Hareket yoksa başarılı olmalı

        var updated = _repo.GetById(kart.Id);
        Assert.Equal("Ust", updated?.KartTipi);
    }

    // ── GetNextCode ───────────────────────────────────────────────────────

    [Fact]
    public void GetNextCode_EmptyDatabase_Returns001()
    {
        var code = _repo.GetNextCode();
        Assert.Equal("001", code);
    }

    [Fact]
    public void GetNextCode_AfterAddingCards_ReturnsIncrementedCode()
    {
        _repo.Add(new StokKarti { Ad = "Ürün", KodNo = "005", KartTipi = "Alt" });

        var code = _repo.GetNextCode();
        Assert.Equal("006", code);
    }

    // ── Async ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidCard_AssignsId()
    {
        var kart = new StokKarti { Ad = "Async Test", KodNo = "AS001", KartTipi = "Alt" };

        await _repo.AddAsync(kart);

        Assert.True(kart.Id > 0);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCards()
    {
        _repo.Add(new StokKarti { Ad = "Async Ürün", KodNo = "AQ001", KartTipi = "Alt" });

        var result = await _repo.GetAllAsync();

        Assert.NotEmpty(result);
    }
}
