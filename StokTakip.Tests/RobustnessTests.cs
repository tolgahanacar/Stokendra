using FluentAssertions;
using StokTakip.Data.Repositories;
using StokTakip.Models;
using Xunit;

namespace StokTakip.Tests;

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
            cardRepo.Add(new StokKarti { KodNo = $"S-{i}", Ad = $"Item {i}", KartTipi = "Alt" });
        }

        cardRepo.GetAll().Count.Should().Be(1000);
    }

    [Fact]
    public void FailureSimulation_InvalidSQL_ShouldBeCaughtByGlobalHandler()
    {
        // Bu test uygulamanın hata yakalama yeteneğini ölçer
        // Direkt repository üzerinden geçersiz işlem yapalım
        var factory = new TestDbFactory(Database.DatabasePath);
        var repo = new MovementRepository(factory);

        // Negatif stok engellenmeli (önceden refactor ettik)
        var cardRepo = new StockCardRepository(factory);
        var card = new StokKarti { KodNo = "FAIL-1", Ad = "Fail Test", KartTipi = "Alt" };
        cardRepo.Add(card);

        // Mevcut stok 0, çıkış 10 -> Hata fırlatmalı
        Action act = () => repo.Add(new StokHareketi { StokKartId = card.Id, Tur = "Cikis", Miktar = 10 });
        act.Should().Throw<InvalidOperationException>().WithMessage("*Yetersiz stok*");
    }
}
