using System.Globalization;
using StokTakip.Data.Interfaces;
using StokTakip.Infrastructure;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Services;

public class MovementService : IMovementService
{
    private readonly IMovementRepository _movementRepository;
    private readonly IStockCardRepository _stockCardRepository;

    public MovementService(IMovementRepository movementRepository, IStockCardRepository stockCardRepository)
    {
        _movementRepository = movementRepository;
        _stockCardRepository = stockCardRepository;
    }

    public PagedResult<StokHareketi> GetPagedMovements(
        DateTime? startDate,
        DateTime? endDate,
        int? stockCardId,
        string? department,
        string? movementType,
        string? searchTerm,
        int page,
        int pageSize)
    {
        // 1. Get initial data from DB (filtered by date, stock, dept, type)
        var data = _movementRepository.GetAll(stockCardId, startDate, endDate, department, movementType);

        // 2. Apply live search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            data = data.Where(h =>
                (h.StokKartKodNo != null && h.StokKartKodNo.ToLowerInvariant().Contains(term)) ||
                (h.StokKartAd != null && h.StokKartAd.ToLowerInvariant().Contains(term)) ||
                (h.TeslimEdilen != null && h.TeslimEdilen.ToLowerInvariant().Contains(term)) ||
                (h.Departman != null && h.Departman.ToLowerInvariant().Contains(term)) ||
                (h.Aciklama != null && h.Aciklama.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        // 3. Paginate
        var totalCount = data.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page > totalPages) page = totalPages;
        if (page < 1) page = 1;

        var items = data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<StokHareketi>(items, totalCount, totalPages, page);
    }

    public async Task<(int Imported, int Skipped, List<string> Warnings)> ImportMovementsFromXlsxAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            using var wb = new ClosedXML.Excel.XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 2) return (0, 0, new List<string>());

            // Header validation
            string[] expected = { L("code_no"), L("stock_name"), L("delivered_to"), L("operation_type"), L("department"), L("date"), L("description") };
            var fileHeaders = new List<string>();
            for (int c = 1; c <= Math.Min(7, ws.LastColumnUsed()?.ColumnNumber() ?? 0); c++)
                fileHeaders.Add(ws.Cell(1, c).GetString().Trim());

            bool match = fileHeaders.Count >= 6;
            for (int i = 0; i < Math.Min(expected.Length, fileHeaders.Count) && match; i++)
                if (!expected[i].Equals(fileHeaders[i], StringComparison.OrdinalIgnoreCase)) match = false;

            if (!match)
                throw new InvalidOperationException(L("import_header_mismatch", string.Join(" | ", expected), string.Join(" | ", fileHeaders)));

            var altKartlar = _stockCardRepository.GetChildCards();
            int imported = 0, skipped = 0;
            var warnings = new List<string>();
            var hareketler = new List<StokHareketi>();

            for (int r = 2; r <= lastRow; r++)
            {
                string kodNo = ws.Cell(r, 1).GetString().Trim();
                if (string.IsNullOrEmpty(kodNo)) continue;

                var kart = altKartlar.Find(k => k.KodNo.Equals(kodNo, StringComparison.OrdinalIgnoreCase));
                if (kart == null) { warnings.Add(L("import_stock_not_found", r, kodNo)); skipped++; continue; }

                string gcStr = ws.Cell(r, 4).GetString().Trim();
                var parsed = MovementFormatter.ParseCell(gcStr);
                if (!parsed.IsSuccess) { skipped++; continue; }

                string teslim = ws.Cell(r, 3).GetString().Trim();
                string dept = ws.Cell(r, 5).GetString().Trim();
                string aciklama = ws.Cell(r, 7).GetString().Trim();

                DateTime tarih = DateTime.Now;
                string tarihStr = ws.Cell(r, 6).GetString().Trim();
                string[] fmt = { "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" };
                if (!DateTime.TryParseExact(tarihStr, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out tarih))
                    if (ws.Cell(r, 6).TryGetValue(out DateTime dtVal)) tarih = dtVal;

                hareketler.Add(new StokHareketi
                {
                    StokKartId = kart.Id,
                    Tur = parsed.Tur.ToDbString(),
                    Miktar = parsed.Miktar,
                    TeslimEdilen = teslim,
                    Departman = dept,
                    Tarih = tarih,
                    Aciklama = aciklama
                });
            }

            if (hareketler.Count > 0)
            {
                _movementRepository.AddBulk(hareketler);
                imported = hareketler.Count;
            }

            return (imported, skipped, warnings);
        });
    }
}
