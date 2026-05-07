using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Models;
using System;

namespace StokTakip.ViewModels;

public partial class AddServiceViewModel : ViewModelBase
{
    private readonly ServisKaydi? _editingRecord;

    [ObservableProperty] private string _title = "Yeni Servis Kaydı";
    [ObservableProperty] private string _deviceName = "";
    [ObservableProperty] private string _serialNumber = "";
    [ObservableProperty] private string _company = "";
    [ObservableProperty] private DateTimeOffset _date = DateTimeOffset.Now;
    [ObservableProperty] private string _issue = "";
    [ObservableProperty] private string _resultText = "";

    public AddServiceViewModel(ServisKaydi? record = null)
    {
        _editingRecord = record;
        if (record != null)
        {
            Title = "Kaydı Düzenle";
            DeviceName = record.CihazAdi;
            SerialNumber = record.SeriNumarasi;
            Company = record.Firma;
            Date = new DateTimeOffset(record.BakimTarihi);
            Issue = record.Sorun;
            ResultText = record.Sonuc;
        }
    }

    public ServisKaydi? Result { get; private set; }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(DeviceName)) return;

        Result = new ServisKaydi
        {
            Id = _editingRecord?.Id ?? 0,
            CihazAdi = DeviceName,
            SeriNumarasi = SerialNumber,
            Firma = Company,
            BakimTarihi = Date.DateTime,
            Sorun = Issue,
            Sonuc = ResultText
        };
        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
