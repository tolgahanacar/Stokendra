using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

public class ServiceRecordRepositoryTests : IDisposable
{
    private readonly TestDb _db;
    public ServiceRecordRepositoryTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    private ServisKaydi MakeRecord(string cihaz = "Projeksiyon", string firma = "ABC") => new()
    {
        CihazAdi = cihaz, SeriNumarasi = "SN001", Firma = firma,
        BakimTarihi = DateTime.Today, Sorun = "Lamba arızası", Sonuc = "Değiştirildi"
    };

    [Fact]
    public async Task Add_ValidRecord_Persisted()
    {
        await _db.Services.AddAsync(MakeRecord());
        var all = await _db.Services.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Projeksiyon", all[0].CihazAdi);
    }

    [Fact]
    public async Task GetAll_NoFilter_ReturnsAll()
    {
        await _db.Services.AddAsync(MakeRecord("Cihaz1"));
        await _db.Services.AddAsync(MakeRecord("Cihaz2"));
        await _db.Services.AddAsync(MakeRecord("Cihaz3"));

        Assert.Equal(3, (await _db.Services.GetAllAsync()).Count);
    }

    [Fact]
    public async Task GetAll_FilterBySearch_ReturnsMatching()
    {
        await _db.Services.AddAsync(MakeRecord("Projeksiyon", "ABC"));
        await _db.Services.AddAsync(MakeRecord("Yazıcı", "XYZ"));

        var result = await _db.Services.GetAllAsync(searchTerm: "Projeksiyon");
        Assert.Single(result);
        Assert.Equal("Projeksiyon", result[0].CihazAdi);
    }

    [Fact]
    public async Task GetAll_FilterByDateRange_ReturnsInRange()
    {
        var r = MakeRecord();
        r.BakimTarihi = DateTime.Today;
        await _db.Services.AddAsync(r);

        var result = await _db.Services.GetAllAsync(
            startDate: DateTime.Today.AddDays(-1),
            endDate: DateTime.Today.AddDays(1));

        Assert.Single(result);
    }

    [Fact]
    public async Task Update_ChangesFields_Persisted()
    {
        var r = MakeRecord();
        await _db.Services.AddAsync(r);

        r.Sonuc = "Yeni Sonuç";
        await _db.Services.UpdateAsync(r);

        Assert.Equal("Yeni Sonuç", (await _db.Services.GetAllAsync())[0].Sonuc);
    }

    [Fact]
    public async Task Delete_ExistingRecord_Removed()
    {
        var r = MakeRecord();
        await _db.Services.AddAsync(r);

        await _db.Services.DeleteAsync(r.Id);

        Assert.Empty(await _db.Services.GetAllAsync());
    }

    [Fact]
    public async Task AddBulk_MultipleRecords_AllPersisted()
    {
        var records = new[]
        {
            MakeRecord("Cihaz1"),
            MakeRecord("Cihaz2"),
            MakeRecord("Cihaz3")
        };

        await _db.Services.AddBulkAsync(records);

        Assert.Equal(3, (await _db.Services.GetAllAsync()).Count);
    }

    [Fact]
    public async Task GetAll_OrderedByDateDesc()
    {
        var r1 = MakeRecord("Eski"); r1.BakimTarihi = DateTime.Today.AddDays(-5);
        var r2 = MakeRecord("Yeni"); r2.BakimTarihi = DateTime.Today;
        await _db.Services.AddAsync(r1);
        await _db.Services.AddAsync(r2);

        var all = await _db.Services.GetAllAsync();
        Assert.Equal("Yeni", all[0].CihazAdi);
    }
}
