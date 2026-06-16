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

public class DepartmentRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _database;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DepartmentRepository _repository;

    public DepartmentRepositoryTests()
    {
        // Initialize LocalizationManager for test strings
        LocalizationManager.Initialize("tr");

        _dbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_Depts_{Guid.NewGuid():N}.db");
        _database = new Database(_dbPath);
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _repository = new DepartmentRepository(_connectionFactory);
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
    public async Task Test_Add_And_Get_Departments_Succeeds()
    {
        // Act
        await _repository.AddAsync("IT");
        await _repository.AddAsync("Sales");

        var depts = await _repository.GetAllAsync();

        // Assert
        Assert.Contains("IT", depts);
        Assert.Contains("Sales", depts);
    }

    [Fact]
    public async Task Test_Delete_Department_Not_In_Use_Succeeds()
    {
        // Arrange
        await _repository.AddAsync("Finance");

        // Act
        await _repository.DeleteAsync("Finance");
        var depts = await _repository.GetAllAsync();

        // Assert
        Assert.DoesNotContain("Finance", depts);
    }

    [Fact]
    public async Task Test_Delete_Department_In_Use_ThrowsException()
    {
        // Arrange
        await _repository.AddAsync("IT");

        var cardRepo = new StockCardRepository(_connectionFactory);
        var card = new StockCard { Code = "CRD002", Name = "IT Asset", CardType = "Child" };
        cardRepo.Add(card);

        var movementRepo = new MovementRepository(_connectionFactory);
        var movement = new StockMovement
        {
            StockCardId = card.Id,
            Type = "Entry",
            Quantity = 5,
            Date = DateTime.Now,
            Department = "IT",
            Recipient = "IT Admin"
        };
        movementRepo.Add(movement);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.DeleteAsync("IT"));
        Assert.Equal(LocalizationManager.L("dept_in_use"), ex.Message);
    }
}
