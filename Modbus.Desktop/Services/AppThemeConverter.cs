using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Modbus.Desktop.Services;

public class AppThemeConverter : IValueConverter
{
    public static readonly AppThemeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AppTheme theme
            ? LocalizationService.Instance[theme == AppTheme.Light ? "ThemeLight" : "ThemeDark"]
            : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
