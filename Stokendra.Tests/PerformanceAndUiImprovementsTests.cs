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
using Stokendra.Services;
using Stokendra.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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

    [Fact]
    public async Task Test_MovementRepository_GetConsumptionSummaryAsync_And_Last30DaysTotals()
    {
        var cardsRepo = new StockCardRepository(_connectionFactory);
        var movRepo = new MovementRepository(_connectionFactory);

        var c1 = new StockCard { Code = "T01", Name = "Toner A", CardType = "Child" };
        var c2 = new StockCard { Code = "T02", Name = "Toner B", CardType = "Child" };
        await cardsRepo.AddAsync(c1);
        await cardsRepo.AddAsync(c2);

        DateTime now = DateTime.Now;
        await movRepo.AddAsync(new StockMovement { StockCardId = c1.Id, Type = "Entry", Quantity = 20, Date = now.AddDays(-10), Department = "Depo", Recipient = "Ali" });
        await movRepo.AddAsync(new StockMovement { StockCardId = c1.Id, Type = "Entry", Quantity = 50, Date = now.AddDays(-45), Department = "Depo", Recipient = "Ali" });
        await movRepo.AddAsync(new StockMovement { StockCardId = c2.Id, Type = "Entry", Quantity = 10, Date = now.AddDays(-15), Department = "Depo", Recipient = "Ali" });
        await movRepo.AddAsync(new StockMovement { StockCardId = c1.Id, Type = "Exit", Quantity = 6, Date = now.AddDays(-5), Department = "IT", Recipient = "Veli" });
        await movRepo.AddAsync(new StockMovement { StockCardId = c1.Id, Type = "Exit", Quantity = 4, Date = now.AddDays(-2), Department = "IK", Recipient = "Ayşe" });
        await movRepo.AddAsync(new StockMovement { StockCardId = c2.Id, Type = "Exit", Quantity = 3, Date = now.AddDays(-1), Department = "IT", Recipient = "Can" });

        var summaryAll = await movRepo.GetConsumptionSummaryAsync(
            startDate: now.AddDays(-20),
            endDate: now.AddDays(1),
            movementType: "Exit",
            fallbackLabel: "Tümü");

        Assert.Equal(13, summaryAll.TotalConsumption);
        Assert.Equal(2, summaryAll.UniqueItemCount);
        Assert.Equal(3, summaryAll.TotalRowCount);
        Assert.Equal(2, summaryAll.ChartData.Count);
        Assert.Equal("T01 - Toner A", summaryAll.ChartData[0].Name);
        Assert.Equal(10, summaryAll.ChartData[0].Total);

        var summaryByCard = await movRepo.GetConsumptionSummaryAsync(
            stockCardId: c1.Id,
            startDate: now.AddDays(-20),
            endDate: now.AddDays(1),
            movementType: "Exit",
            fallbackLabel: "Tümü");

        Assert.Equal(10, summaryByCard.TotalConsumption);
        Assert.Equal(1, summaryByCard.UniqueItemCount);
        Assert.Equal(2, summaryByCard.TotalRowCount);
        Assert.Equal("IT", summaryByCard.ChartData[0].Name);
        Assert.Equal(6, summaryByCard.ChartData[0].Total);

        var (entry30, exit30) = await movRepo.GetCardLast30DaysTotalsAsync(c1.Id);
        Assert.Equal(20, entry30);
        Assert.Equal(10, exit30);
    }

    [Fact]
    public async Task Test_StockCardRepository_GetChildStockSummaryAsync_CountsAccurately()
    {
        var cardsRepo = new StockCardRepository(_connectionFactory);
        var movRepo = new MovementRepository(_connectionFactory);

        var parent = new StockCard { Code = "P1", Name = "Grup", CardType = "Parent" };
        var depleted = new StockCard { Code = "C1", Name = "Kalem", CardType = "Child", MinStock = 5 };
        var low = new StockCard { Code = "C2", Name = "Kagit", CardType = "Child", MinStock = 10 };
        var healthy = new StockCard { Code = "C3", Name = "Dosya", CardType = "Child", MinStock = 2 };

        await cardsRepo.AddAsync(parent);
        await cardsRepo.AddAsync(depleted);
        await cardsRepo.AddAsync(low);
        await cardsRepo.AddAsync(healthy);

        await movRepo.AddAsync(new StockMovement { StockCardId = low.Id, Type = "Entry", Quantity = 4 });
        await movRepo.AddAsync(new StockMovement { StockCardId = healthy.Id, Type = "Entry", Quantity = 25 });

        var summary = await cardsRepo.GetChildStockSummaryAsync(null, fallbackThreshold: 3);
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(1, summary.DepletedCount);

        var filtered = await cardsRepo.GetChildStockSummaryAsync("Kagit", fallbackThreshold: 3);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(1, filtered.LowStockCount);
        Assert.Equal(0, filtered.DepletedCount);
    }

    [Fact]
    public async Task Test_NoteRepository_GetPagedAsync_And_GetCountAsync_ReturnsDistinctPages()
    {
        var repo = new NoteRepository(_connectionFactory);
        for (int i = 1; i <= 65; i++)
        {
            await repo.AddAsync(new Note { Title = $"Not {i:D2}", Content = $"Icerik {i}" });
        }

        Assert.Equal(65, await repo.GetCountAsync());

        var page1 = await repo.GetPagedAsync(1, 50);
        var page2 = await repo.GetPagedAsync(2, 50);

        Assert.Equal(50, page1.Count);
        Assert.Equal(15, page2.Count);
        Assert.Empty(page1.Select(x => x.Id).Intersect(page2.Select(x => x.Id)));
    }

    [Fact]
    public void Test_ExcelService_ExportStreaming_ProducesValidWorkbook()
    {
        string xlsxPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Stream_{Guid.NewGuid():N}.xlsx");
        try
        {
            var rows = Enumerable.Range(1, 150).Select(i => new
            {
                Code = $"KOD-{i:D3}",
                Name = i == 1 ? "Türkçe ŞĞÜÖÇİ ı" : $"Ürün {i}",
                Qty = i,
                Price = i * 1.5,
                Note = i == 1 ? "Satir\u0001Temiz" : (string?)null
            }).ToList();

            ExcelService.ExportStreaming(
                xlsxPath,
                "TestSayfa",
                new[] { "Kod", "Ad", "Miktar", "Fiyat", "Not" },
                rows,
                r => new object?[] { r.Code, r.Name, r.Qty, r.Price, r.Note });

            Assert.True(File.Exists(xlsxPath));
            using var wb = new ClosedXML.Excel.XLWorkbook(xlsxPath);
            var ws = wb.Worksheet("TestSayfa");
            Assert.Equal(151, ws.RowsUsed().Count());
            Assert.Equal("Kod", ws.Cell(1, 1).GetString());
            Assert.Equal("Türkçe ŞĞÜÖÇİ ı", ws.Cell(2, 2).GetString());
            Assert.Equal(1, ws.Cell(2, 3).GetDouble());
            Assert.Equal(1.5, ws.Cell(2, 4).GetDouble());
            Assert.Equal("SatirTemiz", ws.Cell(2, 5).GetString());
        }
        finally
        {
            if (File.Exists(xlsxPath)) try { File.Delete(xlsxPath); } catch { }
        }
    }

    [Fact]
    public async Task Test_BackupService_PerformBackupAsync_ProducesReadableZipAndExcels()
    {
        var cardsRepo = new StockCardRepository(_connectionFactory);
        var movRepo = new MovementRepository(_connectionFactory);
        var notesRepo = new NoteRepository(_connectionFactory);
        var card = new StockCard { Code = "B01", Name = "Yedek Ürün", CardType = "Child" };
        await cardsRepo.AddAsync(card);
        await movRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 7, Department = "IT", Recipient = "Admin" });
        await notesRepo.AddAsync(new Note { Title = "Yedek Not", Content = "Test icerigi" });

        string backupDir = Path.Combine(Path.GetTempPath(), $"Stokendra_BkpDir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupDir);
        try
        {
            var settings = new AppSettings { Language = "tr", DbPath = _dbPath };
            var backupSvc = new BackupService(
                settings,
                new ConfigRepository(_connectionFactory),
                new ReportRepository(_connectionFactory),
                cardsRepo,
                movRepo,
                new ServiceRecordRepository(_connectionFactory),
                notesRepo,
                new DepartmentRepository(_connectionFactory),
                _connectionFactory);

            string zipPath = await backupSvc.PerformBackupAsync(backupDir);
            Assert.True(File.Exists(zipPath));

            using var archive = ZipFile.OpenRead(zipPath);
            Assert.Contains(archive.Entries, e => e.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));

            var notesEntry = archive.Entries.FirstOrDefault(e => e.Name.Contains("Notlar", StringComparison.OrdinalIgnoreCase) || e.Name.Contains("Notes", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(notesEntry);

            using var ms = new MemoryStream();
            using (var es = notesEntry!.Open())
                await es.CopyToAsync(ms);
            ms.Position = 0;

            using var wb = new ClosedXML.Excel.XLWorkbook(ms);
            Assert.True(wb.Worksheets.First().RowsUsed().Count() >= 2);
        }
        finally
        {
            if (Directory.Exists(backupDir)) try { Directory.Delete(backupDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Test_ViewModels_ServerSidePagination_NavigatesPagesCorrectly()
    {
        var settings = new AppSettings { Language = "tr", DbPath = _dbPath, LowStockThreshold = 3 };
        using var app = new AppServices(settings, _database);
        app.BeginSession("admin", "admin");

        var cardsMock = Substitute.For<IStockCardRepository>();
        var movMock = Substitute.For<IMovementRepository>();
        var deptMock = Substitute.For<IDepartmentRepository>();
        var notesMock = Substitute.For<INoteRepository>();
        var dialogMock = Substitute.For<IDialogService>();

        // StockCardsViewModel pagination (75 items, page size 50)
        cardsMock.GetCountAsync(null, null, Arg.Any<System.Threading.CancellationToken>()).Returns(Task.FromResult(75));
        cardsMock.GetPagedAsync(1, 50, null, null, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(1, 50).Select(i => new StockCard { Id = i, Code = $"K{i}" }).ToList()));
        cardsMock.GetPagedAsync(2, 50, null, null, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(51, 25).Select(i => new StockCard { Id = i, Code = $"K{i}" }).ToList()));

        var cardsVm = new StockCardsViewModel(cardsMock, movMock, deptMock, dialogMock);
        await cardsVm.LoadAsync();
        Assert.Equal(2, cardsVm.TotalPages);
        Assert.Equal(50, cardsVm.Cards.Count);
        Assert.True(cardsVm.CanNextPage);

        await cardsVm.NextPageAsync();
        Assert.Equal(2, cardsVm.CurrentPage);
        Assert.Equal(25, cardsVm.Cards.Count);
        Assert.False(cardsVm.CanNextPage);

        // StocksViewModel pagination (75 child cards, page size 50)
        cardsMock.GetChildStockSummaryAsync(null, 3, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult((TotalCount: 75, LowStockCount: 4, DepletedCount: 2)));
        cardsMock.GetPagedAsync(1, 50, null, "Child", Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(1, 50).Select(i => new StockCard { Id = i, Code = $"S{i}", CardType = "Child" }).ToList()));
        cardsMock.GetPagedAsync(2, 50, null, "Child", Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(51, 25).Select(i => new StockCard { Id = i, Code = $"S{i}", CardType = "Child" }).ToList()));

        var stocksVm = new StocksViewModel(cardsMock, dialogMock);
        await stocksVm.LoadAsync();
        Assert.Equal(2, stocksVm.TotalPages);
        Assert.Equal(50, stocksVm.Stocks.Count);
        await stocksVm.NextPageAsync();
        Assert.Equal(2, stocksVm.CurrentPage);
        Assert.Equal(25, stocksVm.Stocks.Count);

        // NotesViewModel pagination (75 notes, page size 50)
        notesMock.GetCountAsync(Arg.Any<System.Threading.CancellationToken>()).Returns(Task.FromResult(75));
        notesMock.GetPagedAsync(1, 50, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(1, 50).Select(i => new Note { Id = i, Title = $"N{i}" }).ToList()));
        notesMock.GetPagedAsync(2, 50, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(51, 25).Select(i => new Note { Id = i, Title = $"N{i}" }).ToList()));

        var notesVm = new NotesViewModel(notesMock, dialogMock);
        await notesVm.LoadAsync();
        Assert.Equal(2, notesVm.TotalPages);
        Assert.Equal(50, notesVm.Notes.Count);
        await notesVm.NextPageAsync();
        Assert.Equal(25, notesVm.Notes.Count);

        // StockCardDetailViewModel pagination (75 movements, page size 50)
        cardsMock.GetByIdAsync(10).Returns(Task.FromResult<StockCard?>(new StockCard { Id = 10, Code = "D10", Name = "Detay" }));
        movMock.GetCountAsync(stockCardId: 10, startDate: null, endDate: null, department: null, movementType: null, category: null, searchTerm: null, recipient: null, cancellationToken: Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(75));
        movMock.GetPagedAsync(1, 50, stockCardId: 10, startDate: null, endDate: null, department: null, movementType: null, category: null, searchTerm: null, recipient: null, cancellationToken: Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(1, 50).Select(i => new StockMovement { Id = i, StockCardId = 10 }).ToList()));
        movMock.GetPagedAsync(2, 50, stockCardId: 10, startDate: null, endDate: null, department: null, movementType: null, category: null, searchTerm: null, recipient: null, cancellationToken: Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(51, 25).Select(i => new StockMovement { Id = i, StockCardId = 10 }).ToList()));
        movMock.GetCardLast30DaysTotalsAsync(10, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult((12.0, 5.0)));

        var detailVm = new StockCardDetailViewModel(10, cardsMock, movMock, deptMock, dialogMock);
        await detailVm.LoadDataAsync();
        Assert.Equal(2, detailVm.TotalPages);
        Assert.Equal(50, detailVm.Movements.Count);
        Assert.Equal(12.0, detailVm.TotalEntryLast30);
        Assert.Equal(5.0, detailVm.TotalExitLast30);
        await detailVm.NextPageAsync();
        Assert.Equal(2, detailVm.CurrentPage);
        Assert.Equal(25, detailVm.Movements.Count);

        // ReportsViewModel pagination (150 items, page size 100)
        var repMovMock = Substitute.For<IMovementRepository>();
        repMovMock.GetConsumptionSummaryAsync(
            stockCardId: null,
            startDate: Arg.Any<DateTime?>(),
            endDate: Arg.Any<DateTime?>(),
            department: Arg.Any<string?>(),
            movementType: "Exit",
            category: Arg.Any<string?>(),
            searchTerm: Arg.Is<string?>(s => s == null),
            recipient: Arg.Any<string?>(),
            fallbackLabel: Arg.Any<string>(),
            topN: 10,
            cancellationToken: Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(new ConsumptionReportSummary(300, 3, 150, new List<(string Name, double Total)> { ("Genel", 300) })));

        repMovMock.GetPagedAsync(
            1, 100,
            stockCardId: null,
            startDate: Arg.Any<DateTime?>(),
            endDate: Arg.Any<DateTime?>(),
            department: Arg.Any<string?>(),
            movementType: "Exit",
            category: Arg.Any<string?>(),
            searchTerm: Arg.Is<string?>(s => s == null),
            recipient: Arg.Any<string?>(),
            cancellationToken: Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(1, 100).Select(i => new StockMovement { Id = i, Quantity = 2 }).ToList()));

        repMovMock.GetPagedAsync(
            2, 100,
            stockCardId: null,
            startDate: Arg.Any<DateTime?>(),
            endDate: Arg.Any<DateTime?>(),
            department: Arg.Any<string?>(),
            movementType: "Exit",
            category: Arg.Any<string?>(),
            searchTerm: Arg.Is<string?>(s => s == null),
            recipient: Arg.Any<string?>(),
            cancellationToken: Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Range(101, 50).Select(i => new StockMovement { Id = i, Quantity = 2 }).ToList()));

        var reportsVm = new ReportsViewModel(repMovMock, deptMock, cardsMock, dialogMock);
        await reportsVm.GenerateReportCommand.ExecuteAsync(null);
        Assert.Equal(2, reportsVm.TotalPages);
        Assert.Equal(100, reportsVm.ReportRows.Count);
        Assert.Equal(300, reportsVm.TotalConsumption);
        Assert.True(reportsVm.CanNextPage);

        await reportsVm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, reportsVm.CurrentPage);
        Assert.Equal(50, reportsVm.ReportRows.Count);
        Assert.Equal(300, reportsVm.TotalConsumption);
        Assert.False(reportsVm.CanNextPage);
    }
}
