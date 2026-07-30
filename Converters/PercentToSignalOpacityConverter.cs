using System.Globalization;
using System.Windows.Data;

namespace AkiLink.Converters;

/// <summary>
/// Converts a signal strength percentage (0–100) to an opacity value
/// for individual signal bars. ConverterParameter (1–4) selects which bar.
/// Bar N is fully opaque when signal ≥ N * 25; otherwise dimmed.
/// </summary>
public sealed class PercentToSignalOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var signal = value as int? ?? 0;
        var barIndex = parameter switch
        {
            int i => i,
            string s when int.TryParse(s, out var n) => n,
            _ => 1
        };

        // Clamp bar index 1–4
        barIndex = Math.Clamp(barIndex, 1, 4);

        // Bar N lights at N * 25%
        var threshold = barIndex * 25;
        return signal >= threshold ? 1.0 : 0.18;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
