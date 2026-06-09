using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.Services;

public class StockCardService : IStockCardService
{
    private readonly IStockCardRepository _stockCardRepository;

    public StockCardService(IStockCardRepository stockCardRepository)
    {
        _stockCardRepository = stockCardRepository;
    }

    public PagedResult<StockCard> GetPagedStocks(string? searchTerm, int page, int pageSize)
    {
        var allStocks = _stockCardRepository.GetChildCards();
        var filtered = allStocks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLowerInvariant();
            filtered = filtered.Where(k => 
                k.Name.ToLowerInvariant().Contains(s) || 
                k.Code.ToLowerInvariant().Contains(s) || 
                (!string.IsNullOrEmpty(k.ParentName) && k.ParentName.ToLowerInvariant().Contains(s)));
        }

        var totalCount = filtered.Count();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page > totalPages) page = totalPages;
        if (page < 1) page = 1;

        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<StockCard>(items, totalCount, totalPages, page);
    }

    public async Task<List<StockCard>> GetAllAsync()
    {
        return await _stockCardRepository.GetAllAsync();
    }

    public int GetLowStockCount()
    {
        return _stockCardRepository.GetChildCards().Count(k => k.CurrentStock <= (k.MinStock > 0 ? k.MinStock : 3));
    }
}
