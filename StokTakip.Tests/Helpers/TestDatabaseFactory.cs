using StokTakip;
using StokTakip.Data;

namespace StokTakip.Tests.Helpers;

/// <summary>
/// Her test için izole, geçici bir SQLite veritabanı oluşturur.
/// Test bittikten sonra dosyayı temizler.
/// </summary>
public sealed class TestDatabaseFactory : IDisposable
{
    private readonly string _dbPath;
    private Database? _db;

    public TestDatabaseFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"stokendra_test_{Guid.NewGuid():N}.db");
        // LocalizationManager'ı başlat (dil dosyası olmadan fallback modda çalışır)
        LocalizationManager.Initialize("tr");
    }

    /// <summary>Yeni bir test veritabanı örneği oluşturur.</summary>
    public Database Create()
    {
        _db = new Database(_dbPath);
        return _db;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _db?.Dispose();
        _db = null;

        // Kısa bekleme: SQLite bağlantı havuzunun kapanması için
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Thread.Sleep(50);

        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        try { if (File.Exists(_dbPath + "-wal")) File.Delete(_dbPath + "-wal"); } catch { }
        try { if (File.Exists(_dbPath + "-shm")) File.Delete(_dbPath + "-shm"); } catch { }
    }
}
