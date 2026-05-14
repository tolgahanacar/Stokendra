using Moq;
using StokTakip.ViewModels;
using StokTakip.Data.Interfaces;
using StokTakip.Infrastructure;
using Xunit;
using FluentAssertions;

namespace StokTakip.Tests;

public class UITests : TestBase
{
    [Fact]
    public async Task LoginViewModel_CorrectCredentials_ShouldTriggerSuccess()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.VerifyPasswordAsync("admin", "123")).ReturnsAsync((true, "Admin"));
        
        var vm = new LoginViewModel(mockRepo.Object);
        vm.Username = "admin";
        vm.Password = "123";

        bool successTriggered = false;
        vm.LoginSuccessful += (s, e) => successTriggered = true;

        await vm.DoLoginCommand.ExecuteAsync(null);

        successTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task StockCardsViewModel_Refresh_ShouldLoadFromRepo()
    {
        var mockRepo = new Mock<IStockCardRepository>();
        var mockMovements = new Mock<IMovementRepository>();
        var mockDepts = new Mock<IDepartmentRepository>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Models.StokKarti> { 
            new Models.StokKarti { Ad = "Mock Card" } 
        });

        var vm = new StockCardsViewModel(mockRepo.Object, mockMovements.Object, mockDepts.Object, MockDialog.Object, MockLogger.Object);
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Cards.Should().HaveCount(1);
        vm.Cards[0].Ad.Should().Be("Mock Card");
    }

    [Fact]
    public async Task StockMovementsViewModel_ButtonsAndFilters_ShouldFunctionCorrectly()
    {
        // Gerekli Mock'lar
        var mockMovements = new Mock<IMovementRepository>();
        var mockCards = new Mock<IStockCardRepository>();
        var mockDepts = new Mock<IDepartmentRepository>();

        // Sahte veri dönüşleri
        mockCards.Setup(r => r.GetChildCardsAsync()).ReturnsAsync(new List<Models.StokKarti> {
            new Models.StokKarti { Id = 1, KodNo = "STK-1", Ad = "Saldırgan Test Kartı" }
        });

        mockMovements.Setup(r => r.GetCountAsync(It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(100);

        mockMovements.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<Models.StokHareketi> {
                         new Models.StokHareketi { Id = 10, Miktar = 50, Tur = "Giris" }
                     });

        var vm = new StockMovementsViewModel(mockMovements.Object, mockCards.Object, mockDepts.Object, MockDialog.Object, MockLogger.Object);
        
        // Constructor içerisindeki InitAsync'in tamamlanması için ufak bir bekleme
        await Task.Delay(100);

        // 1. Filtre butonunun işlevselliği (LoadMovementsCommand)
        vm.SearchText = "Zorlayıcı Arama";
        vm.SelectedTypeIndex = 1; // Giriş
        await vm.LoadMovementsCommand.ExecuteAsync(null);

        vm.Movements.Should().HaveCount(1);
        vm.TotalPages.Should().Be(4); // 100 / 30 = 3.33 -> 4
        vm.StatusText.Should().Contain("100 hareketten");

        // 2. Sayfalama butonları test ediliyor
        await vm.NextPageCommand.ExecuteAsync(null);
        vm.CurrentPage.Should().Be(2);

        await vm.PrevPageCommand.ExecuteAsync(null);
        vm.CurrentPage.Should().Be(1);

        // 3. Temizle butonunun işlevselliği (ClearFiltersCommand)
        vm.ClearFiltersCommand.Execute(null);
        vm.SearchText.Should().BeEmpty();
        vm.SelectedTypeIndex.Should().Be(0);
        vm.CurrentPage.Should().Be(1);
    }
}
