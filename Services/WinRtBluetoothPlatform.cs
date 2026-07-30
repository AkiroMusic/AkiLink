using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AkiLink.Services;

/// <summary>
/// Real WinRT implementation of <see cref="IBluetoothPlatform"/>.
/// Delegates directly to <see cref="AudioPlaybackConnection"/>
/// and <see cref="DeviceInformation"/> static methods.
/// </summary>
public sealed class WinRtBluetoothPlatform : IBluetoothPlatform
{
    public AudioPlaybackConnection? TryCreateAudioPlaybackConnection(string deviceId)
    {
        return AudioPlaybackConnection.TryCreateFromId(deviceId);
    }

    public string GetDeviceSelector()
    {
        return AudioPlaybackConnection.GetDeviceSelector();
    }

    public async Task<IReadOnlyList<DeviceInformation>> FindAllAudioDevicesAsync(string selector)
    {
        var devices = await DeviceInformation.FindAllAsync(selector);
        return devices is not null
            ? (IReadOnlyList<DeviceInformation>)devices
            : Array.Empty<DeviceInformation>();
    }
}
