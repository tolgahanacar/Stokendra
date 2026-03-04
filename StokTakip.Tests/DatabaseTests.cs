using StokTakip.Data;
using StokTakip.Models;

namespace StokTakip.Tests;

/// <summary>Database layer tests using in-memory SQLite.</summary>
public class DatabaseTests : IDisposable
{
    private readonly Database _db;
    private readonly string _tempPath;

    public DatabaseTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"stokendra_test_{Guid.NewGuid():N}.db");
        _db = new Database(_tempPath);
    }

    public void Dispose()
    {
        try { File.Delete(_tempPath); } catch { }
    }

    // ═══ STOK KARTLARI ═══

    [Fact]
    public void StokKartiEkle_VeGetir_Basarili()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "Test \u00dcr\u00fcn", KodNo = "001", KartTipi = "Alt", Kategori = "Toner/Kartu\u015f" });
        var kartlar = _db.StokKartlariniGetir();
        Assert.Single(kartlar);
        Assert.Equal("Test \u00dcr\u00fcn", kartlar[0].Ad);
        Assert.Equal("001", kartlar[0].KodNo);
        Assert.Equal("Alt", kartlar[0].KartTipi);
    }

    [Fact]
    public void StokKartiGuncelle_Basarili()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "Eski Ad", KodNo = "002", KartTipi = "Alt" });
        var kart = _db.StokKartlariniGetir().First();
        kart.Ad = "Yeni Ad"; kart.Kategori = "Yedek Par\u00e7a";
        _db.StokKartiGuncelle(kart);
        var guncellenen = _db.StokKartiDetayGetir(kart.Id);
        Assert.NotNull(guncellenen);
        Assert.Equal("Yeni Ad", guncellenen!.Ad);
    }

    [Fact]
    public void StokKartiSil_Basarili()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "Silinecek", KodNo = "003", KartTipi = "Alt" });
        var kart = _db.StokKartlariniGetir().First();
        _db.StokKartiSil(kart.Id);
        Assert.Empty(_db.StokKartlariniGetir());
    }

    [Fact]
    public void OtoStokKodu_ArttirarakDevamEder()
    {
        Assert.Equal("001", _db.SonrakiStokKodu());
        _db.StokKartiEkle(new StokKarti { Ad = "A", KodNo = "001", KartTipi = "Alt" });
        Assert.Equal("002", _db.SonrakiStokKodu());
        _db.StokKartiEkle(new StokKarti { Ad = "B", KodNo = "002", KartTipi = "Alt" });
        Assert.Equal("003", _db.SonrakiStokKodu());
    }

    [Fact]
    public void UstAltKart_Iliskisi()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "\u00dcst Kart", KodNo = "U01", KartTipi = "Ust" });
        var ust = _db.StokKartlariniGetir().First(k => k.KartTipi == "Ust");
        _db.StokKartiEkle(new StokKarti { Ad = "Alt Kart", KodNo = "A01", KartTipi = "Alt", UstKartId = ust.Id });

        var ustler = _db.UstKartlariGetir();
        var altlar = _db.AltKartlariGetir(ust.Id);
        Assert.Single(ustler);
        Assert.Single(altlar);
        Assert.Equal("Alt Kart", altlar[0].Ad);
    }

    // ═══ STOK HAREKETLERİ ═══

    [Fact]
    public void HareketEkle_StokHesaplanir()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "Toner", KodNo = "T01", KartTipi = "Alt" });
        var kart = _db.StokKartlariniGetir().First();

        _db.HareketEkle(new StokHareketi { StokKartId = kart.Id, Tur = "Giris", Miktar = 10, Tarih = DateTime.Now });
        _db.HareketEkle(new StokHareketi { StokKartId = kart.Id, Tur = "Cikis", Miktar = 3, Tarih = DateTime.Now });

        var guncellenen = _db.StokKartiDetayGetir(kart.Id);
        Assert.Equal(7, guncellenen!.MevcutStok);
    }

    [Fact]
    public void HareketGuncelle_Basarili()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "X", KodNo = "X01", KartTipi = "Alt" });
        var kart = _db.StokKartlariniGetir().First();
        _db.HareketEkle(new StokHareketi { StokKartId = kart.Id, Tur = "Giris", Miktar = 5, Tarih = DateTime.Now });

        var hareket = _db.HareketleriGetir(kart.Id).First();
        hareket.Miktar = 20;
        _db.HareketGuncelle(hareket);

        var guncellenen = _db.StokKartiDetayGetir(kart.Id);
        Assert.Equal(20, guncellenen!.MevcutStok);
    }

    [Fact]
    public void HareketSil_Basarili()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "Y", KodNo = "Y01", KartTipi = "Alt" });
        var kart = _db.StokKartlariniGetir().First();
        _db.HareketEkle(new StokHareketi { StokKartId = kart.Id, Tur = "Giris", Miktar = 5, Tarih = DateTime.Now });
        var hareket = _db.HareketleriGetir(kart.Id).First();
        _db.HareketSil(hareket.Id);
        Assert.Empty(_db.HareketleriGetir(kart.Id));
    }

    [Fact]
    public void HareketFiltre_CalisiR()
    {
        _db.StokKartiEkle(new StokKarti { Ad = "F1", KodNo = "F01", KartTipi = "Alt" });
        var kart = _db.StokKartlariniGetir().First();
        _db.DepartmanEkle("IT");
        _db.HareketEkle(new StokHareketi { StokKartId = kart.Id, Tur = "Giris", Miktar = 5, Departman = "IT", Tarih = new DateTime(2026, 1, 15) });
        _db.HareketEkle(new StokHareketi { StokKartId = kart.Id, Tur = "Cikis", Miktar = 2, Departman = "HR", Tarih = new DateTime(2026, 3, 10) });

        var it = _db.HareketleriGetir(departman: "IT");
        Assert.Single(it);
        var giris = _db.HareketleriGetir(tur: "Giris");
        Assert.Single(giris);
    }

    // ═══ AUTH ═══

    [Fact]
    public void VarsayilanAdmin_GirisBasarili()
    {
        Assert.True(_db.KullaniciDogrula("admin", "admin"));
    }

    [Fact]
    public void YanlisKullanici_GirisBasarisiz()
    {
        Assert.False(_db.KullaniciDogrula("admin", "wrongpassword"));
        Assert.False(_db.KullaniciDogrula("nonexistent", "admin"));
    }

    [Fact]
    public void SifreDegistir_Basarili()
    {
        Assert.True(_db.SifreDegistir("admin", "admin", "yenisifre123"));
        Assert.True(_db.KullaniciDogrula("admin", "yenisifre123"));
        Assert.False(_db.KullaniciDogrula("admin", "admin"));
    }

    [Fact]
    public void SifreDegistir_EskiSifreYanlis()
    {
        Assert.False(_db.SifreDegistir("admin", "yanlis", "yenisifre"));
        // Eski şifre hala çalışıyor olmalı
        Assert.True(_db.KullaniciDogrula("admin", "admin"));
    }

    // ═══ DEPARTMANLAR ═══

    [Fact]
    public void DepartmanEkleSil_Basarili()
    {
        _db.DepartmanEkle("Bilgi \u0130\u015flem");
        _db.DepartmanEkle("Muhasebe");
        var dept = _db.DepartmanlariGetir();
        Assert.Equal(2, dept.Count);
        _db.DepartmanSil("Muhasebe");
        Assert.Single(_db.DepartmanlariGetir());
    }

    // ═══ NOTLAR ═══

    [Fact]
    public void NotEkleDuzenle_Basarili()
    {
        _db.NotEkle(new Not { Baslik = "Test Notu", Icerik = "\u0130\u00e7erik", Tarih = DateTime.Now });
        var notlar = _db.NotlariGetir();
        Assert.Single(notlar);
        Assert.Equal("Test Notu", notlar[0].Baslik);

        notlar[0].Baslik = "G\u00fcncellendi";
        _db.NotGuncelle(notlar[0]);
        Assert.Equal("G\u00fcncellendi", _db.NotlariGetir()[0].Baslik);
    }

    [Fact]
    public void NotSil_Basarili()
    {
        _db.NotEkle(new Not { Baslik = "Silinecek", Tarih = DateTime.Now });
        var n = _db.NotlariGetir().First();
        _db.NotSil(n.Id);
        Assert.Empty(_db.NotlariGetir());
    }

    // ═══ CONFIG ═══

    [Fact]
    public void AppConfig_OkuYaz()
    {
        _db.SetConfig("test_key", "test_value");
        Assert.Equal("test_value", _db.GetConfig("test_key"));
        Assert.Equal("default", _db.GetConfig("nonexistent", "default"));
    }
}
