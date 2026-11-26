using Xunit;
using NSubstitute;
using Stokendra.Data;
using Stokendra.Data.Repositories;
using Stokendra.Models;
using Stokendra.Infrastructure;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.Tests;

/// <summary>
/// Çapraz Etki ve Sistem Bütünlüğü (Cross-Impact & State Integrity) Testleri.
/// Bir işlem yapıldığında sadece o işlemin başarılı olup olmadığını değil,
/// sistemin GERİ KALANINDA (diğer kartlar, diğer hareketler, bakiye hesapları,
/// departmanlar, yan veriler) hiçbir şeyin bozulmadığını matematiksel kesinlikle denetler.
/// </summary>
public class HardenedCrossImpactTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _database;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MovementRepository _movementRepo;
    private readonly StockCardRepository _stockCardRepo;
    private readonly DepartmentRepository _departmentRepo;
    private readonly ReportRepository _reportRepo;

    public HardenedCrossImpactTests()
    {
        DapperConfig.Initialize();
        LocalizationManager.Initialize("tr");

        _dbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_CrossImpact_{Guid.NewGuid():N}.db");
        _database = new Database(_dbPath);
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _movementRepo = new MovementRepository(_connectionFactory);
        _stockCardRepo = new StockCardRepository(_connectionFactory);
        _departmentRepo = new DepartmentRepository(_connectionFactory);
        _reportRepo = new ReportRepository(_connectionFactory);
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
    public async Task Test_CrossImpact_BulkMovementEdit_LeavesUneditedFieldsAndOtherRowsUntouched()
    {
        // 1. HAZIRLIK: 3 Departman, 3 Stok Kartı, 9 Hareket oluştur
        await _departmentRepo.AddAsync("Sicil Müdürlüğü");
        await _departmentRepo.AddAsync("Muhasebe");
        await _departmentRepo.AddAsync("Bilgi İşlem");

        var card1 = new StockCard { Code = "TK-7235", Name = "Toner 7235", CardType = "Child" };
        var card2 = new StockCard { Code = "KAG-A4", Name = "A4 Kağıt", CardType = "Child" };
        var card3 = new StockCard { Code = "PIL-AAA", Name = "İnce Pil", CardType = "Child" };
        await _stockCardRepo.AddAsync(card1);
        await _stockCardRepo.AddAsync(card2);
        await _stockCardRepo.AddAsync(card3);

        // Her karta giriş ve çıkış hareketleri ekle
        var initialMovements = new List<StockMovement>
        {
            new StockMovement { StockCardId = card1.Id, Type = "Entry", Quantity = 100, Department = "Genel", Recipient = "Depo", Description = "İlk Giriş" },
            new StockMovement { StockCardId = card1.Id, Type = "Exit", Quantity = 10, Department = "Sicil Müdürlüğü", Recipient = "Ahmet", Description = "Eski Açıklama 1" },
            new StockMovement { StockCardId = card1.Id, Type = "Exit", Quantity = 15, Department = "Sicil Müdürlüğü", Recipient = "Mehmet", Description = "Eski Açıklama 2" },
            new StockMovement { StockCardId = card1.Id, Type = "Exit", Quantity = 5, Department = "Muhasebe", Recipient = "Ayşe", Description = "Eski Açıklama 3" },

            new StockMovement { StockCardId = card2.Id, Type = "Entry", Quantity = 50, Department = "Genel", Recipient = "Depo", Description = "Kağıt Giriş" },
            new StockMovement { StockCardId = card2.Id, Type = "Exit", Quantity = 20, Department = "Sicil Müdürlüğü", Recipient = "Fatma", Description = "Kağıt Sicil" },

            new StockMovement { StockCardId = card3.Id, Type = "Entry", Quantity = 200, Department = "Genel", Recipient = "Depo", Description = "Pil Giriş" },
            new StockMovement { StockCardId = card3.Id, Type = "Exit", Quantity = 30, Department = "Bilgi İşlem", Recipient = "Ali", Description = "Pil IT" }
        };

        foreach (var m in initialMovements)
        {
            await _movementRepo.AddAsync(m);
        }

        // 2. İŞLEM ÖNCESİ SİSTEM SNAPSHOT'I (Çapraz kontrol referansı)
        var preAllMovements = await _movementRepo.GetAllAsync(null, null, null, null, null, null, null);
        var preCard1 = await _stockCardRepo.GetByIdAsync(card1.Id);
        var preCard2 = await _stockCardRepo.GetByIdAsync(card2.Id);
        var preCard3 = await _stockCardRepo.GetByIdAsync(card3.Id);

        // Düzenlenecek 3 hareketi seç: card1'in Sicil ve Muhasebe çıkışları (Id: 2, 3, 4)
        var targetsToEdit = preAllMovements.Where(m => m.StockCardId == card1.Id && m.TypeEnum == MovementType.Exit).ToList();
        Assert.Equal(3, targetsToEdit.Count);

        // 3. EYLEM: ViewModel mantığıyla sadece Açıklamayı toplu değiştir
        // Kullanıcı sadece Açıklama kutucuğunu işaretledi, yeni metin yazdı (Departman kutucuğuna dokunmadı)
        var dialogMock = NSubstitute.Substitute.For<IDialogService>();
        var bulkVm = new ViewModels.BulkEditMovementsViewModel(targetsToEdit, _movementRepo, _departmentRepo, dialogMock);
        await Task.Delay(50); // InitAsync bekle

        bulkVm.IsApplyDescription = true;
        bulkVm.Description = "GÜNCELLENMİŞ TOPLU AÇIKLAMA";
        bulkVm.IsApplyDepartment = false; // Departman işaretlenmedi!

        // Kaydet çalıştır
        await bulkVm.SaveCommand.ExecuteAsync(null);

        // 4. ÇAPRAZ ETKİ ANALİZİ (Hiçbir şeyin bozulmadığını kanıtla)
        var postAllMovements = await _movementRepo.GetAllAsync(null, null, null, null, null, null, null);
        Assert.Equal(preAllMovements.Count, postAllMovements.Count);

        // A) Düzenlenen 3 hareketin kontrolleri:
        foreach (var edited in targetsToEdit)
        {
            var dbRecord = postAllMovements.First(m => m.Id == edited.Id);

            // Açıklama değişmiş olmalı
            Assert.Equal("GÜNCELLENMİŞ TOPLU AÇIKLAMA", dbRecord.Description);

            // KRİTİK: Departman ASLA silinmemiş veya değişmemiş olmalı!
            Assert.False(string.IsNullOrWhiteSpace(dbRecord.Department), $"Hareket #{dbRecord.Id} departmanı silinmiş!");
            Assert.Equal(edited.Department, dbRecord.Department);

            // Miktar, Tarih, Kart, Tip, Teslim Alan korunmuş olmalı
            Assert.Equal(edited.Quantity, dbRecord.Quantity);
            Assert.Equal(edited.StockCardId, dbRecord.StockCardId);
            Assert.Equal(edited.Type, dbRecord.Type);
            Assert.Equal(edited.Recipient, dbRecord.Recipient);
        }

        // B) Düzenlenmeyen diğer 5 hareketin kontrolleri:
        var untouchedOriginalIds = preAllMovements.Where(m => !targetsToEdit.Any(t => t.Id == m.Id)).Select(m => m.Id).ToList();
        foreach (var uneditedId in untouchedOriginalIds)
        {
            var original = preAllMovements.First(m => m.Id == uneditedId);
            var current = postAllMovements.First(m => m.Id == uneditedId);

            Assert.Equal(original.Description, current.Description);
            Assert.Equal(original.Department, current.Department);
            Assert.Equal(original.Quantity, current.Quantity);
            Assert.Equal(original.StockCardId, current.StockCardId);
        }

        // C) Stok Kartı Bakiyeleri: Hiçbir kartın stoğunda 0.0001 bile sapma olmamalı
        var postCard1 = await _stockCardRepo.GetByIdAsync(card1.Id);
        var postCard2 = await _stockCardRepo.GetByIdAsync(card2.Id);
        var postCard3 = await _stockCardRepo.GetByIdAsync(card3.Id);

        Assert.Equal(preCard1!.CurrentStock, postCard1!.CurrentStock); // 100 - 10 - 15 - 5 = 70
        Assert.Equal(preCard2!.CurrentStock, postCard2!.CurrentStock); // 50 - 20 = 30
        Assert.Equal(preCard3!.CurrentStock, postCard3!.CurrentStock); // 200 - 30 = 170
    }

    [Fact]
    public async Task Test_CrossImpact_StockExit_ToDepartment_VerifiesExactSystemReconciliation()
    {
        // 1. HAZIRLIK: Kart ve Departman tanımla
        await _departmentRepo.AddAsync("Sicil Müdürlüğü");
        await _departmentRepo.AddAsync("Arşiv");

        var card = new StockCard { Code = "TK-7235", Name = "Toner 7235", CardType = "Child" };
        var otherCard = new StockCard { Code = "DIGER-01", Name = "Diğer Ürün", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);
        await _stockCardRepo.AddAsync(otherCard);

        // Giriş yap
        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 100, Department = "Genel" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = otherCard.Id, Type = "Entry", Quantity = 50, Department = "Genel" });

        var preStats = await _reportRepo.GetDashboardStatsAsync();

        // 2. EYLEM: Sicil Müdürlüğü'ne 18 adet TK-7235 çıkışı yap
        var exitMovement = new StockMovement
        {
            StockCardId = card.Id,
            Type = "Exit",
            Quantity = 18,
            Department = "Sicil Müdürlüğü",
            Recipient = "Birim Sorumlusu",
            Description = "Aylık toner talebi"
        };
        await _movementRepo.AddAsync(exitMovement);

        // 3. ÇAPRAZ ETKİ VE MUTASYON KONTROLLERİ:
        var postCard = await _stockCardRepo.GetByIdAsync(card.Id);
        var postOtherCard = await _stockCardRepo.GetByIdAsync(otherCard.Id);

        // Hedef kartın stoğu tam olarak 82 olmalı (100 - 18)
        Assert.Equal(82, postCard!.CurrentStock);

        // Diğer kartın stoğu KESİNLİKLE değişmemiş olmalı (50)
        Assert.Equal(50, postOtherCard!.CurrentStock);

        // Sistem genelindeki hareket sayısı tam 1 artmış olmalı
        var postStats = await _reportRepo.GetDashboardStatsAsync();
        Assert.Equal(preStats.TotalMovements + 1, postStats.TotalMovements);

        // Toplam stok tam 18 azalmış olmalı
        Assert.Equal(preStats.TotalStock - 18, postStats.TotalStock);

        // Departmanlar tablosu bozulmamış olmalı
        var depts = await _departmentRepo.GetAllAsync();
        Assert.Contains("Sicil Müdürlüğü", depts);
        Assert.Contains("Arşiv", depts);
    }

    [Fact]
    public async Task Test_CrossImpact_ReportsQuery_CombinesCardAndDepartment_WithZeroSideEffects()
    {
        // 1. HAZIRLIK: Sicil Müdürlüğü ve diğer birimlere çeşitli hareketler ekle
        await _departmentRepo.AddAsync("Sicil Müdürlüğü");
        await _departmentRepo.AddAsync("Fen İşleri");

        var targetCard = new StockCard { Code = "TK-7235", Name = "Toner 7235", CardType = "Child" };
        var fillerCard = new StockCard { Code = "KAG-A4", Name = "Kağıt", CardType = "Child" };
        await _stockCardRepo.AddAsync(targetCard);
        await _stockCardRepo.AddAsync(fillerCard);

        // TK-7235 hareketleri:
        await _movementRepo.AddAsync(new StockMovement { StockCardId = targetCard.Id, Type = "Entry", Quantity = 100, Department = "Genel" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = targetCard.Id, Type = "Exit", Quantity = 7, Department = "Sicil Müdürlüğü", Recipient = "Ali" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = targetCard.Id, Type = "Exit", Quantity = 13, Department = "Sicil Müdürlüğü", Recipient = "Veli" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = targetCard.Id, Type = "Exit", Quantity = 25, Department = "Fen İşleri", Recipient = "Hasan" });

        // KAG-A4 hareketleri (karışıklık testi):
        await _movementRepo.AddAsync(new StockMovement { StockCardId = fillerCard.Id, Type = "Entry", Quantity = 50, Department = "Genel" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = fillerCard.Id, Type = "Exit", Quantity = 10, Department = "Sicil Müdürlüğü", Recipient = "Ali" });

        // İşlem öncesi hareket sayısı ve kart durumları
        var preMovements = await _movementRepo.GetAllAsync(null, null, null, null, null, null, null);

        // 2. EYLEM: Rapor Al -> Departman: "Sicil Müdürlüğü" VE Stok Kartı: "TK-7235"
        var dialogMock = NSubstitute.Substitute.For<IDialogService>();
        var reportsVm = new ViewModels.ReportsViewModel(_movementRepo, _departmentRepo, _stockCardRepo, dialogMock);
        await Task.Delay(50); // InitializeAsync bekle

        reportsVm.SelectedDepartment = "Sicil Müdürlüğü";
        reportsVm.SelectedStockCard = new ViewModels.StockCardReportItem(targetCard.Id, "TK-7235 - Toner 7235");

        await reportsVm.GenerateReportCommand.ExecuteAsync(null);

        // 3. ÇAPRAZ RAPOR BÜTÜNLÜĞÜ:
        // A) Sadece ve sadece Sicil Müdürlüğü'ne giden TK-7235 hareketleri gelmeli (7 ve 13 adetlik 2 hareket)
        Assert.Equal(2, reportsVm.ReportRows.Count);
        Assert.All(reportsVm.ReportRows, r =>
        {
            Assert.Equal(targetCard.Id, r.StockCardId);
            Assert.Equal("Sicil Müdürlüğü", r.Department);
            Assert.Equal("Exit", r.Type);
        });

        // B) Toplam tüketim tam olarak 20 olmalı (7 + 13)
        Assert.Equal(20, reportsVm.TotalConsumption);
        Assert.Equal(1, reportsVm.UniqueItemCount);

        // C) Grafik kırılımı: Sicil Müdürlüğü seçili ve kart tek olduğu için kişi bazlı olmalı (Ali: 7, Veli: 13)
        Assert.Equal(2, reportsVm.ChartData.Count);
        var aliConsumption = reportsVm.ChartData.First(c => c.Name == "Ali").Total;
        var veliConsumption = reportsVm.ChartData.First(c => c.Name == "Veli").Total;
        Assert.Equal(7, aliConsumption);
        Assert.Equal(13, veliConsumption);

        // D) YAN ETKİ KONTROLÜ (Query veritabanında hiçbir şeyi değiştirmemeli):
        var postMovements = await _movementRepo.GetAllAsync(null, null, null, null, null, null, null);
        Assert.Equal(preMovements.Count, postMovements.Count);
    }

    [Fact]
    public async Task Test_CrossImpact_DepartmentRename_CascadesCleanly_WithoutNullingOrOrphaning()
    {
        // 1. HAZIRLIK:
        await _departmentRepo.AddAsync("Eski Sicil");
        await _departmentRepo.AddAsync("Diğer Birim");

        var card = new StockCard { Code = "CRG-725", Name = "Canon CRG-725", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 50, Department = "Genel" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 10, Department = "Eski Sicil", Recipient = "Memur 1" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 15, Department = "Eski Sicil", Recipient = "Memur 2" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 5, Department = "Diğer Birim", Recipient = "Memur 3" });

        // 2. EYLEM: Departman adını güncelle
        await _departmentRepo.UpdateAsync("Eski Sicil", "Sicil İşleri Müdürlüğü");

        // 3. ÇAPRAZ KONTROLLER:
        var allMovements = await _movementRepo.GetAllAsync(null, null, null, null, null, null, null);

        // A) "Eski Sicil"e ait hareketlerin hepsi "Sicil İşleri Müdürlüğü" olmuş mu?
        var updatedMovements = allMovements.Where(m => m.StockCardId == card.Id && m.Type == "Exit" && m.Department == "Sicil İşleri Müdürlüğü").ToList();
        Assert.Equal(2, updatedMovements.Count);

        // B) Hiçbir hareketin departmanı NULL veya boş kalmamış olmalı
        Assert.All(allMovements, m => Assert.False(string.IsNullOrWhiteSpace(m.Department)));

        // C) "Diğer Birim" hareketleri KESİNLİKLE etkilenmemiş olmalı
        var otherMovements = allMovements.Where(m => m.Department == "Diğer Birim").ToList();
        Assert.Single(otherMovements);
        Assert.Equal(5, otherMovements[0].Quantity);

        // D) Stok bakiyesi tam olarak korunmuş olmalı: 50 - 10 - 15 - 5 = 20
        var currentCard = await _stockCardRepo.GetByIdAsync(card.Id);
        Assert.Equal(20, currentCard!.CurrentStock);
    }

    [Fact]
    public async Task Test_CrossImpact_DeleteMovement_OnlyReversesTargetCardImpact()
    {
        // 1. HAZIRLIK: 2 kart, birden fazla hareket
        var cardA = new StockCard { Code = "A-01", Name = "Kart A", CardType = "Child" };
        var cardB = new StockCard { Code = "B-01", Name = "Kart B", CardType = "Child" };
        await _stockCardRepo.AddAsync(cardA);
        await _stockCardRepo.AddAsync(cardB);

        await _movementRepo.AddAsync(new StockMovement { StockCardId = cardA.Id, Type = "Entry", Quantity = 100, Department = "Genel" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = cardB.Id, Type = "Entry", Quantity = 200, Department = "Genel" });

        var exitA = new StockMovement { StockCardId = cardA.Id, Type = "Exit", Quantity = 25, Department = "Genel" };
        var exitB = new StockMovement { StockCardId = cardB.Id, Type = "Exit", Quantity = 40, Department = "Genel" };
        await _movementRepo.AddAsync(exitA);
        await _movementRepo.AddAsync(exitB);

        // 2. EYLEM: Sadece Kart A'nın çıkış hareketini (25 adet) sil
        await _movementRepo.DeleteAsync(exitA.Id);

        // 3. ÇAPRAZ DENETİM:
        var postCardA = await _stockCardRepo.GetByIdAsync(cardA.Id);
        var postCardB = await _stockCardRepo.GetByIdAsync(cardB.Id);

        // Kart A bakiyesi 100'e geri dönmeli (25 geri geldi)
        Assert.Equal(100, postCardA!.CurrentStock);

        // Kart B bakiyesi KESİNLİKLE etkilenmemiş olmalı (200 - 40 = 160)
        Assert.Equal(160, postCardB!.CurrentStock);

        // Silinen hareket veritabanında olmamalı
        var allMovements = await _movementRepo.GetAllAsync(null, null, null, null, null, null, null);
        Assert.DoesNotContain(allMovements, m => m.Id == exitA.Id);
        Assert.Contains(allMovements, m => m.Id == exitB.Id);
    }

    [Fact]
    public async Task Test_StockCardRepository_AddBulkAsync_LogsAuditWithoutSelfDeadlock()
    {
        var cards = Enumerable.Range(1, 20).Select(i => new StockCard
        {
            Code = $"BLK-{i:D3}",
            Name = $"Toplu Kart {i}",
            CardType = "Child",
            Unit = "Adet"
        }).ToList();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _stockCardRepo.AddBulkAsync(cards);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000, $"AddBulkAsync took suspiciously long ({sw.ElapsedMilliseconds}ms), possible SQLite lock.");

        var allCards = await _stockCardRepo.GetAllAsync();
        Assert.Equal(20, allCards.Count(c => c.Code.StartsWith("BLK-", StringComparison.Ordinal)));

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM AuditLog WHERE Action = 'Insert' AND TableName = 'StockCards';";
        long auditCount = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(20, auditCount);
    }

    [Fact]
    public async Task Test_MovementRepository_AddBulkAsync_RejectsInvalidQuantitiesAndRollsBack()
    {
        var card = new StockCard { Code = "VAL-01", Name = "Doğrulama Kartı", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        var validMove = new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 10, Department = "Genel" };
        var zeroExit = new StockMovement { StockCardId = card.Id, Type = "Exit", Quantity = 0, Department = "Genel" };

        await Assert.ThrowsAnyAsync<Exception>(() => _movementRepo.AddBulkAsync(new[] { validMove, zeroExit }));

        var persisted = await _movementRepo.GetAllAsync(card.Id, null, null, null, null, null, null);
        Assert.Empty(persisted);
    }

    [Fact]
    public async Task Test_Database_CascadeDeleteOnStockCard_DeletesMovements()
    {
        var card = new StockCard { Code = "CAS-01", Name = "Kaskad Kartı", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);
        await _movementRepo.AddAsync(new StockMovement { StockCardId = card.Id, Type = "Entry", Quantity = 12, Department = "Genel" });

        using (var conn = _connectionFactory.CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM StockCards WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", card.Id);
            cmd.ExecuteNonQuery();
        }

        var orphanMoves = await _movementRepo.GetAllAsync(card.Id, null, null, null, null, null, null);
        Assert.Empty(orphanMoves);
    }

    [Fact]
    public async Task Test_ReportsViewModel_DataEventsSync_ReloadsFiltersAndTotalsOnNewMovement()
    {
        await _departmentRepo.AddAsync("Bilgi İşlem");
        var dialogMock = NSubstitute.Substitute.For<Stokendra.Infrastructure.IDialogService>();
        var reportsVm = new Stokendra.ViewModels.ReportsViewModel(_movementRepo, _departmentRepo, _stockCardRepo, dialogMock);
        await reportsVm.EnsureLoadedAsync();
        Assert.Equal(0, reportsVm.TotalConsumption);

        var newCard = new StockCard { Code = "RPT-NEW", Name = "Yeni Rapor Ürünü", CardType = "Child", Category = "Ağ" };
        await _stockCardRepo.AddAsync(newCard);
        await _movementRepo.AddAsync(new StockMovement { StockCardId = newCard.Id, Type = "Entry", Quantity = 20, Department = "Bilgi İşlem" });
        await _movementRepo.AddAsync(new StockMovement { StockCardId = newCard.Id, Type = "Exit", Quantity = 6, Department = "Bilgi İşlem", Recipient = "Ada" });

        DataEvents.NotifyChanged(DataDomain.StockCards);
        DataEvents.NotifyChanged(DataDomain.Movements);
        await reportsVm.EnsureLoadedAsync();

        Assert.Equal(6, reportsVm.TotalConsumption);
        Assert.Contains(reportsVm.StockCards, s => s.Id == newCard.Id);
    }

    [Fact]
    public async Task Test_StockMovementsViewModel_ReloadCards_PreservesSelectedCardByIdOnInsert()
    {
        var cardB = new StockCard { Code = "B-200", Name = "B Kartı", CardType = "Child" };
        await _stockCardRepo.AddAsync(cardB);

        var dialogMock = NSubstitute.Substitute.For<Stokendra.Infrastructure.IDialogService>();
        var vm = new Stokendra.ViewModels.StockMovementsViewModel(_movementRepo, _stockCardRepo, _departmentRepo, dialogMock);
        await vm.EnsureLoadedAsync();

        // Index 0 = "Tümü", Index 1 = "B-200 - B Kartı"
        vm.SelectedStockIndex = 1;
        Assert.StartsWith("B-200", vm.StockItems[vm.SelectedStockIndex]);

        // "A-100" alphabetically precedes "B-200", pushing "B-200" from index 1 to index 2
        var cardA = new StockCard { Code = "A-100", Name = "A Kartı", CardType = "Child" };
        await _stockCardRepo.AddAsync(cardA);

        DataEvents.NotifyChanged(DataDomain.StockCards);
        await vm.EnsureLoadedAsync();

        Assert.Equal(2, vm.SelectedStockIndex);
        Assert.StartsWith("B-200", vm.StockItems[vm.SelectedStockIndex]);
    }

    [Fact]
    public async Task Test_StockMovementsViewModel_ImportXlsxAsync_HandlesEnglishTagAndTurkishWordsAndZeroQty()
    {
        var card = new StockCard { Code = "BR-01", Name = "Demir Boru", CardType = "Child" };
        await _stockCardRepo.AddAsync(card);

        string tempFile = Path.Combine(Path.GetTempPath(), $"stokendra_move_import_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.AddWorksheet("Hareketler");
                ws.Cell(1, 1).Value = "Tarih";
                ws.Cell(1, 2).Value = "Stok Kodu";
                ws.Cell(1, 3).Value = "Tür";
                ws.Cell(1, 4).Value = "Miktar";
                ws.Cell(1, 5).Value = "Açıklama";

                // Row 2: [E] uppercase English entry tag (case-insensitive code lookup too)
                ws.Cell(2, 1).Value = DateTime.Today;
                ws.Cell(2, 2).Value = "br-01";
                ws.Cell(2, 3).Value = "[E] Entry";
                ws.Cell(2, 4).Value = "25";
                ws.Cell(2, 5).Value = "Giriş";

                // Row 3: description/type word containing "bo" ("boru") must map to Exit, NOT Blank
                ws.Cell(3, 1).Value = DateTime.Today;
                ws.Cell(3, 2).Value = "BR-01";
                ws.Cell(3, 3).Value = "Boru Çıkışı";
                ws.Cell(3, 4).Value = "10";
                ws.Cell(3, 5).Value = "Bakım";

                // Row 4: Zero quantity Exit must be ignored cleanly
                ws.Cell(4, 1).Value = DateTime.Today;
                ws.Cell(4, 2).Value = "BR-01";
                ws.Cell(4, 3).Value = "Çıkış";
                ws.Cell(4, 4).Value = "0";
                ws.Cell(4, 5).Value = "Sıfır miktar";

                wb.SaveAs(tempFile);
            }

            var dialogMock = NSubstitute.Substitute.For<Stokendra.Infrastructure.IDialogService>();
            dialogMock.OpenFileAsync(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>())
                .Returns(Task.FromResult<string?>(tempFile));

            var vm = new Stokendra.ViewModels.StockMovementsViewModel(_movementRepo, _stockCardRepo, _departmentRepo, dialogMock);
            await vm.ImportXlsxAsync();

            var imported = await _movementRepo.GetAllAsync(card.Id, null, null, null, null, null, null);
            Assert.Equal(2, imported.Count);
            Assert.Contains(imported, m => m.Type == "Entry" && m.Quantity == 25);
            Assert.Contains(imported, m => m.Type == "Exit" && m.Quantity == 10);

            var updatedCard = await _stockCardRepo.GetByIdAsync(card.Id);
            Assert.Equal(15, updatedCard!.CurrentStock);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test_DepartmentRepository_DuplicateCaseInsensitive_ThrowsLocalizedError()
    {
        await _departmentRepo.AddAsync("Bilgi İşlem");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _departmentRepo.AddAsync("BİLGİ İŞLEM"));
        Assert.Equal(LocalizationManager.L("dept_exists"), ex.Message);
    }
}
