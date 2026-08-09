using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AxiomOps.UI.Converters;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;
}

/// <summary>Visible when the bound bool is false; collapsed when true.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Collapses the element when the bound string is null or empty.</summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// GET /UserAccounts returns balances in cents; the portal (and this app)
/// display them in currency units. 1_009_340 → 10,093.40.
/// </summary>
public sealed class CentsToUnitsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal cents => cents / 100m,
        double cents => cents / 100d,
        long cents => cents / 100m,
        int cents => cents / 100m,
        _ => value ?? 0m,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true → green, false → red, null → gray.</summary>
public sealed class HealthToBrushConverter : IValueConverter
{
    private static readonly Brush Healthy = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
    private static readonly Brush Unhealthy = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush Unknown = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => Healthy,
        false => Unhealthy,
        _ => Unknown,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
