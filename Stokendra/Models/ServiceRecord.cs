using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokendra.Models;

/// <summary>
/// Device maintenance/service record domain model.
/// </summary>
public partial class ServiceRecord : ObservableObject
{
    /// <summary>Primary key.</summary>
    [ObservableProperty]
    private int _id;

    /// <summary>Device name (required).</summary>
    [ObservableProperty]
    private string _deviceName = string.Empty;

    /// <summary>Device serial number.</summary>
    [ObservableProperty]
    private string _serialNumber = string.Empty;

    /// <summary>Maintenance company name.</summary>
    [ObservableProperty]
    private string _company = string.Empty;

    /// <summary>Maintenance/service date.</summary>
    [ObservableProperty]
    private DateTime _serviceDate;

    /// <summary>Identified problem/issue description.</summary>
    [ObservableProperty]
    private string _issue = string.Empty;

    /// <summary>Action taken and service result.</summary>
    [ObservableProperty]
    private string _result = string.Empty;

    /// <inheritdoc/>
    public override string ToString() => $"{DeviceName} ({SerialNumber}) - {ServiceDate:dd.MM.yyyy}";
}
