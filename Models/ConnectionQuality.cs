namespace AkiLink.Models;

/// <summary>
/// Represents the current quality metrics of an active Bluetooth audio connection.
/// Values are best-effort; actual available data depends on WinRT and driver support.
/// </summary>
public class ConnectionQuality
{
    /// <summary>
    /// Estimated signal strength, 0–100. Null when unknown or disconnected.
    /// </summary>
    public int? SignalStrength { get; init; }

    /// <summary>
    /// Negotiated audio codec label, e.g. "AAC", "SBC", "LDAC". Null when not connected.
    /// </summary>
    public string? CodecInUse { get; init; }

    /// <summary>
    /// Approximate round-trip latency in milliseconds, or a descriptive string when unavailable.
    /// </summary>
    public string? Latency { get; init; }

    /// <summary>
    /// Currently configured bitrate label, e.g. "Auto", "320k".
    /// </summary>
    public string? Bitrate { get; init; }

    /// <summary>
    /// Currently configured sample rate label, e.g. "44100 Hz".
    /// </summary>
    public string? SampleRate { get; init; }

    /// <summary>
    /// True when a valid connection is active and metrics are available.
    /// </summary>
    public bool IsAvailable => SignalStrength.HasValue || CodecInUse is not null;

    public static ConnectionQuality Unknown { get; } = new();

    public static ConnectionQuality Disconnected { get; } = new();
}
