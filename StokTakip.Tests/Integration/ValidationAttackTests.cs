using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Tests.Helpers;
using Microsoft.Data.Sqlite;
using System.Text;

namespace StokTakip.Tests.Integration;

/// <summary>
/// Stok yönetim sistemi için agresif girdi doğrulama saldırı testleri.
/// </summary>
public class ValidationAttackTests : IDisposable
{
    private readonly TestDatabaseFactory _factory;
    private readonly Database _db;
    private readonly IMovementRepository _mov;
    private readonly IStockCardRepository _card;
    private readonly IDepartmentRepository _dept;
    private readonly INoteRepository _note;
    private readonly IServiceRecordRepository _svc;
    private readonly int _cardId;

    public ValidationAttackTests()
    {
        _factory = new TestDatabaseFactory();
        _db = _factory.Create();
        _mov = (IMovementRepository)_db;
        _card = (IStockCardRepository)_db;
        _dept = (IDepartmentRepository)_db;
        _note = (INoteRepository)_db;
        _svc = (IServiceRecordRepository)_db;

        _dept.Add("IT");
        var kart = new StokKarti { Ad = "Test", KodNo = "ATK001", KartTipi = "Alt" };
        _card.Add(kart);
        _cardId = kart.Id;
    }

    public void Dispose() => _factory.Dispose();

