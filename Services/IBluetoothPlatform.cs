using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AkiLink.Services;

/// <summary>
/// Abstraction over WinRT static Bluetooth audio APIs
/// (AudioPlaybackConnection, DeviceInformation) to enable
/// unit testing without requiring the real WinRT runtime.
/// </summary>
public interface IBluetoothPlatform
{
    /// <summary>
    /// Wraps <see cref="AudioPlaybackConnection.TryCreateFromId"/>.
    /// </summary>
    AudioPlaybackConnection? TryCreateAudioPlaybackConnection(string deviceId);

    /// <summary>
    /// Wraps <see cref="AudioPlaybackConnection.GetDeviceSelector"/>.
    /// </summary>
    string GetDeviceSelector();

    /// <summary>
    /// Wraps <see cref="DeviceInformation.FindAllAsync(string)"/>.
    /// </summary>
    Task<IReadOnlyList<DeviceInformation>> FindAllAudioDevicesAsync(string selector);
}
