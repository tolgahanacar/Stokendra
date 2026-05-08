using StokTakip.Models;
using StokTakip.Tests.Helpers;
using Microsoft.Data.Sqlite;

namespace StokTakip.Tests.Integration;

/// <summary>
/// Agresif girdi doğrulama ve SQL injection saldırı testleri.
/// Yeni Repository pattern kullanır.
/// </summary>
public class ValidationAttackTests : IDisposable
{
    private readonly TestDb _db;
    private readonly int _cardId;

    public ValidationAttackTests()
    {
        _db = new TestDb();
        _cardId = _db.CreateCard("Attack Test Stok", "ATK001");
    }

    public void Dispose() => _db.Dispose();

    private StokHareketi MakeEntry(double miktar = 10) => new()
    {
        StokKartId = _cardId, Tur = "Giris", Miktar = miktar, Departman = "IT"
    };

    private StokHareketi MakeExit(double miktar = 5) => new()
    {
        StokKartId = _cardId, Tur = "Cikis", Miktar = miktar, Departman = "IT"
    };

    // ── Miktar saldırıları ────────────────────────────────────────────────

    [Fact] public void Miktar_Sifir_Hata() =>
        Assert.Throws<InvalidOperationException>(() => _db.Movements.Add(MakeEntry(0)));

    [Fact] public void Miktar_Negatif_Hata() =>
        Assert.Throws<InvalidOperationException>(() => _db.Movements.Add(MakeEntry(-1)));

    [Fact] public void Miktar_DoubleMaxValue_Hata() =>
        Assert.Throws<InvalidOperationException>(() => _db.Movements.Add(MakeEntry(double.MaxValue)));

    [Fact] public void Miktar_PositiveInfinity_Hata() =>
        Assert.Throws<InvalidOperationException>(() => _db.Movements.Add(MakeEntry(double.PositiveInfinity)));

    [Fact] public void Miktar_NaN_Hata() =>
        Assert.Throws<InvalidOperationException>(() => _db.Movements.Add(MakeEntry(double.NaN)));

    [Fact]
    public void Miktar_OndalikliDeger_Kabul()
    {
        _db.Movements.Add(MakeEntry(1.5));
        Assert.Equal(1.5, _db.GetStock(_cardId));
    }

    // ── Tür saldırıları ───────────────────────────────────────────────────

