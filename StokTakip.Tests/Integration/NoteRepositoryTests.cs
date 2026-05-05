using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// <see cref="INoteRepository"/> implementasyonu için integration testler.
/// </summary>
public class NoteRepositoryTests : IDisposable
{
    private readonly TestDatabaseFactory _factory;
    private readonly INoteRepository _repo;

    public NoteRepositoryTests()
    {
        _factory = new TestDatabaseFactory();
        _repo = _factory.Create();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Add_ValidNote_AssignsId()
    {
        var not = new Not { Baslik = "Test Not", Icerik = "İçerik", Tarih = DateTime.Now };

        _repo.Add(not);

        Assert.True(not.Id > 0);
    }

    [Fact]
    public void Add_EmptyTitle_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _repo.Add(new Not { Baslik = "", Icerik = "İçerik" }));
    }

    [Fact]
    public void GetAll_ReturnsAllNotes()
    {
        _repo.Add(new Not { Baslik = "Not 1", Tarih = DateTime.Now });
        _repo.Add(new Not { Baslik = "Not 2", Tarih = DateTime.Now });

        var result = _repo.GetAll();

        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void GetAll_OrderedByDateDescending()
    {
        _repo.Add(new Not { Baslik = "Eski Not", Tarih = DateTime.Now.AddDays(-5) });
        _repo.Add(new Not { Baslik = "Yeni Not", Tarih = DateTime.Now });

        var result = _repo.GetAll();

        Assert.True(result[0].Tarih >= result[1].Tarih);
    }

    [Fact]
    public void Update_ExistingNote_ChangesArePersisted()
    {
        var not = new Not { Baslik = "Eski Başlık", Tarih = DateTime.Now };
        _repo.Add(not);

        not.Baslik = "Yeni Başlık";
        _repo.Update(not);

        var all = _repo.GetAll();
        Assert.Contains(all, n => n.Baslik == "Yeni Başlık");
    }

    [Fact]
    public void Update_NonExistingNote_ThrowsInvalidOperationException()
    {
        var not = new Not { Id = 99999, Baslik = "Yok", Tarih = DateTime.Now };
        Assert.Throws<InvalidOperationException>(() => _repo.Update(not));
    }

    [Fact]
    public void Delete_ExistingNote_RemovesFromDatabase()
    {
        var not = new Not { Baslik = "Silinecek", Tarih = DateTime.Now };
        _repo.Add(not);

        _repo.Delete(not.Id);

        var all = _repo.GetAll();
        Assert.DoesNotContain(all, n => n.Id == not.Id);
    }

    [Fact]
    public void Delete_NonExistingId_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _repo.Delete(99999));
    }
}
