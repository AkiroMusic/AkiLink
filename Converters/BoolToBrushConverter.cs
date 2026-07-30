using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AkiLink.Converters;

/// <summary>
/// Returns ThemePrimary brush when bound value is true, ThemeTextSecondary otherwise.
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "ThemePrimary" : "ThemeTextSecondary";
        return Application.Current.TryFindResource(key) ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
