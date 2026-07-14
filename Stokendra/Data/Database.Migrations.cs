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
        // No-op: V12 migration logic is now folded into the much more robust V13 migration
        // to prevent partial migration errors and foreign key constraint failures.
    }

    private static void MigrateToV13(SqliteConnection connection, SqliteTransaction? transaction)
    {
        string? GetSourceTable(string trName, string enName)
        {
            if (TableExists(connection, transaction, trName))
            {
                if (TableExists(connection, transaction, enName))
                {
                    using var cmdOld = CreateCommand(connection, transaction, $"SELECT COUNT(*) FROM [{trName}]");
                    using var cmdNew = CreateCommand(connection, transaction, $"SELECT COUNT(*) FROM [{enName}]");
                    long oldCnt = Convert.ToInt64(cmdOld.ExecuteScalar() ?? 0);
                    long newCnt = Convert.ToInt64(cmdNew.ExecuteScalar() ?? 0);
                    return (oldCnt >= newCnt) ? trName : enName;
                }
                return trName;
            }
            return TableExists(connection, transaction, enName) ? enName : null;
        }

        // 1. StockCards
        var srcCards = GetSourceTable("StokKartlari", "StockCards");
        if (srcCards != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS StockCards_temp;");
            TryAlter(connection, transaction, @"
                CREATE TABLE StockCards_temp (
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
                )
            ");

            string colId = GetSelectColumn(connection, transaction, srcCards, "Id", "Id");
            string colName = GetSelectColumn(connection, transaction, srcCards, "Name", "Ad", "Name");
            string colCode = GetSelectColumn(connection, transaction, srcCards, "Code", "KodNo", "Code");
            string colDesc = GetSelectColumn(connection, transaction, srcCards, "Description", "Aciklama", "Description");
            string colMinStock = GetSelectColumn(connection, transaction, srcCards, "MinStock", "MinStok", "MinStock");
            string colCategory = GetSelectColumn(connection, transaction, srcCards, "Category", "Kategori", "Category");
            string colCardType = GetSelectColumn(connection, transaction, srcCards, "CardType", "KartTipi", "CardType");
            string colParentId = GetSelectColumn(connection, transaction, srcCards, "ParentId", "UstKartId", "ParentId");
            string colCreatedAt = GetSelectColumn(connection, transaction, srcCards, "CreatedAt", "OlusturmaTarihi", "CreatedAt");
            string colUpdatedAt = GetSelectColumn(connection, transaction, srcCards, "UpdatedAt", "GuncellenmeTarihi", "UpdatedAt");
            string colUnit = GetSelectColumn(connection, transaction, srcCards, "Unit", "Birim", "Unit");
            string colLocation = GetSelectColumn(connection, transaction, srcCards, "Location", "Konum", "Location");
            string colSupplier = GetSelectColumn(connection, transaction, srcCards, "Supplier", "Tedarikci", "Supplier");
            string colBarcode = GetSelectColumn(connection, transaction, srcCards, "Barcode", "Barkod", "Barcode");
            string colUnitPrice = GetSelectColumn(connection, transaction, srcCards, "UnitPrice", "BirimFiyat", "UnitPrice");

            string exprCardType = $"CASE WHEN {colCardType} IN ('Alt', 'child', 'Child') THEN 'Child' WHEN {colCardType} IN ('Ust', 'Üst', 'parent', 'Parent') THEN 'Parent' ELSE 'Child' END";

            string insertSql = $@"
                INSERT INTO StockCards_temp (Id, Name, Code, Description, MinStock, Category, CardType, ParentId, CreatedAt, UpdatedAt, Unit, Location, Supplier, Barcode, UnitPrice)
                SELECT {colId}, COALESCE({colName}, ''), COALESCE({colCode}, ''), COALESCE({colDesc}, ''), COALESCE({colMinStock}, 0), COALESCE({colCategory}, ''), {exprCardType}, {colParentId}, COALESCE({colCreatedAt}, ''), COALESCE({colUpdatedAt}, ''), COALESCE({colUnit}, 'Adet'), COALESCE({colLocation}, ''), COALESCE({colSupplier}, ''), COALESCE({colBarcode}, ''), COALESCE({colUnitPrice}, 0.0)
                FROM [{srcCards}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // 2. StockMovements
        var srcMovements = GetSourceTable("StokHareketleri", "StockMovements");
        if (srcMovements != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS StockMovements_temp;");
            TryAlter(connection, transaction, @"
                CREATE TABLE StockMovements_temp (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    StockCardId INTEGER NOT NULL,
                    Type TEXT NOT NULL,
                    Quantity REAL NOT NULL,
                    Recipient TEXT DEFAULT '',
                    Department TEXT DEFAULT '',
                    Date TEXT NOT NULL,
                    Description TEXT DEFAULT ''
                )
            ");

            string colId = GetSelectColumn(connection, transaction, srcMovements, "Id", "Id");
            string colCardId = GetSelectColumn(connection, transaction, srcMovements, "StockCardId", "StokKartId", "StockCardId");
            string colType = GetSelectColumn(connection, transaction, srcMovements, "Type", "Tur", "Type");
            string colQty = GetSelectColumn(connection, transaction, srcMovements, "Quantity", "Miktar", "Quantity");
            string colRecipient = GetSelectColumn(connection, transaction, srcMovements, "Recipient", "KimeVerildi", "Recipient");
            string colDept = GetSelectColumn(connection, transaction, srcMovements, "Department", "Departman", "Department");
            string colDate = GetSelectColumn(connection, transaction, srcMovements, "Date", "Tarih", "Date");
            string colDesc = GetSelectColumn(connection, transaction, srcMovements, "Description", "Aciklama", "Description");

            string exprType = $"CASE WHEN {colType} IN ('Giris', 'Giriş', 'entry', 'Entry') THEN 'Entry' WHEN {colType} IN ('Cikis', 'Çıkış', 'exit', 'Exit') THEN 'Exit' ELSE 'Blank' END";

            string insertSql = $@"
                INSERT INTO StockMovements_temp (Id, StockCardId, Type, Quantity, Recipient, Department, Date, Description)
                SELECT {colId}, {colCardId}, {exprType}, COALESCE({colQty}, 0.0), COALESCE({colRecipient}, ''), COALESCE({colDept}, ''), COALESCE({colDate}, datetime('now')), COALESCE({colDesc}, '')
                FROM [{srcMovements}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // 3. Notes
        var srcNotes = GetSourceTable("Notlar", "Notes");
        if (srcNotes != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS Notes_temp;");
            TryAlter(connection, transaction, "CREATE TABLE Notes_temp (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT NOT NULL, Title TEXT NOT NULL, Content TEXT DEFAULT '', CreatedAt TEXT DEFAULT '', UpdatedAt TEXT DEFAULT '')");

            string colId = GetSelectColumn(connection, transaction, srcNotes, "Id", "Id");
            string colDate = GetSelectColumn(connection, transaction, srcNotes, "Date", "Tarih", "Date");
            string colTitle = GetSelectColumn(connection, transaction, srcNotes, "Title", "Baslik", "Title");
            string colContent = GetSelectColumn(connection, transaction, srcNotes, "Content", "Icerik", "Content");
            string colCreatedAt = GetSelectColumn(connection, transaction, srcNotes, "CreatedAt", "OlusturmaTarihi", "CreatedAt");
            string colUpdatedAt = GetSelectColumn(connection, transaction, srcNotes, "UpdatedAt", "GuncellenmeTarihi", "UpdatedAt");

            string insertSql = $@"
                INSERT INTO Notes_temp (Id, Date, Title, Content, CreatedAt, UpdatedAt)
                SELECT {colId}, COALESCE({colDate}, datetime('now')), COALESCE({colTitle}, ''), COALESCE({colContent}, ''), COALESCE({colCreatedAt}, ''), COALESCE({colUpdatedAt}, '')
                FROM [{srcNotes}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // 4. Units
        var srcUnits = GetSourceTable("Birimler", "Units");
        if (srcUnits != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS Units_temp;");
            TryAlter(connection, transaction, "CREATE TABLE Units_temp (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE)");

            string colId = GetSelectColumn(connection, transaction, srcUnits, "Id", "Id");
            string colName = GetSelectColumn(connection, transaction, srcUnits, "Name", "Ad", "Name");

            string insertSql = $@"
                INSERT OR IGNORE INTO Units_temp (Id, Name)
                SELECT {colId}, COALESCE({colName}, '') FROM [{srcUnits}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // 5. Departments
        var srcDepts = GetSourceTable("Departmanlar", "Departments");
        if (srcDepts != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS Departments_temp;");
            TryAlter(connection, transaction, "CREATE TABLE Departments_temp (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE)");

            string colId = GetSelectColumn(connection, transaction, srcDepts, "Id", "Id");
            string colName = GetSelectColumn(connection, transaction, srcDepts, "Name", "Ad", "Name");

            string insertSql = $@"
                INSERT OR IGNORE INTO Departments_temp (Id, Name)
                SELECT {colId}, COALESCE({colName}, '') FROM [{srcDepts}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // 6. Users
        var srcUsers = GetSourceTable("Kullanicilar", "Users");
        if (srcUsers != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS Users_temp;");
            TryAlter(connection, transaction, "CREATE TABLE Users_temp (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL UNIQUE, PasswordHash TEXT NOT NULL, Salt TEXT NOT NULL, Role TEXT DEFAULT 'admin')");

            string colId = GetSelectColumn(connection, transaction, srcUsers, "Id", "Id");
            string colUser = GetSelectColumn(connection, transaction, srcUsers, "Username", "KullaniciAdi", "Username");
            string colHash = GetSelectColumn(connection, transaction, srcUsers, "PasswordHash", "SifreHash", "PasswordHash");
            string colSalt = GetSelectColumn(connection, transaction, srcUsers, "Salt", "Tuz", "Salt");
            string colRole = GetSelectColumn(connection, transaction, srcUsers, "Role", "Rol", "Role");

            string insertSql = $@"
                INSERT OR IGNORE INTO Users_temp (Id, Username, PasswordHash, Salt, Role)
                SELECT {colId}, COALESCE({colUser}, ''), COALESCE({colHash}, ''), COALESCE({colSalt}, ''), COALESCE({colRole}, 'admin') FROM [{srcUsers}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // 7. ServiceRecords
        var srcService = GetSourceTable("ServisKayitlari", "ServiceRecords");
        if (srcService != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS ServiceRecords_temp;");
            TryAlter(connection, transaction, @"
                CREATE TABLE ServiceRecords_temp (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceName TEXT NOT NULL,
                    SerialNumber TEXT DEFAULT '',
                    Company TEXT DEFAULT '',
                    ServiceDate TEXT NOT NULL,
                    Description TEXT DEFAULT '',
                    Issue TEXT DEFAULT '',
                    Result TEXT DEFAULT ''
                )
            ");

            string colId = GetSelectColumn(connection, transaction, srcService, "Id", "Id");
            string colDev = GetSelectColumn(connection, transaction, srcService, "DeviceName", "CihazAdi", "DeviceName");
            string colSer = GetSelectColumn(connection, transaction, srcService, "SerialNumber", "SeriNumarasi", "SerialNumber");
            string colComp = GetSelectColumn(connection, transaction, srcService, "Company", "Firma", "Company");
            string colDate = GetSelectColumn(connection, transaction, srcService, "ServiceDate", "BakimTarihi", "ServiceDate");
            string colDesc = GetSelectColumn(connection, transaction, srcService, "Description", "Aciklama", "Description");
            string colIssue = GetSelectColumn(connection, transaction, srcService, "Issue", "Sorun", "Issue");
            string colResult = GetSelectColumn(connection, transaction, srcService, "Result", "Sonuc", "Result");

            string insertSql = $@"
                INSERT INTO ServiceRecords_temp (Id, DeviceName, SerialNumber, Company, ServiceDate, Description, Issue, Result)
                SELECT {colId}, COALESCE({colDev}, ''), COALESCE({colSer}, ''), COALESCE({colComp}, ''), COALESCE({colDate}, datetime('now')), COALESCE({colDesc}, ''), COALESCE({colIssue}, ''), COALESCE({colResult}, '')
                FROM [{srcService}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // 8. AuditLog
        var srcAudit = GetSourceTable("AuditLog", "AuditLog");
        if (srcAudit != null)
        {
            TryAlter(connection, transaction, "DROP TABLE IF EXISTS AuditLog_temp;");
            TryAlter(connection, transaction, "CREATE TABLE AuditLog_temp (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT NOT NULL, Action TEXT NOT NULL, TableName TEXT NOT NULL, RecordId INTEGER DEFAULT 0, Details TEXT DEFAULT '')");

            string colId = GetSelectColumn(connection, transaction, srcAudit, "Id", "Id");
            string colDate = GetSelectColumn(connection, transaction, srcAudit, "Date", "Tarih", "Date");
            string colAction = GetSelectColumn(connection, transaction, srcAudit, "Action", "IslemTipi", "Action");
            string colTab = GetSelectColumn(connection, transaction, srcAudit, "TableName", "TabloAdi", "TableName");
            string colRec = GetSelectColumn(connection, transaction, srcAudit, "RecordId", "KayitId", "RecordId");
            string colDetails = GetSelectColumn(connection, transaction, srcAudit, "Details", "Detay", "Details");

            string exprAction = $"CASE WHEN {colAction}='Ekle' THEN 'Insert' WHEN {colAction}='Guncelle' THEN 'Update' WHEN {colAction}='Sil' THEN 'Delete' ELSE {colAction} END";
            string exprTable = $"CASE WHEN {colTab}='StokKartlari' THEN 'StockCards' WHEN {colTab}='StokHareketleri' THEN 'StockMovements' WHEN {colTab}='Notlar' THEN 'Notes' WHEN {colTab}='Birimler' THEN 'Units' WHEN {colTab}='Departmanlar' THEN 'Departments' WHEN {colTab}='Kullanicilar' THEN 'Users' WHEN {colTab}='ServisKayitlari' THEN 'ServiceRecords' ELSE {colTab} END";

            string insertSql = $@"
                INSERT INTO AuditLog_temp (Id, Date, Action, TableName, RecordId, Details)
                SELECT {colId}, COALESCE({colDate}, datetime('now')), COALESCE({exprAction}, ''), COALESCE({exprTable}, ''), COALESCE({colRec}, 0), COALESCE({colDetails}, '')
                FROM [{srcAudit}]
            ";
            TryAlter(connection, transaction, insertSql);
        }

        // Drop both old Turkish tables and potential half-migrated English tables
        string[] tablesToDrop = {
            "StokKartlari", "StockCards",
            "StokHareketleri", "StockMovements",
            "Notlar", "Notes",
            "Birimler", "Units",
            "Departmanlar", "Departments",
            "Kullanicilar", "Users",
            "ServisKayitlari", "ServiceRecords",
            "AuditLog"
        };

        foreach (var t in tablesToDrop)
        {
            TryAlter(connection, transaction, $"DROP TABLE IF EXISTS [{t}];");
        }

        // Rename temp tables to final names
        void TryRenameTable(string tempName, string finalName)
        {
            if (TableExists(connection, transaction, tempName))
            {
                TryAlter(connection, transaction, $"ALTER TABLE [{tempName}] RENAME TO [{finalName}];");
            }
        }

        TryRenameTable("StockCards_temp", "StockCards");
        TryRenameTable("StockMovements_temp", "StockMovements");
        TryRenameTable("Notes_temp", "Notes");
        TryRenameTable("Units_temp", "Units");
        TryRenameTable("Departments_temp", "Departments");
        TryRenameTable("Users_temp", "Users");
        TryRenameTable("ServiceRecords_temp", "ServiceRecords");
        TryRenameTable("AuditLog_temp", "AuditLog");
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

    private static string GetSelectColumn(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string targetCol, string sourceCol1, string? sourceCol2 = null)
    {
        if (ColumnExists(connection, transaction, tableName, sourceCol1))
            return $"[{sourceCol1}]";
        if (sourceCol2 != null && ColumnExists(connection, transaction, tableName, sourceCol2))
            return $"[{sourceCol2}]";
        if (ColumnExists(connection, transaction, tableName, targetCol))
            return $"[{targetCol}]";
        return "NULL";
    }

    private static void EnsureIndexes(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Movements_StockCardId_Date ON StockMovements(StockCardId, Date DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Movements_Department_Date ON StockMovements(Department, Date DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Notes_Date ON Notes(Date DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StockCards_ParentId ON StockCards(ParentId)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ServiceRecords_Date_Id ON ServiceRecords(ServiceDate DESC, Id DESC)");
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

        // Validate foreign key constraint integrity
        using var commandFk = CreateCommand(connection, transaction, "PRAGMA foreign_key_check;");
        using var reader = commandFk.ExecuteReader();
        if (reader.Read())
        {
            var table = reader.GetString(0);
            var rowid = reader.GetInt64(1);
            var targetTable = reader.GetString(2);
            throw new InvalidOperationException($"Foreign key constraint violation in table '{table}' at row {rowid} referencing '{targetTable}'");
        }
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
                string hash = HashPasswordV4("admin", salt);
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

        try
        {
            var settings = AppSettings.Load();
            string[] keys = { "CompanyName", "AutoBackupPath", "DbPath", "LastSettingsUpdated" };
            
            foreach (var key in keys)
            {
                using var checkCmd = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM AppConfig WHERE Key=$k");
                checkCmd.Parameters.AddWithValue("$k", key);
                long count = Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0);
                
                if (count == 0)
                {
                    string value = key switch
                    {
                        "CompanyName" => settings.CompanyName ?? "",
                        "AutoBackupPath" => settings.AutoBackupPath ?? "",
                        "DbPath" => settings.DbPath ?? "",
                        "LastSettingsUpdated" => DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                        _ => ""
                    };
                    
                    using var insertCmd = CreateCommand(connection, transaction, "INSERT INTO AppConfig (Key, Value) VALUES ($k, $v)");
                    insertCmd.Parameters.AddWithValue("$k", key);
                    insertCmd.Parameters.AddWithValue("$v", value);
                    insertCmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Seed AppConfig error: " + ex);
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

    private static string HashPasswordV4(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 600000, HashAlgorithmName.SHA512);
        byte[] hash = pbkdf2.GetBytes(PasswordHashSize);
        return "v4:" + Convert.ToBase64String(hash);
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
