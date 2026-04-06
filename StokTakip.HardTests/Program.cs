using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using StokTakip.Data;
using StokTakip.Models;

Console.OutputEncoding = Encoding.UTF8;

var tests = new List<(string Name, Func<Task> Run)>
{
    ("Database path and config", TestDatabasePathAndConfig),
    ("Duplicate stock code rejected", TestDuplicateStockCodeRejected),
    ("Negative stock blocked", TestNegativeStockBlocked),
    ("Deleting critical entry blocked", TestDeletingCriticalEntryBlocked),
    ("Parent cards reject movements", TestParentCardRejectsMovements),
    ("Legacy password upgraded", TestLegacyPasswordUpgrade),
    ("Password policy enforced", TestPasswordPolicy),
    ("Concurrent writes stay consistent", TestConcurrentWrites),
    ("Backup and SQL export", TestBackupAndSqlExport),
    ("Bulk service insert", TestBulkServiceInsert),
    ("Note CRUD operations", TestNotCRUD)
};

var failures = new List<string>();
var total = Stopwatch.StartNew();

foreach (var test in tests)
{
    var sw = Stopwatch.StartNew();
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name} ({sw.ElapsedMilliseconds} ms)");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex);
    }
}

Console.WriteLine();
Console.WriteLine($"Completed {tests.Count} tests in {total.ElapsedMilliseconds} ms");

if (failures.Count > 0)
{
    Console.WriteLine("Failures:");
    foreach (string failure in failures)
        Console.WriteLine("- " + failure);

    Environment.ExitCode = 1;
    return;
}

Environment.ExitCode = 0;

static Task TestDatabasePathAndConfig()
{
    using var scope = new TestScope("stok test ç");
    using var db = new Database(scope.DbPath);

    db.SetConfig("theme", "stress");
    AssertEqual("stress", db.GetConfig("theme"), "Config roundtrip failed.");
    AssertTrue(File.Exists(scope.DbPath), "Database file was not created.");

    return Task.CompletedTask;
}

static Task TestDuplicateStockCodeRejected()
{
    using var scope = new TestScope("duplicate-code");
    using var db = new Database(scope.DbPath);

    AddCard(db, "ABC-001", "Card A");
    AssertThrows<InvalidOperationException>(() => AddCard(db, "abc-001", "Card B"), "Duplicate stock code should fail.");

    return Task.CompletedTask;
}

static Task TestNegativeStockBlocked()
{
    using var scope = new TestScope("negative-stock");
    using var db = new Database(scope.DbPath);

    int cardId = AddCard(db, "NEG-001", "Negative Test");
    db.HareketEkle(new StokHareketi { StokKartId = cardId, Tur = "Giris", Miktar = 5, Departman = "IT", Tarih = DateTime.Now });
    AssertThrows<InvalidOperationException>(() => db.HareketEkle(new StokHareketi { StokKartId = cardId, Tur = "Cikis", Miktar = 6, Departman = "IT", Tarih = DateTime.Now }), "Exit larger than stock should fail.");

    var card = db.StokKartiDetayGetir(cardId) ?? throw new InvalidOperationException("Card not found after movement.");
    AssertNear(5, card.MevcutStok, 0.0001, "Stock should remain unchanged after rejected exit.");

    return Task.CompletedTask;
}

static Task TestDeletingCriticalEntryBlocked()
{
    using var scope = new TestScope("delete-critical-entry");
    using var db = new Database(scope.DbPath);

    int cardId = AddCard(db, "DEL-001", "Delete Guard");
    db.HareketEkle(new StokHareketi { StokKartId = cardId, Tur = "Giris", Miktar = 5, Departman = "IT", Tarih = DateTime.Now });
    db.HareketEkle(new StokHareketi { StokKartId = cardId, Tur = "Cikis", Miktar = 4, Departman = "IT", Tarih = DateTime.Now });

    int entryId = db.HareketleriGetir(cardId).Single(x => x.Tur == "Giris").Id;
    AssertThrows<InvalidOperationException>(() => db.HareketSil(entryId), "Deleting a required entry should fail.");

    return Task.CompletedTask;
}

