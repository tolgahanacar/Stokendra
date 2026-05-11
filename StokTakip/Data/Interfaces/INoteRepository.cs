using StokTakip.Models;

namespace StokTakip.Data.Interfaces;

/// <summary>
/// Not CRUD işlemleri için repository arayüzü.
/// </summary>
public interface INoteRepository
{
    /// <summary>Tüm notları tarihe göre azalan sırada getirir.</summary>
    List<Not> GetAll();

    /// <summary>Tüm notları asenkron getirir.</summary>
    Task<List<Not>> GetAllAsync();

    /// <summary>Yeni bir not ekler. Eklenen kaydın ID'si <paramref name="not"/>.Id alanına yazılır.</summary>
    void Add(Not not);

    /// <summary>Mevcut bir notu günceller.</summary>
    void Update(Not not);

    /// <summary>Belirtilen ID'ye sahip notu siler.</summary>
    void Delete(int id);

    /// <summary>Birden fazla notu toplu siler.</summary>
    void DeleteBulk(IEnumerable<int> ids);
}
