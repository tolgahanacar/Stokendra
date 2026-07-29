using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class BulkEditServicesViewModel : ViewModelBase
{
    private readonly IServiceRecordRepository _services;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _errorMessage = "";

    // Bulk Apply Fields
    [ObservableProperty] private bool _isApplyDate;
    [ObservableProperty] private DateTime? _serviceDate = DateTime.Today;

    partial void OnIsApplyDateChanged(bool value)
    {
        if (value && !ServiceDate.HasValue)
        {
            ServiceDate = EditingRecords.FirstOrDefault()?.ServiceDate.Date ?? DateTime.Today;
        }
    }

    [ObservableProperty] private bool _isApplyCompany;
    [ObservableProperty] private string _company = "";

    [ObservableProperty] private bool _isApplyDeviceName;
    [ObservableProperty] private string _deviceName = "";

    [ObservableProperty] private bool _isApplyIssue;
    [ObservableProperty] private string _issue = "";

    [ObservableProperty] private bool _isApplyResult;
    [ObservableProperty] private string _resultText = "";

    public ObservableCollection<ServiceRecord> EditingRecords { get; } = new();

    public Action<bool>? CloseAction { get; set; }

    public BulkEditServicesViewModel(
        List<ServiceRecord> selectedRecords,
        IServiceRecordRepository services,
        IDialogService dialogService,
        ILogger logger)
    {
        _services = services;
        _dialogService = dialogService;
        _logger = logger;

        Title = LocalizationManager.L("bulk_edit_title", selectedRecords.Count);

        foreach (var r in selectedRecords)
        {
            EditingRecords.Add(new ServiceRecord
            {
                Id = r.Id,
                DeviceName = r.DeviceName,
                SerialNumber = r.SerialNumber,
                Company = r.Company,
                Issue = r.Issue,
                Result = r.Result,
                ServiceDate = r.ServiceDate
            });
        }

        ServiceDate = selectedRecords.FirstOrDefault()?.ServiceDate.Date ?? DateTime.Today;
    }

    [RelayCommand]
    private void ApplyBulk()
    {
        ErrorMessage = "";
        int updatedCount = 0;

        foreach (var r in EditingRecords)
        {
            if (IsApplyDate && ServiceDate.HasValue)
            {
                r.ServiceDate = ServiceDate.Value.Date.Add(r.ServiceDate.TimeOfDay);
                updatedCount++;
            }
            if (IsApplyCompany)
            {
                r.Company = Company ?? "";
                updatedCount++;
            }
            if (IsApplyDeviceName && !string.IsNullOrWhiteSpace(DeviceName))
            {
                r.DeviceName = DeviceName;
                updatedCount++;
            }
            if (IsApplyIssue)
            {
                r.Issue = Issue ?? "";
                updatedCount++;
            }
            if (IsApplyResult)
            {
                r.Result = ResultText ?? "";
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            var temp = EditingRecords.ToList();
            EditingRecords.Clear();
            foreach (var item in temp) EditingRecords.Add(item);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = "";
        try
        {
            foreach (var r in EditingRecords)
            {
                if (string.IsNullOrWhiteSpace(r.DeviceName))
                {
                    ErrorMessage = $"⚠️ {LocalizationManager.L("device_name_required")}";
                    return;
                }
            }

            foreach (var r in EditingRecords)
            {
                await _services.UpdateAsync(r);
            }

            CloseAction?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk services edit save error", ex);
            ErrorMessage = $"⚠️ {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);
}
