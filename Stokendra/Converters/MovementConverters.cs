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
            "Entry" or "Giris" or "Giriş" => "[G]",
            "Exit" or "Cikis" or "Çıkış"   => "[Ç]",
            _ => "[B]"
        });

    public static readonly IValueConverter TypeToColor =
        new FuncValueConverter<string, IBrush>(t => t switch
        {
            "Entry" or "Giris" or "Giriş" => Brush.Parse("#10B981"), // Aesthetic Green
            "Exit" or "Cikis" or "Çıkış"   => Brush.Parse("#EF4444"), // Aesthetic Red
            _ => Brushes.Gray
        });
}
