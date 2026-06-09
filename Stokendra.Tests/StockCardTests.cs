using FluentAssertions;
using Stokendra.Data.Repositories;
using Stokendra.Models;
using Xunit;
using System;

namespace Stokendra.Tests;

public class StockCardTests : TestBase
{
    private readonly StockCardRepository _repo;

    public StockCardTests()
    {
        var factory = new TestDbFactory(Database.DatabasePath);
        _repo = new StockCardRepository(factory);
    }

    [Fact]
    public void Add_ValidCard_ShouldAssignId()
    {
        var card = new StockCard { Code = "T-001", Name = "Test Kartı", CardType = "Child" };
        _repo.Add(card);
        card.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Add_DuplicateCode_ShouldThrowException()
    {
        var card1 = new StockCard { Code = "DUP-1", Name = "Card 1", CardType = "Child" };
        _repo.Add(card1);

        var card2 = new StockCard { Code = "DUP-1", Name = "Card 2", CardType = "Child" };
        Action act = () => _repo.Add(card2);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already in use*");
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("Code", "")]
    [InlineData(null, "Name")]
    public void Add_InvalidData_ShouldThrowException(string code, string name)
    {
        var card = new StockCard { Code = code, Name = name, CardType = "Child" };
        Action act = () => _repo.Add(card);
        act.Should().Throw<InvalidOperationException>();
    }
}
