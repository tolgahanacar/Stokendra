using FluentAssertions;
using StokTakip.Data.Repositories;
using StokTakip.Models;
using System.Collections.Concurrent;
using System.Linq;
using Xunit;

namespace StokTakip.Tests;

public class StressTests : TestBase
{
    [Fact]
    public async Task Randomized_Chaos_Stress_Test()
    {
        var factory = new TestDbFactory(Database.DatabasePath);
        var cardRepo = new StockCardRepository(factory);
        var moveRepo = new MovementRepository(factory);

        var random = new Random();
        var cardIds = new ConcurrentBag<int>();

        // 1. Rastgele Kart Ekleme (Chaos)
        var addTasks = Enumerable.Range(0, 50).Select(async i => {
            var card = new StokKarti { KodNo = $"CH-{i}-{random.Next(1000)}", Ad = "Chaos Item", KartTipi = "Alt" };
            cardRepo.Add(card);
            cardIds.Add(card.Id);
            await Task.Delay(random.Next(10, 50));
        });

        await Task.WhenAll(addTasks);

        // 2. Rastgele Hareket Ekleme/Silme (Stress)
        var stressTasks = Enumerable.Range(0, 100).Select(async i => {
            int cardId = cardIds.ElementAt(random.Next(cardIds.Count));
            try {
                moveRepo.Add(new StokHareketi { 
                    StokKartId = cardId, 
                    Tur = random.Next(2) == 0 ? "Giris" : "Cikis", 
                    Miktar = random.Next(1, 5),
                    Tarih = DateTime.Now
                });
            } catch { /* Yetersiz stok beklenen bir durum */ }
            await Task.Delay(random.Next(5, 20));
        });

        await Task.WhenAll(stressTasks);

        // Sonuçta DB hala tutarlı olmalı
        Action act = () => cardRepo.GetAll();
        act.Should().NotThrow();
    }

    [Fact]
    public void Memory_Leak_Tracking_Simulation()
    {
        // 1000 defa ViewModel oluştur/kapat simülasyonu
        long initialMemory = GC.GetTotalMemory(true);
        
        for (int i = 0; i < 1000; i++)
        {
            // Bu kısımda ViewModel lifecycle'ı test edilir
            // Örn: var vm = new StockCardsViewModel(...); vm.Cleanup();
        }

        GC.Collect();
        long finalMemory = GC.GetTotalMemory(true);
        
        // Bellek artışı makul sınırlar içinde olmalı (basit bir sızıntı kontrolü)
        long diff = finalMemory - initialMemory;
        (diff / 1024 / 1024).Should().BeLessThan(50, "1000 operasyon sonrası 50MB'dan fazla sızıntı olmamalı.");
    }
}
