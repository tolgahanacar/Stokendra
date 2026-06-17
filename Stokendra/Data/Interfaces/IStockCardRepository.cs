using Stokendra.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for StockCard CRUD operations.
/// </summary>
public interface IStockCardRepository
{
    /// <summary>Gets all stock cards.</summary>
    List<StockCard> GetAll();

    /// <summary>Gets all stock cards asynchronously.</summary>
    Task<List<StockCard>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets only parent cards (CardType = Parent).</summary>
    List<StockCard> GetParentCards();

    /// <summary>Gets only parent cards asynchronously.</summary>
    Task<List<StockCard>> GetParentCardsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets child cards. If parentId is provided, returns only child cards linked to that parent.</summary>
    List<StockCard> GetChildCards(int? parentId = null);

    /// <summary>Gets child cards asynchronously.</summary>
    Task<List<StockCard>> GetChildCardsAsync(int? parentId = null, CancellationToken cancellationToken = default);

    /// <summary>Gets low stock child cards directly from the database.</summary>
    Task<List<StockCard>> GetLowStockCardsAsync(int limit, int fallbackThreshold = 3, CancellationToken cancellationToken = default);

    /// <summary>Gets the stock card with the specified ID. Returns null if not found.</summary>
    StockCard? GetById(int id);

    /// <summary>Adds a new stock card. The generated ID is assigned to stockCard.Id.</summary>
    void Add(StockCard stockCard);

    /// <summary>Adds a new stock card asynchronously.</summary>
    Task AddAsync(StockCard stockCard, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing stock card.</summary>
    void Update(StockCard stockCard);

    /// <summary>Updates an existing stock card asynchronously.</summary>
    Task UpdateAsync(StockCard stockCard, CancellationToken cancellationToken = default);

    /// <summary>Deletes the stock card with the specified ID. Cascade deletes linked movements.</summary>
    void Delete(int id);

    /// <summary>Deletes the stock card with the specified ID asynchronously. Cascade deletes linked movements.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Generates the next sequential stock code (e.g. "042").</summary>
    string GetNextCode();

    /// <summary>Adds multiple stock cards in a single transaction.</summary>
    void AddBulk(IEnumerable<StockCard> stockCards);

    /// <summary>Adds multiple stock cards asynchronously in a single transaction.</summary>
    Task AddBulkAsync(IEnumerable<StockCard> stockCards, CancellationToken cancellationToken = default);

    /// <summary>Gets a paged list of stock cards with search and type filters directly from the database.</summary>
    Task<List<StockCard>> GetPagedAsync(int page, int pageSize, string? searchTerm, string? cardType = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the total count of stock cards matching search and type filters.</summary>
    Task<int> GetCountAsync(string? searchTerm, string? cardType = null, CancellationToken cancellationToken = default);
}
