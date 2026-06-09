using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stokendra.Models;

namespace Stokendra.Services;

/// <summary>
/// Business logic interface for stock movements.
/// </summary>
public interface IMovementService
{
    /// <summary>
    /// Returns a filtered and paged list of stock movements.
    /// </summary>
    PagedResult<StockMovement> GetPagedMovements(
        DateTime? startDate,
        DateTime? endDate,
        int? stockCardId,
        string? department,
        string? movementType,
        string? searchTerm,
        int page,
        int pageSize);

    /// <summary>
    /// Imports stock movements from an Excel file.
    /// </summary>
    Task<(int Imported, int Skipped, List<string> Warnings)> ImportMovementsFromXlsxAsync(string filePath);
}
