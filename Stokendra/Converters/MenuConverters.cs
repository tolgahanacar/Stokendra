using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Stokendra.Converters;

public class StringEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string valStr && parameter is string paramStr)
        {
            return valStr.Equals(paramStr, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class RoleColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string role)
        {
            if (role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                return Avalonia.Media.Brush.Parse("#EF4444"); // Red for admin
            return Avalonia.Media.Brush.Parse("#3B82F6"); // Blue for user
        }
        return Avalonia.Media.Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class RoleBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string role)
        {
            if (role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Yönetici", StringComparison.OrdinalIgnoreCase))
                return Avalonia.Media.Brush.Parse("#1E3A5F"); // Deep blue for admin
            return Avalonia.Media.Brush.Parse("#1E293B"); // Slate for user
        }
        return Avalonia.Media.Brush.Parse("#161B27");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class RoleTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string role)
        {
            if (role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                return "Yönetici";
            return "Kullanıcı";
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FirstLetterConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && str.Length > 0)
        {
            return str.Substring(0, 1).ToUpper(culture);
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
