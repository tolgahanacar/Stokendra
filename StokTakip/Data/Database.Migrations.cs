using System.Globalization;
using Microsoft.Data.Sqlite;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

public sealed partial class Database
{
    private static void EnsureSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        string[] tables = {
            @"CREATE TABLE IF NOT EXISTS StokKartlari (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ad TEXT NOT NULL,
                KodNo TEXT NOT NULL COLLATE NOCASE,
                Aciklama TEXT DEFAULT '',
                MinStok INTEGER DEFAULT 0,
                Kategori TEXT DEFAULT '',
                KartTipi TEXT NOT NULL DEFAULT 'Alt',
                UstKartId INTEGER DEFAULT NULL,
                OlusturmaTarihi TEXT DEFAULT '',
                GuncellenmeTarihi TEXT DEFAULT '',
                Birim TEXT DEFAULT 'Adet',
                Konum TEXT DEFAULT '',
                Tedarikci TEXT DEFAULT '',
                Barkod TEXT DEFAULT '',
                BirimFiyat REAL DEFAULT 0
            )",
            @"CREATE TABLE IF NOT EXISTS StokHareketleri (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StokKartId INTEGER NOT NULL REFERENCES StokKartlari(Id) ON DELETE CASCADE,
                Tur TEXT NOT NULL,
                Miktar REAL NOT NULL,
                KimeVerildi TEXT DEFAULT '',
                Departman TEXT DEFAULT '',
                Tarih TEXT NOT NULL,
                Aciklama TEXT DEFAULT ''
            )",
            "CREATE TABLE IF NOT EXISTS Notlar (Id INTEGER PRIMARY KEY AUTOINCREMENT, Tarih TEXT NOT NULL, Baslik TEXT NOT NULL, Icerik TEXT DEFAULT '', OlusturmaTarihi TEXT DEFAULT '', GuncellenmeTarihi TEXT DEFAULT '')",
            "CREATE TABLE IF NOT EXISTS Birimler (Id INTEGER PRIMARY KEY AUTOINCREMENT, Ad TEXT NOT NULL UNIQUE)",
            "CREATE TABLE IF NOT EXISTS Departmanlar (Id INTEGER PRIMARY KEY AUTOINCREMENT, Ad TEXT NOT NULL UNIQUE)",
            "CREATE TABLE IF NOT EXISTS AuditLog (Id INTEGER PRIMARY KEY AUTOINCREMENT, Tarih TEXT NOT NULL, IslemTipi TEXT NOT NULL, TabloAdi TEXT NOT NULL, KayitId INTEGER DEFAULT 0, Detay TEXT DEFAULT '')",
            "CREATE TABLE IF NOT EXISTS AppConfig (Key TEXT PRIMARY KEY, Value TEXT DEFAULT '')",
            "CREATE TABLE IF NOT EXISTS Kullanicilar (Id INTEGER PRIMARY KEY AUTOINCREMENT, KullaniciAdi TEXT NOT NULL UNIQUE, SifreHash TEXT NOT NULL, Tuz TEXT NOT NULL, Rol TEXT DEFAULT 'admin')",
            @"CREATE TABLE IF NOT EXISTS ServisKayitlari (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CihazAdi TEXT NOT NULL,
                SeriNumarasi TEXT DEFAULT '',
                Firma TEXT DEFAULT '',
                BakimTarihi TEXT NOT NULL,
                Aciklama TEXT DEFAULT '',
                Sorun TEXT DEFAULT '',
                Sonuc TEXT DEFAULT ''
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

    private static void MigrateToV1(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Aciklama TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokHareketleri ADD COLUMN Departman TEXT DEFAULT ''");
    }

    private static void MigrateToV2(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Birim TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN MinStok INTEGER DEFAULT 0");
    }

    private static void MigrateToV3(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Kategori TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Konum TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Tedarikci TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN Barkod TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN BirimFiyat REAL DEFAULT 0");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN OlusturmaTarihi TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN GuncellenmeTarihi TEXT DEFAULT ''");
    }

    private static void MigrateToV4(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN KartTipi TEXT DEFAULT 'Alt'");
        TryAlter(connection, transaction, "ALTER TABLE StokKartlari ADD COLUMN UstKartId INTEGER DEFAULT NULL");
    }

    private static void MigrateToV5(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "UPDATE StokKartlari SET KartTipi='Alt' WHERE KartTipi IS NULL OR trim(KartTipi)=''");
    }

    private static void MigrateToV6(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_StokKartId ON StokHareketleri(StokKartId)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_Tarih ON StokHareketleri(Tarih)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StokKart_KodNo ON StokKartlari(KodNo)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StokKart_KartTipi ON StokKartlari(KartTipi)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_AuditLog_Tarih ON AuditLog(Tarih)");
    }

    private static void MigrateToV7(SqliteConnection connection, SqliteTransaction? transaction)
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

    private static void MigrateToV8(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE ServisKayitlari ADD COLUMN Firma TEXT DEFAULT ''");
    }

    private static void MigrateToV9(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE ServisKayitlari ADD COLUMN Sorun TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE ServisKayitlari ADD COLUMN Sonuc TEXT DEFAULT ''");
    }

    private static void MigrateToV10(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "UPDATE StokKartlari SET Birim='Adet' WHERE Birim IS NULL OR trim(Birim)=''");
        TryAlter(connection, transaction, "UPDATE StokKartlari SET MinStok=0 WHERE MinStok < 0");
        TryAlter(connection, transaction, "UPDATE StokKartlari SET BirimFiyat=0 WHERE BirimFiyat < 0");
        TryAlter(connection, transaction, "CREATE UNIQUE INDEX IF NOT EXISTS UX_StokKartlari_KodNo ON StokKartlari(KodNo COLLATE NOCASE)");
    }

    private static void MigrateToV11(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "ALTER TABLE Notlar ADD COLUMN OlusturmaTarihi TEXT DEFAULT ''");
        TryAlter(connection, transaction, "ALTER TABLE Notlar ADD COLUMN GuncellenmeTarihi TEXT DEFAULT ''");
    }

    private static void EnsureIndexes(SqliteConnection connection, SqliteTransaction? transaction)
    {
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_StokKartId_Tarih ON StokHareketleri(StokKartId, Tarih DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Hareket_Departman_Tarih ON StokHareketleri(Departman, Tarih DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Notlar_Tarih ON Notlar(Tarih DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StokKart_UstKartId ON StokKartlari(UstKartId)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ServisKayit_Tarih_Id ON ServisKayitlari(BakimTarihi DESC, Id DESC)");
        TryAlter(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_AppConfig_Key ON AppConfig(Key)");
    }

    private static void NormalizeLegacyData(SqliteConnection connection, SqliteTransaction? transaction)
    {
        // WinForms'dan gelen verileri yeni standarda dönüştür (ş, ç, ü -> s, c, u)
        TryAlter(connection, transaction, "UPDATE StokHareketleri SET Tur='Giris' WHERE Tur='Giriş'");
        TryAlter(connection, transaction, "UPDATE StokHareketleri SET Tur='Cikis' WHERE Tur='Çıkış'");
        TryAlter(connection, transaction, "UPDATE StokKartlari SET KartTipi='Alt' WHERE KartTipi='Alt'"); // Genelde sorunsuz
        TryAlter(connection, transaction, "UPDATE StokKartlari SET KartTipi='Ust' WHERE KartTipi='Üst'");
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
            using var unitsCount = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM Birimler");
            if (Convert.ToInt64(unitsCount.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
            {
                using var insertUnit = CreateCommand(connection, transaction, "INSERT INTO Birimler (Ad) VALUES ('Adet')");
                insertUnit.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Seed Birimler error: " + ex);
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
            AppLogger.LogError("Seed Kullanicilar error: " + ex);
        }
    }
}

