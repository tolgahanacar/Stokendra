using System;

namespace Stokendra.Models;

/// <summary>
/// Extension methods for conversions of domain enum types.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Converts <see cref="MovementType"/> to database string representation.
    /// </summary>
    public static string ToDbString(this MovementType type) => type switch
    {
        MovementType.Entry => "Entry",
        MovementType.Exit => "Exit",
        MovementType.Blank => "Blank",
        _ => "Blank"
    };

    /// <summary>
    /// Converts database string representation to <see cref="MovementType"/>.
    /// </summary>
    public static MovementType ToMovementType(this string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return MovementType.Blank;
        if (trimmed.Equals("Entry", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Giris", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Giriş", StringComparison.OrdinalIgnoreCase))
            return MovementType.Entry;
        if (trimmed.Equals("Exit", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Cikis", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Çıkış", StringComparison.OrdinalIgnoreCase))
            return MovementType.Exit;
        return MovementType.Blank;
    }

    /// <summary>
    /// Converts <see cref="CardType"/> to database string representation.
    /// </summary>
    public static string ToDbString(this CardType type) => type switch
    {
        CardType.Parent => "Parent",
        CardType.Child => "Child",
        _ => "Child"
    };

    /// <summary>
    /// Converts database string representation to <see cref="CardType"/>.
    /// </summary>
    public static CardType ToCardType(this string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return CardType.Child;
        if (trimmed.Equals("Parent", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Ust", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Üst", StringComparison.OrdinalIgnoreCase))
            return CardType.Parent;
        return CardType.Child;
    }
}
