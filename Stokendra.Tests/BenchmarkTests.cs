using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Stokendra.Data;
using Stokendra.Data.Repositories;
using Stokendra.Models;
using Xunit;
using System.IO;
using System.Collections.Generic;

namespace Stokendra.Tests;

[MemoryDiagnoser]
public class RepositoryBenchmarks
{
    private string _dbPath;
    private StockCardRepository _repo;

    [GlobalSetup]
    public void Setup()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "benchmark.db");
        if (File.Exists(_dbPath)) File.Delete(_dbPath);

        var db = new Database(_dbPath);
        var factory = new TestDbFactory(_dbPath);
        _repo = new StockCardRepository(factory);

        // 1000 kart ekle
        for (int i = 0; i < 1000; i++)
        {
            _repo.Add(new StockCard { Code = $"K-{i}", Name = $"Product {i}", CardType = "Child" });
        }
    }

    [Benchmark]
    public List<StockCard> GetAllCards()
    {
        return _repo.GetAll();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

public class BenchmarkTests
{
    [Fact(Skip = "Benchmarks should be run manually via CLI")]
    public void RunBenchmarks()
    {
        BenchmarkRunner.Run<RepositoryBenchmarks>();
    }
}
