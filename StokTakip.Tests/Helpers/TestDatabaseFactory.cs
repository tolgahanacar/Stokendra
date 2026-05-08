using StokTakip;
using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Data.Repositories;

namespace StokTakip.Tests.Helpers;

/// <summary>
/// Her test için izole, geçici bir in-memory SQLite veritabanı oluşturur.
/// Yeni Repository pattern'ını kullanır.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _legacyDb; // Schema oluşturmak için
    private bool _disposed;

    public IDbConnectionFactory Factory { get; }
    public IStockCardRepository StockCards { get; }
    public IMovementRepository Movements { get; }
    public IUserRepository Users { get; }
    public INoteRepository Notes { get; }
    public IServiceRecordRepository Services { get; }
    public IDepartmentRepository Departments { get; }
    public IReportRepository Reports { get; }

    public TestDb()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        LocalizationManager.Initialize("tr");

        // Legacy Database sınıfı schema'yı oluşturur ve migration'ları çalıştırır
        _legacyDb = new Database(_dbPath);

        // Yeni repository'ler aynı DB'ye bağlanır
        Factory     = new SqliteConnectionFactory(_dbPath);
        StockCards  = new StockCardRepository(Factory);
        Movements   = new MovementRepository(Factory);
        Users       = new UserRepository(Factory);
        Notes       = new NoteRepository(Factory);
        Services    = new ServiceRecordRepository(Factory);
        Departments = new DepartmentRepository(Factory);
        Reports     = new ReportRepository(Factory);
    }

    /// <summary>Test için bir alt stok kartı oluşturur ve ID'sini döner.</summary>
    public int CreateCard(string ad = "Test Stok", string kod = "T001",
        int minStok = 0, string kategori = "")
    {
        var kart = new StokTakip.Models.StokKarti
        {
            Ad = ad, KodNo = kod, KartTipi = "Alt",
            MinStok = minStok, Kategori = kategori
        };
        StockCards.Add(kart);
        return kart.Id;
    }

    /// <summary>Karta giriş hareketi ekler.</summary>
    public void AddEntry(int cardId, double miktar, string dept = "Test Dept",
        string teslim = "", DateTime? tarih = null)
    {
        Movements.Add(new StokTakip.Models.StokHareketi
        {
            StokKartId = cardId, Tur = "Giris", Miktar = miktar,
            Departman = dept, TeslimEdilen = teslim,
            Tarih = tarih ?? DateTime.Now
        });
    }

    /// <summary>Karta çıkış hareketi ekler.</summary>
    public void AddExit(int cardId, double miktar, string dept = "Test Dept",
        string teslim = "", DateTime? tarih = null)
    {
        Movements.Add(new StokTakip.Models.StokHareketi
        {
            StokKartId = cardId, Tur = "Cikis", Miktar = miktar,
            Departman = dept, TeslimEdilen = teslim,
            Tarih = tarih ?? DateTime.Now
        });
    }

    /// <summary>Kartın mevcut stok miktarını döner.</summary>
    public double GetStock(int cardId)
        => StockCards.GetById(cardId)?.MevcutStok ?? 0;    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _legacyDb.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Thread.Sleep(50);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        try { if (File.Exists(_dbPath + "-wal")) File.Delete(_dbPath + "-wal"); } catch { }
        try { if (File.Exists(_dbPath + "-shm")) File.Delete(_dbPath + "-shm"); } catch { }
    }
}
