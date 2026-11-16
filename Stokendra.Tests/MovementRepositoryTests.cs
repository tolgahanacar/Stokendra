using Xunit;
using Stokendra.Data;
using Stokendra.Data.Repositories;
using Stokendra.Models;
using Stokendra.Infrastructure;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class MovementRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _database;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MovementRepository _movementRepo;
    private readonly StockCardRepository _stockCardRepo;

    public MovementRepositoryTests()
    {
        DapperConfig.Initialize();
        LocalizationManager.Initialize("tr");

        _dbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_Movements_{Guid.NewGuid():N}.db");
        _database = new Database(_dbPath);
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _movementRepo = new MovementRepository(_connectionFactory);
        _stockCardRepo = new StockCardRepository(_connectionFactory);
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
    public async Task Test_Add_EntryMovement_IncreasesBalance()
    {
        var card = new StockCard { Code = "MOV001", Name = "Item 1", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        var movement = new StockMovement
        {
            StockCardId = card.Id,
            Type = "Entry",
            Quantity = 50,
            Date = DateTime.Now
        };
        await _movementRepo.AddAsync(movement);

        var updatedCard = await _stockCardRepo.GetByIdAsync(card.Id);
        Assert.NotNull(updatedCard);
        Assert.Equal(50, updatedCard.CurrentStock);
        Assert.Equal(50, updatedCard.TotalEntry);
        Assert.Equal(0, updatedCard.TotalExit);
    }

    [Fact]
    public async Task Test_ExitMovement_WhenStockInsufficient_ThrowsException()
    {
        var card = new StockCard { Code = "MOV002", Name = "Item 2", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        // Entry of 10
        await _movementRepo.AddAsync(new StockMovement
        {
            StockCardId = card.Id,
            Type = "Entry",
            Quantity = 10,
            Date = DateTime.Now
        });

        // Exit of 15 should throw
        var exit = new StockMovement
        {
            StockCardId = card.Id,
            Type = "Exit",
            Quantity = 15,
            Date = DateTime.Now
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _movementRepo.AddAsync(exit));
        Assert.Equal(LocalizationManager.L("stock_would_go_negative"), ex.Message);
    }

    [Fact]
    public async Task Test_ExitMovement_FloatingPointPrecision_Succeeds()
    {
        var card = new StockCard { Code = "MOV003", Name = "Item 3", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        // Add 0.1 and 0.2 (which in IEEE 754 produces 0.30000000000000004 or 0.29999999999999993)
        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 0.1, Date = DateTime.Now });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 0.2, Date = DateTime.Now });

        // Exit 0.3 should succeed with epsilon tolerance
        var exit = new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 0.3, Date = DateTime.Now };
        await _movementRepo.AddAsync(exit);

        var updatedCard = await _stockCardRepo.GetByIdAsync(card.Id);
        Assert.NotNull(updatedCard);
        Assert.True(Math.Abs(updatedCard.CurrentStock) < 1e-6);
    }

    [Fact]
    public async Task Test_Movement_OnParentCard_ThrowsException()
    {
        var parent = new StockCard { Code = "MOV004", Name = "Parent Item", CardType = "Parent" };
        await _stockCardRepo.AddAsync(parent);

        var movement = new StockMovement
        {
            StockCardId = parent.Id,
            Type = "Entry",
            Quantity = 10,
            Date = DateTime.Now
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _movementRepo.AddAsync(movement));
        Assert.Equal(LocalizationManager.L("movement_only_child_cards"), ex.Message);
    }

    [Fact]
    public async Task Test_DeleteMovement_WhenCausesNegativeStock_ThrowsException()
    {
        var card = new StockCard { Code = "MOV005", Name = "Item 5", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        var entry = new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 20, Date = DateTime.Now };
        await _movementRepo.AddAsync(entry);

        var exit = new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 15, Date = DateTime.Now };
        await _movementRepo.AddAsync(exit);

        // Deleting the entry should fail because remaining stock would be -15
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _movementRepo.DeleteAsync(entry.Id));
        Assert.Equal(LocalizationManager.L("stock_would_go_negative"), ex.Message);
    }

    [Fact]
    public async Task Test_BulkUpdate_WhenCausesNegativeStock_ThrowsException()
    {
        var card = new StockCard { Code = "MOV006", Name = "Item 6", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        var entry = new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 10, Date = DateTime.Now };
        await _movementRepo.AddAsync(entry);

        // Update entry quantity from 10 to 2
        entry.Quantity = 2;

        var exit = new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 5, Date = DateTime.Now };
        await _movementRepo.AddAsync(exit);

        // Now reducing entry to 2 would make balance: 2 - 5 = -3
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _movementRepo.UpdateBulkAsync(new[] { entry }));
        Assert.Contains(LocalizationManager.L("stock_would_go_negative"), ex.Message);
    }

    [Fact]
    public async Task Test_LegacyTurkishTypes_CalculatesBalanceCorrectly()
    {
        var card = new StockCard { Code = "MOV007", Name = "Item 7", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        // Insert legacy "Giris" and "Cikis" directly into SQLite
        using (var conn = _connectionFactory.CreateConnection())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO StockMovements (StockCardId, Type, Quantity, Date, Recipient, Department, Description)
                VALUES (@CardId, 'Giris', 100, datetime('now'), '', '', '');
                INSERT INTO StockMovements (StockCardId, Type, Quantity, Date, Recipient, Department, Description)
                VALUES (@CardId, 'Cikis', 30, datetime('now'), '', '', '');
            ";
            cmd.Parameters.AddWithValue("@CardId", card.Id);
            cmd.ExecuteNonQuery();
        }

        var updatedCard = await _stockCardRepo.GetByIdAsync(card.Id);
        Assert.NotNull(updatedCard);
        Assert.Equal(70, updatedCard.CurrentStock);
        Assert.Equal(100, updatedCard.TotalEntry);
        Assert.Equal(30, updatedCard.TotalExit);
    }
}
