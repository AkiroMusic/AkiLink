using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AkiLink.Converters;

/// <summary>
/// Converts an integer percentage (0–100) into a star-sized GridLength so a
/// level bar can be expressed as a proportional grid column.
/// Usage: bind a ColumnDefinition.Width to the percent value; bind the trailing
/// (empty) column to the same value with ConverterParameter="Remainder" to get
/// the leftover space.
/// </summary>
public class PercentToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value as int? ?? 0;
        percent = Math.Clamp(percent, 0, 100);

        var isRemainder = parameter is string s
            && s.Equals("Remainder", StringComparison.OrdinalIgnoreCase);

        return isRemainder
            ? new GridLength(100 - percent, GridUnitType.Star)
            : new GridLength(percent, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
