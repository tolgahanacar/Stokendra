using Xunit;
using Stokendra.Data;
using Stokendra.Data.Repositories;
using Stokendra.Models;
using Stokendra.Infrastructure;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class StockCardRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _database;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly StockCardRepository _repository;

    public StockCardRepositoryTests()
    {
        // Initialize LocalizationManager for test strings
        LocalizationManager.Initialize("tr");

        _dbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_Cards_{Guid.NewGuid():N}.db");
        _database = new Database(_dbPath);
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _repository = new StockCardRepository(_connectionFactory);
    }

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public void Test_Add_And_Get_StockCard_Succeeds()
    {
        // Arrange
        var card = new StockCard
        {
            Code = "TST001",
            Name = "Test Card",
            CardType = "Child",
            Category = "Other",
            Unit = "Adet"
        };

        // Act
        _repository.Add(card);
        var retrieved = _repository.GetById(card.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("TST001", retrieved.Code);
        Assert.Equal("Test Card", retrieved.Name);
    }

    [Fact]
    public void Test_Add_DuplicateCode_ThrowsException()
    {
        // Arrange
        var card1 = new StockCard { Code = "DUP001", Name = "Card 1", CardType = "Child" };
        var card2 = new StockCard { Code = "DUP001", Name = "Card 2", CardType = "Child" };

        _repository.Add(card1);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _repository.Add(card2));
        Assert.Equal(LocalizationManager.L("stock_code_exists"), ex.Message);
    }

    [Fact]
    public void Test_SelfReference_ThrowsException()
    {
        // Arrange & Act
        var card = new StockCard { Code = "SELF01", Name = "Self Card", CardType = "Child" };
        _repository.Add(card);

        card.ParentId = card.Id; // Point to self

        // Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _repository.Update(card));
        Assert.Equal(LocalizationManager.L("parent_card_self"), ex.Message);
    }

    [Fact]
    public void Test_Delete_ParentCard_With_Children_ThrowsException()
    {
        // Arrange
        var parent = new StockCard { Code = "PRT001", Name = "Parent Card", CardType = "Parent" };
        _repository.Add(parent);

        var child = new StockCard { Code = "CHD001", Name = "Child Card", CardType = "Child", ParentId = parent.Id };
        _repository.Add(child);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _repository.Delete(parent.Id));
        Assert.Equal(LocalizationManager.L("parent_card_has_children"), ex.Message);
    }

    [Fact]
    public void Test_Delete_ParentCard_Without_Children_Succeeds()
    {
        // Arrange
        var parent = new StockCard { Code = "PRT002", Name = "Parent Card 2", CardType = "Parent" };
        _repository.Add(parent);

        // Act
        _repository.Delete(parent.Id);
        var retrieved = _repository.GetById(parent.Id);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void Test_Cycle_Detection_ThrowsException()
    {
        // Arrange
        var node1 = new StockCard { Code = "CYC001", Name = "Node 1", CardType = "Parent" };
        _repository.Add(node1);

        var node2 = new StockCard { Code = "CYC002", Name = "Node 2", CardType = "Parent", ParentId = node1.Id };
        _repository.Add(node2);

        // Attempting to make node 1 a child of node 2 (creates a loop node1 -> node2 -> node1)
        node1.ParentId = node2.Id;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _repository.Update(node1));
        Assert.Equal(LocalizationManager.L("parent_card_loop"), ex.Message);
    }

    [Fact]
    public void Test_ParentCard_WithMovements_ThrowsException()
    {
        // Arrange
        var card = new StockCard { Code = "MVT001", Name = "Movement Card", CardType = "Child" };
        _repository.Add(card);

        // Add a movement to this card
        var movementRepo = new MovementRepository(_connectionFactory);
        var movement = new StockMovement
        {
            StockCardId = card.Id,
            Type = "Entry",
            Quantity = 10,
            Date = DateTime.Now,
            Department = "IT",
            Recipient = "Staff"
        };
        movementRepo.Add(movement);

        // Now attempt to change the card type to Parent
        card.CardType = "Parent";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _repository.Update(card));
        Assert.Equal(LocalizationManager.L("parent_card_with_movements"), ex.Message);
    }

    [Fact]
    public async Task Test_GetLowStockCards_OnlyReturnsChildCards()
    {
        // Arrange: Create a parent card and a child card. Both have 0 movements, so stock is 0.
        var parent = new StockCard { Code = "PRT999", Name = "Parent Printer", CardType = "Parent" };
        await _repository.AddAsync(parent);

        var child = new StockCard { Code = "CHD999", Name = "Child Toner", CardType = "Child", ParentId = parent.Id };
        await _repository.AddAsync(child);

        // Act
        var lowStockCards = await _repository.GetLowStockCardsAsync(10, 3);

        // Assert
        Assert.Contains(lowStockCards, c => c.Code == "CHD999");
        Assert.DoesNotContain(lowStockCards, c => c.Code == "PRT999");
    }
}
