using StokTakip.Data.Interfaces;
using StokTakip.Models;

namespace StokTakip.Services;

public class StockCardService : IStockCardService
{
    private readonly IStockCardRepository _stockCardRepository;

    public StockCardService(IStockCardRepository stockCardRepository)
    {
        _stockCardRepository = stockCardRepository;
    }

    public PagedResult<StokKarti> GetPagedStocks(string? searchTerm, int page, int pageSize)
    {
        var allStocks = _stockCardRepository.GetChildCards();
        var filtered = allStocks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLowerInvariant();
            filtered = filtered.Where(k => 
                k.Ad.ToLowerInvariant().Contains(s) || 
                k.KodNo.ToLowerInvariant().Contains(s) || 
                (k.UstKartAd != null && k.UstKartAd.ToLowerInvariant().Contains(s)));
        }

        var totalCount = filtered.Count();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page > totalPages) page = totalPages;
        if (page < 1) page = 1;

        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<StokKarti>(items, totalCount, totalPages, page);
    }

    public int GetLowStockCount()
    {
        // Business rule: Low stock is defined as <= 3 (or k.MinStok if defined)
        // Note: The UI currently has k.MevcutStok <= 3 hardcoded in FilterGrid.
        // Let's use the model's property if available or keep it consistent.
        return _stockCardRepository.GetChildCards().Count(k => k.IsLowStock || k.MevcutStok <= 3);
    }
}
