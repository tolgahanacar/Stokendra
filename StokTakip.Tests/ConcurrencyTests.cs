using FluentAssertions;
using StokTakip.Data.Repositories;
using StokTakip.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace StokTakip.Tests;

public class ConcurrencyTests : TestBase
{
    [Fact]
    public async Task MultipleThreads_AddingMovements_ShouldBeAtomic()
    {
        var factory = new TestDbFactory(Database.DatabasePath);
        var cardRepo = new StockCardRepository(factory);
        var moveRepo = new MovementRepository(factory);

        var card = new StokKarti { KodNo = "SYNC-1", Ad = "Sync Test", KartTipi = "Alt" };
        cardRepo.Add(card);

        // 10 thread, her biri 10 giriş yapsın
        int threadCount = 10;
        int movesPerThread = 10;
        
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < movesPerThread; i++)
            {
                moveRepo.Add(new StokHareketi 
                { 
                    StokKartId = card.Id, 
                    Tur = "Giris", 
                    Miktar = 1, 
                    Tarih = DateTime.Now 
                });
            }
        }));

        await Task.WhenAll(tasks);

        var finalCard = cardRepo.GetById(card.Id);
        finalCard.MevcutStok.Should().Be(threadCount * movesPerThread);
    }
}
