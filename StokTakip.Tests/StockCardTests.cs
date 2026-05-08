using FluentAssertions;
using StokTakip.Data.Repositories;
using StokTakip.Models;
using Xunit;

namespace StokTakip.Tests;

public class StockCardTests : TestBase
{
    private readonly StockCardRepository _repo;

    public StockCardTests()
    {
        // DB connection factory mock'lanmalı veya Database nesnesi repo'ya verilmeli
        // Stokendra'da DbConnectionFactory kullanılıyor.
        var factory = new TestDbFactory(Database.DatabasePath);
        _repo = new StockCardRepository(factory);
    }

    [Fact]
    public void Add_ValidCard_ShouldAssignId()
    {
        var card = new StokKarti { KodNo = "T-001", Ad = "Test Kartı", KartTipi = "Alt" };
        _repo.Add(card);
        card.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Add_DuplicateCode_ShouldThrowException()
    {
        var card1 = new StokKarti { KodNo = "DUP-1", Ad = "Card 1", KartTipi = "Alt" };
        _repo.Add(card1);

        var card2 = new StokKarti { KodNo = "DUP-1", Ad = "Card 2", KartTipi = "Alt" };
        Action act = () => _repo.Add(card2);
        act.Should().Throw<InvalidOperationException>().WithMessage("*zaten kullanımda*");
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("Code", "")]
    [InlineData(null, "Name")]
    public void Add_InvalidData_ShouldThrowException(string code, string name)
    {
        var card = new StokKarti { KodNo = code, Ad = name, KartTipi = "Alt" };
        Action act = () => _repo.Add(card);
        act.Should().Throw<InvalidOperationException>();
    }
}
