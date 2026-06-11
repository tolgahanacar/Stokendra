using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
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

    public async Task<PagedResult<StockCard>> GetPagedStocksAsync(string? searchTerm, int page, int pageSize)
    {
        var search = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim().ToTurkishLower();

        int totalCount = await _stockCardRepository.GetCountAsync(search, "Child");
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page > totalPages) page = totalPages;
        if (page < 1) page = 1;

        var items = await _stockCardRepository.GetPagedAsync(page, pageSize, search, "Child");

        return new PagedResult<StockCard>(items, totalCount, totalPages, page);
    }

    public async Task<List<StockCard>> GetAllAsync()
    {
        return await _stockCardRepository.GetAllAsync();
    }

    public int GetLowStockCount()
    {
        // Keep this sync or use GetChildCards (which is now optimized)
        return _stockCardRepository.GetChildCards().Count(k => k.CurrentStock <= (k.MinStock > 0 ? k.MinStock : 3));
    }
}
