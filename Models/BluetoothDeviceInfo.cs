using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Enumeration;

namespace AkiLink.Models;

/// <summary>
/// Represents a paired Bluetooth audio device. Stores only the data needed for binding;
/// the underlying WinRT DeviceInformation is not retained.
/// </summary>
public partial class BluetoothDeviceInfo : ObservableObject
{
    /// <summary>
    /// Creates from a WinRT DeviceInformation object (used by BluetoothAudioService).
    /// </summary>
    public BluetoothDeviceInfo(DeviceInformation deviceInfo)
    {
        Id = deviceInfo.Id;
        Name = deviceInfo.Name;
        Kind = deviceInfo.Kind;
    }

    /// <summary>
    /// Test-friendly constructor (no WinRT dependency).
    /// </summary>
    public BluetoothDeviceInfo(string id, string name)
    {
        Id = id;
        Name = name;
        Kind = DeviceInformationKind.Unknown;
    }

    public string Id { get; }
    public string Name { get; }
    public DeviceInformationKind Kind { get; }

    [ObservableProperty]
    private bool _isConnected;

    public override string ToString() => Name;
}
