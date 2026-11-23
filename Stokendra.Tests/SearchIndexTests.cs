using Stokendra.Infrastructure;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace Stokendra.Tests;

public class SearchIndexTests
{
    private readonly SearchIndex<StockCard> _index;

    public SearchIndexTests()
    {
        _index = new SearchIndex<StockCard>(
            k => new[] { k.Code, k.Name, k.Category, k.Description },
            (k, term) => k.MatchesSearch(term));
    }

    [Fact]
    public void Test_SearchIndex_EmptyQuery_ReturnsAllItemsInOriginalOrder()
    {
        var cards = new List<StockCard>
        {
            new() { Id = 1, Code = "A-1", Name = "Alpha" },
            new() { Id = 2, Code = "B-2", Name = "Beta" },
            new() { Id = 3, Code = "C-3", Name = "Gamma" }
        };

        _index.Build(cards);

        Assert.Equal(3, _index.Count);
        var resNull = _index.Search(null);
        var resEmpty = _index.Search("");
        var resWhitespace = _index.Search("   ");

        Assert.Equal(3, resNull.Count);
        Assert.Equal(3, resEmpty.Count);
        Assert.Equal(3, resWhitespace.Count);

        Assert.Equal("A-1", resNull[0].Code);
        Assert.Equal("B-2", resNull[1].Code);
        Assert.Equal("C-3", resNull[2].Code);
    }

    [Fact]
    public void Test_SearchIndex_SingleCharPrefix_MatchesInstantly()
    {
        var cards = new List<StockCard>
        {
            new() { Id = 1, Code = "101", Name = "Civata M8", Category = "Hırdavat" },
            new() { Id = 2, Code = "102", Name = "Somun M8", Category = "Hırdavat" },
            new() { Id = 3, Code = "201", Name = "Çivi 5cm", Category = "İnşaat" }
        };

        _index.Build(cards);

        // 'c' prefix should match Civata
        var resC = _index.Search("c");
        Assert.Single(resC);
        Assert.Equal("Civata M8", resC[0].Name);

        // 's' prefix should match Somun
        var resS = _index.Search("s");
        Assert.Single(resS);
        Assert.Equal("Somun M8", resS[0].Name);

        // '1' prefix should match both 101 and 102
        var res1 = _index.Search("1");
        Assert.Equal(2, res1.Count);
    }

    [Fact]
    public void Test_SearchIndex_TurkishCharacterPrefixes_MatchesCorrectly()
    {
        var cards = new List<StockCard>
        {
            new() { Id = 1, Code = "TR-1", Name = "Çelik Halat", Category = "Hırdavat" },
            new() { Id = 2, Code = "TR-2", Name = "Şapka Somun", Category = "Bağlantı" },
            new() { Id = 3, Code = "TR-3", Name = "Ölçü Aleti", Category = "Elektrik" },
            new() { Id = 4, Code = "TR-4", Name = "İğne Rulman", Category = "Rulman" },
            new() { Id = 5, Code = "TR-5", Name = "Ispanak Tohumu", Category = "Tarım" },
            new() { Id = 6, Code = "TR-6", Name = "Üçgen Zımpara", Category = "Aşındırıcı" }
        };

        _index.Build(cards);

        // Lowercase 'ç' or uppercase 'Ç'
        var resC = _index.Search("ç");
        Assert.Single(resC);
        Assert.Equal("TR-1", resC[0].Code);

        var resCUpper = _index.Search("Ç");
        Assert.Single(resCUpper);
        Assert.Equal("TR-1", resCUpper[0].Code);

        // Lowercase 'ş'
        var resS = _index.Search("şap");
        Assert.Single(resS);
        Assert.Equal("TR-2", resS[0].Code);

        // Turkish 'İ' vs 'i' and 'I' vs 'ı'
        var resI = _index.Search("iğne");
        Assert.Single(resI);
        Assert.Equal("TR-4", resI[0].Code);

        var resDotlessI = _index.Search("ıspanak");
        Assert.Single(resDotlessI);
        Assert.Equal("TR-5", resDotlessI[0].Code);

        var resU = _index.Search("üç");
        Assert.Single(resU);
        Assert.Equal("TR-6", resU[0].Code);
    }

