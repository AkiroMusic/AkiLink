using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Enumeration;

namespace AkiLink.Models;

public partial class BluetoothDeviceInfo : ObservableObject
{
    public BluetoothDeviceInfo(DeviceInformation deviceInfo)
    {
        Id = deviceInfo.Id;
        Name = deviceInfo.Name;
        Kind = deviceInfo.Kind;
        DeviceInfo = deviceInfo;
    }

    /// <summary>
    /// Test-friendly constructor. DeviceInfo will be null.
    /// </summary>
    public BluetoothDeviceInfo(string id, string name)
    {
        Id = id;
        Name = name;
        Kind = DeviceInformationKind.Unknown;
        DeviceInfo = null!;
    }

    public string Id { get; }
    public string Name { get; }
    public DeviceInformationKind Kind { get; }
    public DeviceInformation DeviceInfo { get; }

    [ObservableProperty]
    private bool _isConnected;

    public override string ToString() => Name;
}
