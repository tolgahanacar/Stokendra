namespace StokTakip.Data.Interfaces;

/// <summary>
/// Departman yönetimi için repository arayüzü.
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>Tüm departman adlarını alfabetik sırada getirir.</summary>
    List<string> GetAll();

    /// <summary>Yeni bir departman ekler. Aynı isim zaten varsa sessizce geçer.</summary>
    void Add(string name);

    /// <summary>Belirtilen isimdeki departmanı siler.</summary>
    void Delete(string name);
}
