using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Models;
using System;

namespace Stokendra.ViewModels;

public partial class AddServiceViewModel : ViewModelBase
{
    private readonly ServiceRecord? _editingRecord;

    [ObservableProperty] private string _title = LocalizationManager.L("new_service_record");
    [ObservableProperty] private string _deviceName = "";
    [ObservableProperty] private string _serialNumber = "";
    [ObservableProperty] private string _company = "";
    [ObservableProperty] private DateTimeOffset _date = DateTimeOffset.Now;
    [ObservableProperty] private string _issue = "";
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private string _errorMessage = "";

    public AddServiceViewModel(ServiceRecord? record = null)
    {
        _editingRecord = record;
        if (record != null)
        {
            Title = LocalizationManager.L("edit_service_record");
            DeviceName = record.DeviceName;
            SerialNumber = record.SerialNumber;
            Company = record.Company;
            Date = (record.ServiceDate > DateTime.MinValue.AddDays(2) && record.ServiceDate < DateTime.MaxValue.AddDays(-2))
                ? new DateTimeOffset(record.ServiceDate)
                : DateTimeOffset.Now;
            Issue = record.Issue;
            ResultText = record.Result;
        }
    }

    public ServiceRecord? Result { get; private set; }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(DeviceName))
        {
            ErrorMessage = $"⚠️ {LocalizationManager.L("device_name_required")}";
            return;
        }

        Result = new ServiceRecord
        {
            Id = _editingRecord?.Id ?? 0,
            DeviceName = DeviceName.Trim(),
            SerialNumber = (SerialNumber ?? "").Trim(),
            Company = (Company ?? "").Trim(),
            ServiceDate = Date.DateTime,
            Issue = (Issue ?? "").Trim(),
            Result = (ResultText ?? "").Trim()
        };
        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
