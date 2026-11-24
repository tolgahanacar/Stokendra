using System;
using System.IO;
using Xunit;
using Stokendra.Data;
using Stokendra.ViewModels;

namespace Stokendra.Tests;

public class GuideViewModelTests : IClassFixture<TestSandbox>, IDisposable
{
    private readonly string _tempDbPath;
    private readonly AppSettings _settings;
    private readonly Database _database;

    public GuideViewModelTests()
    {
        LocalizationManager.Initialize("tr");
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"GuideTest_{Guid.NewGuid():N}.db");
        _settings = new AppSettings();
        _database = new Database(_tempDbPath);
    }

    public void Dispose()
    {
        _database.Dispose();
        try
        {
            if (File.Exists(_tempDbPath))
                File.Delete(_tempDbPath);
        }
        catch { }
    }

    [Fact]
    public void Test_GuideViewModel_Initializes_WithCardsAndDefaultState()
    {
        var vm = new GuideViewModel(_settings, _database);

        Assert.NotEmpty(vm.FilteredUserCards);
        Assert.NotEmpty(vm.FilteredDevCards);
        Assert.True(vm.HasUserResults);
        Assert.True(vm.HasDevResults);
        Assert.Equal(0, vm.SelectedTab);
        Assert.NotEmpty(vm.AppVersion);
    }

    [Fact]
    public void Test_GuideViewModel_Search_FiltersCardsCorrectly()
    {
        var vm = new GuideViewModel(_settings, _database);
        int initialUserCount = vm.FilteredUserCards.Count;

        vm.SearchText = "Excel";

        Assert.True(vm.FilteredUserCards.Count < initialUserCount);
        Assert.NotEmpty(vm.FilteredUserCards);
        Assert.All(vm.FilteredUserCards, card =>
            Assert.True(
                card.Title.Contains("Excel", System.StringComparison.OrdinalIgnoreCase) ||
                card.Content.Contains("Excel", System.StringComparison.OrdinalIgnoreCase) ||
                card.Tag.Contains("Excel", System.StringComparison.OrdinalIgnoreCase)
            ));
    }

    [Fact]
    public void Test_GuideViewModel_ClearSearch_RestoresFullList()
    {
        var vm = new GuideViewModel(_settings, _database);
        int initialCount = vm.FilteredUserCards.Count;

        vm.SearchText = "Excel";
        Assert.True(vm.FilteredUserCards.Count < initialCount);

        vm.ClearSearchCommand.Execute(null);

        Assert.Equal("", vm.SearchText);
        Assert.Equal(initialCount, vm.FilteredUserCards.Count);
        Assert.True(vm.HasUserResults);
    }

    [Fact]
    public void Test_GuideViewModel_NonMatchingQuery_SetsHasResultsFalse()
    {
        var vm = new GuideViewModel(_settings, _database);

        vm.SearchText = "xyz_non_existent_search_query_999";

        Assert.Empty(vm.FilteredUserCards);
        Assert.False(vm.HasUserResults);
        Assert.Empty(vm.FilteredDevCards);
        Assert.False(vm.HasDevResults);
    }
}
