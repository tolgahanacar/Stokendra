using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using StokTakip.Infrastructure;

namespace StokTakip.ViewModels;

public partial class StocksViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<StokKarti> Stocks { get; } = new();
    private List<StokKarti> _allStocks = new();

    public StocksViewModel(IStockCardRepository stockCards, IDialogService dialogService, ILogger logger)
    {
        _stockCards = stockCards;
        _dialogService = dialogService;
        _logger = logger;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _stockCards.GetAllAsync();
            // Sadece Alt kartları (hareket görebilen) ve stok takibi yapılanları göster
            _allStocks = all.Where(k => k.KartTipi == "Alt").ToList();
            ApplyFilter();
        }
        catch (Exception ex) { StatusText = $"Hata: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = SearchText.Trim().ToLowerInvariant();
        var data = string.IsNullOrEmpty(term)
            ? _allStocks
            : _allStocks.Where(k =>
                k.Ad.ToLowerInvariant().Contains(term) ||
                k.KodNo.ToLowerInvariant().Contains(term) ||
                k.Kategori.ToLowerInvariant().Contains(term)).ToList();

        Stocks.Clear();
        foreach (var k in data) Stocks.Add(k);
        
        int lowStockCount = data.Count(k => k.MevcutStok <= k.MinStok && k.MevcutStok > 0);
        int depletedCount = data.Count(k => k.MevcutStok <= 0);
        
        StatusText = $"{data.Count} Ürün | ⚠ {lowStockCount} Düşük Stok | ❌ {depletedCount} Tükenmiş";
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    public void ClearSearch() => SearchText = "";

    [RelayCommand]
    public async Task Export()
    {
        if (Stocks.Count == 0)
        {
            await _dialogService.ShowMessageAsync("Uyarı", "Dışa aktarılacak veri bulunamadı.");
            return;
        }

        var path = await _dialogService.SaveFileAsync("Stok Listesini Kaydet", "Stok_Durum_Raporu.xlsx", "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            string[] headers = { "Kod No", "Stok Adı", "Kategori", "Mevcut Stok", "Min Stok", "Birim", "Konum" };
            ExcelService.ExportToExcel(path, "StokDurumu", headers, Stocks, s => new object?[]
            {
                s.KodNo,
                s.Ad,
                s.Kategori,
                s.MevcutStok,
                s.MinStok,
                s.Birim,
                s.Konum
            });

            await _dialogService.ShowMessageAsync("Başarılı", "Stok listesi başarıyla kaydedildi.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Stocks export error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Dosya kaydedilemedi: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task Print()
    {
        if (Stocks.Count == 0)
        {
            await _dialogService.ShowMessageAsync("Uyarı", "Yazdırılacak veri bulunamadı.");
            return;
        }

        try
        {
            StatusText = "Yazdırma hazırlanıyor...";
            
            double grandTotalIn = Stocks.Sum(s => s.ToplamGiris);
            double grandTotalOut = Stocks.Sum(s => s.ToplamCikis);
            double grandTotalResult = grandTotalIn - grandTotalOut;

            var sb = new System.Text.StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'><title>Stok Durum Raporu</title>");
            sb.Append("<style>");
            sb.Append("@page { size: portrait; margin: 0.5cm; } ");
            sb.Append("body { font-family: 'Segoe UI', Arial, sans-serif; padding: 10px; color: #1a1a1a; line-height: 1.2; } ");
            sb.Append(".top-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #1e293b; padding-bottom: 8px; margin-bottom: 15px; } ");
            sb.Append(".top-header h1 { margin: 0; color: #1e293b; font-size: 20px; font-weight: 800; } ");
            sb.Append(".date-box { text-align: right; font-size: 11px; color: #4b5563; } ");
            sb.Append("table { width: 100%; border-collapse: collapse; font-size: 11px; table-layout: fixed; } ");
            sb.Append("th, td { border: 1px solid #666; padding: 6px 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; } ");
            sb.Append("th { background: #f1f5f9; font-weight: bold; text-align: center; font-size: 10px; text-transform: uppercase; } ");
            sb.Append(".num { text-align: center; } ");
            sb.Append(".bold { font-weight: bold; } ");
            sb.Append(".footer-row { background: #f8fafc; font-weight: 800; } ");
            sb.Append(".footer-row td { border-top: 2px solid #1e293b; font-size: 12px; } ");
            sb.Append("@media print { body { -webkit-print-color-adjust: exact; } table { page-break-inside: auto; } tr { page-break-inside: avoid; page-break-after: auto; } } ");
            sb.Append("</style>");
            sb.Append("<script>window.onload = function() { window.print(); }</script>");
            sb.Append("</head><body>");
            
            sb.Append("<div class='top-header'>");
            sb.Append("<h1>GÜNCEL STOK DURUM RAPORU</h1>");
            sb.Append($"<div class='date-box'>Rapor Tarihi:<br/><b>{DateTime.Now:dd.MM.yyyy HH:mm}</b></div>");
            sb.Append("</div>");

            sb.Append("<table><thead><tr>");
            sb.Append("<th style='width: 15%;'>Stok Kodu</th>");
            sb.Append("<th style='width: 45%; text-align: left; padding-left: 8px;'>Stok Adı</th>");
            sb.Append("<th style='width: 12%;'>Giriş (Y)</th>");
            sb.Append("<th style='width: 12%;'>Çıkış (G)</th>");
            sb.Append("<th style='width: 16%;'>Sonuç</th>");
            sb.Append("</tr></thead><tbody>");
            
            foreach (var s in Stocks)
            {
                sb.Append("<tr>");
                sb.Append($"<td class='num'>{s.KodNo}</td>");
                sb.Append($"<td style='text-align: left; padding-left: 8px;'>{s.Ad}</td>");
                sb.Append($"<td class='num'>{s.ToplamGiris}</td>");
                sb.Append($"<td class='num'>{s.ToplamCikis}</td>");
                sb.Append($"<td class='num bold'>{s.MevcutStok}</td>");
                sb.Append("</tr>");
            }
            
            // Alt Toplam Satırı
            sb.Append("<tr class='footer-row'>");
            sb.Append("<td colspan='2' style='text-align: right; padding-right: 15px;'>GENEL TOPLAM:</td>");
            sb.Append($"<td class='num'>{grandTotalIn}</td>");
            sb.Append($"<td class='num'>{grandTotalOut}</td>");
            sb.Append($"<td class='num'>{grandTotalResult}</td>");
            sb.Append("</tr>");

            sb.Append("</tbody></table>");
            sb.Append("</body></html>");
            
            var previewVm = new PrintPreviewViewModel("Stok Durum Raporu", sb.ToString());
            await _dialogService.ShowDialogAsync(previewVm);
            
            StatusText = "Yazdırma tamamlandı veya iptal edildi.";
        }
        catch (Exception ex)
        {
            _logger.LogError("Stocks print error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Yazdırma penceresi açılamadı: {ex.Message}");
        }
    }
}
