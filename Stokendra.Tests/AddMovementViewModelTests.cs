using Xunit;
using NSubstitute;
using Stokendra.ViewModels;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class AddMovementViewModelTests
{
    private readonly IStockCardRepository _stockCardsMock;
    private readonly IDepartmentRepository _departmentsMock;

    public AddMovementViewModelTests()
    {
        LocalizationManager.Initialize("tr");
        _stockCardsMock = Substitute.For<IStockCardRepository>();
        _departmentsMock = Substitute.For<IDepartmentRepository>();
    }

    private async Task<AddMovementViewModel> CreateViewModelAsync(List<StockCard> cards, List<string> depts, StockMovement? editingMovement = null)
    {
        _stockCardsMock.GetChildCardsAsync().Returns(Task.FromResult(cards));
        _departmentsMock.GetAllAsync().Returns(Task.FromResult(depts));

        var vm = new AddMovementViewModel(_stockCardsMock, _departmentsMock, editingMovement);
        
        // Wait for asynchronous InitAsync inside constructor
        // By calling a task yielding or delay, we wait for the asynchronous constructor task to run
        await Task.Delay(50); 
        return vm;
    }

    [Fact]
    public async Task Test_NewMovement_Save_Succeeds_WhenStockPositive()
    {
        // Arrange
        var cards = new List<StockCard>
        {
            new StockCard { Id = 1, Code = "001", Name = "Item 1", CardType = "Child", CurrentStock = 10 }
        };
        var depts = new List<string> { "IT" };
        var vm = await CreateViewModelAsync(cards, depts);

        vm.SelectedCard = vm.AllCards.First();
        vm.SelectedTypeIndex = 1; // Exit
        vm.Quantity = 5; // Exit 5 from 10
        vm.Department = "IT";

        bool closeCalled = false;
        vm.CloseAction = (result) => closeCalled = result;

        // Act
        // Invoke SaveCommand or call Save() directly via reflection/method
        // Since Save is private but there is SaveCommand, we can execute the command!
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.True(closeCalled);
        Assert.NotNull(vm.Result);
        Assert.Equal(5, vm.Result.Quantity);
        Assert.Equal("Exit", vm.Result.Type);
        Assert.Equal(1, vm.Result.StockCardId);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_NewMovement_Save_Fails_WhenStockGoesNegative()
    {
        // Arrange
        var cards = new List<StockCard>
        {
            new StockCard { Id = 1, Code = "001", Name = "Item 1", CardType = "Child", CurrentStock = 2 }
        };
        var depts = new List<string> { "IT" };
        var vm = await CreateViewModelAsync(cards, depts);

        vm.SelectedCard = vm.AllCards.First();
        vm.SelectedTypeIndex = 1; // Exit
        vm.Quantity = 5; // Exit 5 from 2 (Negative stock!)
        vm.Department = "IT";

        bool closeCalled = false;
        vm.CloseAction = (result) => closeCalled = result;

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.False(closeCalled);
        Assert.Null(vm.Result);
        Assert.Contains(LocalizationManager.L("stock_would_go_negative"), vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_EditMovement_CardSwap_Fails_WhenOriginalCardGoesNegative()
    {
        // Scenario: Card A currently has 5 stock.
        // It has an Entry movement of 10.
        // We edit this movement, and try to swap its card to Card B.
        // If we swap, Card A's stock without this movement becomes 5 - 10 = -5.
        // This is negative, so it must be blocked!

        // Arrange
        var cardA = new StockCard { Id = 1, Code = "A", Name = "Card A", CardType = "Child", CurrentStock = 5 };
        var cardB = new StockCard { Id = 2, Code = "B", Name = "Card B", CardType = "Child", CurrentStock = 10 };
        
        var editingMovement = new StockMovement
        {
            Id = 100,
            StockCardId = 1, // Originally Card A
            Type = "Entry",
            Quantity = 10,
            Date = DateTime.Now,
            Department = "IT",
            Recipient = "Staff"
        };

        var vm = await CreateViewModelAsync(new List<StockCard> { cardA, cardB }, new List<string> { "IT" }, editingMovement);

        // Swap SelectedCard to Card B (Id = 2)
        vm.SelectedCard = vm.AllCards.FirstOrDefault(c => c.Id == 2);
        vm.SelectedTypeIndex = 0; // Keeping it as Entry
        vm.Quantity = 10;

        bool closeCalled = false;
        vm.CloseAction = (result) => closeCalled = result;

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.False(closeCalled);
        Assert.Null(vm.Result);
        Assert.Contains(LocalizationManager.L("stock_would_go_negative"), vm.ErrorMessage);
        Assert.Contains("Card A", vm.ErrorMessage); // Error message specifies which card goes negative
    }

    [Fact]
    public async Task Test_EditMovement_CardSwap_Succeeds_WhenBothCardsStayPositive()
    {
        // Scenario: Card A currently has 10 stock.
        // It has an Exit movement of 5.
        // We swap this movement to Card B (which currently has 20 stock).
        // Card A's stock without this exit becomes 10 + 5 = 15 (Positive, OK).
        // Card B's stock with the new exit impact becomes 20 - 5 = 15 (Positive, OK).
        // Swapping should succeed!

        // Arrange
        var cardA = new StockCard { Id = 1, Code = "A", Name = "Card A", CardType = "Child", CurrentStock = 10 };
        var cardB = new StockCard { Id = 2, Code = "B", Name = "Card B", CardType = "Child", CurrentStock = 20 };
        
        var editingMovement = new StockMovement
        {
            Id = 101,
            StockCardId = 1, // Originally Card A
            Type = "Exit",
            Quantity = 5,
            Date = DateTime.Now,
            Department = "IT",
            Recipient = "Staff"
        };

        var vm = await CreateViewModelAsync(new List<StockCard> { cardA, cardB }, new List<string> { "IT" }, editingMovement);

        // Swap SelectedCard to Card B (Id = 2)
        vm.SelectedCard = vm.AllCards.FirstOrDefault(c => c.Id == 2);
        vm.SelectedTypeIndex = 1; // Exit
        vm.Quantity = 5;

        bool closeCalled = false;
        vm.CloseAction = (result) => closeCalled = result;

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.True(closeCalled);
        Assert.NotNull(vm.Result);
        Assert.Equal(2, vm.Result.StockCardId);
        Assert.Equal("Exit", vm.Result.Type);
        Assert.Equal(5, vm.Result.Quantity);
        Assert.Empty(vm.ErrorMessage);
    }
}