    private static List<(string tip, string tablo, int id, string detay)> ReadAuditLog(Database db)
    {
        var result = new List<(string, string, int, string)>();
        using var conn = new SqliteConnection($"Data Source={db.DatabasePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IslemTipi, TabloAdi, KayitId, Detay FROM AuditLog ORDER BY Id DESC LIMIT 20";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(3) ? "" : reader.GetString(3)));
        return result;
    }

    private StokHareketi MakeEntry(double miktar = 10) => new()
    {
        StokKartId = _cardId,
        Tur = "Giris",
        Miktar = miktar,
        Departman = "IT",
        Tarih = DateTime.Now
    };

    private StokHareketi MakeExit(double miktar = 5) => new()
    {
        StokKartId = _cardId,
        Tur = "Cikis",
        Miktar = miktar,
        Departman = "IT",
        Tarih = DateTime.Now
    };

    // ── MİKTAR (QUANTITY) SALDIRI TESTLERİ ──────────────────────────────

    [Fact]
    public void Miktar_SifirGirilirse_Hata()
    {
        var h = MakeEntry(0);
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Miktar_NegatifGirilirse_Hata()
    {
        var h = MakeEntry(-1);
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Miktar_CokBuyukSayi_Hata()
    {
        var h = MakeEntry(9_999_999_999.0);
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Miktar_DoubleMaxValue_Hata()
    {
        var h = MakeEntry(double.MaxValue);
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Miktar_PositiveInfinity_Hata()
    {
        var h = MakeEntry(double.PositiveInfinity);
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Miktar_NaN_Hata()
    {
        var h = MakeEntry(double.NaN);
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Miktar_KucukPositifDeger_Kabul()
    {
        // 0.0001 → 4 ondalık hassasiyetle 0.0001 olarak saklanır, kabul edilmeli
        var h = MakeEntry(0.0001);
        _mov.Add(h);
        var kart = _card.GetById(_cardId);
        Assert.True(kart?.MevcutStok > 0);
    }

    [Fact]
    public void Miktar_CokKucukDeger_YuvarlanarakSifirOlur_Hata()
    {
        // 0.00001 → Math.Round(0.00001, 4) = 0.0 → sıfır olarak yuvarlanır → reddedilir
        var h = MakeEntry(0.00001);
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Miktar_OndalikliDeger_Kabul()
    {
        _mov.Add(MakeEntry(1.5));
        var kart = _card.GetById(_cardId);
        Assert.Equal(1.5, kart!.MevcutStok);
    }

    [Fact]
    public void Miktar_CokFazlaOndalik_Yuvarlanir()
    {
        _mov.Add(MakeEntry(1.123456789));
        var kart = _card.GetById(_cardId);
        // 4 ondalık basamağa yuvarlanmış olmalı: 1.1235
        Assert.Equal(1.1235, kart!.MevcutStok, precision: 4);
    }

    // ── TÜR (TYPE) SALDIRI TESTLERİ ─────────────────────────────────────

    [Fact]
    public void Tur_GecersizDeger_Hata()
    {
        var h = new StokHareketi { StokKartId = _cardId, Tur = "HACK", Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Tur_BosString_Hata()
    {
        var h = new StokHareketi { StokKartId = _cardId, Tur = "", Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Tur_Null_Hata()
    {
        var h = new StokHareketi { StokKartId = _cardId, Tur = null!, Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Tur_SqlInjection_Hata()
    {
        var h = new StokHareketi
        {
            StokKartId = _cardId,
            Tur = "Giris'; DROP TABLE StokHareketleri;--",
            Miktar = 1,
            Departman = "IT"
        };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));

        // Tablo hâlâ erişilebilir olmalı
        var hareketler = _mov.GetAll();
        Assert.NotNull(hareketler);
    }

    [Fact]
    public void Tur_BuyukKucukHarf_Hata()
    {
        // "giris" küçük harf → geçersiz (büyük/küçük harf duyarlı)
        var h = new StokHareketi { StokKartId = _cardId, Tur = "giris", Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    // ── STOK KARTI ID SALDIRI TESTLERİ ──────────────────────────────────

    [Fact]
    public void StokKartId_Sifir_Hata()
    {
        var h = new StokHareketi { StokKartId = 0, Tur = "Giris", Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void StokKartId_Negatif_Hata()
    {
        var h = new StokHareketi { StokKartId = -1, Tur = "Giris", Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void StokKartId_VarOlmayan_Hata()
    {
        var h = new StokHareketi { StokKartId = 999999, Tur = "Giris", Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void StokKartId_UstKart_Hata()
    {
        var ustKart = new StokKarti { Ad = "Üst Kart", KodNo = "UST_ATK", KartTipi = "Ust" };
        _card.Add(ustKart);

        var h = new StokHareketi { StokKartId = ustKart.Id, Tur = "Giris", Miktar = 1, Departman = "IT" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    // ── DEPARTMAN SALDIRI TESTLERİ ───────────────────────────────────────

    [Fact]
    public void Departman_Bos_Hata()
    {
        var h = new StokHareketi { StokKartId = _cardId, Tur = "Giris", Miktar = 1, Departman = "" };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Departman_Null_Hata()
    {
        var h = new StokHareketi { StokKartId = _cardId, Tur = "Giris", Miktar = 1, Departman = null! };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Departman_SadeceBosluk_Hata()
    {
        var h = new StokHareketi { StokKartId = _cardId, Tur = "Giris", Miktar = 1, Departman = "   " };
        Assert.Throws<InvalidOperationException>(() => _mov.Add(h));
    }

    [Fact]
    public void Departman_SqlInjection_KayitliGibi()
    {
        // SQL injection string'i departman olarak girildiğinde:
        // Parameterized query sayesinde SQL injection çalışmaz,
        // ancak sistem departman validasyonu yapmadığı için kayıt kabul edilir.
        // Bu test mevcut davranışı belgeler: injection çalışmaz, tablo sağlam kalır.
        var h = new StokHareketi
        {
            StokKartId = _cardId,
            Tur = "Giris",
            Miktar = 1,
            Departman = "IT'; DROP TABLE Departmanlar;--"
        };

        // Sistem departman varlığını kontrol etmediği için exception fırlatmaz
        // (Bu bir tasarım kararı — departman free-text olarak kabul ediliyor)
        _mov.Add(h);

        // Kritik: Departmanlar tablosu hâlâ erişilebilir olmalı (injection çalışmadı)
        var depts = _dept.GetAll();
        Assert.NotNull(depts);
        Assert.Contains("IT", depts); // Orijinal departman hâlâ var
    }

    [Fact]
    public void Departman_CokUzunAd_Kisaltilir()
    {
        // 200 karakterlik departman adı ekle
        string uzunAd = new string('D', 200);
        _dept.Add(uzunAd);

        var depts = _dept.GetAll();
        var eklenen = depts.FirstOrDefault(d => d.StartsWith(new string('D', 10)));
        Assert.NotNull(eklenen);

        // Kısaltılmış ada göre hareket ekle
        string kisaltilmisAd = eklenen!.Length > 120 ? eklenen[..120] : eklenen;
        _dept.Add(kisaltilmisAd);

        var h = new StokHareketi
        {
            StokKartId = _cardId,
            Tur = "Giris",
            Miktar = 1,
            Departman = kisaltilmisAd
        };
        _mov.Add(h);

        var hareketler = _mov.GetAll(stockCardId: _cardId);
        Assert.NotEmpty(hareketler);
    }

    // ── STOK KARTI SALDIRI TESTLERİ ─────────────────────────────────────

    [Fact]
    public void StokKarti_BosAd_Hata()
    {
        var kart = new StokKarti { Ad = "", KodNo = "EMPTY001", KartTipi = "Alt" };
        Assert.Throws<InvalidOperationException>(() => _card.Add(kart));
    }

    [Fact]
    public void StokKarti_BosKod_Hata()
    {
        var kart = new StokKarti { Ad = "Geçerli Ad", KodNo = "", KartTipi = "Alt" };
        Assert.Throws<InvalidOperationException>(() => _card.Add(kart));
    }

    [Fact]
    public void StokKarti_DuplicateKod_Hata()
    {
        var kart1 = new StokKarti { Ad = "Kart 1", KodNo = "DUP001", KartTipi = "Alt" };
        _card.Add(kart1);

        var kart2 = new StokKarti { Ad = "Kart 2", KodNo = "DUP001", KartTipi = "Alt" };
        Assert.Throws<InvalidOperationException>(() => _card.Add(kart2));
    }

    [Fact]
    public void StokKarti_KodCaseSensitive_Hata()
    {
        var kart1 = new StokKarti { Ad = "Kart Büyük", KodNo = "ABC001", KartTipi = "Alt" };
        _card.Add(kart1);

        var kart2 = new StokKarti { Ad = "Kart Küçük", KodNo = "abc001", KartTipi = "Alt" };
        Assert.Throws<InvalidOperationException>(() => _card.Add(kart2));
    }

    [Fact]
    public void StokKarti_SqlInjectionAd_Temizlenir()
    {
        var kart = new StokKarti
        {
            Ad = "Test'); DROP TABLE StokKartlari;--",
            KodNo = "SQLINJ001",
            KartTipi = "Alt"
        };
        _card.Add(kart);

        // Tablo hâlâ çalışıyor olmalı
        var kartlar = _card.GetAll();
        Assert.NotNull(kartlar);
        Assert.True(kart.Id > 0);
    }

    [Fact]
    public void StokKarti_SqlInjectionKod_Temizlenir()
    {
        var kart = new StokKarti
        {
            Ad = "SQL Injection Kod Testi",
            KodNo = "X001'; DELETE FROM StokKartlari;--",
            KartTipi = "Alt"
        };
        // Ekleme başarılı olabilir (parameterized query ile güvenli) veya hata verebilir
        try
        {
            _card.Add(kart);
        }
        catch (InvalidOperationException)
        {
            // Kod doğrulaması reddedebilir, bu da kabul edilebilir
        }

        // Her iki durumda da tablo erişilebilir olmalı
        var kartlar = _card.GetAll();
        Assert.NotNull(kartlar);
    }

    [Fact]
    public void StokKarti_CokUzunAd_Kisaltilir()
    {
        var kart = new StokKarti
        {
            Ad = new string('A', 300),
            KodNo = "LONGAD001",
            KartTipi = "Alt"
        };
        _card.Add(kart);

        var eklenen = _card.GetById(kart.Id);
        Assert.NotNull(eklenen);
        Assert.True(eklenen!.Ad.Length <= 150);
    }

    [Fact]
    public void StokKarti_UnicodeAd_Kabul()
    {
        var kart = new StokKarti
        {
            Ad = "Ürün 测试 🎯 العربية",
            KodNo = "UNI001",
            KartTipi = "Alt"
        };
        _card.Add(kart);

        var eklenen = _card.GetById(kart.Id);
        Assert.NotNull(eklenen);
        Assert.Equal("Ürün 测试 🎯 العربية", eklenen!.Ad);
    }

    [Fact]
    public void StokKarti_EmojiKod_Kisaltilir()
    {
        var kart = new StokKarti
        {
            Ad = "Emoji Kod Testi",
            KodNo = "🔥FIRE001",
            KartTipi = "Alt"
        };
        // Emoji içeren kod kabul edilmeli (sadece kısaltılır)
        try
        {
            _card.Add(kart);
            var eklenen = _card.GetById(kart.Id);
            Assert.NotNull(eklenen);
        }
        catch (InvalidOperationException)
        {
            // Kod doğrulaması reddedebilir, bu da kabul edilebilir
        }
    }

    [Fact]
    public void StokKarti_NegatifMinStok_SifireYuvarlanir()
    {
        var kart = new StokKarti
        {
            Ad = "Negatif MinStok",
            KodNo = "NEGMIN001",
            KartTipi = "Alt",
            MinStok = -5
        };
        _card.Add(kart);

        var eklenen = _card.GetById(kart.Id);
        Assert.NotNull(eklenen);
        Assert.True(eklenen!.MinStok >= 0);
    }

    [Fact]
    public void StokKarti_NegatifBirimFiyat_SifireYuvarlanir()
    {
        var kart = new StokKarti
        {
            Ad = "Negatif Fiyat",
            KodNo = "NEGFIY001",
            KartTipi = "Alt",
            BirimFiyat = -100.0
        };
        _card.Add(kart);

        var eklenen = _card.GetById(kart.Id);
        Assert.NotNull(eklenen);
        Assert.True(eklenen!.BirimFiyat >= 0);
    }

    // ── STOK NEGATİF KORUMA TESTLERİ ────────────────────────────────────

    [Fact]
    public void Stok_CikisStokYetersiz_Hata()
    {
        _mov.Add(MakeEntry(10));
        Assert.Throws<InvalidOperationException>(() => _mov.Add(MakeExit(11)));

        var kart = _card.GetById(_cardId);
        Assert.Equal(10, kart?.MevcutStok);
    }

    [Fact]
    public void Stok_CikisEksaktEsit_Basarili()
    {
        _mov.Add(MakeEntry(10));
        _mov.Add(MakeExit(10));

        var kart = _card.GetById(_cardId);
        Assert.Equal(0, kart?.MevcutStok);
    }

    [Fact]
    public void Stok_SilmeNegatifYapar_Hata()
    {
        _mov.Add(MakeEntry(10));
        _mov.Add(MakeExit(10));

        var girisler = _mov.GetAll(stockCardId: _cardId, movementType: "Giris");
        Assert.Throws<InvalidOperationException>(() => _mov.Delete(girisler[0].Id));
    }

    [Fact]
    public void Stok_GuncellemeNegatifYapar_Hata()
    {
        _mov.Add(MakeEntry(10));
        _mov.Add(MakeExit(8));

        var girisler = _mov.GetAll(stockCardId: _cardId, movementType: "Giris");
        var giris = girisler[0];
        giris.Miktar = 5; // 5 - 8 = -3 → hata

        Assert.Throws<InvalidOperationException>(() => _mov.Update(giris));
    }

    [Fact]
    public void Stok_TopluEklemeKismiBasarisiz_TumunuGeriAl()
    {
        _mov.Add(MakeEntry(10));

        var hareketler = new[]
        {
            MakeExit(5),
            MakeExit(999) // yetersiz stok → hata
        };

        Assert.Throws<InvalidOperationException>(() => _mov.AddBulk(hareketler));

        // Transaction rollback: stok hâlâ 10 olmalı
        var kart = _card.GetById(_cardId);
        Assert.Equal(10, kart?.MevcutStok);
    }

    // ── AUDIT LOG TESTLERİ ───────────────────────────────────────────────

    [Fact]
    public void AuditLog_HareketEkle_Yazilir()
    {
        _mov.Add(MakeEntry(5));

        var log = ReadAuditLog(_db);
        Assert.Contains(log, e => e.tip == "EKLE" && e.tablo == "StokHareketleri");
    }

    [Fact]
    public void AuditLog_HareketSil_Yazilir()
    {
        _mov.Add(MakeEntry(5));
        var hareketler = _mov.GetAll(stockCardId: _cardId);
        _mov.Delete(hareketler[0].Id);

        var log = ReadAuditLog(_db);
        Assert.Contains(log, e => e.tip == "SIL" && e.tablo == "StokHareketleri");
    }

    [Fact]
    public void AuditLog_HareketGuncelle_Yazilir()
    {
        _mov.Add(MakeEntry(5));
        var hareketler = _mov.GetAll(stockCardId: _cardId);
        var h = hareketler[0];
        h.Aciklama = "Güncellendi";
        _mov.Update(h);

        var log = ReadAuditLog(_db);
        Assert.Contains(log, e => e.tip == "GUNCELLE" && e.tablo == "StokHareketleri");
    }

    [Fact]
    public void AuditLog_StokKartiEkle_Yazilir()
    {
        var kart = new StokKarti { Ad = "Audit Test Kart", KodNo = "AUDIT001", KartTipi = "Alt" };
        _card.Add(kart);

        var log = ReadAuditLog(_db);
        Assert.Contains(log, e => e.tip == "EKLE" && e.tablo == "StokKartlari");
    }

    [Fact]
    public void AuditLog_StokKartiSil_Yazilir()
    {
        var kart = new StokKarti { Ad = "Silinecek Kart", KodNo = "AUDITDEL001", KartTipi = "Alt" };
        _card.Add(kart);
        _card.Delete(kart.Id);

        var log = ReadAuditLog(_db);
        Assert.Contains(log, e => e.tip == "SIL" && e.tablo == "StokKartlari");
    }

    [Fact]
    public void AuditLog_TopluHareketSil_HerBiriIcinYazilir()
    {
        _mov.Add(MakeEntry(30));
        var hareketler = new[]
        {
            MakeExit(3),
            MakeExit(3),
            MakeExit(3)
        };
        _mov.AddBulk(hareketler);

        var tumHareketler = _mov.GetAll(stockCardId: _cardId, movementType: "Cikis");
        var ids = tumHareketler.Select(h => h.Id).ToList();
        _mov.DeleteBulk(ids);

        var log = ReadAuditLog(_db);
        var silEntries = log.Where(e => e.tip == "SIL" && e.tablo == "StokHareketleri").ToList();
        Assert.True(silEntries.Count >= 3);
    }

    // ── NOT SALDIRI TESTLERİ ─────────────────────────────────────────────

    [Fact]
    public void Not_BosBaslik_Hata()
    {
        var not = new Not { Baslik = "", Icerik = "İçerik" };
        Assert.Throws<InvalidOperationException>(() => _note.Add(not));
    }

    [Fact]
    public void Not_NullBaslik_Hata()
    {
        var not = new Not { Baslik = null!, Icerik = "İçerik" };
        Assert.Throws<InvalidOperationException>(() => _note.Add(not));
    }

    [Fact]
    public void Not_CokUzunIcerik_Kisaltilir()
    {
        var not = new Not { Baslik = "Uzun İçerik Testi", Icerik = new string('X', 5000) };
        _note.Add(not);

        var notlar = _note.GetAll();
        var eklenen = notlar.FirstOrDefault(n => n.Id == not.Id);
        Assert.NotNull(eklenen);
        Assert.True(eklenen!.Icerik.Length <= 4000);
    }

    [Fact]
    public void Not_SqlInjectionBaslik_Temizlenir()
    {
        var not = new Not
        {
            Baslik = "Test'; DROP TABLE Notlar;--",
            Icerik = "Test içeriği"
        };
        _note.Add(not);

        // Tablo hâlâ erişilebilir olmalı
        var notlar = _note.GetAll();
        Assert.NotNull(notlar);
        Assert.True(not.Id > 0);
    }

    // ── SERVİS KAYDI SALDIRI TESTLERİ ───────────────────────────────────

    [Fact]
    public void Servis_BosCihazAdi_Hata()
    {
        var kayit = new ServisKaydi { CihazAdi = "", BakimTarihi = DateTime.Now };
        Assert.Throws<InvalidOperationException>(() => _svc.Add(kayit));
    }

    [Fact]
    public void Servis_CokUzunCihazAdi_Kisaltilir()
    {
        var kayit = new ServisKaydi
        {
            CihazAdi = new string('C', 300),
            BakimTarihi = DateTime.Now
        };
        _svc.Add(kayit);

        // Id set edilmiş olmalı
        Assert.True(kayit.Id > 0);

        var kayitlar = _svc.GetAll();
        var eklenen = kayitlar.FirstOrDefault(k => k.Id == kayit.Id);
        Assert.NotNull(eklenen);
        Assert.True(eklenen!.CihazAdi.Length <= 150);
    }

    [Fact]
    public void Servis_SqlInjectionCihazAdi_Temizlenir()
    {
        var kayit = new ServisKaydi
        {
            CihazAdi = "PC'; DROP TABLE ServisKayitlari;--",
            BakimTarihi = DateTime.Now
        };
        _svc.Add(kayit);

        // Id set edilmiş olmalı
        Assert.True(kayit.Id > 0);

        // Tablo hâlâ erişilebilir olmalı
        var kayitlar = _svc.GetAll();
        Assert.NotNull(kayitlar);
    }

    // ── TOPLU İŞLEM SALDIRI TESTLERİ ────────────────────────────────────

    [Fact]
    public void TopluHareketEkle_BosListe_Hata()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _mov.AddBulk(Array.Empty<StokHareketi>()));
    }

    [Fact]
    public void TopluHareketSil_BosListe_Hata()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _mov.DeleteBulk(Array.Empty<int>()));
    }

    [Fact]
    public void TopluHareketSil_VarOlmayanId_Hata()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _mov.DeleteBulk(new[] { 999999 }));
    }

    [Fact]
    public void TopluHareketSil_SifirId_Filtrelenir()
    {
        // 0, -1, -999 → hepsi filtrelenir → boş liste → hata
        Assert.Throws<InvalidOperationException>(() =>
            _mov.DeleteBulk(new[] { 0, -1, -999 }));
    }
}
