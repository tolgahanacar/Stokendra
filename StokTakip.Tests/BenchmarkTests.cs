using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using StokTakip.Data;
using StokTakip.Data.Repositories;
using StokTakip.Models;
using Xunit;

namespace StokTakip.Tests;

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
            _repo.Add(new StokKarti { KodNo = $"K-{i}", Ad = $"Product {i}", KartTipi = "Alt" });
        }
    }

    [Benchmark]
    public List<StokKarti> GetAllCards()
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
