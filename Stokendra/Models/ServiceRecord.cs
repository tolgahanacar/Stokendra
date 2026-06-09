using System;

namespace Stokendra.Models;

/// <summary>
/// Device maintenance/service record domain model.
/// </summary>
public class ServiceRecord
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Device name (required).</summary>
    public string DeviceName { get; set; } = "";

    /// <summary>Device serial number.</summary>
    public string SerialNumber { get; set; } = "";

    /// <summary>Maintenance company name.</summary>
    public string Company { get; set; } = "";

    /// <summary>Maintenance/service date.</summary>
    public DateTime ServiceDate { get; set; }

    /// <summary>Identified problem/issue description.</summary>
    public string Issue { get; set; } = "";

    /// <summary>Action taken and service result.</summary>
    public string Result { get; set; } = "";

    /// <inheritdoc/>
    public override string ToString() => $"{DeviceName} ({SerialNumber}) - {ServiceDate:dd.MM.yyyy}";
}
