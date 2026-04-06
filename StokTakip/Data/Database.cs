using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

public sealed partial class Database : IDisposable
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

    private static void EnsureSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = CreateCommand(connection, transaction, @"
            CREATE TABLE IF NOT EXISTS StokKartlari (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ad TEXT NOT NULL CHECK (trim(Ad) <> ''),
                KodNo TEXT NOT NULL COLLATE NOCASE CHECK (trim(KodNo) <> ''),
                Aciklama TEXT DEFAULT '',
                MinStok INTEGER DEFAULT 0 CHECK (MinStok >= 0),
                Kategori TEXT DEFAULT '',
                KartTipi TEXT NOT NULL DEFAULT 'Alt' CHECK (KartTipi IN ('Alt','Ust')),
                UstKartId INTEGER DEFAULT NULL,
                OlusturmaTarihi TEXT DEFAULT '',
                GuncellenmeTarihi TEXT DEFAULT '',
                Birim TEXT DEFAULT 'Adet',
                Konum TEXT DEFAULT '',
                Tedarikci TEXT DEFAULT '',
                Barkod TEXT DEFAULT '',
                BirimFiyat REAL DEFAULT 0 CHECK (BirimFiyat >= 0)
            );

            CREATE TABLE IF NOT EXISTS StokHareketleri (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StokKartId INTEGER NOT NULL REFERENCES StokKartlari(Id) ON DELETE CASCADE,
                Tur TEXT NOT NULL CHECK (Tur IN ('Giris','Cikis','Bos')),
                Miktar REAL NOT NULL CHECK (Miktar >= 0),
                KimeVerildi TEXT DEFAULT '',
                Departman TEXT DEFAULT '',
                Tarih TEXT NOT NULL,
                Aciklama TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Notlar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Tarih TEXT NOT NULL,
                Baslik TEXT NOT NULL CHECK (trim(Baslik) <> ''),
                Icerik TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Birimler (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ad TEXT NOT NULL UNIQUE CHECK (trim(Ad) <> '')
            );

            CREATE TABLE IF NOT EXISTS Departmanlar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ad TEXT NOT NULL UNIQUE CHECK (trim(Ad) <> '')
            );

            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Tarih TEXT NOT NULL,
                IslemTipi TEXT NOT NULL,
                TabloAdi TEXT NOT NULL,
                KayitId INTEGER DEFAULT 0,
                Detay TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS AppConfig (
                Key TEXT PRIMARY KEY,
                Value TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Kullanicilar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                KullaniciAdi TEXT NOT NULL UNIQUE CHECK (trim(KullaniciAdi) <> ''),
                SifreHash TEXT NOT NULL,
                Tuz TEXT NOT NULL,
                Rol TEXT DEFAULT 'admin'
            );

            CREATE TABLE IF NOT EXISTS ServisKayitlari (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CihazAdi TEXT NOT NULL CHECK (trim(CihazAdi) <> ''),
                SeriNumarasi TEXT DEFAULT '',
                Firma TEXT DEFAULT '',
                BakimTarihi TEXT NOT NULL,
                Aciklama TEXT DEFAULT '',
                Sorun TEXT DEFAULT '',
                Sonuc TEXT DEFAULT ''
            );
        ");
        command.ExecuteNonQuery();
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
            System.Diagnostics.Debug.WriteLine("TryAlter error: " + ex);
        }
    }

    private static void MigrateToV1(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Aciklama TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokHareketleri ADD COLUMN Departman TEXT DEFAULT ''");
    }

    private static void MigrateToV2(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Birim TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN MinStok INTEGER DEFAULT 0");
    }

    private static void MigrateToV3(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Kategori TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Konum TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Tedarikci TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Barkod TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN BirimFiyat REAL DEFAULT 0");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN OlusturmaTarihi TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN GuncellenmeTarihi TEXT DEFAULT ''");
    }

    private static void MigrateToV4(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN KartTipi TEXT DEFAULT 'Alt'");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN UstKartId INTEGER DEFAULT NULL");
    }

    private static void MigrateToV5(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "UPDATE StokKartlari SET KartTipi='Alt' WHERE KartTipi IS NULL OR trim(KartTipi)=''");
    }

    private static void MigrateToV6(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_StokKartId ON StokHareketleri(StokKartId)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_Tarih ON StokHareketleri(Tarih)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StokKart_KodNo ON StokKartlari(KodNo)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StokKart_KartTipi ON StokKartlari(KartTipi)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_AuditLog_Tarih ON AuditLog(Tarih)");
    }

    private static void MigrateToV7(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, @"CREATE TABLE IF NOT EXISTS ServisKayitlari (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CihazAdi TEXT NOT NULL,
            SeriNumarasi TEXT DEFAULT '',
            BakimTarihi TEXT NOT NULL,
            Aciklama TEXT DEFAULT ''
        )");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ServisKayit_Tarih ON ServisKayitlari(BakimTarihi)");
    }

    private static void MigrateToV8(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE ServisKayitlari ADD COLUMN Firma TEXT DEFAULT ''");
    }

    private static void MigrateToV9(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE ServisKayitlari ADD COLUMN Sorun TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE ServisKayitlari ADD COLUMN Sonuc TEXT DEFAULT ''");
    }

    private static void MigrateToV10(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "UPDATE StokKartlari SET Birim='Adet' WHERE Birim IS NULL OR trim(Birim)=''");
        TryAlter(connection, transaction, "UPDATE StokKartlari SET MinStok=0 WHERE MinStok < 0");
        TryAlter(connection, transaction, "UPDATE StokKartlari SET BirimFiyat=0 WHERE BirimFiyat < 0");
        TryAlter(connection, transaction, "CREATE UNIQUE INDEX IF NOT EXISTS UX_StokKartlari_KodNo ON StokKartlari(KodNo COLLATE NOCASE)");
    }

    private static void EnsureIndexes(SqliteConnection connection, SqliteTransaction transaction)
    {
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_StokKartId_Tarih ON StokHareketleri(StokKartId, Tarih DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_Departman_Tarih ON StokHareketleri(Departman, Tarih DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Notlar_Tarih ON Notlar(Tarih DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StokKart_UstKartId ON StokKartlari(UstKartId)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ServisKayit_Tarih_Id ON ServisKayitlari(BakimTarihi DESC, Id DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_AppConfig_Key ON AppConfig(Key)");
    }

    private static void ValidateDatabase(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = CreateCommand(connection, transaction, "PRAGMA quick_check(1);");
        string result = command.ExecuteScalar()?.ToString() ?? "ok";
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L("db_integrity_failed", result));
    }

    private static void SeedDefaults(SqliteConnection connection, SqliteTransaction transaction)
    {
        try
        {
            using var unitsCount = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM Birimler");
            if (Convert.ToInt64(unitsCount.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
            {
                using var insertUnit = CreateCommand(connection, transaction, "INSERT INTO Birimler (Ad) VALUES ('Adet')");
                insertUnit.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Seed Birimler error: " + ex);
        }

        try
        {
            using var userCount = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM Kullanicilar");
            if (Convert.ToInt64(userCount.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
            {
                string salt = GenerateSalt();
                string hash = HashPasswordV3("admin", salt);
                using var insertUser = CreateCommand(connection, transaction,
                    "INSERT INTO Kullanicilar (KullaniciAdi,SifreHash,Tuz,Rol) VALUES ('admin',$h,$s,'admin')");
                insertUser.Parameters.AddWithValue("$h", hash);
                insertUser.Parameters.AddWithValue("$s", salt);
                insertUser.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Seed Kullanicilar error: " + ex);
        }
    }

    public string GetConfig(string key, string def = "")
    {
        string normalizedKey = key?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return def;

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Value FROM AppConfig WHERE Key=$k");
            command.Parameters.AddWithValue("$k", normalizedKey);
            return command.ExecuteScalar()?.ToString() ?? def;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("GetConfig error: " + ex);
            return def;
        }
    }

    public void SetConfig(string key, string val)
    {
        string normalizedKey = key?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new InvalidOperationException(L("config_key_required"));

        try
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();
            using var command = CreateCommand(connection, transaction,
                "INSERT INTO AppConfig (Key,Value) VALUES ($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value");
            command.Parameters.AddWithValue("$k", normalizedKey);
            command.Parameters.AddWithValue("$v", val ?? "");
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SetConfig error: " + ex);
            throw;
        }
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
            System.Diagnostics.Debug.WriteLine("AuditLogYaz error: " + ex);
        }
    }

    private static void InsertAuditLog(SqliteConnection connection, SqliteTransaction? transaction, string tip, string tablo, int id, string detay)
    {
        using var command = CreateCommand(connection, transaction,
            "INSERT INTO AuditLog (Tarih,IslemTipi,TabloAdi,KayitId,Detay) VALUES ($t,$it,$ta,$ki,$d)");
        command.Parameters.AddWithValue("$t", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$it", TrimTo(tip, 80));
        command.Parameters.AddWithValue("$ta", TrimTo(tablo, 80));
        command.Parameters.AddWithValue("$ki", id);
        command.Parameters.AddWithValue("$d", TrimTo(detay, 500));
        command.ExecuteNonQuery();
        
        AppPaths.LogActivity(tip.ToLowerInvariant(), tablo.ToLowerInvariant(), detay);
    }

    public List<string> DepartmanlariGetir()
    {
        var liste = new List<string>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Ad FROM Departmanlar ORDER BY Ad COLLATE NOCASE");
            using var reader = command.ExecuteReader();
            while (reader.Read())
                liste.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DepartmanlariGetir error: " + ex);
        }

        return liste;
    }

    public void DepartmanEkle(string ad)
    {
        string normalized = NormalizeRequiredText(ad, 120, L("department_required"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction, "INSERT OR IGNORE INTO Departmanlar (Ad) VALUES ($a)");
            command.Parameters.AddWithValue("$a", normalized);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DepartmanEkle error: " + ex);
            throw;
        }
    }

    public void DepartmanSil(string ad)
    {
        string normalized = NormalizeRequiredText(ad, 120, L("department_required"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction, "DELETE FROM Departmanlar WHERE Ad=$a");
            command.Parameters.AddWithValue("$a", normalized);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DepartmanSil error: " + ex);
            throw;
        }
    }

    public string SonrakiStokKodu()
    {
        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null,
                "SELECT MAX(CAST(KodNo AS INTEGER)) FROM StokKartlari WHERE KodNo GLOB '[0-9]*'");
            object? result = command.ExecuteScalar();
            int max = result != null && result != DBNull.Value
                ? Convert.ToInt32(result, CultureInfo.InvariantCulture)
                : 0;
            return (max + 1).ToString("D3", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "001";
        }
    }

    public List<StokKarti> StokKartlariniGetir() => GetStockCards();
    public List<StokKarti> UstKartlariGetir() => GetStockCards("Ust");
    public List<StokKarti> AltKartlariGetir(int? ustId = null) => GetStockCards("Alt", ustId);

    private List<StokKarti> GetStockCards(string? kartTipi = null, int? ustKartId = null, int? id = null)
    {
        var liste = new List<StokKarti>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, BuildStockCardQuery(kartTipi, ustKartId, id));

            if (!string.IsNullOrWhiteSpace(kartTipi))
                command.Parameters.AddWithValue("$kartTipi", kartTipi);
            if (ustKartId.HasValue)
                command.Parameters.AddWithValue("$ustKartId", ustKartId.Value);
            if (id.HasValue)
                command.Parameters.AddWithValue("$id", id.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
                liste.Add(ReadStockCard(reader));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartlariniGetir error: " + ex);
        }

        return liste;
    }

    public (int toplamKart, double toplamStok, int dusuk, int tukenmis, int toplamHareket, int bugunHareket) DashboardIstatistikleriGetir()
    {
        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, @"
                WITH hareket_ozet AS (
                    SELECT
                        StokKartId,
                        SUM(CASE WHEN Tur='Giris' THEN Miktar WHEN Tur='Cikis' THEN -Miktar ELSE 0 END) AS Mevcut
                    FROM StokHareketleri
                    GROUP BY StokKartId
                ),
                alt_stok AS (
                    SELECT
                        s.Id,
                        s.MinStok,
                        COALESCE(h.Mevcut, 0) AS Mevcut
                    FROM StokKartlari s
                    LEFT JOIN hareket_ozet h ON h.StokKartId = s.Id
                    WHERE COALESCE(s.KartTipi, 'Alt')='Alt'
                )
                SELECT
                    (SELECT COUNT(*) FROM alt_stok),
                    (SELECT COALESCE(SUM(CASE WHEN Mevcut > 0 THEN Mevcut ELSE 0 END), 0) FROM alt_stok),
                    (SELECT COUNT(*) FROM alt_stok WHERE Mevcut > 0 AND Mevcut <= CASE WHEN MinStok > 0 THEN MinStok ELSE 3 END),
                    (SELECT COUNT(*) FROM alt_stok WHERE Mevcut <= 0),
                    (SELECT COUNT(*) FROM StokHareketleri),
                    (SELECT COUNT(*) FROM StokHareketleri WHERE Tarih LIKE $today);");
            command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "%");

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return (
                    reader.GetInt32(0),
                    reader.GetDouble(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DashboardIstatistikleriGetir error: " + ex);
        }

        return (0, 0, 0, 0, 0, 0);
    }

    public StokKarti? StokKartiDetayGetir(int id)
    {
        return GetStockCards(id: id).FirstOrDefault();
    }

    public void StokKartiEkle(StokKarti stokKarti)
    {
        StokKarti normalized = NormalizeStokKarti(stokKarti);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            ValidateStokKarti(connection, transaction, normalized, false);

            using var command = CreateCommand(connection, transaction, @"
                INSERT INTO StokKartlari
                    (Ad, KodNo, Aciklama, MinStok, Kategori, KartTipi, UstKartId, OlusturmaTarihi, Birim, Konum, Tedarikci, Barkod, BirimFiyat)
                VALUES
                    ($a, $k, $ac, $ms, $kat, $kt, $uk, $ot, $b, $kon, $ted, $bar, $bf)");
            BindStockCardParameters(command, normalized, includeId: false);
            command.Parameters.AddWithValue("$ot", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();

            int newId = GetLastInsertRowId(connection, transaction);
            stokKarti.Id = newId;
            InsertAuditLog(connection, transaction, "EKLE", "StokKartlari", newId, $"{normalized.Ad} ({normalized.KodNo}) [{normalized.KartTipi}]");
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartiEkle error: " + ex);
            throw;
        }
    }

    public void StokKartiGuncelle(StokKarti stokKarti)
    {
        StokKarti normalized = NormalizeStokKarti(stokKarti);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            EnsureStockCardExists(connection, transaction, normalized.Id);
            ValidateStokKarti(connection, transaction, normalized, true);

            using var command = CreateCommand(connection, transaction, @"
                UPDATE StokKartlari
                SET
                    Ad=$a,
                    KodNo=$k,
                    Aciklama=$ac,
                    MinStok=$ms,
                    Kategori=$kat,
                    KartTipi=$kt,
                    UstKartId=$uk,
                    GuncellenmeTarihi=$gt,
                    Birim=$b,
                    Konum=$kon,
                    Tedarikci=$ted,
                    Barkod=$bar,
                    BirimFiyat=$bf
                WHERE Id=$id");
            BindStockCardParameters(command, normalized, includeId: true);
            command.Parameters.AddWithValue("$gt", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            InsertAuditLog(connection, transaction, "GUNCELLE", "StokKartlari", normalized.Id, $"{normalized.Ad} ({normalized.KodNo})");
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartiGuncelle error: " + ex);
            throw;
        }
    }

    public void StokKartiSil(int id)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            StokKarti mevcut = GetStockCardById(connection, transaction, id) ?? throw new InvalidOperationException(L("record_not_found"));

            using (var detachChildren = CreateCommand(connection, transaction, "UPDATE StokKartlari SET UstKartId=NULL WHERE UstKartId=$id"))
            {
                detachChildren.Parameters.AddWithValue("$id", id);
                detachChildren.ExecuteNonQuery();
            }

            using (var deleteMovements = CreateCommand(connection, transaction, "DELETE FROM StokHareketleri WHERE StokKartId=$id"))
            {
                deleteMovements.Parameters.AddWithValue("$id", id);
                deleteMovements.ExecuteNonQuery();
            }

            using (var deleteCard = CreateCommand(connection, transaction, "DELETE FROM StokKartlari WHERE Id=$id"))
            {
                deleteCard.Parameters.AddWithValue("$id", id);
                if (deleteCard.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException(L("record_not_found"));
            }

            InsertAuditLog(connection, transaction, "SIL", "StokKartlari", id, $"{mevcut.Ad} ({mevcut.KodNo})");
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartiSil error: " + ex);
            throw;
        }
    }

    public List<StokHareketi> HareketleriGetir(
        int? stokKartId = null,
        DateTime? baslangic = null,
        DateTime? bitis = null,
        string? departman = null,
        string? tur = null)
    {
        var liste = new List<StokHareketi>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, BuildMovementQuery(stokKartId, baslangic, bitis, departman, tur));

            if (stokKartId.HasValue)
                command.Parameters.AddWithValue("$kid", stokKartId.Value);
            if (baslangic.HasValue)
                command.Parameters.AddWithValue("$ts", baslangic.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (bitis.HasValue)
                command.Parameters.AddWithValue("$te", bitis.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(departman))
                command.Parameters.AddWithValue("$dep", departman.Trim());
            if (!string.IsNullOrWhiteSpace(tur))
                command.Parameters.AddWithValue("$tur", tur.Trim());

            using var reader = command.ExecuteReader();
            while (reader.Read())
                liste.Add(ReadMovement(reader));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("HareketleriGetir error: " + ex);
        }

        return liste;
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
            System.Diagnostics.Debug.WriteLine("GetTeslimEdilenler error: " + ex);
        }

        return liste;
    }

    public void HareketEkle(StokHareketi hareket)
    {
        TopluHareketEkle(new[] { hareket });
    }

    public void TopluHareketEkle(IEnumerable<StokHareketi> hareketler)
    {
        var liste = hareketler?.Select(NormalizeHareket).ToList() ?? new List<StokHareketi>();
        if (liste.Count == 0)
            throw new InvalidOperationException(L("bulk_operation_empty"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var hareket in liste)
            {
                ValidateHareket(connection, transaction, hareket);
                EnsureMovementWillNotCreateNegativeStock(connection, transaction, hareket.StokKartId, MovementImpact(hareket));

                using var command = CreateCommand(connection, transaction, @"
                    INSERT INTO StokHareketleri
                        (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama)
                    VALUES
                        ($sk, $t, $m, $kv, $d, $ta, $ac)");
                BindMovementParameters(command, hareket, includeId: false);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("TopluHareketEkle error: " + ex);
            throw;
        }
    }

    public void HareketGuncelle(StokHareketi hareket)
    {
        StokHareketi normalized = NormalizeHareket(hareket);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            StokHareketi mevcut = GetMovementById(connection, transaction, normalized.Id) ?? throw new InvalidOperationException(L("movement_not_found"));
            ValidateHareket(connection, transaction, normalized);

            double oldCardProjected = GetCurrentStockCore(connection, transaction, mevcut.StokKartId) - MovementImpact(mevcut);
            if (oldCardProjected < 0)
                throw new InvalidOperationException(L("stock_would_go_negative"));

            double newCardBase = mevcut.StokKartId == normalized.StokKartId
                ? oldCardProjected
                : GetCurrentStockCore(connection, transaction, normalized.StokKartId);
            double newCardProjected = newCardBase + MovementImpact(normalized);
            if (newCardProjected < 0)
                throw new InvalidOperationException(L("stock_would_go_negative"));

            using var command = CreateCommand(connection, transaction, @"
                UPDATE StokHareketleri
                SET
                    StokKartId=$sk,
                    Tur=$t,
                    Miktar=$m,
                    KimeVerildi=$kv,
                    Departman=$d,
                    Tarih=$ta,
                    Aciklama=$ac
                WHERE Id=$id");
            BindMovementParameters(command, normalized, includeId: true);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("movement_not_found"));

            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("HareketGuncelle error: " + ex);
            throw;
        }
    }

    public void HareketSil(int id)
    {
        TopluHareketSil(new[] { id });
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
            System.Diagnostics.Debug.WriteLine("TopluHareketSil error: " + ex);
            throw;
        }
    }

    public List<Birim> BirimleriGetir()
    {
        var liste = new List<Birim>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Id, Ad FROM Birimler ORDER BY Ad COLLATE NOCASE");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                liste.Add(new Birim
                {
                    Id = reader.GetInt32(0),
                    Ad = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("BirimleriGetir error: " + ex);
        }

        return liste;
    }

    public List<Not> NotlariGetir()
    {
        var liste = new List<Not>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Id, Tarih, Baslik, Icerik FROM Notlar ORDER BY Tarih DESC, Id DESC");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                liste.Add(new Not
                {
                    Id = reader.GetInt32(0),
                    Tarih = ParseDateSafe(reader.GetString(1)),
                    Baslik = reader.GetString(2),
                    Icerik = reader.IsDBNull(3) ? "" : reader.GetString(3)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("NotlariGetir error: " + ex);
        }

        return liste;
    }

    public void NotEkle(Not not)
    {
        Not normalized = NormalizeNot(not);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction,
                "INSERT INTO Notlar (Tarih, Baslik, Icerik) VALUES ($t, $b, $i)");
            command.Parameters.AddWithValue("$t", normalized.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$b", normalized.Baslik);
            command.Parameters.AddWithValue("$i", normalized.Icerik);
            command.ExecuteNonQuery();
            not.Id = GetLastInsertRowId(connection, transaction);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("NotEkle error: " + ex);
            throw;
        }
    }

    public void NotGuncelle(Not not)
    {
        Not normalized = NormalizeNot(not);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction,
                "UPDATE Notlar SET Baslik=$b, Icerik=$i, Tarih=$t WHERE Id=$id");
            command.Parameters.AddWithValue("$b", normalized.Baslik);
            command.Parameters.AddWithValue("$i", normalized.Icerik);
            command.Parameters.AddWithValue("$t", normalized.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$id", normalized.Id);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("NotGuncelle error: " + ex);
            throw;
        }
    }

    public void NotSil(int id)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction, "DELETE FROM Notlar WHERE Id=$id");
            command.Parameters.AddWithValue("$id", id);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("NotSil error: " + ex);
            throw;
        }
    }

    public List<(DateTime Tarih, double Giris, double Cikis)> Son7GunHareketOzetleri()
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

            using var reader = command.ExecuteReader();
            var dataMap = new Dictionary<DateTime, (double g, double c)>();
            while (reader.Read())
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
            System.Diagnostics.Debug.WriteLine("Son7GunHareketOzetleri error: " + ex);
        }

        return result;
    }

    public List<(string KodNo, string Ad, double ToplamGiris, double ToplamCikis, double Mevcut)> StokRaporVerisi()
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
            using var reader = command.ExecuteReader();
            while (reader.Read())
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
            System.Diagnostics.Debug.WriteLine("StokRaporVerisi error: " + ex);
        }

        return result;
    }

    public List<ServisKaydi> ServisKayitlariniGetir(DateTime? baslangic = null, DateTime? bitis = null, string? arama = null)
    {
        var liste = new List<ServisKaydi>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, BuildServiceQuery(baslangic, bitis, arama));

            if (baslangic.HasValue)
                command.Parameters.AddWithValue("$bas", baslangic.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (bitis.HasValue)
                command.Parameters.AddWithValue("$bit", bitis.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(arama))
                command.Parameters.AddWithValue("$ara", $"%{arama.Trim()}%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                liste.Add(new ServisKaydi
                {
                    Id = reader.GetInt32(0),
                    CihazAdi = reader.GetString(1),
                    SeriNumarasi = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Firma = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    BakimTarihi = ParseDateSafe(reader.GetString(4)),
                    Sorun = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Sonuc = reader.IsDBNull(6) ? "" : reader.GetString(6)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ServisKayitlariniGetir error: " + ex);
        }

        return liste;
    }

    public void ServisKaydiEkle(ServisKaydi kayit)
    {
        TopluServisKaydiEkle(new[] { kayit });
    }

    public void TopluServisKaydiEkle(IEnumerable<ServisKaydi> kayitlar)
    {
        var liste = kayitlar?.Select(NormalizeServisKaydi).ToList() ?? new List<ServisKaydi>();
        if (liste.Count == 0)
            throw new InvalidOperationException(L("bulk_operation_empty"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var kayit in liste)
            {
                ValidateServisKaydi(kayit);

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
            System.Diagnostics.Debug.WriteLine("TopluServisKaydiEkle error: " + ex);
            throw;
        }
    }

    public void ServisKaydiGuncelle(ServisKaydi kayit)
    {
        ServisKaydi normalized = NormalizeServisKaydi(kayit);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            ValidateServisKaydi(normalized);

            using var command = CreateCommand(connection, transaction, @"
                UPDATE ServisKayitlari
                SET
                    CihazAdi=$ca,
                    SeriNumarasi=$sn,
                    Firma=$f,
                    BakimTarihi=$bt,
                    Sorun=$sr,
                    Sonuc=$sc
                WHERE Id=$id");
            BindServisKaydiParameters(command, normalized, includeId: true);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            InsertAuditLog(connection, transaction, "GUNCELLE", "ServisKayitlari", normalized.Id, normalized.CihazAdi);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ServisKaydiGuncelle error: " + ex);
            throw;
        }
    }

    public void ServisKaydiSil(int id)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            ServisKaydi? mevcut = GetServisKaydiById(connection, transaction, id);
            if (mevcut == null)
                throw new InvalidOperationException(L("record_not_found"));

            using var command = CreateCommand(connection, transaction, "DELETE FROM ServisKayitlari WHERE Id=$id");
            command.Parameters.AddWithValue("$id", id);
            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            InsertAuditLog(connection, transaction, "SIL", "ServisKayitlari", id, mevcut.CihazAdi);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ServisKaydiSil error: " + ex);
            throw;
        }
    }

    public bool VarsayilanAdminSifresiKullanimda()
    {
        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT SifreHash, Tuz FROM Kullanicilar WHERE KullaniciAdi='admin'");
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return false;

            string storedHash = reader.GetString(0);
            string storedSalt = reader.GetString(1);
            return VerifyPassword("admin", storedSalt, storedHash, out _);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("VarsayilanAdminSifresiKullanimda error: " + ex);
            return false;
        }
    }

    public string? SifrePolitikasiHatasi(string sifre, string? kullaniciAdi = null)
    {
        if (string.IsNullOrWhiteSpace(sifre))
            return L("password_empty");
        if (sifre.Length < 10)
            return L("password_policy_length");
        if (sifre.Any(char.IsWhiteSpace))
            return L("password_policy_whitespace");

        int score = 0;
        if (sifre.Any(char.IsLower)) score++;
        if (sifre.Any(char.IsUpper)) score++;
        if (sifre.Any(char.IsDigit)) score++;
        if (sifre.Any(ch => !char.IsLetterOrDigit(ch))) score++;

        if (score < 3)
            return L("password_policy_complexity");

        if (!string.IsNullOrWhiteSpace(kullaniciAdi) &&
            sifre.Contains(kullaniciAdi, StringComparison.OrdinalIgnoreCase))
            return L("password_policy_username");

        return null;
    }

    public bool KullaniciDogrula(string kullaniciAdi, string sifre)
    {
        string user = kullaniciAdi?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(sifre))
            return false;

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null,
                "SELECT SifreHash, Tuz FROM Kullanicilar WHERE KullaniciAdi=$u");
            command.Parameters.AddWithValue("$u", user);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return false;

            string storedHash = reader.GetString(0);
            string storedSalt = reader.GetString(1);
            bool verified = VerifyPassword(sifre, storedSalt, storedHash, out bool needsUpgrade);
            reader.Close();

            if (verified && needsUpgrade)
                UpgradePasswordHash(user, sifre, connection);

            return verified;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("KullaniciDogrula error: " + ex);
            return false;
        }
    }

    public bool SifreDegistir(string kullaniciAdi, string eskiSifre, string yeniSifre)
    {
        string user = kullaniciAdi?.Trim() ?? "";
        string? policyError = SifrePolitikasiHatasi(yeniSifre, user);
        if (policyError != null || string.Equals(eskiSifre, yeniSifre, StringComparison.Ordinal))
            return false;
        if (!KullaniciDogrula(user, eskiSifre))
            return false;

        try
        {
            string newSalt = GenerateSalt();
            string newHash = HashPasswordV3(yeniSifre, newSalt);

            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();
            using var command = CreateCommand(connection, transaction,
                "UPDATE Kullanicilar SET SifreHash=$h, Tuz=$s WHERE KullaniciAdi=$u");
            command.Parameters.AddWithValue("$h", newHash);
            command.Parameters.AddWithValue("$s", newSalt);
            command.Parameters.AddWithValue("$u", user);

            if (command.ExecuteNonQuery() == 0)
                return false;

            InsertAuditLog(connection, transaction, "SIFRE_DEGISTIR", "Kullanicilar", 0, user);
            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SifreDegistir error: " + ex);
            return false;
        }
    }

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

    public void ExportSqlBackup(string destinationPath)
    {
        string normalizedPath = AppPaths.NormalizeWritableFilePath(destinationPath);
        var builder = new StringBuilder();

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();

        builder.AppendLine("-- Stokendra SQL Backup");
        builder.AppendLine($"-- Date: {DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture)}");
        builder.AppendLine();

        foreach (string table in SqlBackupTables)
        {
            using var schemaCommand = CreateCommand(connection, transaction,
                "SELECT sql FROM sqlite_master WHERE type='table' AND name=$name");
            schemaCommand.Parameters.AddWithValue("$name", table);
            string? createSql = schemaCommand.ExecuteScalar()?.ToString();
            if (string.IsNullOrWhiteSpace(createSql))
                continue;

            builder.AppendLine($"DROP TABLE IF EXISTS {table};");
            builder.AppendLine(createSql + ";");

            using var dataCommand = CreateCommand(connection, transaction, $"SELECT * FROM {table}");
            using var reader = dataCommand.ExecuteReader();
            while (reader.Read())
            {
                var values = new List<string>(reader.FieldCount);
                for (int i = 0; i < reader.FieldCount; i++)
                    values.Add(ToSqlLiteral(reader.GetValue(i)));

                builder.AppendLine($"INSERT INTO {table} VALUES ({string.Join(",", values)});");
            }

            builder.AppendLine();
        }

        File.WriteAllText(normalizedPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction != null)
            command.Transaction = transaction;
        return command;
    }

    private static void BindStockCardParameters(SqliteCommand command, StokKarti stokKarti, bool includeId)
    {
        command.Parameters.AddWithValue("$a", stokKarti.Ad);
        command.Parameters.AddWithValue("$k", stokKarti.KodNo);
        command.Parameters.AddWithValue("$ac", stokKarti.Aciklama);
        command.Parameters.AddWithValue("$ms", stokKarti.MinStok);
        command.Parameters.AddWithValue("$kat", stokKarti.Kategori);
        command.Parameters.AddWithValue("$kt", stokKarti.KartTipi);
        command.Parameters.AddWithValue("$uk", stokKarti.UstKartId.HasValue ? stokKarti.UstKartId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$b", stokKarti.Birim);
        command.Parameters.AddWithValue("$kon", stokKarti.Konum);
        command.Parameters.AddWithValue("$ted", stokKarti.Tedarikci);
        command.Parameters.AddWithValue("$bar", stokKarti.Barkod);
        command.Parameters.AddWithValue("$bf", stokKarti.BirimFiyat);

        if (includeId)
            command.Parameters.AddWithValue("$id", stokKarti.Id);
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

    private static StokKarti ReadStockCard(SqliteDataReader reader)
    {
        return new StokKarti
        {
            Id = reader.GetInt32(0),
            Ad = reader.GetString(1),
            KodNo = reader.GetString(2),
            Aciklama = reader.IsDBNull(3) ? "" : reader.GetString(3),
            MinStok = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            MevcutStok = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
            Kategori = reader.IsDBNull(6) ? "" : reader.GetString(6),
            KartTipi = reader.IsDBNull(7) ? "Alt" : reader.GetString(7),
            UstKartId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            UstKartAd = reader.IsDBNull(9) ? "" : reader.GetString(9),
            Birim = reader.IsDBNull(10) ? "Adet" : reader.GetString(10),
            Konum = reader.IsDBNull(11) ? "" : reader.GetString(11),
            Tedarikci = reader.IsDBNull(12) ? "" : reader.GetString(12),
            Barkod = reader.IsDBNull(13) ? "" : reader.GetString(13),
            BirimFiyat = reader.IsDBNull(14) ? 0 : reader.GetDouble(14)
        };
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

    private static string BuildStockCardQuery(string? kartTipi, int? ustKartId, int? id)
    {
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(kartTipi))
            conditions.Add("COALESCE(s.KartTipi, 'Alt') = $kartTipi");
        if (ustKartId.HasValue)
            conditions.Add("s.UstKartId = $ustKartId");
        if (id.HasValue)
            conditions.Add("s.Id = $id");

        string whereClause = conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions);

        return $@"
            WITH hareket_ozet AS (
                SELECT
                    StokKartId,
                    SUM(CASE WHEN Tur='Giris' THEN Miktar WHEN Tur='Cikis' THEN -Miktar ELSE 0 END) AS MevcutStok
                FROM StokHareketleri
                GROUP BY StokKartId
            )
            SELECT
                s.Id,
                s.Ad,
                s.KodNo,
                s.Aciklama,
                s.MinStok,
                COALESCE(h.MevcutStok, 0),
                s.Kategori,
                COALESCE(s.KartTipi, 'Alt'),
                s.UstKartId,
                COALESCE(ust.Ad, ''),
                s.Birim,
                s.Konum,
                s.Tedarikci,
                s.Barkod,
                s.BirimFiyat
            FROM StokKartlari s
            LEFT JOIN StokKartlari ust ON ust.Id = s.UstKartId
            LEFT JOIN hareket_ozet h ON h.StokKartId = s.Id
            {whereClause}
            ORDER BY s.KodNo COLLATE NOCASE, s.Id";
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

    private static StokKarti NormalizeStokKarti(StokKarti stokKarti)
    {
        string kartTipi = TrimTo(stokKarti.KartTipi, 10);
        if (!ValidCardTypes.Contains(kartTipi))
            kartTipi = "Alt";

        return new StokKarti
        {
            Id = stokKarti.Id,
            Ad = NormalizeRequiredText(stokKarti.Ad, 150, L("name_required")),
            KodNo = NormalizeRequiredText(stokKarti.KodNo, 80, L("code_required")).ToUpperInvariant(),
            Aciklama = TrimTo(stokKarti.Aciklama, 500),
            MinStok = Math.Max(0, stokKarti.MinStok),
            Kategori = TrimTo(stokKarti.Kategori, 120),
            KartTipi = kartTipi,
            UstKartId = kartTipi == "Alt" ? stokKarti.UstKartId : null,
            Birim = string.IsNullOrWhiteSpace(stokKarti.Birim) ? "Adet" : TrimTo(stokKarti.Birim, 40),
            Konum = TrimTo(stokKarti.Konum, 150),
            Tedarikci = TrimTo(stokKarti.Tedarikci, 150),
            Barkod = TrimTo(stokKarti.Barkod, 80),
            BirimFiyat = stokKarti.BirimFiyat < 0 ? 0 : stokKarti.BirimFiyat
        };
    }

    private static StokHareketi NormalizeHareket(StokHareketi hareket)
    {
        string tur = TrimTo(hareket.Tur, 10);
        if (!ValidMovementTypes.Contains(tur))
            throw new InvalidOperationException(L("movement_type_invalid"));

        double miktar = tur == "Bos" ? 0 : hareket.Miktar;

        return new StokHareketi
        {
            Id = hareket.Id,
            StokKartId = hareket.StokKartId,
            Tur = tur,
            Miktar = Math.Round(miktar, 4, MidpointRounding.AwayFromZero),
            TeslimEdilen = TrimTo(hareket.TeslimEdilen, 150),
            Departman = NormalizeRequiredText(hareket.Departman, 120, L("select_dept")),
            Tarih = hareket.Tarih == default ? DateTime.Now : hareket.Tarih,
            Aciklama = TrimTo(hareket.Aciklama, 500)
        };
    }

    private static ServisKaydi NormalizeServisKaydi(ServisKaydi kayit)
    {
        return new ServisKaydi
        {
            Id = kayit.Id,
            CihazAdi = NormalizeRequiredText(kayit.CihazAdi, 150, L("device_name_required")),
            SeriNumarasi = TrimTo(kayit.SeriNumarasi, 120),
            Firma = TrimTo(kayit.Firma, 150),
            BakimTarihi = kayit.BakimTarihi == default ? DateTime.Now : kayit.BakimTarihi,
            Sorun = TrimTo(kayit.Sorun, 500),
            Sonuc = TrimTo(kayit.Sonuc, 500)
        };
    }

    private static Not NormalizeNot(Not not)
    {
        return new Not
        {
            Id = not.Id,
            Tarih = not.Tarih == default ? DateTime.Now : not.Tarih,
            Baslik = NormalizeRequiredText(not.Baslik, 150, L("title_empty")),
            Icerik = TrimTo(not.Icerik, 4000)
        };
    }

    private static void ValidateStokKarti(SqliteConnection connection, SqliteTransaction transaction, StokKarti stokKarti, bool isUpdate)
    {
        if (stokKarti.KartTipi == "Alt" && stokKarti.UstKartId == stokKarti.Id && stokKarti.Id > 0)
            throw new InvalidOperationException(L("parent_card_self"));

        if (stokKarti.KartTipi == "Alt" && stokKarti.UstKartId.HasValue)
        {
            using var parentCommand = CreateCommand(connection, transaction,
                "SELECT COUNT(*) FROM StokKartlari WHERE Id=$id AND COALESCE(KartTipi,'Alt')='Ust'");
            parentCommand.Parameters.AddWithValue("$id", stokKarti.UstKartId.Value);
            if (Convert.ToInt64(parentCommand.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
                throw new InvalidOperationException(L("parent_card_not_found"));

            if (stokKarti.Id > 0)
                EnsureNoHierarchyLoop(connection, transaction, stokKarti.Id, stokKarti.UstKartId.Value);
        }

        if (stokKarti.KartTipi == "Ust" && isUpdate && HasMovements(connection, transaction, stokKarti.Id))
            throw new InvalidOperationException(L("parent_card_with_movements"));

        using var duplicateCode = CreateCommand(connection, transaction,
            "SELECT COUNT(*) FROM StokKartlari WHERE KodNo=$kod COLLATE NOCASE AND Id<>$id");
        duplicateCode.Parameters.AddWithValue("$kod", stokKarti.KodNo);
        duplicateCode.Parameters.AddWithValue("$id", stokKarti.Id);
        if (Convert.ToInt64(duplicateCode.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0)
            throw new InvalidOperationException(L("stock_code_exists"));
    }

    private static void ValidateHareket(SqliteConnection connection, SqliteTransaction transaction, StokHareketi hareket)
    {
        if (!ValidMovementTypes.Contains(hareket.Tur))
            throw new InvalidOperationException(L("movement_type_invalid"));
        if (hareket.StokKartId <= 0)
            throw new InvalidOperationException(L("select_stock_card"));
        if (hareket.Tur != "Bos" && hareket.Miktar <= 0)
            throw new InvalidOperationException(L("enter_valid_qty"));
        if (hareket.Tur == "Bos")
            hareket.Miktar = 0;

        using var cardCommand = CreateCommand(connection, transaction,
            "SELECT COALESCE(KartTipi,'Alt') FROM StokKartlari WHERE Id=$id");
        cardCommand.Parameters.AddWithValue("$id", hareket.StokKartId);
        string? cardType = cardCommand.ExecuteScalar()?.ToString();

        if (cardType == null)
            throw new InvalidOperationException(L("card_not_found"));
        if (!string.Equals(cardType, "Alt", StringComparison.Ordinal))
            throw new InvalidOperationException(L("movement_only_child_cards"));
    }

    private static void ValidateServisKaydi(ServisKaydi kayit)
    {
        if (string.IsNullOrWhiteSpace(kayit.CihazAdi))
            throw new InvalidOperationException(L("device_name_required"));
    }

    private static void EnsureStockCardExists(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM StokKartlari WHERE Id=$id");
        command.Parameters.AddWithValue("$id", id);
        if (Convert.ToInt64(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
            throw new InvalidOperationException(L("record_not_found"));
    }

    private static void EnsureNoHierarchyLoop(SqliteConnection connection, SqliteTransaction transaction, int cardId, int parentId)
    {
        var visited = new HashSet<int>();
        int? current = parentId;

        while (current.HasValue)
        {
            if (!visited.Add(current.Value) || current.Value == cardId)
                throw new InvalidOperationException(L("parent_card_loop"));

            using var command = CreateCommand(connection, transaction, "SELECT UstKartId FROM StokKartlari WHERE Id=$id");
            command.Parameters.AddWithValue("$id", current.Value);
            object? value = command.ExecuteScalar();
            current = value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    private static bool HasMovements(SqliteConnection connection, SqliteTransaction transaction, int stockCardId)
    {
        using var command = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM StokHareketleri WHERE StokKartId=$id");
        command.Parameters.AddWithValue("$id", stockCardId);
        return Convert.ToInt64(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0;
    }

    private static double GetCurrentStockCore(SqliteConnection connection, SqliteTransaction transaction, int stockCardId)
    {
        using var command = CreateCommand(connection, transaction, @"
            SELECT COALESCE(SUM(
                CASE
                    WHEN Tur='Giris' THEN Miktar
                    WHEN Tur='Cikis' THEN -Miktar
                    ELSE 0
                END
            ), 0)
            FROM StokHareketleri
            WHERE StokKartId=$id");
        command.Parameters.AddWithValue("$id", stockCardId);
        return Convert.ToDouble(command.ExecuteScalar() ?? 0d, CultureInfo.InvariantCulture);
    }

    private static void EnsureMovementWillNotCreateNegativeStock(SqliteConnection connection, SqliteTransaction transaction, int stockCardId, double impact)
    {
        double projected = GetCurrentStockCore(connection, transaction, stockCardId) + impact;
        if (projected < 0)
            throw new InvalidOperationException(L("stock_would_go_negative"));
    }

    private static double MovementImpact(StokHareketi hareket)
    {
        return hareket.Tur switch
        {
            "Giris" => hareket.Miktar,
            "Cikis" => -hareket.Miktar,
            _ => 0
        };
    }

    private static StokKarti? GetStockCardById(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, BuildStockCardQuery(null, null, id));
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadStockCard(reader) : null;
    }

    private static StokHareketi? GetMovementById(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, @"
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
            WHERE h.Id=$id");
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadMovement(reader) : null;
    }

    private static ServisKaydi? GetServisKaydiById(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, @"
            SELECT Id, CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc
            FROM ServisKayitlari
            WHERE Id=$id");
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new ServisKaydi
        {
            Id = reader.GetInt32(0),
            CihazAdi = reader.GetString(1),
            SeriNumarasi = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Firma = reader.IsDBNull(3) ? "" : reader.GetString(3),
            BakimTarihi = ParseDateSafe(reader.GetString(4)),
            Sorun = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Sonuc = reader.IsDBNull(6) ? "" : reader.GetString(6)
        };
    }

    private static string GenerateSalt()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(salt);
    }

    private static string HashPasswordV2Legacy(string password, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), PasswordIterationsV2, HashAlgorithmName.SHA512);
        byte[] hash = pbkdf2.GetBytes(PasswordHashSize);
        return "v2:" + Convert.ToBase64String(hash);
    }

    private static string HashPasswordV3(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, PasswordIterationsV3, HashAlgorithmName.SHA512);
        byte[] hash = pbkdf2.GetBytes(PasswordHashSize);
        return "v3:" + Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string salt, string storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;

        if (storedHash.StartsWith("v3:", StringComparison.Ordinal))
            return FixedTimeEquals(storedHash, HashPasswordV3(password, salt));

        if (storedHash.StartsWith("v2:", StringComparison.Ordinal))
        {
            bool match = FixedTimeEquals(storedHash, HashPasswordV2Legacy(password, salt));
            needsUpgrade = match;
            return match;
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        string legacyHash = Convert.ToBase64String(bytes);
        bool legacyMatch = FixedTimeEquals(legacyHash, storedHash);
        needsUpgrade = legacyMatch;
        return legacyMatch;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static void UpgradePasswordHash(string kullaniciAdi, string sifre, SqliteConnection connection)
    {
        string newSalt = GenerateSalt();
        string newHash = HashPasswordV3(sifre, newSalt);

        using var transaction = connection.BeginTransaction();
        using var command = CreateCommand(connection, transaction,
            "UPDATE Kullanicilar SET SifreHash=$h, Tuz=$s WHERE KullaniciAdi=$u");
        command.Parameters.AddWithValue("$h", newHash);
        command.Parameters.AddWithValue("$s", newSalt);
        command.Parameters.AddWithValue("$u", kullaniciAdi);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static string NormalizeRequiredText(string? value, int maxLength, string errorMessage)
    {
        string normalized = TrimTo(value, maxLength);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException(errorMessage);
        return normalized;
    }

    private static string TrimTo(string? value, int maxLength)
    {
        string normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string ToSqlLiteral(object? value)
    {
        if (value == null || value == DBNull.Value)
            return "NULL";

        return value switch
        {
            byte[] bytes => "X'" + Convert.ToHexString(bytes) + "'",
            string text => $"'{text.Replace("'", "''")}'",
            bool flag => flag ? "1" : "0",
            float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            DateTime dateTime => $"'{dateTime.ToString(DateFormat, CultureInfo.InvariantCulture)}'",
            _ => $"'{value.ToString()?.Replace("'", "''")}'"
        };
    }

    private static DateTime ParseDateSafe(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return DateTime.MinValue;

        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy",
            "O",
            "o"
        };

        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        return DateTime.MinValue;
    }

    private static int GetLastInsertRowId(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = CreateCommand(connection, transaction, "SELECT last_insert_rowid();");
        return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }
}
