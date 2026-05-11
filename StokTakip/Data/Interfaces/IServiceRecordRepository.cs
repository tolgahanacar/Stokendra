using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StokTakip.Models;

namespace StokTakip.Data.Interfaces;

/// <summary>
/// Servis kaydı CRUD ve sorgulama işlemleri için repository arayüzü (Asenkron).
/// </summary>
public interface IServiceRecordRepository
{
    /// <summary>
    /// Servis kayıtlarını getirir. Tüm parametreler opsiyoneldir.
    /// </summary>
    Task<List<ServisKaydi>> GetAllAsync(DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null);

    /// <summary>Yeni bir servis kaydı ekler.</summary>
    Task AddAsync(ServisKaydi kayit);

    /// <summary>Birden fazla servis kaydını toplu ekler.</summary>
    Task AddBulkAsync(IEnumerable<ServisKaydi> kayitlar);

    /// <summary>Mevcut bir servis kaydını günceller.</summary>
    Task UpdateAsync(ServisKaydi kayit);

    /// <summary>Belirtilen ID'ye sahip servis kaydını siler.</summary>
    Task DeleteAsync(int id);

    /// <summary>Sayfalanmış servis kayıtlarını getirir.</summary>
    Task<List<ServisKaydi>> GetPagedAsync(int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null);

    /// <summary>Filtrelere uyan toplam kayıt sayısını getirir.</summary>
    Task<int> GetCountAsync(DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null);
}
