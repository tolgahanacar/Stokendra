using Avalonia.Data.Converters;
using Stokendra.Infrastructure;
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
                return LocalizationManager.L("role_admin");
            return LocalizationManager.L("role_user");
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

public class SuccessColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? Avalonia.Media.Brush.Parse("#10B981")
            : Avalonia.Media.Brush.Parse("#EF4444");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class SuccessIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "✔" : "⚠";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Maps nullable NumericUpDown decimal values to non-nullable double (null maps to 0).
/// </summary>
public class SafeDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d) return System.Convert.ToDecimal(d);
        if (value is decimal m) return m;
        return 0m;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return 0d;
        if (value is decimal m) return System.Convert.ToDouble(m);
        if (value is double d) return d;
        return 0d;
    }
}

/// <summary>
/// Returns true when integer count equals zero (for empty state visibility).
/// </summary>
public class ZeroToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count && count == 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
