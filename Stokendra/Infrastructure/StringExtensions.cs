using System.Globalization;

namespace Stokendra.Infrastructure;

public static class StringExtensions
{
    private static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");

    public static string ToTurkishLower(this string? text)
    {
        if (text == null) return string.Empty;
        return text.ToLower(TurkishCulture);
    }
}
