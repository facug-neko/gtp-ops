using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AxiomOps.UI.Converters;

/// <summary>Maps a <see cref="Services.LogSeverity"/> to the foreground brush for a log line.</summary>
public sealed class LogSeverityToForegroundConverter : IValueConverter
{
    private static readonly Brush Error = Freeze(0xB7, 0x1C, 0x1C);
    private static readonly Brush Warning = Freeze(0x8D, 0x6E, 0x00);
    private static readonly Brush Info = Freeze(0x21, 0x21, 0x21);
    private static readonly Brush Debug = Freeze(0x78, 0x90, 0x9C);
    private static readonly Brush Trace = Freeze(0xB0, 0xBE, 0xC5);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Services.LogSeverity.Error => Error,
        Services.LogSeverity.Warning => Warning,
        Services.LogSeverity.Debug => Debug,
        Services.LogSeverity.Trace => Trace,
        _ => Info,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Row background tint for a log line — a soft wash for errors/warnings, transparent otherwise.</summary>
public sealed class LogSeverityToBackgroundConverter : IValueConverter
{
    private static readonly Brush Error = Freeze(0xFD, 0xEC, 0xEA);
    private static readonly Brush Warning = Freeze(0xFF, 0xF8, 0xE1);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Services.LogSeverity.Error => Error,
        Services.LogSeverity.Warning => Warning,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Left accent-bar brush: red for errors, amber for warnings, transparent otherwise.</summary>
public sealed class LogSeverityToAccentConverter : IValueConverter
{
    private static readonly Brush Error = Freeze(0xC6, 0x28, 0x28);
    private static readonly Brush Warning = Freeze(0xF9, 0xA8, 0x25);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Services.LogSeverity.Error => Error,
        Services.LogSeverity.Warning => Warning,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>true → <see cref="TextWrapping.Wrap"/>, false → <see cref="TextWrapping.NoWrap"/>.</summary>
public sealed class BoolToTextWrappingConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