    [Fact] public void Tur_GecersizDeger_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi { StokKartId = _cardId, Tur = "HACK", Miktar = 1, Departman = "IT" }));

    [Fact] public void Tur_BosString_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi { StokKartId = _cardId, Tur = "", Miktar = 1, Departman = "IT" }));

    [Fact]
    public void Tur_SqlInjection_TabloSaglamKalir()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi
            {
                StokKartId = _cardId,
                Tur = "Giris'; DROP TABLE StokHareketleri;--",
                Miktar = 1, Departman = "IT"
            }));

        // Tablo hâlâ erişilebilir
        Assert.NotNull(_db.Movements.GetAll());
    }

    // ── StokKartId saldırıları ────────────────────────────────────────────

    [Fact] public void StokKartId_Sifir_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi { StokKartId = 0, Tur = "Giris", Miktar = 1, Departman = "IT" }));

    [Fact] public void StokKartId_Negatif_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi { StokKartId = -1, Tur = "Giris", Miktar = 1, Departman = "IT" }));

    [Fact] public void StokKartId_VarOlmayan_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi { StokKartId = 999999, Tur = "Giris", Miktar = 1, Departman = "IT" }));

    [Fact]
    public void StokKartId_UstKart_Hata()
    {
        var ust = new StokKarti { Ad = "Üst", KodNo = "UST_ATK", KartTipi = "Ust" };
        _db.StockCards.Add(ust);
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.Add(new StokHareketi { StokKartId = ust.Id, Tur = "Giris", Miktar = 1, Departman = "IT" }));
    }

    // ── Stok negatif koruma ───────────────────────────────────────────────

    [Fact]
    public void Stok_CikisYetersiz_HataVeStokDegismez()
    {
        _db.AddEntry(_cardId, 10);
        Assert.Throws<InvalidOperationException>(() => _db.AddExit(_cardId, 11));
        Assert.Equal(10, _db.GetStock(_cardId));
    }

    [Fact]
    public void Stok_CikisEksakt_Basarili()
    {
        _db.AddEntry(_cardId, 10);
        _db.AddExit(_cardId, 10);
        Assert.Equal(0, _db.GetStock(_cardId));
    }

    [Fact]
    public void Stok_SilmeNegatifYapar_Hata()
    {
        _db.AddEntry(_cardId, 10);
        _db.AddExit(_cardId, 10);
        var entry = _db.Movements.GetAll(stockCardId: _cardId, movementType: "Giris").Single();
        Assert.Throws<InvalidOperationException>(() => _db.Movements.Delete(entry.Id));
    }

    [Fact]
    public void Stok_TopluEklemeKismiBasarisiz_TumunuGeriAl()
    {
        _db.AddEntry(_cardId, 10);
        var hareketler = new[]
        {
            MakeExit(5),
            MakeExit(999) // yetersiz → hata
        };
        Assert.Throws<InvalidOperationException>(() => _db.Movements.AddBulk(hareketler));
        // Rollback: stok hâlâ 10
        Assert.Equal(10, _db.GetStock(_cardId));
    }

    // ── Stok kartı saldırıları ────────────────────────────────────────────

    [Fact] public void StokKarti_BosAd_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.StockCards.Add(new StokKarti { Ad = "", KodNo = "E001", KartTipi = "Alt" }));

    [Fact] public void StokKarti_BosKod_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.StockCards.Add(new StokKarti { Ad = "Ad", KodNo = "", KartTipi = "Alt" }));

    [Fact] public void StokKarti_DuplicateKod_Hata()
    {
        _db.StockCards.Add(new StokKarti { Ad = "A", KodNo = "DUP001", KartTipi = "Alt" });
        Assert.Throws<InvalidOperationException>(() =>
            _db.StockCards.Add(new StokKarti { Ad = "B", KodNo = "DUP001", KartTipi = "Alt" }));
    }

    [Fact]
    public void StokKarti_SqlInjectionAd_TabloSaglamKalir()
    {
        var kart = new StokKarti
        {
            Ad = "Test'); DROP TABLE StokKartlari;--",
            KodNo = "SQLINJ001", KartTipi = "Alt"
        };
        _db.StockCards.Add(kart);
        Assert.True(kart.Id > 0);
        Assert.NotNull(_db.StockCards.GetAll());
    }

    [Fact]
    public void StokKarti_NegatifMinStok_SifireYuvarlanir()
    {
        var kart = new StokKarti { Ad = "Test", KodNo = "NEG001", KartTipi = "Alt", MinStok = -5 };
        _db.StockCards.Add(kart);
        Assert.True(_db.StockCards.GetById(kart.Id)!.MinStok >= 0);
    }

    [Fact]
    public void StokKarti_NegatifBirimFiyat_SifireYuvarlanir()
    {
        var kart = new StokKarti { Ad = "Test", KodNo = "NEG002", KartTipi = "Alt", BirimFiyat = -100 };
        _db.StockCards.Add(kart);
        Assert.True(_db.StockCards.GetById(kart.Id)!.BirimFiyat >= 0);
    }

    [Fact]
    public void StokKarti_UnicodeAd_Kabul()
    {
        var kart = new StokKarti { Ad = "Ürün 测试 🎯", KodNo = "UNI001", KartTipi = "Alt" };
        _db.StockCards.Add(kart);
        Assert.Equal("Ürün 测试 🎯", _db.StockCards.GetById(kart.Id)!.Ad);
    }

    // ── Not saldırıları ───────────────────────────────────────────────────

    [Fact] public void Not_BosBaslik_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Notes.Add(new Not { Baslik = "", Icerik = "İçerik" }));

    [Fact]
    public void Not_SqlInjectionBaslik_TabloSaglamKalir()
    {
        var not = new Not { Baslik = "Test'; DROP TABLE Notlar;--", Icerik = "İçerik" };
        _db.Notes.Add(not);
        Assert.True(not.Id > 0);
        Assert.NotNull(_db.Notes.GetAll());
    }

    // ── Servis saldırıları ────────────────────────────────────────────────

    [Fact] public async Task Servis_BosCihazAdi_Hata() =>
        await Assert.ThrowsAsync<Exception>(async () =>
            await _db.Services.AddAsync(new ServisKaydi { CihazAdi = "", BakimTarihi = DateTime.Now }));

    [Fact]
    public async Task Servis_SqlInjectionCihazAdi_TabloSaglamKalir()
    {
        var kayit = new ServisKaydi
        {
            CihazAdi = "PC'; DROP TABLE ServisKayitlari;--",
            BakimTarihi = DateTime.Now
        };
        await _db.Services.AddAsync(kayit);
        Assert.True(kayit.Id > 0);
        Assert.NotNull(await _db.Services.GetAllAsync());
    }

    // ── Toplu işlem saldırıları ───────────────────────────────────────────

    [Fact] public void TopluHareketEkle_BosListe_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.AddBulk(Array.Empty<StokHareketi>()));

    [Fact] public void TopluHareketSil_BosListe_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.DeleteBulk(Array.Empty<int>()));

    [Fact] public void TopluHareketSil_VarOlmayanId_Hata() =>
        Assert.Throws<InvalidOperationException>(() =>
            _db.Movements.DeleteBulk(new[] { 999999 }));
}
