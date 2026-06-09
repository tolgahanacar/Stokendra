using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Stokendra.Converters;

public static class MovementConverters
{
    public static readonly IValueConverter TypeToText =
        new FuncValueConverter<string, string>(t => t switch
        {
            "Giris" => "[G]",
            "Cikis" => "[Ç]",
            _ => "[B]"
        });

    public static readonly IValueConverter TypeToColor =
        new FuncValueConverter<string, IBrush>(t => t switch
        {
            "Giris" => Brush.Parse("#10B981"), // Aesthetic Green
            "Cikis" => Brush.Parse("#EF4444"), // Aesthetic Red
            _ => Brushes.Gray
        });
}
