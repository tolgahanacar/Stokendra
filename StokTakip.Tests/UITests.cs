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
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Models.StokKarti> { 
            new Models.StokKarti { Ad = "Mock Card" } 
        });

        var vm = new StockCardsViewModel(mockRepo.Object, MockDialog.Object, MockLogger.Object);
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.Cards.Should().HaveCount(1);
        vm.Cards[0].Ad.Should().Be("Mock Card");
    }
}