static Task TestNotCRUD()
{
    using var scope = new TestScope("note-crud");
    using var db = new Database(scope.DbPath);

    // Create
    var note = new Not { Baslik = "Test Note", Icerik = "Test Content", Tarih = DateTime.Now };
    db.NotEkle(note);
    AssertTrue(note.Id > 0, "Note ID should be populated.");

    // Read
    var notes = db.NotlariGetir();
    AssertEqual(1, notes.Count, "Should retrieve exactly 1 note.");
    AssertEqual("Test Note", notes[0].Baslik, "Title should match.");

    // Update
    notes[0].Baslik = "Updated Note";
    db.NotGuncelle(notes[0]);
    var updatedNotes = db.NotlariGetir();
    AssertEqual("Updated Note", updatedNotes[0].Baslik, "Title should be updated.");

    // Delete
    db.NotSil(notes[0].Id);
    var remainingNotes = db.NotlariGetir();
    AssertEqual(0, remainingNotes.Count, "Should retrieve 0 notes after deletion.");

    return Task.CompletedTask;
}
static Task TestParentCardRejectsMovements()
{
    using var scope = new TestScope("parent-card");
    using var db = new Database(scope.DbPath);

    int parentId = AddCard(db, "PARENT-001", "Parent", kartTipi: "Ust");
    AssertThrows<InvalidOperationException>(() =>
        db.HareketEkle(new StokHareketi { StokKartId = parentId, Tur = "Giris", Miktar = 1, Departman = "IT", Tarih = DateTime.Now }),
        "Parent cards must not accept movements.");

    return Task.CompletedTask;
}

static Task TestLegacyPasswordUpgrade()
{
    using var scope = new TestScope("legacy-auth");
    using var db = new Database(scope.DbPath);

    InsertLegacyUser(scope.DbPath, "legacy.user", "Legacy!Pass123");
    AssertTrue(db.KullaniciDogrula("legacy.user", "Legacy!Pass123"), "Legacy password should validate.");

    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = scope.DbPath }.ToString());
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT SifreHash FROM Kullanicilar WHERE KullaniciAdi='legacy.user'";
    string storedHash = command.ExecuteScalar()?.ToString() ?? string.Empty;
    AssertTrue(storedHash.StartsWith("v3:", StringComparison.Ordinal), "Legacy password hash should be upgraded to v3.");

    return Task.CompletedTask;
}

static Task TestPasswordPolicy()
{
    using var scope = new TestScope("password-policy");
    using var db = new Database(scope.DbPath);

    AssertTrue(db.SifrePolitikasiHatasi("short", "admin") is not null, "Weak password should be rejected.");
    AssertTrue(!db.SifreDegistir("admin", "admin", "short"), "Weak password change must fail.");
    AssertTrue(db.SifreDegistir("admin", "admin", "Strong!Pass123"), "Strong password change should succeed.");
    AssertTrue(db.KullaniciDogrula("admin", "Strong!Pass123"), "New password must validate.");

    return Task.CompletedTask;
}

static async Task TestConcurrentWrites()
{
    using var scope = new TestScope("concurrency");
    using var db = new Database(scope.DbPath);

    int cardId = AddCard(db, "CON-001", "Concurrency");
    db.HareketEkle(new StokHareketi { StokKartId = cardId, Tur = "Giris", Miktar = 1000, Departman = "IT", Tarih = DateTime.Now });

    const int workerCount = 12;
    const int operationsPerWorker = 40;

    var sw = Stopwatch.StartNew();
    Task[] tasks = Enumerable.Range(0, workerCount).Select(worker => Task.Run(() =>
    {
        bool isEntryWorker = worker % 3 == 0;
        for (int i = 0; i < operationsPerWorker; i++)
        {
            db.HareketEkle(new StokHareketi
            {
                StokKartId = cardId,
                Tur = isEntryWorker ? "Giris" : "Cikis",
                Miktar = isEntryWorker ? 3 : 1,
                Departman = "OPS",
                Tarih = DateTime.Now
            });
        }
    })).ToArray();

    await Task.WhenAll(tasks);
    sw.Stop();

    var card = db.StokKartiDetayGetir(cardId) ?? throw new InvalidOperationException("Card not found after concurrency test.");
    AssertNear(1160, card.MevcutStok, 0.0001, "Final stock is inconsistent after concurrent writes.");
    AssertEqual(1 + workerCount * operationsPerWorker, db.HareketleriGetir(cardId).Count, "Movement count mismatch after concurrent writes.");

    Console.WriteLine($"  Throughput: {workerCount * operationsPerWorker} writes in {sw.ElapsedMilliseconds} ms");
}

