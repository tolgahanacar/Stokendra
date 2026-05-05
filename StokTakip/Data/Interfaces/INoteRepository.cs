using StokTakip.Models;

namespace StokTakip.Data.Interfaces;

/// <summary>
/// Not CRUD işlemleri için repository arayüzü.
/// </summary>
public interface INoteRepository
{
    /// <summary>Tüm notları tarihe göre azalan sırada getirir.</summary>
    List<Not> GetAll();

    /// <summary>Yeni bir not ekler. Eklenen kaydın ID'si <paramref name="not"/>.Id alanına yazılır.</summary>
    void Add(Not not);

    /// <summary>Mevcut bir notu günceller.</summary>
    void Update(Not not);

    /// <summary>Belirtilen ID'ye sahip notu siler.</summary>
    void Delete(int id);
}
