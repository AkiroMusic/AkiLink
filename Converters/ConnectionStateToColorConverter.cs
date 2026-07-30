using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Windows.Media.Audio;

namespace AkiLink.Converters;

public class ConnectionStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AudioPlaybackConnectionState state)
        {
            return state switch
            {
                AudioPlaybackConnectionState.Closed => new SolidColorBrush(Color.FromRgb(156, 163, 175)), // Gray (disconnected)
                AudioPlaybackConnectionState.Opened => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Green (connected)
                _ => new SolidColorBrush(Color.FromRgb(245, 158, 11))                                     // Orange (connecting/unknown)
            };
        }
        return new SolidColorBrush(Color.FromRgb(156, 163, 175));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
