using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using static Stokendra.LocalizationManager;

namespace Stokendra.Data;

/// <summary>
/// SQLite tabanlı ana veritabanı sınıfı.
/// Tüm repository arayüzlerini tek bir bağlantı yönetimi altında uygular.
/// </summary>
public sealed partial class Database : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    private const int CurrentSchemaVersion = 15;
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private const int PasswordIterationsV2 = 20000;
    private const int PasswordIterationsV3 = 120000;
    private const int PasswordIterationsV4 = 600000;
    private const int PasswordHashSize = 32;

    private static readonly string[] ValidMovementTypes = { nameof(MovementType.Entry), nameof(MovementType.Exit), nameof(MovementType.Blank) };
    private static readonly string[] ValidCardTypes = { nameof(CardType.Child), nameof(CardType.Parent) };
    private static readonly string[] SqlBackupTables =
    {
        "StockCards",
        "StockMovements",
        "Notes",
        "Units",
        "Departments",
        "AppConfig",
        "AuditLog",
        "Users",
        "ServiceRecords"
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
        SqliteHelpers.ApplyConnectionPragmas(connection);
        return connection;
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
        
        // Turn foreign keys OFF during schema changes so we can rename/drop tables without constraints triggering
        ExecutePragma(connection, "foreign_keys", "OFF");

        using var cmdBegin = connection.CreateCommand();
        cmdBegin.CommandText = "BEGIN IMMEDIATE";
        cmdBegin.ExecuteNonQuery();

        try
        {
            int version = GetSchemaVersion(connection, null);
            // V1–V12 migrations are no-ops (legacy, already folded into V13)
            if (version < 13) MigrateToV13(connection, null);
            if (version < 14) MigrateToV14(connection, null);
            if (version < 15) MigrateToV15(connection, null);

            EnsureSchema(connection, null);
            EnsureIndexes(connection, null);
            SetSchemaVersion(connection, null, CurrentSchemaVersion);
            NormalizeLegacyData(connection, null);
            SeedDefaults(connection, null);

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
        finally
        {
            // Restore foreign keys enforcement
            ExecutePragma(connection, "foreign_keys", "ON");
        }

        // Validate database integrity and foreign key constraints
        ValidateDatabase(connection, null);

        using var optimize = connection.CreateCommand();
        optimize.CommandText = "PRAGMA optimize;";
        optimize.ExecuteNonQuery();
    }
}
