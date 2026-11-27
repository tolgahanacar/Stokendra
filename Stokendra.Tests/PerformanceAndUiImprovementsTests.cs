using Xunit;
using NSubstitute;
using Dapper;
using Microsoft.Data.Sqlite;
using Stokendra.Converters;
using Stokendra.Data;
using Stokendra.Data.Interfaces;
using Stokendra.Data.Repositories;
using Stokendra.Infrastructure;
using Stokendra.Models;
using Stokendra.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class PerformanceAndUiImprovementsTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestSandbox _sandbox;
    private readonly Database _database;
    private readonly SqliteConnectionFactory _connectionFactory;

    public PerformanceAndUiImprovementsTests()
    {
        DapperConfig.Initialize();
        LocalizationManager.Initialize("tr");
        _sandbox = new TestSandbox();
        _dbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_PerfUi_{Guid.NewGuid():N}.db");
        _database = new Database(_dbPath);
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
    }

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
        _sandbox.Dispose();
    }

    [Fact]
    public async Task Test_ConfigRepository_SetConfigsAsync_PersistsBatchAtomically()
    {
        var repo = new ConfigRepository(_connectionFactory);
        await repo.SetConfigsAsync(new Dictionary<string, string>
        {
            { "CompanyName", "Stokendra A.S." },
            { "LowStockThreshold", "7" },
            { "MasterSecurityCode", "1951" }
        });

        Assert.Equal("Stokendra A.S.", repo.GetConfig("CompanyName"));
        Assert.Equal("7", repo.GetConfig("LowStockThreshold"));
        Assert.Equal("1951", repo.GetConfig("MasterSecurityCode"));
    }

    [Fact]
    public async Task Test_NoteRepository_UpdateBulkAsync_UpdatesAllRowsInSingleTransaction()
    {
        var repo = new NoteRepository(_connectionFactory);
        var n1 = new Note { Title = "Birinci Not", Content = "Icerik 1" };
        var n2 = new Note { Title = "Ikinci Not", Content = "Icerik 2" };
        await repo.AddAsync(n1);
        await repo.AddAsync(n2);

        n1.Title = "Guncel Birinci";
        n2.Content = "Guncel Icerik 2";
        await repo.UpdateBulkAsync(new[] { n1, n2 });

        var all = await repo.GetAllAsync();
        Assert.Contains(all, x => x.Id == n1.Id && x.Title == "Guncel Birinci");
        Assert.Contains(all, x => x.Id == n2.Id && x.Content == "Guncel Icerik 2");
    }

    [Fact]
    public async Task Test_ServiceRecordRepository_UpdateBulkAsync_UpdatesAllRows()
    {
        var repo = new ServiceRecordRepository(_connectionFactory);
        var today = DateTime.Today;
        var s1 = new ServiceRecord { DeviceName = "Yazici A", SerialNumber = "S1", Company = "Firma1", Issue = "Ariza 1", Result = "Bekliyor", ServiceDate = today };
        var s2 = new ServiceRecord { DeviceName = "Yazici B", SerialNumber = "S2", Company = "Firma1", Issue = "Ariza 2", Result = "Bekliyor", ServiceDate = today };
        await repo.AddAsync(s1);
        await repo.AddAsync(s2);

        s1.Result = "Onarildi";
        s2.Result = "Degistirildi";
        await repo.UpdateBulkAsync(new[] { s1, s2 });

        var all = await repo.GetAllAsync(today.AddDays(-1), today.AddDays(1), null);
        Assert.Equal("Onarildi", all.First(x => x.Id == s1.Id).Result);
        Assert.Equal("Degistirildi", all.First(x => x.Id == s2.Id).Result);
    }

    [Fact]
    public async Task Test_StockCardRepository_Lookups_NextCode_And_LowStock_ExcludeParents()
    {
        var cardsRepo = new StockCardRepository(_connectionFactory);
        var movRepo = new MovementRepository(_connectionFactory);

        var parent = new StockCard { Code = "P001", Name = "Ana Grup", CardType = "Parent" };
        var child = new StockCard { Code = "001", Name = "Toner", CardType = "Child", MinStock = 5 };
        await cardsRepo.AddAsync(parent);
        await cardsRepo.AddAsync(child);
        await movRepo.AddAsync(new StockMovement { StockCardId = child.Id, Type = "Entry", Quantity = 2, Department = "IT", Recipient = "Ali" });

        var lookups = await cardsRepo.GetCardLookupsAsync("Child");
        Assert.Single(lookups);
        Assert.Equal(child.Id, lookups[0].Id);

        var lowStock = await cardsRepo.GetLowStockCardsAsync(20, 3);
        Assert.Single(lowStock);
        Assert.Equal(child.Id, lowStock[0].Id);
        Assert.Equal(2, lowStock[0].CurrentStock);

        var nextCode = await cardsRepo.GetNextCodeAsync();
        Assert.False(string.IsNullOrWhiteSpace(nextCode));
    }

    [Fact]
    public async Task Test_MovementRepository_GetDeliveredPersonsAsync_And_TurkishSqlLike()
    {
        var cardsRepo = new StockCardRepository(_connectionFactory);
        var movRepo = new MovementRepository(_connectionFactory);
        var card = new StockCard { Code = "K01", Name = "Isik", CardType = "Child" };
        await cardsRepo.AddAsync(card);
        await movRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 10, Department = "Depo", Recipient = "Işık Yılmaz" });
        await movRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 2, Department = "Depo", Recipient = "Ahmet Demir" });

        var persons = await movRepo.GetDeliveredPersonsAsync();
        Assert.Contains("Ahmet Demir", persons);
        Assert.Contains("Işık Yılmaz", persons);

        using var conn = _connectionFactory.CreateConnection();
        int matchCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM StockMovements WHERE Recipient LIKE @Pattern",
            new { Pattern = "%ışık%" });
        Assert.Equal(1, matchCount);
    }

    [Fact]
    public void Test_Converters_HandleNullAndBooleanStates()
    {
        var safeDouble = new SafeDoubleConverter();
        Assert.Equal(0d, safeDouble.ConvertBack(null, typeof(double), null, CultureInfo.InvariantCulture));
        Assert.Equal(12.5d, safeDouble.ConvertBack(12.5m, typeof(double), null, CultureInfo.InvariantCulture));
        Assert.Equal(4m, safeDouble.Convert(4.0, typeof(decimal?), null, CultureInfo.InvariantCulture));

        var zeroBool = new ZeroToBoolConverter();
        Assert.Equal(true, zeroBool.Convert(0, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, zeroBool.Convert(3, typeof(bool), null, CultureInfo.InvariantCulture));

        var iconConv = new SuccessIconConverter();
        Assert.Equal("✔", iconConv.Convert(true, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("⚠", iconConv.Convert(false, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Test_PrintPreviewViewModel_TruncatesLargePreviewOnly()
    {
        string oversized = new string('A', 90000);
        var vm = new PrintPreviewViewModel("Rapor", oversized);
        Assert.True(vm.Content.Length < oversized.Length);
        Assert.Contains(LocalizationManager.L("print_preview_truncated"), vm.Content);
    }

    [Fact]
    public async Task Test_BulkMovementViewModel_WarnsWhenCheckedRowMissingQuantity()
    {
        var settings = new AppSettings { Language = "tr", DbPath = _dbPath };
        using var app = new AppServices(settings, _database);
        app.BeginSession("admin", "admin");

        var cardsMock = Substitute.For<IStockCardRepository>();
        cardsMock.GetChildCardsAsync().Returns(Task.FromResult(new List<StockCard>
        {
            new StockCard { Id = 1, Code = "001", Name = "Test Urun", CardType = "Child", CurrentStock = 10 }
        }));
        var deptsMock = Substitute.For<IDepartmentRepository>();
        deptsMock.GetAllAsync().Returns(Task.FromResult(new List<string> { "IT" }));
        var movMock = Substitute.For<IMovementRepository>();
        var dialogMock = Substitute.For<IDialogService>();

        var vm = new BulkMovementViewModel(movMock, deptsMock, cardsMock, dialogMock);
        await Task.Delay(50);

        vm.Items[0].IsSelected = true;
        vm.Items[0].Quantity = null;

        await vm.SaveCommand.ExecuteAsync(null);

        await dialogMock.Received(1).ShowMessageAsync(
            LocalizationManager.L("warning"),
            LocalizationManager.L("bulk_checked_missing_quantity", 1));
        await movMock.DidNotReceive().AddBulkAsync(Arg.Any<IEnumerable<StockMovement>>());
    }

    [Fact]
    public void Test_SafeCalendarDatePicker_InheritsCalendarDatePickerStyleKey()
    {
        var picker = new Stokendra.Views.SafeCalendarDatePicker();
        var styleKeyProp = typeof(Avalonia.StyledElement).GetProperty(
            "StyleKeyOverride",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(styleKeyProp);
        var styleKey = styleKeyProp!.GetValue(picker) as Type;
        Assert.Equal(typeof(Avalonia.Controls.CalendarDatePicker), styleKey);
    }

    [Fact]
    public void Test_NotesListBox_SupportsShiftRangeAndCtrlMultiSelect()
    {
        var notes = Enumerable.Range(1, 5)
            .Select(i => new Note { Id = i, Title = $"Not {i}" })
            .ToList();

        var listBox = new Avalonia.Controls.ListBox
        {
            ItemsSource = notes,
            SelectionMode = Avalonia.Controls.SelectionMode.Multiple
        };

        var updateMethod = typeof(Avalonia.Controls.Primitives.SelectingItemsControl).GetMethod(
            "UpdateSelection",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) },
            null);
        Assert.NotNull(updateMethod);

        // Plain click on index 1 -> only index 1 selected
        updateMethod!.Invoke(listBox, new object[] { 1, true, false, false, false, false });
        Assert.Single(listBox.SelectedItems!);

        // Shift+Click on index 4 -> range [1..4] selected (4 items)
        updateMethod.Invoke(listBox, new object[] { 4, true, true, false, false, false });
        Assert.Equal(4, listBox.SelectedItems!.Count);
        Assert.Equal(new[] { 2, 3, 4, 5 }, listBox.SelectedItems.OfType<Note>().Select(n => n.Id).OrderBy(x => x).ToArray());

        // Ctrl+Click on index 0 -> adds index 0 to selection (5 items total)
        updateMethod.Invoke(listBox, new object[] { 0, true, false, true, false, false });
        Assert.Equal(5, listBox.SelectedItems!.Count);

        // Plain click on index 2 -> collapses back to single item (index 2)
        updateMethod.Invoke(listBox, new object[] { 2, true, false, false, false, false });
        Assert.Single(listBox.SelectedItems!);
        Assert.Equal(3, listBox.SelectedItems!.OfType<Note>().First().Id);
    }
}
