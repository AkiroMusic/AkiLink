using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AkiLink.Converters;

/// <summary>
/// Returns ThemePrimary brush when CurrentView matches ConverterParameter,
/// otherwise returns ThemeTextSecondary. Eliminates the need for inline Styles
/// with DataTriggers in sidebar buttons (which triggered BAML deserialization bugs).
/// </summary>
public sealed class ViewToActiveBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var currentView = value?.ToString() ?? string.Empty;
        var targetView = parameter as string ?? string.Empty;
        var isActive = string.Equals(currentView, targetView, StringComparison.OrdinalIgnoreCase);
        var key = isActive ? "ThemePrimary" : "ThemeTextSecondary";
        return Application.Current.TryFindResource(key)
               ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
