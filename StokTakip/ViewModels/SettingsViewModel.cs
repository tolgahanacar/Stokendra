using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Infrastructure;

namespace StokTakip.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserRepository _users;
    private readonly IStockCardRepository _stockCards;
    private readonly IMovementRepository _movements;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly INoteRepository _notes;

    [ObservableProperty] private string _companyName   = "";
    [ObservableProperty] private string _dbPath        = "";
    [ObservableProperty] private string _oldPassword   = "";
    [ObservableProperty] private string _newPassword   = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool   _isSuccess;
    [ObservableProperty] private string _appVersion    = "";

    public SettingsViewModel(
        IUserRepository users,
        IStockCardRepository stockCards,
        IMovementRepository movements,
        IServiceRecordRepository serviceRecords,
        INoteRepository notes)
    {
        _users = users;
        _stockCards = stockCards;
        _movements = movements;
        _serviceRecords = serviceRecords;
        _notes = notes;
        Load();
    }

    public Func<string, string, Task<string?>>? SaveFileAction { get; set; }

    private void Load()
    {
        var settings = AppServices.Current.Settings;
        CompanyName  = settings.CompanyName;
        DbPath       = settings.DbPath;
        AppVersion   = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "4.0.0"}";
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var settings = AppServices.Current.Settings;
        settings.CompanyName = CompanyName.Trim();
        settings.Kaydet();
        StatusMessage = "Ayarlar kaydedildi.";
        IsSuccess     = true;
    }

    [RelayCommand]
    public void ChangePassword()
    {
        StatusMessage = "";
        IsSuccess     = false;

        if (string.IsNullOrWhiteSpace(OldPassword) ||
            string.IsNullOrWhiteSpace(NewPassword) ||
            string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            StatusMessage = "Tüm şifre alanlarını doldurun.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "Yeni şifreler eşleşmiyor.";
            return;
        }

        var policyError = _users.ValidatePasswordPolicy(NewPassword,
            AppServices.Current.Session?.Username);
        if (policyError != null)
        {
            StatusMessage = policyError;
            return;
        }

        var username = AppServices.Current.Session?.Username ?? "admin";
        bool ok = _users.ChangePassword(username, OldPassword, NewPassword);
        if (ok)
        {
            StatusMessage   = "Şifre başarıyla değiştirildi.";
            IsSuccess       = true;
            OldPassword     = "";
            NewPassword     = "";
            ConfirmPassword = "";
        }
        else
        {
            StatusMessage = "Mevcut şifre hatalı.";
        }
    }

    [RelayCommand]
    public async Task FullBackupAsync()
    {
        if (SaveFileAction == null) return;
        string fileName = $"Stokendra_Yedek_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await SaveFileAction(fileName, "Excel Dosyası (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusMessage = "Yedek oluşturuluyor...";
            await Task.Run(async () => {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                
                // Stok Kartları
                var cards = await _stockCards.GetAllAsync();
                var wsCards = workbook.Worksheets.Add("Stok Kartları");
                wsCards.Cell(1,1).Value = "Kod"; wsCards.Cell(1,2).Value = "Ad"; wsCards.Cell(1,3).Value = "Stok";
                for(int i=0; i<cards.Count; i++) {
                    wsCards.Cell(i+2,1).Value = cards[i].KodNo;
                    wsCards.Cell(i+2,2).Value = cards[i].Ad;
                    wsCards.Cell(i+2,3).Value = cards[i].MevcutStok;
                }

                // Hareketler
                var movements = _movements.GetAll(null, new DateTime(2000,1,1), DateTime.Now.AddDays(1), null, null, null);
                var wsMov = workbook.Worksheets.Add("Stok Hareketleri");
                wsMov.Cell(1,1).Value = "Tarih"; wsMov.Cell(1,2).Value = "Kart"; wsMov.Cell(1,3).Value = "Tür"; wsMov.Cell(1,4).Value = "Miktar";
                for(int i=0; i<movements.Count; i++) {
                    wsMov.Cell(i+2,1).Value = movements[i].Tarih;
                    wsMov.Cell(i+2,2).Value = movements[i].StokKartAd;
                    wsMov.Cell(i+2,3).Value = movements[i].Tur;
                    wsMov.Cell(i+2,4).Value = movements[i].Miktar;
                }

                // Servis Kayıtları
                var services = _serviceRecords.GetAll(new DateTime(2000,1,1), DateTime.Now.AddDays(1), null);
                var wsSrv = workbook.Worksheets.Add("Servis Kayıtları");
                wsSrv.Cell(1,1).Value = "Tarih"; wsSrv.Cell(1,2).Value = "Cihaz"; wsSrv.Cell(1,3).Value = "Sorun";
                for(int i=0; i<services.Count; i++) {
                    wsSrv.Cell(i+2,1).Value = services[i].BakimTarihi;
                    wsSrv.Cell(i+2,2).Value = services[i].CihazAdi;
                    wsSrv.Cell(i+2,3).Value = services[i].Sorun;
                }

                // Notlar
                var notes = _notes.GetAll();
                var wsNotes = workbook.Worksheets.Add("Notlar");
                wsNotes.Cell(1,1).Value = "Tarih"; wsNotes.Cell(1,2).Value = "Başlık"; wsNotes.Cell(1,3).Value = "İçerik";
                for(int i=0; i<notes.Count; i++) {
                    wsNotes.Cell(i+2,1).Value = notes[i].Tarih;
                    wsNotes.Cell(i+2,2).Value = notes[i].Baslik;
                    wsNotes.Cell(i+2,3).Value = notes[i].Icerik;
                }

                workbook.SaveAs(path);
            });
            StatusMessage = "Tam yedek başarıyla alındı.";
            IsSuccess = true;
        }
        catch (Exception ex) { StatusMessage = $"Hata: {ex.Message}"; IsSuccess = false; }
    }
}
