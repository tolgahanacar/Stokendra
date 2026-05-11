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
    Task<List<ServisKaydi>> GetAllAsync(DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null, CancellationToken cancellationToken = default);

    /// <summary>Yeni bir servis kaydı ekler.</summary>
    Task AddAsync(ServisKaydi kayit, CancellationToken cancellationToken = default);

    /// <summary>Birden fazla servis kaydını toplu ekler.</summary>
    Task AddBulkAsync(IEnumerable<ServisKaydi> kayitlar, CancellationToken cancellationToken = default);

    /// <summary>Mevcut bir servis kaydını günceller.</summary>
    Task UpdateAsync(ServisKaydi kayit, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen ID'ye sahip servis kaydını siler.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Sayfalanmış servis kayıtlarını getirir.</summary>
    Task<List<ServisKaydi>> GetPagedAsync(int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null, CancellationToken cancellationToken = default);

    /// <summary>Filtrelere uyan toplam kayıt sayısını getirir.</summary>
    Task<int> GetCountAsync(DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null, CancellationToken cancellationToken = default);
}
