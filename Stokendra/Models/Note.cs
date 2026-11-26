using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokendra.Models;

/// <summary>
/// User note domain model.
/// </summary>
public partial class Note : ObservableObject
{
    /// <summary>Primary key.</summary>
    [ObservableProperty]
    private int _id;

    /// <summary>Note date and time (legacy).</summary>
    [ObservableProperty]
    private DateTime _date = DateTime.Now;

    /// <summary>Creation date.</summary>
    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    /// <summary>Last update date.</summary>
    [ObservableProperty]
    private DateTime _updatedAt = DateTime.Now;

    /// <summary>Note title (required).</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>Note content.</summary>
    [ObservableProperty]
    private string _content = string.Empty;

    /// <inheritdoc/>
    public override string ToString() => $"[{Date:dd.MM.yyyy}] {Title}";
}
