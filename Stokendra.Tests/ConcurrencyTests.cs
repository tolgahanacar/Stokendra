using FluentAssertions;
using Stokendra.Data.Repositories;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Stokendra.Tests;

public class ConcurrencyTests : TestBase
{
    [Fact]
    public async Task MultipleThreads_AddingMovements_ShouldBeAtomic()
    {
        var factory = new TestDbFactory(Database.DatabasePath);
        var cardRepo = new StockCardRepository(factory);
        var moveRepo = new MovementRepository(factory);

        var card = new StockCard { Code = "SYNC-1", Name = "Sync Test", CardType = "Child" };
        cardRepo.Add(card);

        // 10 thread, her biri 10 giriş yapsın
        int threadCount = 10;
        int movesPerThread = 10;
        
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < movesPerThread; i++)
            {
                moveRepo.Add(new StockMovement 
                { 
                    StockCardId = card.Id, 
                    Type = "Entry", 
                    Quantity = 1, 
                    Date = DateTime.Now 
                });
            }
        }));

        await Task.WhenAll(tasks);

        var finalCard = cardRepo.GetById(card.Id);
        finalCard.CurrentStock.Should().Be(threadCount * movesPerThread);
    }
}
