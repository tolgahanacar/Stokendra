using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

public class NoteRepositoryTests : IDisposable
{
    private readonly TestDb _db;
    public NoteRepositoryTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void Add_ValidNote_Persisted()
    {
        var not = new Not { Baslik = "Test Not", Icerik = "İçerik", Tarih = DateTime.Now };
        _db.Notes.Add(not);

        var all = _db.Notes.GetAll();
        Assert.Single(all);
        Assert.Equal("Test Not", all[0].Baslik);
    }

    [Fact]
    public void GetAll_OrderedByDateDesc()
    {
        _db.Notes.Add(new Not { Baslik = "Eski", Icerik = "", Tarih = DateTime.Now.AddDays(-2) });
        _db.Notes.Add(new Not { Baslik = "Yeni", Icerik = "", Tarih = DateTime.Now });

        var all = _db.Notes.GetAll();
        Assert.Equal("Yeni", all[0].Baslik);
        Assert.Equal("Eski", all[1].Baslik);
    }

    [Fact]
    public void Update_ChangesTitle_Persisted()
    {
        var not = new Not { Baslik = "Eski Başlık", Icerik = "", Tarih = DateTime.Now };
        _db.Notes.Add(not);

        not.Baslik = "Yeni Başlık";
        _db.Notes.Update(not);

        Assert.Equal("Yeni Başlık", _db.Notes.GetAll()[0].Baslik);
    }

    [Fact]
    public void Delete_ExistingNote_Removed()
    {
        var not = new Not { Baslik = "Silinecek", Icerik = "", Tarih = DateTime.Now };
        _db.Notes.Add(not);

        _db.Notes.Delete(not.Id);

        Assert.Empty(_db.Notes.GetAll());
    }

    [Fact]
    public void GetAll_EmptyDb_ReturnsEmpty()
    {
        Assert.Empty(_db.Notes.GetAll());
    }

    [Fact]
    public void Add_MultipleNotes_AllPersisted()
    {
        for (int i = 1; i <= 5; i++)
            _db.Notes.Add(new Not { Baslik = $"Not {i}", Icerik = "", Tarih = DateTime.Now });

        Assert.Equal(5, _db.Notes.GetAll().Count);
    }
}
