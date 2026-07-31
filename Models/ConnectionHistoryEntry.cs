using CommunityToolkit.Mvvm.ComponentModel;

namespace AkiLink.Models;

/// <summary>
/// Represents a single entry in the connection history log.
/// </summary>
public enum ConnectionEventType
{
    Connected,
    Disconnected,
    Error
}

public partial class ConnectionHistoryEntry : ObservableObject
{
    public ConnectionHistoryEntry(DateTime timestamp, string deviceName, ConnectionEventType eventType, string? detail = null)
    {
        Timestamp = timestamp;
        DeviceName = deviceName;
        EventType = eventType;
        Detail = detail;
    }

    public DateTime Timestamp { get; }
    public string DeviceName { get; }
    public ConnectionEventType EventType { get; }
    public string? Detail { get; }

    /// <summary>
    /// True when the entry carries a detail line (error message, etc.), used to
    /// show/hide the detail row. (The Detail string itself cannot be bound to
    /// BoolToVisibilityConverter — it only handles bool.)
    /// </summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>
    /// Formatted timestamp string for display.
    /// </summary>
    public string TimestampFormatted => Timestamp.ToString("HH:mm:ss");

    /// <summary>
    /// Localized event type key suffix for DynamicResource binding.
    /// </summary>
    public string EventTypeKey => EventType switch
    {
        ConnectionEventType.Connected => "EventConnected",
        ConnectionEventType.Disconnected => "EventDisconnected",
        ConnectionEventType.Error => "EventError",
        _ => "EventError"
    };
}
