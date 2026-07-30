using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AkiLink.Converters;

/// <summary>
/// Returns true when the button's Tag (string) matches the bound CurrentView (string).
/// Used for sidebar active state: Foreground="{Binding CurrentView, Converter={StaticResource ViewTypeEqualityConverter}, ConverterParameter=devices}"
/// </summary>
public class ViewTypeEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var currentView = value?.ToString() ?? string.Empty;
        var targetView = parameter as string;
        return string.Equals(currentView, targetView, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string view name to a boolean for visibility.
/// </summary>
public class ViewTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var currentView = value?.ToString() ?? string.Empty;
        var targetView = parameter as string;
        return string.Equals(currentView, targetView, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
