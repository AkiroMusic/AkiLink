using AkiLink.Models;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AkiLink.Services;

public interface IBluetoothAudioService : IDisposable
{
    event Action<IReadOnlyList<DeviceInformation>>? DevicesUpdated;
    event Action<AudioPlaybackConnectionState>? StateChanged;
    event Action<string>? ErrorOccurred;
    event Action<string>? LogMessage;
    event Action<ConnectionQuality>? QualityUpdated;

    Task<IReadOnlyList<DeviceInformation>> ScanDevicesAsync();
    Task<bool> ConnectAsync(string deviceId);
    void Disconnect();
    Task StartAutoReconnectAsync(string deviceId);
    void StopAutoReconnect();

    /// <summary>
    /// Apply audio codec / quality preferences for subsequent connections.
    /// </summary>
    void ConfigureCodec(AudioCodecSettings settings);

    /// <summary>
    /// Current negotiated connection quality metrics.
    /// </summary>
    ConnectionQuality CurrentQuality { get; }

    /// <summary>
    /// Last configured codec settings (null if never configured).
    /// </summary>
    AudioCodecSettings? CodecPreferences { get; }

    AudioPlaybackConnectionState CurrentState { get; }
    bool IsConnected { get; }
}
