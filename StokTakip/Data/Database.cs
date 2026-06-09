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

    private const int CurrentSchemaVersion = 11;
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
        SqliteHelpers.RegisterCustomFunctions(connection);
        ApplyConnectionPragmas(connection);
        return connection;
    }

    private static void ApplyConnectionPragmas(SqliteConnection connection)
    {
        ExecutePragma(connection, "foreign_keys", "ON");
        ExecutePragma(connection, "busy_timeout", "30000"); // 30 sn
        ExecutePragma(connection, "synchronous", "FULL");  // Maksimum güvenlik
        ExecutePragma(connection, "journal_mode", "WAL");
        ExecutePragma(connection, "temp_store", "MEMORY");
        ExecutePragma(connection, "cache_size", "-32000"); // 32MB cache
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
        
        using var cmdBegin = connection.CreateCommand();
        cmdBegin.CommandText = "BEGIN IMMEDIATE";
        cmdBegin.ExecuteNonQuery();

        try
        {
            EnsureSchema(connection, null);

            int version = GetSchemaVersion(connection, null);
            if (version < 1) MigrateToV1(connection, null);
            if (version < 2) MigrateToV2(connection, null);
            if (version < 3) MigrateToV3(connection, null);
            if (version < 4) MigrateToV4(connection, null);
            if (version < 5) MigrateToV5(connection, null);
            if (version < 6) MigrateToV6(connection, null);
            if (version < 7) MigrateToV7(connection, null);
            if (version < 8) MigrateToV8(connection, null);
            if (version < 9) MigrateToV9(connection, null);
            if (version < 10) MigrateToV10(connection, null);
            if (version < 11) MigrateToV11(connection, null);

            EnsureIndexes(connection, null);
            SetSchemaVersion(connection, null, CurrentSchemaVersion);
            NormalizeLegacyData(connection, null);
            SeedDefaults(connection, null);
            ValidateDatabase(connection, null);

            using var cmdCommit = connection.CreateCommand();
            cmdCommit.CommandText = "COMMIT";
            cmdCommit.ExecuteNonQuery();
        }
        catch
        {
            using var cmdRollback = connection.CreateCommand();
            cmdRollback.CommandText = "ROLLBACK";
            cmdRollback.ExecuteNonQuery();
            throw;
        }

        using var optimize = connection.CreateCommand();
        optimize.CommandText = "PRAGMA optimize;";
        optimize.ExecuteNonQuery();
    }
}
