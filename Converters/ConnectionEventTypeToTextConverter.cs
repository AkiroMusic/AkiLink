using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AkiLink.Models;

namespace AkiLink.Converters;

/// <summary>
/// Converts a ConnectionEventType enum value to a localized display string
/// by looking up the corresponding DynamicResource key.
/// </summary>
public sealed class ConnectionEventTypeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ConnectionEventType.Connected => "EventConnected",
            ConnectionEventType.Disconnected => "EventDisconnected",
            ConnectionEventType.Error => "EventError",
            _ => null
        };

        if (key is not null)
        {
            var localized = Application.Current?.TryFindResource(key) as string;
            if (localized is not null)
                return localized;
        }

        return value?.ToString() ?? "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
