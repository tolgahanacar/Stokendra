using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// <see cref="IServiceRecordRepository"/> implementasyonu için integration testler.
/// </summary>
public class ServiceRecordRepositoryTests : IDisposable
{
    private readonly TestDatabaseFactory _factory;
    private readonly IServiceRecordRepository _repo;

    public ServiceRecordRepositoryTests()
    {
        _factory = new TestDatabaseFactory();
        _repo = _factory.Create();
    }

    public void Dispose() => _factory.Dispose();

    private static ServisKaydi MakeRecord(string cihazAdi = "Test Cihaz") => new()
    {
        CihazAdi = cihazAdi,
        SeriNumarasi = "SN-001",
        Firma = "Test Firma",
        BakimTarihi = DateTime.Now,
        Sorun = "Test sorun",
        Sonuc = "Çözüldü"
    };

    [Fact]
    public void Add_ValidRecord_CanBeRetrieved()
    {
        _repo.Add(MakeRecord("Laptop"));

        var result = _repo.GetAll();
        Assert.Contains(result, r => r.CihazAdi == "Laptop");
    }

    [Fact]
    public void Add_EmptyDeviceName_ThrowsInvalidOperationException()
    {
        var kayit = MakeRecord("");
        Assert.Throws<InvalidOperationException>(() => _repo.Add(kayit));
    }

    [Fact]
    public void GetAll_FilterByDateRange_ReturnsOnlyInRange()
    {
        var kayit = MakeRecord();
        kayit.BakimTarihi = DateTime.Today;
        _repo.Add(kayit);

        var result = _repo.GetAll(
            startDate: DateTime.Today.AddDays(-1),
            endDate: DateTime.Today.AddDays(1));

        Assert.NotEmpty(result);
    }

    [Fact]
    public void GetAll_FilterBySearchTerm_ReturnsMatchingRecords()
    {
        _repo.Add(MakeRecord("Yazıcı"));
        _repo.Add(MakeRecord("Bilgisayar"));

        var result = _repo.GetAll(searchTerm: "Yazıcı");

        Assert.All(result, r => Assert.Contains("Yazıcı", r.CihazAdi));
    }

    [Fact]
    public void Update_ExistingRecord_ChangesArePersisted()
    {
        _repo.Add(MakeRecord("Eski Cihaz"));
        var records = _repo.GetAll();
        var kayit = records[0];

        kayit.CihazAdi = "Yeni Cihaz";
        _repo.Update(kayit);

        var updated = _repo.GetAll();
        Assert.Contains(updated, r => r.CihazAdi == "Yeni Cihaz");
    }

    [Fact]
    public void Delete_ExistingRecord_RemovesFromDatabase()
    {
        _repo.Add(MakeRecord("Silinecek Cihaz"));
        var records = _repo.GetAll();
        int id = records[0].Id;

        _repo.Delete(id);

        var after = _repo.GetAll();
        Assert.DoesNotContain(after, r => r.Id == id);
    }

    [Fact]
    public void Delete_NonExistingId_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _repo.Delete(99999));
    }

    [Fact]
    public void AddBulk_MultipleRecords_AllPersisted()
    {
        var kayitlar = new[]
        {
            MakeRecord("Cihaz 1"),
            MakeRecord("Cihaz 2"),
            MakeRecord("Cihaz 3")
        };

        _repo.AddBulk(kayitlar);

        var result = _repo.GetAll();
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void AddBulk_EmptyList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _repo.AddBulk(Array.Empty<ServisKaydi>()));
    }
}
