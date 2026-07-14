using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokendra.Models;

/// <summary>
/// Device maintenance/service record domain model.
/// </summary>
public class ServiceRecord : ObservableObject
{
    private int _id;
    private string _deviceName = "";
    private string _serialNumber = "";
    private string _company = "";
    private DateTime _serviceDate;
    private string _issue = "";
    private string _result = "";

    /// <summary>Primary key.</summary>
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>Device name (required).</summary>
    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    /// <summary>Device serial number.</summary>
    public string SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value);
    }

    /// <summary>Maintenance company name.</summary>
    public string Company
    {
        get => _company;
        set => SetProperty(ref _company, value);
    }

    /// <summary>Maintenance/service date.</summary>
    public DateTime ServiceDate
    {
        get => _serviceDate;
        set => SetProperty(ref _serviceDate, value);
    }

    /// <summary>Identified problem/issue description.</summary>
    public string Issue
    {
        get => _issue;
        set => SetProperty(ref _issue, value);
    }

    /// <summary>Action taken and service result.</summary>
    public string Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{DeviceName} ({SerialNumber}) - {ServiceDate:dd.MM.yyyy}";
}
