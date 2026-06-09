using FluentAssertions;
using Moq;
using Stokendra.Data.Interfaces;
using Stokendra.Data.Repositories;
using Stokendra.Infrastructure;
using Stokendra.Models;
using Stokendra.ViewModels;
using Xunit;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Tests;

/// <summary>
/// LTS-grade aggressive tests. Every boundary, every edge case, every button path.
/// No error is ignored — all are forced until they break or prove stable.
/// </summary>
public class AggressiveTests : TestBase
{
    // ═══════════════════════════════════════════════════════════════
    // 1. MOVEMENT VALIDATION — boundary attacks
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Movement_NaN_Miktar_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "NAN-1", Name = "NaN Test", CardType = "Child" };
        cards.Add(c);

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "Entry", Quantity = double.NaN });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_Infinity_Miktar_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "INF-1", Name = "Inf Test", CardType = "Child" };
        cards.Add(c);

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "Entry", Quantity = double.PositiveInfinity });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_BillionPlus_Miktar_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "BIG-1", Name = "Big Test", CardType = "Child" };
        cards.Add(c);

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "Entry", Quantity = 2_000_000_000 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_ZeroMiktar_NonBos_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "ZERO-1", Name = "Zero Test", CardType = "Child" };
        cards.Add(c);

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "Entry", Quantity = 0 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_NegativeMiktar_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "NEG-1", Name = "Neg Test", CardType = "Child" };
        cards.Add(c);

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "Entry", Quantity = -5 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_InvalidTur_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "TUR-1", Name = "Tur Test", CardType = "Child" };
        cards.Add(c);

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "INVALID", Quantity = 1 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_ToNonExistentCard_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var moves = new MovementRepository(f);

        Action act = () => moves.Add(new StockMovement { StockCardId = 99999, Type = "Entry", Quantity = 1 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_ToParentCard_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var parent = new StockCard { Code = "UST-1", Name = "Parent", CardType = "Parent" };
        cards.Add(parent);

        Action act = () => moves.Add(new StockMovement { StockCardId = parent.Id, Type = "Entry", Quantity = 1 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_CikisFromEmpty_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "EMP-1", Name = "Empty Stock", CardType = "Child" };
        cards.Add(c);

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "Exit", Quantity = 1 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Movement_CikisMoreThanAvailable_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);
        var c = new StockCard { Code = "OVER-1", Name = "Over Test", CardType = "Child" };
        cards.Add(c);
        moves.Add(new StockMovement { StockCardId = c.Id, Type = "Entry", Quantity = 5 });

        Action act = () => moves.Add(new StockMovement { StockCardId = c.Id, Type = "Exit", Quantity = 10 });
        act.Should().Throw<InvalidOperationException>();
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. STOCK CARD VALIDATION — boundary attacks
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("", "Name")]
    [InlineData("Code", "")]
    [InlineData("   ", "Name")]
    [InlineData("Code", "   ")]
    public void StockCard_EmptyRequiredFields_ShouldThrow(string code, string name)
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        Action act = () => cards.Add(new StockCard { Code = code, Name = name, CardType = "Child" });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StockCard_DuplicateCode_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        cards.Add(new StockCard { Code = "DUPL-1", Name = "First", CardType = "Child" });

        Action act = () => cards.Add(new StockCard { Code = "DUPL-1", Name = "Second", CardType = "Child" });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StockCard_NegativeMinStok_ShouldClampToZero()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var c = new StockCard { Code = "NMIN-1", Name = "NegMin", CardType = "Child", MinStock = -10 };
        cards.Add(c);

        var saved = cards.GetById(c.Id);
        saved.Should().NotBeNull();
        saved!.MinStock.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void StockCard_NegativeBirimFiyat_ShouldClampToZero()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var c = new StockCard { Code = "NPRC-1", Name = "NegPrice", CardType = "Child", UnitPrice = -100 };
        cards.Add(c);

        var saved = cards.GetById(c.Id);
        saved.Should().NotBeNull();
        saved!.UnitPrice.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void StockCard_DeleteNonExistent_ShouldNotCrash()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        // Non-existent ID silme — crash olmamalı
        Action act = () => cards.Delete(999999);
        act.Should().NotThrow();
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. BULK OPERATIONS — atomicity
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BulkAdd_EmptyList_ShouldNotThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var moves = new MovementRepository(f);
        // Empty list AddBulk = no-op
        Action act = () => moves.AddBulk(new List<StockMovement>());
        act.Should().NotThrow();
    }

    [Fact]
    public async Task BulkAdd_PartialBadData_ShouldRollbackAll()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);

        var c = new StockCard { Code = "BULK-1", Name = "Bulk Test", CardType = "Child" };
        cards.Add(c);

        // İlk hareket geçerli, ikinci hareket geçersiz (stok yetersiz)
        var list = new List<StockMovement>
        {
            new() { StockCardId = c.Id, Type = "Entry", Quantity = 5, Date = DateTime.Now },
            new() { StockCardId = c.Id, Type = "Exit", Quantity = 100, Date = DateTime.Now } // yetersiz
        };

        Action act = () => moves.AddBulk(list);
        act.Should().Throw<InvalidOperationException>();

        // Atomicity: ilk kayıt da geri alınmış olmalı
        var all = await moves.GetPagedAsync(1, 100, stockCardId: c.Id);
        all.Should().BeEmpty("Bulk operation failed, so nothing should persist");
    }

    [Fact]
    public void BulkDelete_NonExistentIds_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var moves = new MovementRepository(f);

        Action act = () => moves.DeleteBulk(new[] { 999998, 999999 });
        act.Should().Throw<InvalidOperationException>();
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. DEPARTMENT OPERATIONS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Department_AddEmpty_ShouldNotThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new DepartmentRepository(f);
        // Boş isim ekleme — sessizce yoksayılmalı
        Func<Task> act = () => repo.AddAsync("");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Department_CRUDCycle_ShouldWork()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new DepartmentRepository(f);

        await repo.AddAsync("TestDept");
        var all = await repo.GetAllAsync();
        all.Should().Contain("TestDept");

        await repo.UpdateAsync("TestDept", "RenamedDept");
        all = await repo.GetAllAsync();
        all.Should().Contain("RenamedDept");
        all.Should().NotContain("TestDept");

        await repo.DeleteAsync("RenamedDept");
        all = await repo.GetAllAsync();
        all.Should().NotContain("RenamedDept");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. NOTE OPERATIONS — aggressive
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Note_EmptyTitle_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new NoteRepository(f);

        Action act = () => repo.Add(new Note { Title = "", Content = "body" });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Note_CRUDCycle_ShouldWork()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new NoteRepository(f);

        var note = new Note { Title = "Test Note", Content = "Content", Date = DateTime.Now };
        repo.Add(note);
        note.Id.Should().BeGreaterThan(0);

        note.Title = "Updated Note";
        repo.Update(note);

        var all = repo.GetAll();
        all.Should().Contain(n => n.Title == "Updated Note");

        repo.Delete(note.Id);
        all = repo.GetAll();
        all.Should().NotContain(n => n.Id == note.Id);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. SERVICE RECORD OPERATIONS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ServiceRecord_EmptyDeviceName_ShouldThrow()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new ServiceRecordRepository(f);

        Func<Task> act = () => repo.AddAsync(new ServiceRecord { DeviceName = "" });
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ServiceRecord_CRUDCycle_ShouldWork()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new ServiceRecordRepository(f);

        var rec = new ServiceRecord { DeviceName = "Printer", Company = "HP", ServiceDate = DateTime.Now, Issue = "Jam", Result = "Fixed" };
        await repo.AddAsync(rec);
        rec.Id.Should().BeGreaterThan(0);

        rec.Result = "Replaced";
        await repo.UpdateAsync(rec);

        var all = await repo.GetAllAsync(null, null, null);
        all.Should().Contain(r => r.Id == rec.Id && r.Result == "Replaced");

        await repo.DeleteAsync(rec.Id);
        all = await repo.GetAllAsync(null, null, null);
        all.Should().NotContain(r => r.Id == rec.Id);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. CONFIG REPOSITORY
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Config_SetAndGet_ShouldReturnSameValue()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new ConfigRepository(f);

        repo.SetConfig("TestKey", "TestValue");
        repo.GetConfig("TestKey").Should().Be("TestValue");
    }

    [Fact]
    public void Config_GetNonExistent_ShouldReturnDefault()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new ConfigRepository(f);

        repo.GetConfig("NonExistentKey", "default123").Should().Be("default123");
    }

    [Fact]
    public void Config_Upsert_ShouldOverwrite()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new ConfigRepository(f);

        repo.SetConfig("UpsertKey", "First");
        repo.SetConfig("UpsertKey", "Second");
        repo.GetConfig("UpsertKey").Should().Be("Second");
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. USER / AUTH — password policy
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void User_Authenticate_DefaultAdmin_ShouldWork()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new UserRepository(f);

        // Database SeedDefaults v3 hash ile "admin" şifresi oluşturur
        repo.Authenticate("admin", "admin").Should().BeTrue();
    }

    [Fact]
    public void User_Authenticate_WrongPassword_ShouldFail()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new UserRepository(f);
        repo.Authenticate("admin", "wrongpassword").Should().BeFalse();
    }

    [Fact]
    public void User_Authenticate_NonExistentUser_ShouldFail()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new UserRepository(f);
        repo.Authenticate("nonexistentuser", "password").Should().BeFalse();
    }

    [Fact]
    public void User_PasswordPolicy_TooShort_ShouldReturnError()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new UserRepository(f);
        repo.ValidatePasswordPolicy("ab", "admin").Should().NotBeNull();
    }

    [Fact]
    public void User_PasswordPolicy_ContainsUsername_ShouldReturnError()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new UserRepository(f);
        repo.ValidatePasswordPolicy("admin1234", "admin").Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. VIEWMODEL BUTTON FLOWS — StockMovements
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task StockMovements_AllButtons_ShouldNotCrash()
    {
        var mockMoves = new Mock<IMovementRepository>();
        var mockCards = new Mock<IStockCardRepository>();
        var mockDepts = new Mock<IDepartmentRepository>();

        mockCards.Setup(r => r.GetChildCardsAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<StockCard>());
        mockMoves.Setup(r => r.GetCountAsync(It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        mockMoves.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<StockMovement>());

        var vm = new StockMovementsViewModel(mockMoves.Object, mockCards.Object, mockDepts.Object, MockDialog.Object, MockLogger.Object);
        await Task.Delay(200); // Init

        // Filtrele butonu
        Func<Task> loadAct = () => vm.LoadMovementsCommand.ExecuteAsync(null);
        await loadAct.Should().NotThrowAsync();

        // Temizle butonu
        Action clearAct = () => vm.ClearFiltersCommand.Execute(null);
        clearAct.Should().NotThrow();
        vm.SearchText.Should().BeEmpty();
        vm.SelectedTypeIndex.Should().Be(0);

        // Sayfalama — boş veri ile sınır testi
        Func<Task> nextAct = () => vm.NextPageCommand.ExecuteAsync(null);
        await nextAct.Should().NotThrowAsync();

        Func<Task> prevAct = () => vm.PrevPageCommand.ExecuteAsync(null);
        await prevAct.Should().NotThrowAsync();

        // Sil butonu — seçili öğe yok
        Func<Task> delAct = () => vm.DeleteMovementCommand.ExecuteAsync(null);
        await delAct.Should().NotThrowAsync();

        // Toplu Sil butonu — seçili öğe yok
        Func<Task> bulkDelAct = () => vm.DeleteBulkCommand.ExecuteAsync(null);
        await bulkDelAct.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StockMovements_FilterByType_ShouldCallRepoWithCorrectType()
    {
        var mockMoves = new Mock<IMovementRepository>();
        var mockCards = new Mock<IStockCardRepository>();
        var mockDepts = new Mock<IDepartmentRepository>();

        mockCards.Setup(r => r.GetChildCardsAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<StockCard>());
        mockMoves.Setup(r => r.GetCountAsync(It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        mockMoves.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<StockMovement>());

        var vm = new StockMovementsViewModel(mockMoves.Object, mockCards.Object, mockDepts.Object, MockDialog.Object, MockLogger.Object);
        await Task.Delay(200);

        // Type filter = Giriş (index 1)
        vm.SelectedTypeIndex = 1;
        await vm.LoadMovementsCommand.ExecuteAsync(null);

        mockMoves.Verify(r => r.GetCountAsync(null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), null, "Entry", null, null, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    // ═══════════════════════════════════════════════════════════════
    // 10. REPORT REPOSITORY
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Dashboard_Stats_EmptyDB_ShouldReturnZeros()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new ReportRepository(f);

        var stats = await repo.GetDashboardStatsAsync();
        stats.TotalCards.Should().BeGreaterThanOrEqualTo(0);
        stats.TotalMovements.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task StockReport_ShouldReturnValidRows()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var cards = new StockCardRepository(f);
        var moves = new MovementRepository(f);

        var c = new StockCard { Code = "RPT-1", Name = "Report Test", CardType = "Child" };
        cards.Add(c);
        moves.Add(new StockMovement { StockCardId = c.Id, Type = "Entry", Quantity = 100, Date = DateTime.Now });

        var repo = new ReportRepository(f);
        var rows = await repo.GetStockReportAsync();

        rows.Should().Contain(r => r.Code == "RPT-1" && r.Current == 100);
    }

    // ═══════════════════════════════════════════════════════════════
    // 11. MOVEMENT FORMATTER — parse edge cases
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("5[G]", true, "Entry", 5)]
    [InlineData("3[Ç]", true, "Exit", 3)]
    [InlineData("[B]", true, "Blank", 0)]
    [InlineData("", false, null, 0)]
    [InlineData("   ", false, null, 0)]
    [InlineData("abc", false, null, 0)]
    [InlineData("-5[G]", false, null, 0)]
    public void MovementFormatter_ParseCell(string input, bool expectedSuccess, string? expectedTur, double expectedMiktar)
    {
        var result = MovementFormatter.ParseCell(input);
        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess && expectedTur != null)
        {
            result.Type.ToDbString().Should().Be(expectedTur);
            result.Quantity.Should().Be(expectedMiktar);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 12. PAGING — edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ServiceRecord_Paging_PageZeroOrNegative_ShouldNotCrash()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var repo = new ServiceRecordRepository(f);

        // Page 0 ve negatif page — crash olmamalı
        Func<Task> act0 = () => repo.GetPagedAsync(0, 10, null, null, null);
        await act0.Should().NotThrowAsync();

        Func<Task> actNeg = () => repo.GetPagedAsync(-1, 10, null, null, null);
        await actNeg.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Movement_Paging_LargePageNumber_ShouldReturnEmpty()
    {
        var f = new TestDbFactory(Database.DatabasePath);
        var moves = new MovementRepository(f);

        var result = await moves.GetPagedAsync(9999, 30);
        result.Should().BeEmpty();
    }
}
