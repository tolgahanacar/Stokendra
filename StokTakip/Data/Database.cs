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
public sealed partial class Database : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    private const int CurrentSchemaVersion = 10;
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private const int PasswordIterationsV2 = 20000;
    private const int PasswordIterationsV3 = 120000;
    private const int PasswordIterationsV4 = 600000;
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
            // Pool'daki bağlantıları temizlemeden önce WAL checkpoint yap.
            // Yeni bağlantı açmak yerine mevcut pool'dan al.
            SqliteConnection.ClearAllPools();

            // Pool temizlendikten sonra kısa ömürlü bir bağlantıyla checkpoint yap.
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ToString());
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch { /* Best-effort checkpoint */ }
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
            NormalizeLegacyData(connection, transaction);
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
        // Internal usage only, actual writing should move to ConfigRepository eventually
        try
        {
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AuditLog (Tarih, IslemTipi, TabloAdi, KayitId, Detay)
                VALUES ($t, $it, $ta, $id, $d)";
            command.Parameters.AddWithValue("$t", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$it", tip);
            command.Parameters.AddWithValue("$ta", tablo);
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$d", detay ?? "");
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("AuditLogYaz error: " + ex);
        }
    }

    public void TopluHareketSil(IEnumerable<int> ids)
    {
        var idList = ids?.ToList() ?? new List<int>();
        using var conn = CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var id in idList) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "DELETE FROM StokHareketleri WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    public void TopluServisKaydiEkle(IEnumerable<ServisKaydi> kayitlar)
    {
        using var conn = CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var s in kayitlar) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "INSERT INTO ServisKayitlari (CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc) VALUES ($ca, $sn, $f, $bt, $sr, $sc)";
                BindServisKaydiParameters(cmd, s, false);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

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

    private static string BuildMovementQuery(int? stokKartId, DateTime? baslangic, DateTime? bitis, string? departman, string? tur, string? kategori = null)
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
        if (!string.IsNullOrWhiteSpace(kategori))
            conditions.Add("COALESCE(s.Kategori,'') = $kat");

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
