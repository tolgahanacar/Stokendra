using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

public partial class AddStockCardViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly StokKarti? _editingCard;

    [ObservableProperty] private string _title = "Yeni Stok Kartı";
    [ObservableProperty] private string _ad = "";
    [ObservableProperty] private string _kodNo = "";
    [ObservableProperty] private string _aciklama = "";
    [ObservableProperty] private int _minStok = 0;
    [ObservableProperty] private string _kategori = "";
    [ObservableProperty] private string _birim = "Adet";
    [ObservableProperty] private string _konum = "";
    [ObservableProperty] private string _tedarikci = "";
    [ObservableProperty] private string _barkod = "";
    [ObservableProperty] private double _birimFiyat = 0;
    [ObservableProperty] private int _selectedTypeIndex = 0; // 0: Alt, 1: Ust
    [ObservableProperty] private StokKarti? _selectedParent;

    public bool IsAltCard => SelectedTypeIndex == 0;
    partial void OnSelectedTypeIndexChanged(int value) => OnPropertyChanged(nameof(IsAltCard));

    public ObservableCollection<StokKarti> ParentCards { get; } = new();

    public AddStockCardViewModel(IStockCardRepository stockCards, StokKarti? card = null)
    {
        _stockCards = stockCards;
        _editingCard = card;

        if (card != null)
        {
            Title = "Stok Kartını Düzenle";
            Ad = card.Ad;
            KodNo = card.KodNo;
            Aciklama = card.Aciklama;
            MinStok = card.MinStok;
            Kategori = card.Kategori;
            Birim = card.Birim;
            Konum = card.Konum;
            Tedarikci = card.Tedarikci;
            Barkod = card.Barkod;
            BirimFiyat = card.BirimFiyat;
            SelectedTypeIndex = card.KartTipi == "Ust" ? 1 : 0;
        }

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var all = await _stockCards.GetAllAsync();
        var parents = all.Where(k => k.KartTipi == "Ust" && k.Id != _editingCard?.Id).ToList();
        
        ParentCards.Clear();
        foreach (var p in parents) ParentCards.Add(p);

        if (_editingCard?.UstKartId != null)
        {
            SelectedParent = ParentCards.FirstOrDefault(p => p.Id == _editingCard.UstKartId);
        }

        if (string.IsNullOrEmpty(KodNo) && _editingCard == null)
        {
            // Auto generate code if possible
            // In a real app we might call a service for this
        }
    }

    public StokKarti? Result { get; private set; }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Ad)) return;

        Result = new StokKarti
        {
            Id = _editingCard?.Id ?? 0,
            Ad = Ad,
            KodNo = KodNo,
            Aciklama = Aciklama,
            MinStok = MinStok,
            Kategori = Kategori,
            Birim = Birim,
            Konum = Konum,
            Tedarikci = Tedarikci,
            Barkod = Barkod,
            BirimFiyat = BirimFiyat,
            KartTipi = SelectedTypeIndex == 1 ? "Ust" : "Alt",
            UstKartId = SelectedTypeIndex == 0 ? SelectedParent?.Id : null
        };

        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
