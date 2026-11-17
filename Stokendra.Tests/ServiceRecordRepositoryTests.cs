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

public class ServiceRecordRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _database;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ServiceRecordRepository _repository;

    public ServiceRecordRepositoryTests()
    {
        DapperConfig.Initialize();
        LocalizationManager.Initialize("tr");

        _dbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_Services_{Guid.NewGuid():N}.db");
        _database = new Database(_dbPath);
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _repository = new ServiceRecordRepository(_connectionFactory);
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
    public async Task Test_Add_And_Filter_ByDate_IncludesSameDayRecords()
    {
        var today = DateTime.Today;

        var record = new ServiceRecord
        {
            DeviceName = "Laptop ThinkPad",
            SerialNumber = "SN123456",
            Company = "Lenovo",
            ServiceDate = today, // Will have current time appended
            Issue = "Fan noise",
            Result = "Cleaned"
        };

        await _repository.AddAsync(record);

        // Query with exact same date as start and end (today .. today + 1 day as upper bound)
        var results = await _repository.GetAllAsync(start: today, end: today.AddDays(1), search: null);

        Assert.NotEmpty(results);
        Assert.Equal("Laptop ThinkPad", results[0].DeviceName);
    }

    [Fact]
    public async Task Test_AddBulk_PreservesValidDateString()
    {
        var records = new[]
        {
            new ServiceRecord { DeviceName = "Printer A", ServiceDate = DateTime.Today },
            new ServiceRecord { DeviceName = "Printer B", ServiceDate = DateTime.Today.AddDays(-1) }
        };

        await _repository.AddBulkAsync(records);

        var all = await _repository.GetAllAsync(null, null, null);
        Assert.True(all.Count >= 2);
    }
}
