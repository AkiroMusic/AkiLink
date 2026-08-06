using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AkiLink.Converters;

/// <summary>
/// Returns the active pill background (ThemePrimaryTint) when CurrentView matches
/// ConverterParameter, otherwise Transparent. Drives the Material 3 navigation-rail
/// pill behind each sidebar button.
/// </summary>
public sealed class ViewToActiveBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var currentView = value?.ToString() ?? string.Empty;
        var targetView = parameter as string ?? string.Empty;
        var isActive = string.Equals(currentView, targetView, StringComparison.OrdinalIgnoreCase);

        if (isActive)
            return Application.Current.TryFindResource("ThemePrimaryTint")
                   ?? new SolidColorBrush(Colors.Transparent);

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
