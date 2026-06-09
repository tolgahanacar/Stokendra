using System;

namespace Stokendra.Models;

/// <summary>
/// User note domain model.
/// </summary>
public class Note
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Note date and time (legacy).</summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>Creation date.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last update date.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>Note title (required).</summary>
    public string Title { get; set; } = "";

    /// <summary>Note content.</summary>
    public string Content { get; set; } = "";

    /// <inheritdoc/>
    public override string ToString() => $"[{Date:dd.MM.yyyy}] {Title}";
}
