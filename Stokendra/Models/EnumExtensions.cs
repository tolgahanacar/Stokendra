using System.Globalization;

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
    public static MovementType ToMovementType(this string? value) => value?.Trim() switch
    {
        "Entry" or "Giris" or "Giriş" => MovementType.Entry,
        "Exit" or "Cikis" or "Çıkış" => MovementType.Exit,
        "Blank" or "Bos" => MovementType.Blank,
        _ => MovementType.Blank
    };

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
    public static CardType ToCardType(this string? value) => value?.Trim() switch
    {
        "Parent" or "Ust" or "Üst" => CardType.Parent,
        "Child" or "Alt" => CardType.Child,
        _ => CardType.Child
    };

    /// <summary>
    /// Converts string to upper case using Turkish culture rules.
    /// </summary>
    public static string ToUpperTr(this string text)
    {
        return text.ToUpper(new CultureInfo("tr-TR"));
    }

    /// <summary>
    /// Converts string to lower case using Turkish culture rules.
    /// </summary>
    public static string ToLowerTr(this string text)
    {
        return text.ToLower(new CultureInfo("tr-TR"));
    }
}
