using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stokendra.Models;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for StockMovement CRUD and query operations.
/// </summary>
public interface IMovementRepository
{
    /// <summary>Gets stock movements matching the specified filters.</summary>
    List<StockMovement> GetAll(
        int? stockCardId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? department = null,
        string? movementType = null,
        string? category = null,
        string? recipient = null);

    /// <summary>Gets stock movements asynchronously matching the specified filters.</summary>
    Task<List<StockMovement>> GetAllAsync(
        int? stockCardId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? department = null,
        string? movementType = null,
        string? category = null,
        string? recipient = null,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a single stock movement.</summary>
    void Add(StockMovement movement);

    /// <summary>Adds a single stock movement asynchronously.</summary>
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default);

    /// <summary>Adds multiple stock movements in bulk under a single transaction.</summary>
    void AddBulk(IEnumerable<StockMovement> movements);

    /// <summary>Adds multiple stock movements in bulk asynchronously.</summary>
    Task AddBulkAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing stock movement.</summary>
    void Update(StockMovement movement);

    /// <summary>Updates an existing stock movement asynchronously.</summary>
    Task UpdateAsync(StockMovement movement, CancellationToken cancellationToken = default);

    /// <summary>Deletes the stock movement with the specified ID.</summary>
    void Delete(int id);

    /// <summary>Deletes the stock movement with the specified ID asynchronously.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple stock movements in bulk.</summary>
    void DeleteBulk(IEnumerable<int> ids);

    /// <summary>Deletes multiple stock movements in bulk asynchronously.</summary>
    Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Updates multiple stock movements in bulk under a single transaction.</summary>
    void UpdateBulk(IEnumerable<StockMovement> movements);

    /// <summary>Updates multiple stock movements in bulk asynchronously.</summary>
    Task UpdateBulkAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default);

    /// <summary>Gets entry/exit summary data for the last 7 days.</summary>
    Task<List<(DateTime Date, double Entry, double Exit)>> GetLast7DaysSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a distinct list of recipients.</summary>
    List<string> GetDeliveredPersons();

    /// <summary>Gets a distinct list of recipients asynchronously.</summary>
    Task<List<string>> GetDeliveredPersonsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets paged stock movements matching the specified filters.</summary>
    Task<List<StockMovement>> GetPagedAsync(
        int page, int pageSize,
        int? stockCardId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? department = null,
        string? movementType = null,
        string? category = null,
        string? searchTerm = null,
        string? recipient = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets total count of stock movements matching the specified filters.</summary>
    Task<int> GetCountAsync(
        int? stockCardId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? department = null,
        string? movementType = null,
        string? category = null,
        string? searchTerm = null,
        string? recipient = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets aggregated consumption totals and top grouped chart items via SQL without loading all rows.</summary>
    Task<ConsumptionReportSummary> GetConsumptionSummaryAsync(
        int? stockCardId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? department = null,
        string? movementType = null,
        string? category = null,
        string? searchTerm = null,
        string? recipient = null,
        string fallbackLabel = "All",
        int topN = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Gets total Entry and Exit quantities for a specific stock card over the last 30 days.</summary>
    Task<(double TotalEntry, double TotalExit)> GetCardLast30DaysTotalsAsync(
        int stockCardId,
        CancellationToken cancellationToken = default);
}

/// <summary>Aggregated summary result for consumption reports computed directly in SQL.</summary>
public sealed record ConsumptionReportSummary(
    double TotalConsumption,
    int UniqueItemCount,
    int TotalRowCount,
    List<(string Name, double Total)> ChartData);
