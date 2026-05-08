using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

public partial class AddMovementViewModel : ViewModelBase
{
    private readonly IStockCardRepository _stockCards;
    private readonly IDepartmentRepository _departments;
    private readonly StokHareketi? _editingMovement;

    [ObservableProperty] private string _title = "Yeni Hareket";
    [ObservableProperty] private StokKarti? _selectedCard;
    [ObservableProperty] private int _selectedTypeIndex = 0; // 0: Giriş, 1: Çıkış, 2: Boş
    [ObservableProperty] private double _quantity = 1;
    [ObservableProperty] private string _deliveredTo = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private DateTimeOffset _date = DateTimeOffset.Now;
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _errorMessage = "";

    public ObservableCollection<StokKarti> AllCards { get; } = new();
    public ObservableCollection<string> AllDepartments { get; } = new();

    public AddMovementViewModel(
        IStockCardRepository stockCards,
        IDepartmentRepository departments,
        StokHareketi? movement = null)
    {
        _stockCards = stockCards;
        _departments = departments;
        _editingMovement = movement;

        if (movement != null)
        {
            Title = "Hareketi Düzenle";
            SelectedTypeIndex = movement.Tur switch { "Giris" => 0, "Cikis" => 1, "Bos" => 2, _ => 0 };
            Quantity = movement.Miktar;
            DeliveredTo = movement.TeslimEdilen;
            Department = movement.Departman;
            Date = new DateTimeOffset(movement.Tarih);
            Description = movement.Aciklama;
        }

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var cards = await _stockCards.GetChildCardsAsync();
        foreach (var c in cards) AllCards.Add(c);

        if (_editingMovement != null)
        {
            SelectedCard = AllCards.FirstOrDefault(c => c.Id == _editingMovement.StokKartId);
        }

        var depts = await _departments.GetAllAsync();
        foreach (var d in depts) AllDepartments.Add(d);
        
        if (_editingMovement == null)
        {
            // Default Values
            SelectedTypeIndex = 1; // Çıkış
            
            // Prioritize cards starting with 015- as requested
            SelectedCard = AllCards.FirstOrDefault(c => c.KodNo.StartsWith("015-")) 
                        ?? AllCards.FirstOrDefault(c => c.KodNo.Contains("725") || c.KodNo.Contains("285"));
            
            if (string.IsNullOrEmpty(Department))
            {
                // Prioritize "Sicil Müdürlüğü" exactly, then fall back to others containing "Sicil"
                var exactSicil = AllDepartments.FirstOrDefault(d => d.Equals("Sicil Müdürlüğü", StringComparison.OrdinalIgnoreCase));
                var partialSicil = AllDepartments.FirstOrDefault(d => d.Contains("Sicil", StringComparison.OrdinalIgnoreCase));
                
                Department = exactSicil ?? partialSicil ?? (AllDepartments.Count > 0 ? AllDepartments[0] : "");
            }
        }
    }

    public StokHareketi? Result { get; private set; }

    [RelayCommand]
    private void Save()
    {
        if (SelectedCard == null) return;
        
        // Negative Stock Validation
        string tur = SelectedTypeIndex switch { 0 => "Giris", 1 => "Cikis", 2 => "Bos", _ => "Giris" };
        if (tur == "Cikis" && Quantity > SelectedCard.MevcutStok)
        {
            ErrorMessage = $"⚠️ Yetersiz Stok! Mevcut: {SelectedCard.MevcutStok}";
            return;
        }

        Result = new StokHareketi
        {
            Id = _editingMovement?.Id ?? 0,
            StokKartId = SelectedCard.Id,
            Tur = tur,
            Miktar = Quantity,
            TeslimEdilen = DeliveredTo,
            Departman = Department,
            Tarih = Date.Date.Add(DateTime.Now.TimeOfDay),
            Aciklama = Description,
            StokKartAd = SelectedCard.Ad,
            StokKartKodNo = SelectedCard.KodNo
        };
        
        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