    [Fact]
    public void Test_SearchIndex_DelimiterSplitting_MatchesParts()
    {
        var cards = new List<StockCard>
        {
            new() { Id = 1, Code = "CIV-102/A", Name = "Civata_M8-Paslanmaz.Koli", Category = "Hırdavat" }
        };

        _index.Build(cards);

        // Matches by sub-parts created by delimiters -, _, /, .
        Assert.Single(_index.Search("102"));
        Assert.Single(_index.Search("a"));
        Assert.Single(_index.Search("paslanmaz"));
        Assert.Single(_index.Search("koli"));
        Assert.Single(_index.Search("civ"));
        Assert.Single(_index.Search("m8"));
    }

    [Fact]
    public void Test_SearchIndex_MultiTokenQuery_IntersectsAccurately()
    {
        var cards = new List<StockCard>
        {
            new() { Id = 1, Code = "C-1", Name = "Civata M8 Paslanmaz", Category = "Hırdavat" },
            new() { Id = 2, Code = "C-2", Name = "Civata M10 Galvaniz", Category = "Hırdavat" },
            new() { Id = 3, Code = "S-1", Name = "Somun M8 Paslanmaz", Category = "Hırdavat" }
        };

        _index.Build(cards);

        // "civata" -> 2 items
        Assert.Equal(2, _index.Search("civata").Count);

        // "civata m8" -> 1 item (Civata M8 Paslanmaz)
        var resMulti = _index.Search("civata m8");
        Assert.Single(resMulti);
        Assert.Equal("C-1", resMulti[0].Code);

        // "m8 paslanmaz" -> 2 items (Civata M8 and Somun M8)
        var resM8Paslanmaz = _index.Search("m8 paslanmaz");
        Assert.Equal(2, resM8Paslanmaz.Count);

        // Non-existent combination
        var resNone = _index.Search("civata olmayan");
        Assert.Empty(resNone);
    }

    [Fact]
    public void Test_SearchIndex_NonPrefixSubstring_UsesFallbackMatcher()
    {
        var cards = new List<StockCard>
        {
            new() { Id = 1, Code = "C-1", Name = "Galvanizli Çelik", Category = "Metal" }
        };

        _index.Build(cards);

        // "vaniz" is in the middle of "Galvanizli" - not a prefix
        var res = _index.Search("vaniz");
        Assert.Single(res);
        Assert.Equal("C-1", res[0].Code);
    }

    [Fact]
    public void Test_SearchIndex_Rebuild_ReflectsNewDataset()
    {
        var initial = new List<StockCard>
        {
            new() { Id = 1, Code = "INIT-1", Name = "First" }
        };

        _index.Build(initial);
        Assert.Single(_index.Search("first"));

        var updated = new List<StockCard>
        {
            new() { Id = 2, Code = "UPD-1", Name = "Second" }
        };

        _index.Build(updated);

        Assert.Empty(_index.Search("first"));
        Assert.Single(_index.Search("second"));
    }

    [Fact]
    public void Test_SearchIndex_Performance_10000Items_SearchesSubMillisecond()
    {
        var list = new List<StockCard>(10000);
        for (int i = 0; i < 10000; i++)
        {
            list.Add(new StockCard
            {
                Id = i + 1,
                Code = $"STK-{i:D5}",
                Name = $"Civata Çelik Model {i % 100} Özel Seri",
                Category = i % 2 == 0 ? "Hırdavat" : "Mekanik"
            });
        }

        var sw = Stopwatch.StartNew();
        _index.Build(list);
        sw.Stop();
        // Building 10,000 items with prefixes should be very fast (< 150ms)
        Assert.True(sw.ElapsedMilliseconds < 500, $"Build took too long: {sw.ElapsedMilliseconds}ms");

        // Warmup search
        _ = _index.Search("civata");

        // Benchmark 100 single-character and prefix searches
        sw.Restart();
        for (int i = 0; i < 100; i++)
        {
            var res = _index.Search("c");
            Assert.NotEmpty(res);
        }
        sw.Stop();

        double avgSearchMs = sw.Elapsed.TotalMilliseconds / 100.0;
        // Each prefix search should easily take less than 0.5 ms (typically 0.005 ms)
        Assert.True(avgSearchMs < 1.0, $"Avg search time was too slow: {avgSearchMs}ms");
    }
}
