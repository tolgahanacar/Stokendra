using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

/// <summary>
/// SQLite tabanlı ana veritabanı sınıfı.
/// Tüm repository arayüzlerini tek bir bağlantı yönetimi altında uygular.
/// </summary>
public sealed partial class Database :
    IStockCardRepository,
    IMovementRepository,
    IServiceRecordRepository,
    INoteRepository,
    IDepartmentRepository,
    IReportRepository,
    IUserRepository,
    IConfigRepository,
    IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    private const int CurrentSchemaVersion = 10;
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private const int PasswordIterationsV2 = 20000;
    private const int PasswordIterationsV3 = 120000;
    private const int PasswordHashSize = 32;

    private static readonly string[] ValidMovementTypes = { nameof(HareketTuru.Giris), nameof(HareketTuru.Cikis), nameof(HareketTuru.Bos) };
    private static readonly string[] ValidCardTypes = { nameof(KartTipi.Alt), nameof(KartTipi.Ust) };
    private static readonly string[] SqlBackupTables =
    {
        "StokKartlari",
        "StokHareketleri",
        "Notlar",
        "Birimler",
        "Departmanlar",
        "AppConfig",
        "AuditLog",
        "Kullanicilar",
        "ServisKayitlari"
    };

    public Database(string dbPath)
    {
        _databasePath = AppPaths.NormalizeDatabasePath(dbPath);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            ForeignKeys = true,
            DefaultTimeout = 15
        };

        _connectionString = builder.ToString();
        Initialize();
    }

    public string DatabasePath => _databasePath;

    public void Dispose()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch { /* Best-effort checkpoint */ }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        ApplyConnectionPragmas(connection);
        return connection;
    }

    private static void ApplyConnectionPragmas(SqliteConnection connection)
    {
        ExecutePragma(connection, "foreign_keys", "ON");
        ExecutePragma(connection, "busy_timeout", "15000");
        ExecutePragma(connection, "synchronous", "NORMAL");
        ExecutePragma(connection, "temp_store", "MEMORY");
        ExecutePragma(connection, "cache_size", "-16000");
    }

    private static void ExecutePragma(SqliteConnection connection, string pragma, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma}={value};";
        command.ExecuteNonQuery();
    }

    private void Initialize()
    {
        using var connection = CreateConnection();

        using (var journalMode = connection.CreateCommand())
        {
            journalMode.CommandText = "PRAGMA journal_mode=WAL;";
            journalMode.ExecuteScalar();
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            EnsureSchema(connection, transaction);

            int version = GetSchemaVersion(connection, transaction);
            if (version < 1) MigrateToV1(connection, transaction);
            if (version < 2) MigrateToV2(connection, transaction);
            if (version < 3) MigrateToV3(connection, transaction);
            if (version < 4) MigrateToV4(connection, transaction);
            if (version < 5) MigrateToV5(connection, transaction);
            if (version < 6) MigrateToV6(connection, transaction);
            if (version < 7) MigrateToV7(connection, transaction);
            if (version < 8) MigrateToV8(connection, transaction);
            if (version < 9) MigrateToV9(connection, transaction);
            if (version < 10) MigrateToV10(connection, transaction);

            EnsureIndexes(connection, transaction);
            SetSchemaVersion(connection, transaction, CurrentSchemaVersion);
            SeedDefaults(connection, transaction);
            ValidateDatabase(connection, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        using var optimize = connection.CreateCommand();
        optimize.CommandText = "PRAGMA optimize;";
        optimize.ExecuteNonQuery();
    }


    public void AuditLogYaz(string tip, string tablo, int id, string detay)
    {
        try
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();
            InsertAuditLog(connection, transaction, tip, tablo, id, detay);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("AuditLogYaz error: " + ex);
        }
    }

    public List<string> GetTeslimEdilenler()
    {
        var liste = new List<string>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null,
                "SELECT DISTINCT KimeVerildi FROM StokHareketleri WHERE trim(KimeVerildi) <> '' ORDER BY KimeVerildi COLLATE NOCASE");
            using var reader = command.ExecuteReader();
            while (reader.Read())
                liste.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("GetTeslimEdilenler error: " + ex);
        }

        return liste;
    }

    public void TopluHareketSil(IEnumerable<int> ids)
    {
        var idList = ids?.Distinct().Where(x => x > 0).ToList() ?? new List<int>();
        if (idList.Count == 0)
            throw new InvalidOperationException(L("bulk_operation_empty"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (int id in idList)
            {
                StokHareketi mevcut = GetMovementById(connection, transaction, id) ?? throw new InvalidOperationException(L("movement_not_found"));
                double projectedStock = GetCurrentStockCore(connection, transaction, mevcut.StokKartId) - MovementImpact(mevcut);
                if (projectedStock < 0)
                    throw new InvalidOperationException(L("stock_would_go_negative"));

                using var deleteCommand = CreateCommand(connection, transaction, "DELETE FROM StokHareketleri WHERE Id=$id");
                deleteCommand.Parameters.AddWithValue("$id", id);
                if (deleteCommand.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException(L("movement_not_found"));
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("TopluHareketSil error: " + ex);
            throw;
        }
    }

    public async Task<List<(DateTime Tarih, double Giris, double Cikis)>> Son7GunHareketOzetleriAsync()
    {
        var result = new List<(DateTime Tarih, double Giris, double Cikis)>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, @"
                SELECT
                    DATE(Tarih) AS Gun,
                    COALESCE(SUM(CASE WHEN Tur='Giris' THEN Miktar ELSE 0 END), 0) AS TopGiris,
                    COALESCE(SUM(CASE WHEN Tur='Cikis' THEN Miktar ELSE 0 END), 0) AS TopCikis
                FROM StokHareketleri
                WHERE Tarih >= $bas
                GROUP BY DATE(Tarih)
                ORDER BY DATE(Tarih)");
            command.Parameters.AddWithValue("$bas", DateTime.Today.AddDays(-6).ToString(DateFormat, CultureInfo.InvariantCulture));

            using var reader = await command.ExecuteReaderAsync();
            var dataMap = new Dictionary<DateTime, (double g, double c)>();
            while (await reader.ReadAsync())
            {
                DateTime day = ParseDateSafe(reader.GetString(0)).Date;
                dataMap[day] = (reader.GetDouble(1), reader.GetDouble(2));
            }

            for (int i = 0; i < 7; i++)
            {
                DateTime day = DateTime.Today.AddDays(-6 + i);
                if (dataMap.TryGetValue(day, out var values))
                    result.Add((day, values.g, values.c));
                else
                    result.Add((day, 0, 0));
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Son7GunHareketOzetleriAsync error", ex);
        }

        return result;
    }

    public async Task<List<(string KodNo, string Ad, double ToplamGiris, double ToplamCikis, double Mevcut)>> StokRaporVerisiAsync()
    {
        var result = new List<(string, string, double, double, double)>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, @"
                WITH hareket_ozet AS (
                    SELECT
                        StokKartId,
                        SUM(CASE WHEN Tur='Giris' THEN Miktar ELSE 0 END) AS ToplamGiris,
                        SUM(CASE WHEN Tur='Cikis' THEN Miktar ELSE 0 END) AS ToplamCikis,
                        SUM(CASE WHEN Tur='Giris' THEN Miktar WHEN Tur='Cikis' THEN -Miktar ELSE 0 END) AS Mevcut
                    FROM StokHareketleri
                    GROUP BY StokKartId
                )
                SELECT
                    s.KodNo,
                    s.Ad,
                    COALESCE(h.ToplamGiris, 0),
                    COALESCE(h.ToplamCikis, 0),
                    COALESCE(h.Mevcut, 0)
                FROM StokKartlari s
                LEFT JOIN hareket_ozet h ON h.StokKartId = s.Id
                WHERE COALESCE(s.KartTipi, 'Alt')='Alt'
                ORDER BY s.KodNo COLLATE NOCASE, s.Id");
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetDouble(4)));
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("StokRaporVerisiAsync error", ex);
        }

        return result;
    }

    public void TopluServisKaydiEkle(IEnumerable<ServisKaydi> kayitlar)
    {
        var liste = kayitlar?.Select(NormalizeServiceRecord).ToList() ?? new List<ServisKaydi>();
        if (liste.Count == 0)
            throw new InvalidOperationException(L("bulk_operation_empty"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var kayit in liste)
            {
                ValidateServiceRecord(kayit);

                using var command = CreateCommand(connection, transaction, @"
                    INSERT INTO ServisKayitlari (CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc)
                    VALUES ($ca, $sn, $f, $bt, $sr, $sc)");
                BindServisKaydiParameters(command, kayit, includeId: false);
                command.ExecuteNonQuery();

                int newId = GetLastInsertRowId(connection, transaction);
                InsertAuditLog(connection, transaction, "EKLE", "ServisKayitlari", newId, kayit.CihazAdi);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("TopluServisKaydiEkle error: " + ex);
            throw;
        }
    }

    /// <summary>
    /// Veritabanının SQLite yedeğini belirtilen dosya yoluna kopyalar.
    /// </summary>
    /// <param name="destinationPath">Hedef yedek dosya yolu.</param>
    public void CreateBackup(string destinationPath)
    {
        string normalizedPath = AppPaths.NormalizeWritableFilePath(destinationPath);

        using var source = CreateConnection();
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = normalizedPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());

        destination.Open();
        source.BackupDatabase(destination);
    }

    // ── IConfigRepository ──────────────────────────────────────────────────
    /// <inheritdoc/>
    void IConfigRepository.WriteAuditLog(string type, string table, int recordId, string detail)
        => AuditLogYaz(type, table, recordId, detail);

    /// <inheritdoc/>
    List<Birim> IConfigRepository.GetUnits() => BirimleriGetir();

    // ── IStockCardRepository ───────────────────────────────────────────────
    /// <inheritdoc/>
    List<StokKarti> IStockCardRepository.GetAll() => StokKartlariniGetir();

    /// <inheritdoc/>
    Task<List<StokKarti>> IStockCardRepository.GetAllAsync() => StokKartlariniGetirAsync();

    /// <inheritdoc/>
    List<StokKarti> IStockCardRepository.GetParentCards() => UstKartlariGetir();

    /// <inheritdoc/>
    Task<List<StokKarti>> IStockCardRepository.GetParentCardsAsync() => UstKartlariGetirAsync();

    /// <inheritdoc/>
    List<StokKarti> IStockCardRepository.GetChildCards(int? parentId) => AltKartlariGetir(parentId);

    /// <inheritdoc/>
    Task<List<StokKarti>> IStockCardRepository.GetChildCardsAsync(int? parentId) => AltKartlariGetirAsync(parentId);

    /// <inheritdoc/>
    StokKarti? IStockCardRepository.GetById(int id) => StokKartiDetayGetir(id);

    /// <inheritdoc/>
    void IStockCardRepository.Add(StokKarti stokKarti) => StokKartiEkle(stokKarti);

    /// <inheritdoc/>
    Task IStockCardRepository.AddAsync(StokKarti stokKarti) => StokKartiEkleAsync(stokKarti);

    /// <inheritdoc/>
    void IStockCardRepository.Update(StokKarti stokKarti) => StokKartiGuncelle(stokKarti);

    /// <inheritdoc/>
    void IStockCardRepository.Delete(int id) => StokKartiSil(id);

    /// <inheritdoc/>
    string IStockCardRepository.GetNextCode() => SonrakiStokKodu();

    // ── IMovementRepository ────────────────────────────────────────────────
    /// <inheritdoc/>
    List<StokHareketi> IMovementRepository.GetAll(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType)
        => HareketleriGetir(stockCardId, startDate, endDate, department, movementType);

    /// <inheritdoc/>
    Task<List<StokHareketi>> IMovementRepository.GetAllAsync(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType)
        => HareketleriGetirAsync(stockCardId, startDate, endDate, department, movementType);

    /// <inheritdoc/>
    void IMovementRepository.Add(StokHareketi hareket) => HareketEkle(hareket);

    /// <inheritdoc/>
    Task IMovementRepository.AddAsync(StokHareketi hareket) => HareketEkleAsync(hareket);

    /// <inheritdoc/>
    void IMovementRepository.AddBulk(IEnumerable<StokHareketi> hareketler) => TopluHareketEkle(hareketler);

    /// <inheritdoc/>
    Task IMovementRepository.AddBulkAsync(IEnumerable<StokHareketi> hareketler) => TopluHareketEkleAsync(hareketler);

    /// <inheritdoc/>
    void IMovementRepository.Update(StokHareketi hareket) => HareketGuncelle(hareket);

    /// <inheritdoc/>
    void IMovementRepository.Delete(int id) => HareketSil(id);

    /// <inheritdoc/>
    void IMovementRepository.DeleteBulk(IEnumerable<int> ids) => TopluHareketSil(ids);

    /// <inheritdoc/>
    async Task<List<(DateTime Date, double Entry, double Exit)>> IMovementRepository.GetLast7DaysSummaryAsync()
    {
        var raw = await Son7GunHareketOzetleriAsync();
        return raw.Select(x => (x.Tarih, x.Giris, x.Cikis)).ToList();
    }

    /// <inheritdoc/>
    List<string> IMovementRepository.GetDeliveredPersons() => GetTeslimEdilenler();

    // ── IServiceRecordRepository ───────────────────────────────────────────
    /// <inheritdoc/>
    List<ServisKaydi> IServiceRecordRepository.GetAll(DateTime? startDate, DateTime? endDate, string? searchTerm)
        => ServisKayitlariniGetir(startDate, endDate, searchTerm);

    /// <inheritdoc/>
    void IServiceRecordRepository.Add(ServisKaydi kayit) => ServisKaydiEkle(kayit);

    /// <inheritdoc/>
    void IServiceRecordRepository.AddBulk(IEnumerable<ServisKaydi> kayitlar) => TopluServisKaydiEkle(kayitlar);

    /// <inheritdoc/>
    void IServiceRecordRepository.Update(ServisKaydi kayit) => ServisKaydiGuncelle(kayit);

    /// <inheritdoc/>
    void IServiceRecordRepository.Delete(int id) => ServisKaydiSil(id);

    // ── INoteRepository ────────────────────────────────────────────────────
    /// <inheritdoc/>
    List<Not> INoteRepository.GetAll() => NotlariGetir();

    /// <inheritdoc/>
    void INoteRepository.Add(Not not) => NotEkle(not);

    /// <inheritdoc/>
    void INoteRepository.Update(Not not) => NotGuncelle(not);

    /// <inheritdoc/>
    void INoteRepository.Delete(int id) => NotSil(id);

    // ── IDepartmentRepository ──────────────────────────────────────────────
    /// <inheritdoc/>
    List<string> IDepartmentRepository.GetAll() => DepartmanlariGetir();

    /// <inheritdoc/>
    void IDepartmentRepository.Add(string name) => DepartmanEkle(name);

    /// <inheritdoc/>
    void IDepartmentRepository.Delete(string name) => DepartmanSil(name);

    // ── IReportRepository ──────────────────────────────────────────────────
    /// <inheritdoc/>
    async Task<DashboardStats> IReportRepository.GetDashboardStatsAsync()
    {
        var t = await DashboardIstatistikleriGetirAsync();
        return new DashboardStats(t.toplamKart, t.toplamStok, t.dusuk, t.tukenmis, t.toplamHareket, t.bugunHareket);
    }

    /// <inheritdoc/>
    async Task<List<StockReportRow>> IReportRepository.GetStockReportAsync()
    {
        var raw = await StokRaporVerisiAsync();
        return raw.Select(r => new StockReportRow(r.KodNo, r.Ad, r.ToplamGiris, r.ToplamCikis, r.Mevcut)).ToList();
    }

    /// <inheritdoc/>
    void IReportRepository.ExportSqlBackup(string destinationPath) => ExportSqlBackup(destinationPath);

    // ── IUserRepository ────────────────────────────────────────────────────
    /// <inheritdoc/>
    bool IUserRepository.Authenticate(string username, string password) => KullaniciDogrula(username, password);

    /// <inheritdoc/>
    bool IUserRepository.ChangePassword(string username, string oldPassword, string newPassword)
        => SifreDegistir(username, oldPassword, newPassword);

    /// <inheritdoc/>
    bool IUserRepository.IsDefaultAdminPasswordInUse() => VarsayilanAdminSifresiKullanimda();

    /// <inheritdoc/>
    string? IUserRepository.ValidatePasswordPolicy(string password, string? username)
        => SifrePolitikasiHatasi(password, username);

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction != null)
            command.Transaction = transaction;
        return command;
    }

    private static void BindMovementParameters(SqliteCommand command, StokHareketi hareket, bool includeId)
    {
        command.Parameters.AddWithValue("$sk", hareket.StokKartId);
        command.Parameters.AddWithValue("$t", hareket.Tur);
        command.Parameters.AddWithValue("$m", hareket.Miktar);
        command.Parameters.AddWithValue("$kv", hareket.TeslimEdilen);
        command.Parameters.AddWithValue("$d", hareket.Departman);
        command.Parameters.AddWithValue("$ta", hareket.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$ac", hareket.Aciklama);

        if (includeId)
            command.Parameters.AddWithValue("$id", hareket.Id);
    }

    private static void BindServisKaydiParameters(SqliteCommand command, ServisKaydi kayit, bool includeId)
    {
        command.Parameters.AddWithValue("$ca", kayit.CihazAdi);
        command.Parameters.AddWithValue("$sn", kayit.SeriNumarasi);
        command.Parameters.AddWithValue("$f", kayit.Firma);
        command.Parameters.AddWithValue("$bt", kayit.BakimTarihi.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$sr", kayit.Sorun);
        command.Parameters.AddWithValue("$sc", kayit.Sonuc);

        if (includeId)
            command.Parameters.AddWithValue("$id", kayit.Id);
    }

    private static StokHareketi ReadMovement(SqliteDataReader reader)
    {
        return new StokHareketi
        {
            Id = reader.GetInt32(0),
            StokKartId = reader.GetInt32(1),
            StokKartAd = reader.GetString(2),
            StokKartKodNo = reader.GetString(3),
            Tur = reader.GetString(4),
            Miktar = reader.GetDouble(5),
            TeslimEdilen = reader.IsDBNull(6) ? "" : reader.GetString(6),
            Departman = reader.IsDBNull(7) ? "" : reader.GetString(7),
            Tarih = ParseDateSafe(reader.GetString(8)),
            Aciklama = reader.IsDBNull(9) ? "" : reader.GetString(9)
        };
    }

    private static string BuildMovementQuery(int? stokKartId, DateTime? baslangic, DateTime? bitis, string? departman, string? tur)
    {
        var conditions = new List<string>();

        if (stokKartId.HasValue)
            conditions.Add("h.StokKartId = $kid");
        if (baslangic.HasValue)
            conditions.Add("h.Tarih >= $ts");
        if (bitis.HasValue)
            conditions.Add("h.Tarih < $te");
        if (!string.IsNullOrWhiteSpace(departman))
            conditions.Add("h.Departman = $dep");
        if (!string.IsNullOrWhiteSpace(tur))
            conditions.Add("h.Tur = $tur");

        string whereClause = conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions);

        return $@"
            SELECT
                h.Id,
                h.StokKartId,
                s.Ad,
                s.KodNo,
                h.Tur,
                h.Miktar,
                h.KimeVerildi,
                h.Departman,
                h.Tarih,
                h.Aciklama
            FROM StokHareketleri h
            JOIN StokKartlari s ON s.Id = h.StokKartId
            {whereClause}
            ORDER BY h.Tarih DESC, h.Id DESC";
    }

    private static string BuildServiceQuery(DateTime? baslangic, DateTime? bitis, string? arama)
    {
        var conditions = new List<string>();

        if (baslangic.HasValue)
            conditions.Add("BakimTarihi >= $bas");
        if (bitis.HasValue)
            conditions.Add("BakimTarihi < $bit");
        if (!string.IsNullOrWhiteSpace(arama))
            conditions.Add("(CihazAdi LIKE $ara OR SeriNumarasi LIKE $ara OR Firma LIKE $ara OR Sorun LIKE $ara OR Sonuc LIKE $ara)");

        string whereClause = conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions);
        return $@"
            SELECT Id, CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc
            FROM ServisKayitlari
            {whereClause}
            ORDER BY BakimTarihi DESC, Id DESC";
    }

}
