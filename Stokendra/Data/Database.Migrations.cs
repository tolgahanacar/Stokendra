using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using static Stokendra.LocalizationManager;

namespace Stokendra.Data;

public sealed partial class Database
{
    private static void EnsureSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        string[] tables = {
            @"CREATE TABLE IF NOT EXISTS StockCards (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Code TEXT NOT NULL COLLATE NOCASE,
                Description TEXT DEFAULT '',
                MinStock INTEGER DEFAULT 0,
                Category TEXT DEFAULT '',
                CardType TEXT NOT NULL DEFAULT 'Child',
                ParentId INTEGER DEFAULT NULL,
                CreatedAt TEXT DEFAULT '',
                UpdatedAt TEXT DEFAULT '',
                Unit TEXT DEFAULT 'Adet',
                Location TEXT DEFAULT '',
                Supplier TEXT DEFAULT '',
                Barcode TEXT DEFAULT '',
                UnitPrice REAL DEFAULT 0
            )",
            @"CREATE TABLE IF NOT EXISTS StockMovements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StockCardId INTEGER NOT NULL REFERENCES StockCards(Id) ON DELETE CASCADE,
                Type TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Recipient TEXT DEFAULT '',
                Department TEXT DEFAULT '',
                Date TEXT NOT NULL,
                Description TEXT DEFAULT ''
            )",
            "CREATE TABLE IF NOT EXISTS Notes (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT NOT NULL, Title TEXT NOT NULL, Content TEXT DEFAULT '', CreatedAt TEXT DEFAULT '', UpdatedAt TEXT DEFAULT '')",
            "CREATE TABLE IF NOT EXISTS Units (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE)",
            "CREATE TABLE IF NOT EXISTS Departments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE)",
            "CREATE TABLE IF NOT EXISTS AuditLog (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT NOT NULL, Action TEXT NOT NULL, TableName TEXT NOT NULL, RecordId INTEGER DEFAULT 0, Details TEXT DEFAULT '')",
            "CREATE TABLE IF NOT EXISTS AppConfig (Key TEXT PRIMARY KEY, Value TEXT DEFAULT '')",
            "CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL UNIQUE, PasswordHash TEXT NOT NULL, Salt TEXT NOT NULL, Role TEXT DEFAULT 'admin')",
            @"CREATE TABLE IF NOT EXISTS ServiceRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DeviceName TEXT NOT NULL,
                SerialNumber TEXT DEFAULT '',
                Company TEXT DEFAULT '',
                ServiceDate TEXT NOT NULL,
                Description TEXT DEFAULT '',
                Issue TEXT DEFAULT '',
                Result TEXT DEFAULT ''
            )"
        };

        foreach (var sql in tables)
        {
            using var cmd = CreateCommand(connection, transaction, sql);
            cmd.ExecuteNonQuery();
        }
    }

    private static int GetSchemaVersion(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = CreateCommand(connection, transaction, "PRAGMA user_version;");
        return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    private static void SetSchemaVersion(SqliteConnection connection, SqliteTransaction? transaction, int version)
    {
        using var command = CreateCommand(connection, transaction, $"PRAGMA user_version = {version};");
        command.ExecuteNonQuery();
    }

    private static void TryAlter(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        try
        {
            using var command = CreateCommand(connection, transaction, sql);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("TryAlter error: " + ex);
        }
    }

    private static bool ColumnExists(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string columnName)
    {
        try
        {
            using var command = CreateCommand(connection, transaction, $"PRAGMA table_info({tableName})");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(1);
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static void TryAddColumn(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string columnName, string columnDef)
    {
        if (!ColumnExists(connection, transaction, tableName, columnName))
        {
            TryAlter(connection, transaction, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDef}");
        }
    }

    private static void MigrateToV1(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV2(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV3(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV4(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV5(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV6(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV7(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV8(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV9(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV10(SqliteConnection connection, SqliteTransaction? transaction) { }
    private static void MigrateToV11(SqliteConnection connection, SqliteTransaction? transaction) { }

    private static void MigrateToV12(SqliteConnection connection, SqliteTransaction? transaction)
    {
        // 1. Rename tables if they exist under Turkish names
        if (TableExists(connection, transaction, "StokKartlari"))
        {
            TryAlter(connection, transaction, "ALTER TABLE StokKartlari RENAME TO StockCards;");
        }
        if (TableExists(connection, transaction, "StokHareketleri"))
        {
            TryAlter(connection, transaction, "ALTER TABLE StokHareketleri RENAME TO StockMovements;");
        }
        if (TableExists(connection, transaction, "Notlar"))
        {
            TryAlter(connection, transaction, "ALTER TABLE Notlar RENAME TO Notes;");
        }
        if (TableExists(connection, transaction, "Birimler"))
        {
            TryAlter(connection, transaction, "ALTER TABLE Birimler RENAME TO Units;");
        }
        if (TableExists(connection, transaction, "Departmanlar"))
        {
            TryAlter(connection, transaction, "ALTER TABLE Departmanlar RENAME TO Departments;");
        }
        if (TableExists(connection, transaction, "Kullanicilar"))
        {
            TryAlter(connection, transaction, "ALTER TABLE Kullanicilar RENAME TO Users;");
        }
        if (TableExists(connection, transaction, "ServisKayitlari"))
        {
            TryAlter(connection, transaction, "ALTER TABLE ServisKayitlari RENAME TO ServiceRecords;");
        }

        // 2. Rename columns to English in the renamed tables
        TryRenameColumn(connection, transaction, "StockCards", "Ad", "Name");
        TryRenameColumn(connection, transaction, "StockCards", "KodNo", "Code");
        TryRenameColumn(connection, transaction, "StockCards", "Aciklama", "Description");
        TryRenameColumn(connection, transaction, "StockCards", "MinStok", "MinStock");
        TryRenameColumn(connection, transaction, "StockCards", "Kategori", "Category");
        TryRenameColumn(connection, transaction, "StockCards", "KartTipi", "CardType");
        TryRenameColumn(connection, transaction, "StockCards", "UstKartId", "ParentId");
        TryRenameColumn(connection, transaction, "StockCards", "OlusturmaTarihi", "CreatedAt");
        TryRenameColumn(connection, transaction, "StockCards", "GuncellenmeTarihi", "UpdatedAt");
        TryRenameColumn(connection, transaction, "StockCards", "Birim", "Unit");
        TryRenameColumn(connection, transaction, "StockCards", "Konum", "Location");
        TryRenameColumn(connection, transaction, "StockCards", "Tedarikci", "Supplier");
        TryRenameColumn(connection, transaction, "StockCards", "Barkod", "Barcode");
        TryRenameColumn(connection, transaction, "StockCards", "BirimFiyat", "UnitPrice");

        // Rename StockMovements columns:
        TryRenameColumn(connection, transaction, "StockMovements", "StokKartId", "StockCardId");
        TryRenameColumn(connection, transaction, "StockMovements", "Tur", "Type");
        TryRenameColumn(connection, transaction, "StockMovements", "Miktar", "Quantity");
        TryRenameColumn(connection, transaction, "StockMovements", "KimeVerildi", "Recipient");
        TryRenameColumn(connection, transaction, "StockMovements", "Departman", "Department");
        TryRenameColumn(connection, transaction, "StockMovements", "Tarih", "Date");
        TryRenameColumn(connection, transaction, "StockMovements", "Aciklama", "Description");

        // Rename Notes columns:
        TryRenameColumn(connection, transaction, "Notes", "Tarih", "Date");
        TryRenameColumn(connection, transaction, "Notes", "Baslik", "Title");
        TryRenameColumn(connection, transaction, "Notes", "Icerik", "Content");
        TryRenameColumn(connection, transaction, "Notes", "OlusturmaTarihi", "CreatedAt");
        TryRenameColumn(connection, transaction, "Notes", "GuncellenmeTarihi", "UpdatedAt");

        // Rename Units / Departments columns:
        TryRenameColumn(connection, transaction, "Units", "Ad", "Name");
        TryRenameColumn(connection, transaction, "Departments", "Ad", "Name");

        // Rename Users columns:
        TryRenameColumn(connection, transaction, "Users", "KullaniciAdi", "Username");
        TryRenameColumn(connection, transaction, "Users", "SifreHash", "PasswordHash");
        TryRenameColumn(connection, transaction, "Users", "Tuz", "Salt");
        TryRenameColumn(connection, transaction, "Users", "Rol", "Role");

        // Rename ServiceRecords columns:
        TryRenameColumn(connection, transaction, "ServiceRecords", "CihazAdi", "DeviceName");
        TryRenameColumn(connection, transaction, "ServiceRecords", "SeriNumarasi", "SerialNumber");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Firma", "Company");
        TryRenameColumn(connection, transaction, "ServiceRecords", "BakimTarihi", "ServiceDate");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Aciklama", "Description");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Sorun", "Issue");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Sonuc", "Result");

        // Rename AuditLog columns:
        TryRenameColumn(connection, transaction, "AuditLog", "Tarih", "Date");
        TryRenameColumn(connection, transaction, "AuditLog", "IslemTipi", "Action");
        TryRenameColumn(connection, transaction, "AuditLog", "TabloAdi", "TableName");
        TryRenameColumn(connection, transaction, "AuditLog", "KayitId", "RecordId");
        TryRenameColumn(connection, transaction, "AuditLog", "Detay", "Details");

        // Normalize legacy value strings to English
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Entry' WHERE Type IN ('Giris', 'Giriş')");
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Exit' WHERE Type IN ('Cikis', 'Çıkış')");
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Blank' WHERE Type='Bos'");
        
        TryAlter(connection, transaction, "UPDATE StockCards SET CardType='Child' WHERE CardType='Alt'");
        TryAlter(connection, transaction, "UPDATE StockCards SET CardType='Parent' WHERE CardType IN ('Ust', 'Üst')");
        
        // Clean up legacy indexes to prevent duplicate index names on different columns
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_Hareket_StokKartId");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_Hareket_Tarih");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_StokKart_KodNo");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_StokKart_KartTipi");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS UX_StokKartlari_KodNo");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_ServisKayit_Tarih");
    }

    private static void MigrateToV13(SqliteConnection connection, SqliteTransaction? transaction)
    {
        // Recovery mechanism for failed V12 migration where empty English tables were created before rename could happen.
        // If a Turkish table exists, we check if the corresponding English table has 0 rows.
        // If it has 0 rows, we drop it so the Turkish table can be renamed to it.
        string[,] tableMapping = {
            { "StokKartlari", "StockCards" },
            { "StokHareketleri", "StockMovements" },
            { "Notlar", "Notes" },
            { "Birimler", "Units" },
            { "Departmanlar", "Departments" },
            { "Kullanicilar", "Users" },
            { "ServisKayitlari", "ServiceRecords" }
        };

        for (int i = 0; i < tableMapping.GetLength(0); i++)
        {
            string oldTable = tableMapping[i, 0];
            string newTable = tableMapping[i, 1];

            if (TableExists(connection, transaction, oldTable))
            {
                bool dropEmptyNewTable = false;
                if (TableExists(connection, transaction, newTable))
                {
                    using var cmdCount = CreateCommand(connection, transaction, $"SELECT COUNT(*) FROM [{newTable}]");
                    try
                    {
                        long count = Convert.ToInt64(cmdCount.ExecuteScalar() ?? 0);
                        if (count == 0)
                        {
                            dropEmptyNewTable = true;
                        }
                    }
                    catch
                    {
                        dropEmptyNewTable = true;
                    }
                }

                if (dropEmptyNewTable)
                {
                    TryAlter(connection, transaction, $"DROP TABLE IF EXISTS [{newTable}];");
                }

                if (!TableExists(connection, transaction, newTable))
                {
                    TryAlter(connection, transaction, $"ALTER TABLE [{oldTable}] RENAME TO [{newTable}];");
                }
            }
        }

        // Rename columns for StockCards
        TryRenameColumn(connection, transaction, "StockCards", "Ad", "Name");
        TryRenameColumn(connection, transaction, "StockCards", "KodNo", "Code");
        TryRenameColumn(connection, transaction, "StockCards", "Aciklama", "Description");
        TryRenameColumn(connection, transaction, "StockCards", "MinStok", "MinStock");
        TryRenameColumn(connection, transaction, "StockCards", "Kategori", "Category");
        TryRenameColumn(connection, transaction, "StockCards", "KartTipi", "CardType");
        TryRenameColumn(connection, transaction, "StockCards", "UstKartId", "ParentId");
        TryRenameColumn(connection, transaction, "StockCards", "OlusturmaTarihi", "CreatedAt");
        TryRenameColumn(connection, transaction, "StockCards", "GuncellenmeTarihi", "UpdatedAt");
        TryRenameColumn(connection, transaction, "StockCards", "Birim", "Unit");
        TryRenameColumn(connection, transaction, "StockCards", "Konum", "Location");
        TryRenameColumn(connection, transaction, "StockCards", "Tedarikci", "Supplier");
        TryRenameColumn(connection, transaction, "StockCards", "Barkod", "Barcode");
        TryRenameColumn(connection, transaction, "StockCards", "BirimFiyat", "UnitPrice");

        // Rename columns for StockMovements
        TryRenameColumn(connection, transaction, "StockMovements", "StokKartId", "StockCardId");
        TryRenameColumn(connection, transaction, "StockMovements", "Tur", "Type");
        TryRenameColumn(connection, transaction, "StockMovements", "Miktar", "Quantity");
        TryRenameColumn(connection, transaction, "StockMovements", "KimeVerildi", "Recipient");
        TryRenameColumn(connection, transaction, "StockMovements", "Departman", "Department");
        TryRenameColumn(connection, transaction, "StockMovements", "Tarih", "Date");
        TryRenameColumn(connection, transaction, "StockMovements", "Aciklama", "Description");

        // Rename columns for Notes
        TryRenameColumn(connection, transaction, "Notes", "Tarih", "Date");
        TryRenameColumn(connection, transaction, "Notes", "Baslik", "Title");
        TryRenameColumn(connection, transaction, "Notes", "Icerik", "Content");
        TryRenameColumn(connection, transaction, "Notes", "OlusturmaTarihi", "CreatedAt");
        TryRenameColumn(connection, transaction, "Notes", "GuncellenmeTarihi", "UpdatedAt");

        // Rename columns for Units / Departments
        TryRenameColumn(connection, transaction, "Units", "Ad", "Name");
        TryRenameColumn(connection, transaction, "Departments", "Ad", "Name");

        // Rename columns for Users
        TryRenameColumn(connection, transaction, "Users", "KullaniciAdi", "Username");
        TryRenameColumn(connection, transaction, "Users", "SifreHash", "PasswordHash");
        TryRenameColumn(connection, transaction, "Users", "Tuz", "Salt");
        TryRenameColumn(connection, transaction, "Users", "Rol", "Role");

        // Rename columns for ServiceRecords
        TryRenameColumn(connection, transaction, "ServiceRecords", "CihazAdi", "DeviceName");
        TryRenameColumn(connection, transaction, "ServiceRecords", "SeriNumarasi", "SerialNumber");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Firma", "Company");
        TryRenameColumn(connection, transaction, "ServiceRecords", "BakimTarihi", "ServiceDate");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Aciklama", "Description");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Sorun", "Issue");
        TryRenameColumn(connection, transaction, "ServiceRecords", "Sonuc", "Result");

        // Rename columns for AuditLog
        TryRenameColumn(connection, transaction, "AuditLog", "Tarih", "Date");
        TryRenameColumn(connection, transaction, "AuditLog", "IslemTipi", "Action");
        TryRenameColumn(connection, transaction, "AuditLog", "TabloAdi", "TableName");
        TryRenameColumn(connection, transaction, "AuditLog", "KayitId", "RecordId");
        TryRenameColumn(connection, transaction, "AuditLog", "Detay", "Details");

        // Normalize legacy value strings to English
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Entry' WHERE Type IN ('Giris', 'Giriş')");
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Exit' WHERE Type IN ('Cikis', 'Çıkış')");
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Blank' WHERE Type='Bos'");
        
        TryAlter(connection, transaction, "UPDATE StockCards SET CardType='Child' WHERE CardType='Alt'");
        TryAlter(connection, transaction, "UPDATE StockCards SET CardType='Parent' WHERE CardType IN ('Ust', 'Üst')");

        // Clean up legacy indexes to prevent duplicate index names on different columns
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_Hareket_StokKartId");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_Hareket_Tarih");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_StokKart_KodNo");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_StokKart_KartTipi");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS UX_StokKartlari_KodNo");
        TryAlter(connection, transaction, "DROP INDEX IF EXISTS IX_ServisKayit_Tarih");
    }

    private static bool TableExists(SqliteConnection connection, SqliteTransaction? transaction, string tableName)
    {
        using var cmd = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n");
        cmd.Parameters.AddWithValue("$n", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
    }

    private static void TryRenameColumn(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string oldCol, string newCol)
    {
        if (ColumnExists(connection, transaction, tableName, oldCol) && !ColumnExists(connection, transaction, tableName, newCol))
        {
            TryAlter(connection, transaction, $"ALTER TABLE {tableName} RENAME COLUMN {oldCol} TO {newCol};");
        }
    }

    private static void EnsureIndexes(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Movements_StockCardId_Date ON StockMovements(StockCardId, Date DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Movements_Department_Date ON StockMovements(Department, Date DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Notes_Date ON Notes(Date DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StockCards_ParentId ON StockCards(ParentId)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ServiceRecords_Date_Id ON ServiceRecords(ServiceDate DESC, Id DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_AppConfig_Key ON AppConfig(Key)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StockCards_Code ON StockCards(Code)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StockCards_CardType ON StockCards(CardType)");
        TryAlter(connection, transaction, "CREATE UNIQUE INDEX IF NOT EXISTS UX_StockCards_Code ON StockCards(Code COLLATE NOCASE)");
    }

    private static void NormalizeLegacyData(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Entry' WHERE Type IN ('Giris', 'Giriş', 'entry')");
        TryAlter(connection, transaction, "UPDATE StockMovements SET Type='Exit' WHERE Type IN ('Cikis', 'Çıkış', 'exit')");
        TryAlter(connection, transaction, "UPDATE StockCards SET CardType='Child' WHERE CardType IN ('Alt', 'child')");
        TryAlter(connection, transaction, "UPDATE StockCards SET CardType='Parent' WHERE CardType IN ('Ust', 'Üst', 'parent')");
    }

    private static void ValidateDatabase(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = CreateCommand(connection, transaction, "PRAGMA quick_check(1);");
        string result = command.ExecuteScalar()?.ToString() ?? "ok";
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L("db_integrity_failed", result));
    }

    private static void SeedDefaults(SqliteConnection connection, SqliteTransaction? transaction)
    {
        try
        {
            using var unitsCount = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM Units");
            if (Convert.ToInt64(unitsCount.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
            {
                using var insertUnit = CreateCommand(connection, transaction, "INSERT INTO Units (Name) VALUES ('Adet')");
                insertUnit.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Seed Units error: " + ex);
        }

        try
        {
            using var userCount = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM Users");
            if (Convert.ToInt64(userCount.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
            {
                string salt = GenerateSalt();
                string hash = HashPasswordV3("admin", salt);
                using var insertUser = CreateCommand(connection, transaction,
                    "INSERT INTO Users (Username,PasswordHash,Salt,Role) VALUES ('admin',$h,$s,'admin')");
                insertUser.Parameters.AddWithValue("$h", hash);
                insertUser.Parameters.AddWithValue("$s", salt);
                insertUser.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Seed Users error: " + ex);
        }
    }

    private static string GenerateSalt()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(salt);
    }

    private static string HashPasswordV3(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, PasswordIterationsV3, HashAlgorithmName.SHA512);
        byte[] hash = pbkdf2.GetBytes(PasswordHashSize);
        return "v3:" + Convert.ToBase64String(hash);
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction != null)
            command.Transaction = transaction;
        return command;
    }
}
