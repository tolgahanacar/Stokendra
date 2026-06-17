using System.Collections.Generic;
using System.Threading.Tasks;
using Stokendra.Models;

namespace Stokendra.Services;

/// <summary>
/// Business logic interface for stock cards.
/// </summary>
public interface IStockCardService
{
    /// <summary>
    /// Returns a filtered and paged list of stock cards asynchronously.
    /// </summary>
    Task<PagedResult<StockCard>> GetPagedStocksAsync(string? searchTerm, int page, int pageSize);

    /// <summary>
    /// Gets all stock cards asynchronously.
    /// </summary>
    Task<List<StockCard>> GetAllAsync();

}

/// <summary>Paged result model.</summary>
public record PagedResult<T>(List<T> Items, int TotalCount, int TotalPages, int CurrentPage);
