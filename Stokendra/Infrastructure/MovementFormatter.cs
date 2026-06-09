using Stokendra.Models;
using System;
using System.Globalization;

namespace Stokendra.Infrastructure;

/// <summary>
/// Central helper for formatting and parsing stock movements.
/// Handles localized symbols such as "[G]", "[Ç]", "[E]", "[X]".
/// </summary>
public static class MovementFormatter
{
    /// <summary>Entry movement symbol/tag.</summary>
    public static string EntryTag => LocalizationManager.L("entry_symbol");

    /// <summary>Exit movement symbol/tag.</summary>
    public static string ExitTag => LocalizationManager.L("exit_symbol");

    /// <summary>Empty/blank movement symbol/tag.</summary>
    public static string EmptyTag => "—";

    /// <summary>
    /// Formats the movement into a display string: e.g., "5[G]" / "3[Ç]" / "5[E]"
    /// </summary>
    public static string Format(StockMovement movement)
        => Format(movement.Type, movement.Quantity);

    /// <summary>
    /// Formats type and quantity into a display string.
    /// </summary>
    public static string Format(string type, double quantity)
    {
        string tag = type switch
        {
            "Entry" or "Giris" or "Giriş" => EntryTag,
            "Exit" or "Cikis" or "Çıkış" => ExitTag,
            _ => EmptyTag
        };
        return $"{(quantity == Math.Floor(quantity) ? ((int)quantity).ToString() : quantity.ToString("N2"))}{tag}";
    }

    /// <summary>
    /// Parses the symbol tag to determine movement type.
    /// </summary>
    public static MovementType ParseTur(string formatted)
    {
        if (formatted.Contains("[Ç]") || formatted.Contains("[X]") || formatted.Contains(ExitTag)) return MovementType.Exit;
        if (formatted.Contains("[B]") || formatted.Contains("—") || formatted.Contains(EmptyTag)) return MovementType.Blank;
        return MovementType.Entry;
    }

    /// <summary>
    /// Parses a cell string value (quantity + symbol) into a parse result.
    /// </summary>
    public static ParseResult ParseCell(string cellValue)
    {
        if (string.IsNullOrWhiteSpace(cellValue))
            return ParseResult.Failure("Empty cell");

        MovementType type = ParseTur(cellValue);
        if (type == MovementType.Blank)
            return ParseResult.Success(MovementType.Blank, 0);

        string raw = cellValue
            .Replace("[G]", "")
            .Replace("[Ç]", "")
            .Replace("[E]", "")
            .Replace("[X]", "")
            .Replace("[B]", "")
            .Replace("—", "")
            .Replace(EntryTag, "")
            .Replace(ExitTag, "")
            .Replace(EmptyTag, "")
            .Replace(',', '.')
            .Trim();

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double quantity) && quantity > 0)
            return ParseResult.Success(type, quantity);

        return ParseResult.Failure($"Invalid quantity: '{cellValue}'");
    }

    /// <summary>Result of a parse operation.</summary>
    public sealed class ParseResult
    {
        public bool IsSuccess { get; private init; }
        public MovementType Type { get; private init; }
        public double Quantity { get; private init; }
        public string? Error { get; private init; }

        private ParseResult() { }

        internal static ParseResult Success(MovementType type, double quantity)
            => new() { IsSuccess = true, Type = type, Quantity = quantity };

        internal static ParseResult Failure(string error)
            => new() { IsSuccess = false, Error = error };
    }
}
