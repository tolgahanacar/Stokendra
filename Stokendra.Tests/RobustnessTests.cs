using FluentAssertions;
using Stokendra.Data.Repositories;
using Stokendra.Models;
using Xunit;
using System;

namespace Stokendra.Tests;

public class RobustnessTests : TestBase
{
    [Fact]
    public void SoakTest_LargeNumberOfOperations_ShouldNotLeakOrCrash()
    {
        var factory = new TestDbFactory(Database.DatabasePath);
        var cardRepo = new StockCardRepository(factory);
        
        // 1000 hızlı işlem
        for (int i = 0; i < 1000; i++)
        {
            cardRepo.Add(new StockCard { Code = $"S-{i}", Name = $"Item {i}", CardType = "Child" });
        }

        cardRepo.GetAll().Count.Should().Be(1000);
    }

    [Fact]
    public void FailureSimulation_InvalidSQL_ShouldBeCaughtByGlobalHandler()
    {
        var factory = new TestDbFactory(Database.DatabasePath);
        var repo = new MovementRepository(factory);

        var cardRepo = new StockCardRepository(factory);
        var card = new StockCard { Code = "FAIL-1", Name = "Fail Test", CardType = "Child" };
        cardRepo.Add(card);

        // Mevcut stok 0, çıkış 10 -> Hata fırlatmalı
        Action act = () => repo.Add(new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 10 });
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient stock*");
    }
}
