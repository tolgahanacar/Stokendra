using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using StokTakip.Models;

namespace StokTakip.Data;

public class Database
{
    private readonly string _connectionString;
    private const int CurrentSchemaVersion = 6;
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    public Database(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private SqliteConnection OpenConnection()
    {
        var con = new SqliteConnection(_connectionString);
        con.Open();
        using var pragma = con.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return con;
    }

    private void Initialize()
    {
        using var con = OpenConnection();
        var cmd = con.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS StokKartlari (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ad TEXT NOT NULL, KodNo TEXT NOT NULL, Aciklama TEXT DEFAULT '',
                MinStok INTEGER DEFAULT 0, Kategori TEXT DEFAULT '',
                KartTipi TEXT DEFAULT 'Alt', UstKartId INTEGER DEFAULT NULL,
                OlusturmaTarihi TEXT DEFAULT '', GuncellenmeTarihi TEXT DEFAULT '',
                Birim TEXT DEFAULT 'Adet', Konum TEXT DEFAULT '', Tedarikci TEXT DEFAULT '',
                Barkod TEXT DEFAULT '', BirimFiyat REAL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS StokHareketleri (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StokKartId INTEGER NOT NULL REFERENCES StokKartlari(Id) ON DELETE CASCADE,
                Tur TEXT NOT NULL, Miktar REAL NOT NULL,
                KimeVerildi TEXT DEFAULT '', Departman TEXT DEFAULT '',
                Tarih TEXT NOT NULL, Aciklama TEXT DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Notlar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Tarih TEXT NOT NULL, Baslik TEXT NOT NULL, Icerik TEXT DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Birimler (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, Ad TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Departmanlar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, Ad TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Tarih TEXT NOT NULL, IslemTipi TEXT NOT NULL,
                TabloAdi TEXT NOT NULL, KayitId INTEGER DEFAULT 0, Detay TEXT DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS AppConfig (
                Key TEXT PRIMARY KEY, Value TEXT DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Kullanicilar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                KullaniciAdi TEXT NOT NULL UNIQUE,
                SifreHash TEXT NOT NULL,
                Tuz TEXT NOT NULL,
                Rol TEXT DEFAULT 'admin'
            );
        ";
        cmd.ExecuteNonQuery();

        int version = GetSchemaVersion(con);
        if (version < 1) MigrateToV1(con);
        if (version < 2) MigrateToV2(con);
        if (version < 3) MigrateToV3(con);
        if (version < 4) MigrateToV4(con);
        if (version < 5) MigrateToV5(con);
        if (version < 6) MigrateToV6(con);
        SetSchemaVersion(con, CurrentSchemaVersion);
        SeedDefaults(con);
    }

    private static int GetSchemaVersion(SqliteConnection c) { using var m = c.CreateCommand(); m.CommandText = "PRAGMA user_version"; return Convert.ToInt32(m.ExecuteScalar()); }
    private static void SetSchemaVersion(SqliteConnection c, int v) { using var m = c.CreateCommand(); m.CommandText = $"PRAGMA user_version = {v}"; m.ExecuteNonQuery(); }
    private static void TryAlter(SqliteConnection c, string sql) { try { using var m = c.CreateCommand(); m.CommandText = sql; m.ExecuteNonQuery(); } catch { } }

    private static void MigrateToV1(SqliteConnection c) { TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN Aciklama TEXT DEFAULT ''"); TryAlter(c, "ALTER TABLE StokHareketleri ADD COLUMN Departman TEXT DEFAULT ''"); }
    private static void MigrateToV2(SqliteConnection c) { TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN Birim TEXT DEFAULT ''"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN MinStok INTEGER DEFAULT 0"); }
    private static void MigrateToV3(SqliteConnection c) { TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN Kategori TEXT DEFAULT ''"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN Konum TEXT DEFAULT ''"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN Tedarikci TEXT DEFAULT ''"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN Barkod TEXT DEFAULT ''"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN BirimFiyat REAL DEFAULT 0"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN OlusturmaTarihi TEXT DEFAULT ''"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN GuncellenmeTarihi TEXT DEFAULT ''"); }
    private static void MigrateToV4(SqliteConnection c) { TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN KartTipi TEXT DEFAULT 'Alt'"); TryAlter(c, "ALTER TABLE StokKartlari ADD COLUMN UstKartId INTEGER DEFAULT NULL"); }
    private static void MigrateToV5(SqliteConnection c) { /* Departmanlar tablosu CREATE IF NOT EXISTS ile oluşturuldu */ }
    private static void MigrateToV6(SqliteConnection c)
    {
        // Performance indexes
        TryAlter(c, "CREATE INDEX IF NOT EXISTS IX_Hareket_StokKartId ON StokHareketleri(StokKartId)");
        TryAlter(c, "CREATE INDEX IF NOT EXISTS IX_Hareket_Tarih ON StokHareketleri(Tarih)");
        TryAlter(c, "CREATE INDEX IF NOT EXISTS IX_StokKart_KodNo ON StokKartlari(KodNo)");
        TryAlter(c, "CREATE INDEX IF NOT EXISTS IX_StokKart_KartTipi ON StokKartlari(KartTipi)");
        TryAlter(c, "CREATE INDEX IF NOT EXISTS IX_AuditLog_Tarih ON AuditLog(Tarih)");
    }

    private static void SeedDefaults(SqliteConnection c)
    {
        try { using var m = c.CreateCommand(); m.CommandText = "SELECT COUNT(*) FROM Birimler"; if ((long)(m.ExecuteScalar() ?? 0) == 0) { m.CommandText = "INSERT INTO Birimler (Ad) VALUES ('Adet')"; m.ExecuteNonQuery(); } } catch { }
        // Seed default admin user
        try
        {
            using var m = c.CreateCommand(); m.CommandText = "SELECT COUNT(*) FROM Kullanicilar";
            if ((long)(m.ExecuteScalar() ?? 0) == 0)
            {
                string salt = GenerateSalt();
                string hash = HashPassword("admin", salt);
                using var ins = c.CreateCommand();
                ins.CommandText = "INSERT INTO Kullanicilar (KullaniciAdi,SifreHash,Tuz,Rol) VALUES ('admin',$h,$s,'admin')";
                ins.Parameters.AddWithValue("$h", hash); ins.Parameters.AddWithValue("$s", salt);
                ins.ExecuteNonQuery();
            }
        } catch { }
    }

    // ═══ APP CONFIG ═══
    public string GetConfig(string key, string def = "") { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "SELECT Value FROM AppConfig WHERE Key=$k"; m.Parameters.AddWithValue("$k", key); return m.ExecuteScalar()?.ToString() ?? def; } catch { return def; } }
    public void SetConfig(string key, string val) { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "INSERT OR REPLACE INTO AppConfig (Key,Value) VALUES ($k,$v)"; m.Parameters.AddWithValue("$k", key); m.Parameters.AddWithValue("$v", val); m.ExecuteNonQuery(); } catch { } }

    // ═══ AUDIT LOG ═══
    public void AuditLogYaz(string tip, string tablo, int id, string detay) { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "INSERT INTO AuditLog (Tarih,IslemTipi,TabloAdi,KayitId,Detay) VALUES ($t,$it,$ta,$ki,$d)"; m.Parameters.AddWithValue("$t", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture)); m.Parameters.AddWithValue("$it", tip); m.Parameters.AddWithValue("$ta", tablo); m.Parameters.AddWithValue("$ki", id); m.Parameters.AddWithValue("$d", detay); m.ExecuteNonQuery(); } catch { } }

    // ═══ DEPARTMANLAR ═══
    public List<string> DepartmanlariGetir()
    {
        var liste = new List<string>();
        try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "SELECT Ad FROM Departmanlar ORDER BY Ad"; using var r = m.ExecuteReader(); while (r.Read()) liste.Add(r.GetString(0)); } catch { }
        return liste;
    }
    public void DepartmanEkle(string ad) { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "INSERT OR IGNORE INTO Departmanlar (Ad) VALUES ($a)"; m.Parameters.AddWithValue("$a", ad.Trim()); m.ExecuteNonQuery(); } catch { } }
    public void DepartmanSil(string ad) { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "DELETE FROM Departmanlar WHERE Ad=$a"; m.Parameters.AddWithValue("$a", ad); m.ExecuteNonQuery(); } catch { } }

    // ═══ OTO STOK KODU ═══
    public string SonrakiStokKodu()
    {
        try
        {
            using var c = OpenConnection();
            var m = c.CreateCommand();
            m.CommandText = "SELECT MAX(CAST(KodNo AS INTEGER)) FROM StokKartlari WHERE KodNo GLOB '[0-9]*'";
            var result = m.ExecuteScalar();
            int max = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            return (max + 1).ToString("D3");
        }
        catch { return "001"; }
    }

    // ═══ STOK KARTLARI ═══
    public List<StokKarti> StokKartlariniGetir()
    {
        var liste = new List<StokKarti>();
        try
        {
            using var c = OpenConnection();
            var m = c.CreateCommand();
            m.CommandText = @"SELECT s.Id, s.Ad, s.KodNo, s.Aciklama, s.MinStok,
                COALESCE((SELECT SUM(CASE WHEN h.Tur='Giris' THEN h.Miktar ELSE -h.Miktar END) FROM StokHareketleri h WHERE h.StokKartId=s.Id),0),
                s.Kategori, COALESCE(s.KartTipi,'Alt'), s.UstKartId, COALESCE(ust.Ad,'')
                FROM StokKartlari s LEFT JOIN StokKartlari ust ON ust.Id=s.UstKartId ORDER BY s.KodNo";
            using var r = m.ExecuteReader();
            while (r.Read())
                liste.Add(new StokKarti { Id = r.GetInt32(0), Ad = r.GetString(1), KodNo = r.GetString(2),
                    Aciklama = r.IsDBNull(3) ? "" : r.GetString(3), MinStok = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                    MevcutStok = r.IsDBNull(5) ? 0 : r.GetDouble(5), Kategori = r.IsDBNull(6) ? "" : r.GetString(6),
                    KartTipi = r.IsDBNull(7) ? "Alt" : r.GetString(7), UstKartId = r.IsDBNull(8) ? null : r.GetInt32(8),
                    UstKartAd = r.IsDBNull(9) ? "" : r.GetString(9) });
        }
        catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
        return liste;
    }

    public List<StokKarti> UstKartlariGetir() => StokKartlariniGetir().Where(k => k.KartTipi == "Ust").ToList();
    public List<StokKarti> AltKartlariGetir(int? ustId = null) { var t = StokKartlariniGetir().Where(k => k.KartTipi == "Alt").ToList(); return ustId.HasValue ? t.Where(k => k.UstKartId == ustId).ToList() : t; }

    public StokKarti? StokKartiDetayGetir(int id)
    {
        try
        {
            using var c = OpenConnection(); var m = c.CreateCommand();
            m.CommandText = @"SELECT s.Id, s.Ad, s.KodNo, s.Aciklama, s.MinStok,
                COALESCE((SELECT SUM(CASE WHEN h.Tur='Giris' THEN h.Miktar ELSE -h.Miktar END) FROM StokHareketleri h WHERE h.StokKartId=s.Id),0),
                s.Kategori, COALESCE(s.KartTipi,'Alt'), s.UstKartId, COALESCE(ust.Ad,'')
                FROM StokKartlari s LEFT JOIN StokKartlari ust ON ust.Id=s.UstKartId WHERE s.Id=$id";
            m.Parameters.AddWithValue("$id", id);
            using var r = m.ExecuteReader();
            if (r.Read()) return new StokKarti { Id = r.GetInt32(0), Ad = r.GetString(1), KodNo = r.GetString(2),
                Aciklama = r.IsDBNull(3) ? "" : r.GetString(3), MinStok = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                MevcutStok = r.IsDBNull(5) ? 0 : r.GetDouble(5), Kategori = r.IsDBNull(6) ? "" : r.GetString(6),
                KartTipi = r.IsDBNull(7) ? "Alt" : r.GetString(7), UstKartId = r.IsDBNull(8) ? null : r.GetInt32(8),
                UstKartAd = r.IsDBNull(9) ? "" : r.GetString(9) };
        } catch { } return null;
    }

    public void StokKartiEkle(StokKarti s)
    {
        try
        {
            using var c = OpenConnection(); var m = c.CreateCommand();
            m.CommandText = "INSERT INTO StokKartlari (Ad,KodNo,Aciklama,MinStok,Kategori,KartTipi,UstKartId,OlusturmaTarihi,Birim) VALUES ($a,$k,$ac,$ms,$kat,$kt,$uk,$ot,'Adet')";
            m.Parameters.AddWithValue("$a", s.Ad); m.Parameters.AddWithValue("$k", s.KodNo);
            m.Parameters.AddWithValue("$ac", s.Aciklama ?? ""); m.Parameters.AddWithValue("$ms", s.MinStok);
            m.Parameters.AddWithValue("$kat", s.Kategori ?? ""); m.Parameters.AddWithValue("$kt", s.KartTipi ?? "Alt");
            m.Parameters.AddWithValue("$uk", s.UstKartId.HasValue ? (object)s.UstKartId.Value : DBNull.Value);
            m.Parameters.AddWithValue("$ot", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));
            m.ExecuteNonQuery();
            AuditLogYaz("EKLE", "StokKartlari", 0, $"{s.Ad} ({s.KodNo}) [{s.KartTipi}]");
        } catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
    }

    public void StokKartiGuncelle(StokKarti s)
    {
        try
        {
            using var c = OpenConnection(); var m = c.CreateCommand();
            m.CommandText = "UPDATE StokKartlari SET Ad=$a,KodNo=$k,Aciklama=$ac,MinStok=$ms,Kategori=$kat,KartTipi=$kt,UstKartId=$uk,GuncellenmeTarihi=$gt WHERE Id=$id";
            m.Parameters.AddWithValue("$a", s.Ad); m.Parameters.AddWithValue("$k", s.KodNo);
            m.Parameters.AddWithValue("$ac", s.Aciklama ?? ""); m.Parameters.AddWithValue("$ms", s.MinStok);
            m.Parameters.AddWithValue("$kat", s.Kategori ?? ""); m.Parameters.AddWithValue("$kt", s.KartTipi ?? "Alt");
            m.Parameters.AddWithValue("$uk", s.UstKartId.HasValue ? (object)s.UstKartId.Value : DBNull.Value);
            m.Parameters.AddWithValue("$gt", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));
            m.Parameters.AddWithValue("$id", s.Id);
            m.ExecuteNonQuery();
            AuditLogYaz("GUNCELLE", "StokKartlari", s.Id, $"{s.Ad} ({s.KodNo})");
        } catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
    }

    public void StokKartiSil(int id)
    {
        try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "DELETE FROM StokHareketleri WHERE StokKartId=$id"; m.Parameters.AddWithValue("$id", id); m.ExecuteNonQuery();
            var m2 = c.CreateCommand(); m2.CommandText = "DELETE FROM StokKartlari WHERE Id=$id"; m2.Parameters.AddWithValue("$id", id); m2.ExecuteNonQuery();
            AuditLogYaz("SIL", "StokKartlari", id, "Silindi");
        } catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
    }

    // ═══ STOK HAREKETLERİ ═══
    public List<StokHareketi> HareketleriGetir(int? stokKartId = null, DateTime? baslangic = null, DateTime? bitis = null, string? departman = null, string? tur = null)
    {
        var liste = new List<StokHareketi>();
        try
        {
            using var c = OpenConnection(); var m = c.CreateCommand();
            var w = new List<string>();
            if (stokKartId.HasValue) { w.Add("h.StokKartId=$kid"); m.Parameters.AddWithValue("$kid", stokKartId.Value); }
            if (baslangic.HasValue) { w.Add("h.Tarih>=$ts"); m.Parameters.AddWithValue("$ts", baslangic.Value.ToString(DateFormat, CultureInfo.InvariantCulture)); }
            if (bitis.HasValue) { w.Add("h.Tarih<=$te"); m.Parameters.AddWithValue("$te", bitis.Value.ToString(DateFormat, CultureInfo.InvariantCulture)); }
            if (!string.IsNullOrWhiteSpace(departman)) { w.Add("h.Departman=$dep"); m.Parameters.AddWithValue("$dep", departman); }
            if (!string.IsNullOrWhiteSpace(tur)) { w.Add("h.Tur=$tur"); m.Parameters.AddWithValue("$tur", tur); }
            string wc = w.Count > 0 ? "WHERE " + string.Join(" AND ", w) : "";
            m.CommandText = $"SELECT h.Id,h.StokKartId,s.Ad,s.KodNo,h.Tur,h.Miktar,h.KimeVerildi,h.Departman,h.Tarih,h.Aciklama FROM StokHareketleri h JOIN StokKartlari s ON s.Id=h.StokKartId {wc} ORDER BY h.Tarih DESC,h.Id DESC";
            using var r = m.ExecuteReader();
            while (r.Read())
                liste.Add(new StokHareketi { Id = r.GetInt32(0), StokKartId = r.GetInt32(1), StokKartAd = r.GetString(2), StokKartKodNo = r.GetString(3),
                    Tur = r.GetString(4), Miktar = r.GetDouble(5), TeslimEdilen = r.IsDBNull(6) ? "" : r.GetString(6),
                    Departman = r.IsDBNull(7) ? "" : r.GetString(7), Tarih = ParseDateSafe(r.GetString(8)), Aciklama = r.IsDBNull(9) ? "" : r.GetString(9) });
        } catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
        return liste;
    }

    public void HareketEkle(StokHareketi h)
    {
        try { using var c = OpenConnection(); var m = c.CreateCommand();
            m.CommandText = "INSERT INTO StokHareketleri (StokKartId,Tur,Miktar,KimeVerildi,Departman,Tarih,Aciklama) VALUES ($sk,$t,$m,$kv,$d,$ta,$ac)";
            m.Parameters.AddWithValue("$sk", h.StokKartId); m.Parameters.AddWithValue("$t", h.Tur); m.Parameters.AddWithValue("$m", h.Miktar);
            m.Parameters.AddWithValue("$kv", h.TeslimEdilen ?? ""); m.Parameters.AddWithValue("$d", h.Departman ?? "");
            m.Parameters.AddWithValue("$ta", h.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture)); m.Parameters.AddWithValue("$ac", h.Aciklama ?? "");
            m.ExecuteNonQuery();
        } catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
    }

    public void HareketGuncelle(StokHareketi h)
    {
        try { using var c = OpenConnection(); var m = c.CreateCommand();
            m.CommandText = "UPDATE StokHareketleri SET StokKartId=$sk,Tur=$t,Miktar=$m,KimeVerildi=$kv,Departman=$d,Tarih=$ta,Aciklama=$ac WHERE Id=$id";
            m.Parameters.AddWithValue("$sk", h.StokKartId); m.Parameters.AddWithValue("$t", h.Tur); m.Parameters.AddWithValue("$m", h.Miktar);
            m.Parameters.AddWithValue("$kv", h.TeslimEdilen ?? ""); m.Parameters.AddWithValue("$d", h.Departman ?? "");
            m.Parameters.AddWithValue("$ta", h.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture)); m.Parameters.AddWithValue("$ac", h.Aciklama ?? "");
            m.Parameters.AddWithValue("$id", h.Id); m.ExecuteNonQuery();
        } catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
    }

    public void HareketSil(int id)
    { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "DELETE FROM StokHareketleri WHERE Id=$id"; m.Parameters.AddWithValue("$id", id); m.ExecuteNonQuery(); } catch { } }

    // ═══ BIRIMLER (eski uyumluluk) ═══
    public List<Birim> BirimleriGetir() { var l = new List<Birim>(); try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "SELECT Id,Ad FROM Birimler ORDER BY Ad"; using var r = m.ExecuteReader(); while (r.Read()) l.Add(new Birim { Id = r.GetInt32(0), Ad = r.GetString(1) }); } catch { } return l; }

    // ═══ NOTLAR ═══
    public List<Not> NotlariGetir() { var l = new List<Not>(); try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "SELECT Id,Tarih,Baslik,Icerik FROM Notlar ORDER BY Tarih DESC"; using var r = m.ExecuteReader(); while (r.Read()) l.Add(new Not { Id = r.GetInt32(0), Tarih = ParseDateSafe(r.GetString(1)), Baslik = r.GetString(2), Icerik = r.IsDBNull(3) ? "" : r.GetString(3) }); } catch { } return l; }
    public void NotEkle(Not n) { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "INSERT INTO Notlar (Tarih,Baslik,Icerik) VALUES ($t,$b,$i)"; m.Parameters.AddWithValue("$t", n.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture)); m.Parameters.AddWithValue("$b", n.Baslik); m.Parameters.AddWithValue("$i", n.Icerik ?? ""); m.ExecuteNonQuery(); } catch { } }
    public void NotGuncelle(Not n) { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "UPDATE Notlar SET Baslik=$b,Icerik=$i WHERE Id=$id"; m.Parameters.AddWithValue("$b", n.Baslik); m.Parameters.AddWithValue("$i", n.Icerik ?? ""); m.Parameters.AddWithValue("$id", n.Id); m.ExecuteNonQuery(); } catch { } }
    public void NotSil(int id) { try { using var c = OpenConnection(); var m = c.CreateCommand(); m.CommandText = "DELETE FROM Notlar WHERE Id=$id"; m.Parameters.AddWithValue("$id", id); m.ExecuteNonQuery(); } catch { } }

    private static DateTime ParseDateSafe(string s) { if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue; string[] f = { "yyyy-MM-dd HH:mm:ss","yyyy-MM-dd","dd.MM.yyyy HH:mm:ss","dd.MM.yyyy HH:mm","dd.MM.yyyy","MM/dd/yyyy HH:mm:ss","MM/dd/yyyy" }; if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d; if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d; return DateTime.MinValue; }

    // ═══ AUTH ═══
    private static string GenerateSalt()
    {
        byte[] salt = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return Convert.ToBase64String(salt);
    }

    private static string HashPassword(string password, string salt)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        return Convert.ToBase64String(bytes);
    }

    public bool KullaniciDogrula(string kullaniciAdi, string sifre)
    {
        try
        {
            using var c = OpenConnection(); var m = c.CreateCommand();
            m.CommandText = "SELECT SifreHash, Tuz FROM Kullanicilar WHERE KullaniciAdi=$u";
            m.Parameters.AddWithValue("$u", kullaniciAdi);
            using var r = m.ExecuteReader();
            if (!r.Read()) return false;
            string storedHash = r.GetString(0), storedSalt = r.GetString(1);
            return HashPassword(sifre, storedSalt) == storedHash;
        }
        catch { return false; }
    }

    public bool SifreDegistir(string kullaniciAdi, string eskiSifre, string yeniSifre)
    {
        if (!KullaniciDogrula(kullaniciAdi, eskiSifre)) return false;
        try
        {
            string newSalt = GenerateSalt();
            string newHash = HashPassword(yeniSifre, newSalt);
            using var c = OpenConnection(); var m = c.CreateCommand();
            m.CommandText = "UPDATE Kullanicilar SET SifreHash=$h, Tuz=$s WHERE KullaniciAdi=$u";
            m.Parameters.AddWithValue("$h", newHash); m.Parameters.AddWithValue("$s", newSalt); m.Parameters.AddWithValue("$u", kullaniciAdi);
            m.ExecuteNonQuery();
            AuditLogYaz("SIFRE_DEGISTIR", "Kullanicilar", 0, kullaniciAdi);
            return true;
        }
        catch { return false; }
    }
}