static Task TestBackupAndSqlExport()
{
    using var scope = new TestScope("backup");
    using var db = new Database(scope.DbPath);

    int cardId = AddCard(db, "BKP-001", "Backup Card");
    db.HareketEkle(new StokHareketi { StokKartId = cardId, Tur = "Giris", Miktar = 3, Departman = "IT", Tarih = DateTime.Now });

    string backupPath = Path.Combine(scope.RootPath, "backup copy.db");
    string sqlPath = Path.Combine(scope.RootPath, "backup.sql");

    db.CreateBackup(backupPath);
    db.ExportSqlBackup(sqlPath);

    using var backupDb = new Database(backupPath);
    AssertEqual(db.StokKartlariniGetir().Count, backupDb.StokKartlariniGetir().Count, "Backup database does not match source.");
    AssertTrue(File.Exists(sqlPath), "SQL export file was not created.");
    AssertContains("INSERT INTO StokKartlari", File.ReadAllText(sqlPath), "SQL export does not contain stock data.");

    return Task.CompletedTask;
}

static Task TestBulkServiceInsert()
{
    using var scope = new TestScope("bulk-service");
    using var db = new Database(scope.DbPath);

    var records = Enumerable.Range(1, 200).Select(i => new ServisKaydi
    {
        CihazAdi = $"Device {i}",
        SeriNumarasi = $"SN-{i:0000}",
        Firma = "Vendor",
        Sorun = "Issue",
        Sonuc = "Solved",
        BakimTarihi = DateTime.Today.AddDays(-i)
    }).ToList();

    db.TopluServisKaydiEkle(records);
    AssertEqual(200, db.ServisKayitlariniGetir().Count, "Bulk service insert count mismatch.");

    return Task.CompletedTask;
}

static int AddCard(Database db, string code, string name, string kartTipi = "Alt", int? ustKartId = null)
{
    var card = new StokKarti
    {
        KodNo = code,
        Ad = name,
        KartTipi = kartTipi,
        UstKartId = ustKartId,
        MinStok = 1
    };

    db.StokKartiEkle(card);
    return card.Id;
}

static void InsertLegacyUser(string dbPath, string userName, string password)
{
    string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
    string hash = Convert.ToBase64String(bytes);

    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO Kullanicilar (KullaniciAdi, SifreHash, Tuz, Rol) VALUES ($u, $h, $s, 'admin')";
    command.Parameters.AddWithValue("$u", userName);
    command.Parameters.AddWithValue("$h", hash);
    command.Parameters.AddWithValue("$s", salt);
    command.ExecuteNonQuery();
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message} Expected: {expected}, Actual: {actual}");
}

static void AssertNear(double expected, double actual, double tolerance, string message)
{
    if (Math.Abs(expected - actual) > tolerance)
        throw new InvalidOperationException($"{message} Expected: {expected}, Actual: {actual}");
}

static void AssertContains(string needle, string haystack, string message)
{
    if (!haystack.Contains(needle, StringComparison.Ordinal))
        throw new InvalidOperationException(message);
}

static void AssertThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

sealed class TestScope : IDisposable
{
    public string RootPath { get; }
    public string DbPath { get; }

    public TestScope(string name)
    {
        string safe = string.Concat(name.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        RootPath = Path.Combine(Path.GetTempPath(), "Stokendra.HardTests", $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safe}");
        Directory.CreateDirectory(RootPath);
        DbPath = Path.Combine(RootPath, $"{safe}.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
        catch
        {
        }
    }
}
