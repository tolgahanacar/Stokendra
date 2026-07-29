using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stokendra.Models;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for ServiceRecord CRUD and query operations (Async).
/// </summary>
public interface IServiceRecordRepository
{
    /// <summary>Gets all service records matching the specified filters.</summary>
    Task<List<ServiceRecord>> GetAllAsync(DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null, CancellationToken cancellationToken = default);

    /// <summary>Adds a new service record.</summary>
    Task AddAsync(ServiceRecord record, CancellationToken cancellationToken = default);

    /// <summary>Adds multiple service records in bulk.</summary>
    Task AddBulkAsync(IEnumerable<ServiceRecord> records, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing service record.</summary>
    Task UpdateAsync(ServiceRecord record, CancellationToken cancellationToken = default);

    /// <summary>Deletes the service record with the specified ID.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple service records in bulk.</summary>
    Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Gets paged service records matching the specified filters.</summary>
    Task<List<ServiceRecord>> GetPagedAsync(int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null, CancellationToken cancellationToken = default);

    /// <summary>Gets total count of service records matching the specified filters.</summary>
    Task<int> GetCountAsync(DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null, CancellationToken cancellationToken = default);
}
