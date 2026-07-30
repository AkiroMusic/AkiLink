using Moq;
using AkiLink.Models;
using AkiLink.Services;
using AkiLink.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AkiLink.Tests;

public class MainViewModelTests
{
    private readonly Mock<IBluetoothAudioService> _btMock;
    private readonly Mock<IAudioVolumeService> _volumeMock;
    private readonly Mock<IDialogService> _dialogMock;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _btMock = new Mock<IBluetoothAudioService>();
        _volumeMock = new Mock<IAudioVolumeService>();
        _dialogMock = new Mock<IDialogService>();

        // Setup default mocks
        _volumeMock.Setup(x => x.Volume).Returns(0.75f);
        _volumeMock.Setup(x => x.IsMuted).Returns(false);

        _viewModel = new MainViewModel(_btMock.Object, _volumeMock.Object, _dialogMock.Object);
    }

    [Fact]
    public void Constructor_SubscribesToServiceEvents()
    {
        Assert.Equal(0.75f, _viewModel.Volume);
        Assert.False(_viewModel.IsMuted);
        Assert.False(_viewModel.IsConnected);
        Assert.Equal("Disconnected", _viewModel.ConnectionStateText);
    }

    [Fact]
    public void Constructor_CallsInitializeOnVolumeService()
    {
        _volumeMock.Verify(x => x.Initialize(), Times.Once);
    }

    [Fact]
    public void Constructor_DevicesCollection_IsEmpty()
    {
        Assert.Empty(_viewModel.Devices);
    }

    [Fact]
    public void VolumeChange_FromService_FiresVolumeChangedEvent()
    {
        _volumeMock.Raise(x => x.VolumeChanged += null, 0.5f);

        Assert.Equal(0.5f, _viewModel.Volume);
    }

    [Fact]
    public void VolumeChange_FromViewModel_SyncsToService()
    {
        _viewModel.Volume = 0.3f;

        _volumeMock.VerifySet(x => x.Volume = 0.3f);
    }

    [Fact]
    public void MuteChange_FromService_FiresMuteChangedEvent()
    {
        _volumeMock.Raise(x => x.MuteChanged += null, true);

        Assert.True(_viewModel.IsMuted);
    }

    [Fact]
    public void MuteChange_FromViewModel_SyncsToService()
    {
        _viewModel.IsMuted = true;

        _volumeMock.VerifySet(x => x.IsMuted = true);
    }

    [Fact]
    public void StateChange_ToOpened_SetsIsConnected()
    {
        _btMock.Raise(x => x.StateChanged += null, AudioPlaybackConnectionState.Opened);

        Assert.True(_viewModel.IsConnected);
        Assert.Equal("StatusConnected", _viewModel.ConnectionStateText);
    }

    [Fact]
    public void StateChange_ToClosed_SetsIsDisconnected()
    {
        _btMock.Raise(x => x.StateChanged += null, AudioPlaybackConnectionState.Closed);

        Assert.False(_viewModel.IsConnected);
        Assert.Equal("StatusDisconnected", _viewModel.ConnectionStateText);
    }

    [Fact]
    public void CanConnect_WhenNoDeviceSelected_ReturnsFalse()
    {
        Assert.False(_viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void CanConnect_WhenAlreadyConnected_ReturnsFalse()
    {
        _btMock.Raise(x => x.StateChanged += null, AudioPlaybackConnectionState.Opened);

        _viewModel.SelectedDevice = CreateBluetoothDevice("Test Device", "test-id-1");

        Assert.False(_viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void CanConnect_WhenScanning_ReturnsFalse()
    {
        _viewModel.IsScanning = true;
        _viewModel.SelectedDevice = CreateBluetoothDevice("Test Device", "test-id-1");

        Assert.False(_viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public async Task RefreshDevices_WhenScanSucceeds_CallsService()
    {
        // Use empty list — DeviceInformation has no public constructor in WinRT projection,
        // so tests verify scan invocation and state changes rather than wrapping behavior.
        _btMock.Setup(x => x.ScanDevicesAsync())
            .ReturnsAsync(Array.Empty<DeviceInformation>().AsReadOnly());

        await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);

        _btMock.Verify(x => x.ScanDevicesAsync(), Times.Once);
        Assert.Empty(_viewModel.Devices);
        Assert.False(_viewModel.HasDevices);
        Assert.Contains("StatusNoDevices", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshDevices_WhenScanFails_SetsErrorMessage()
    {
        _btMock.Setup(x => x.ScanDevicesAsync())
            .ThrowsAsync(new InvalidOperationException("Bluetooth radio unavailable"));

        await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);

        Assert.Empty(_viewModel.Devices);
        Assert.Contains("StatusErrorMsg", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshDevices_TogglesIsScanning()
    {
        _btMock.Setup(x => x.ScanDevicesAsync())
            .ReturnsAsync(Array.Empty<DeviceInformation>().AsReadOnly());

        var scanningStates = new List<bool>();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsScanning))
                scanningStates.Add(_viewModel.IsScanning);
        };

        await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);

        // Should be false at the end
        Assert.False(_viewModel.IsScanning);
        // Should have toggled: true then false
        Assert.Contains(true, scanningStates);
        Assert.Contains(false, scanningStates);
    }

    [Fact]
    public async Task Connect_CallsConnectOnService()
    {
        var device = CreateBluetoothDevice("Test Device", "test-id-1");
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;

        _btMock.Setup(x => x.ConnectAsync(device.Id))
            .ReturnsAsync(true);

        await _viewModel.ConnectCommand.ExecuteAsync(null);

        _btMock.Verify(x => x.ConnectAsync(device.Id), Times.Once);
    }

    [Fact]
    public async Task Connect_WhenSuccessful_UpdatesConnectionState()
    {
        var device = CreateBluetoothDevice("Test Device", "test-id-1");
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;

        _btMock.Setup(x => x.ConnectAsync(device.Id))
            .ReturnsAsync(true);

        await _viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(_viewModel.IsConnected);
        Assert.Equal("StatusConnected", _viewModel.ConnectionStateText);
        Assert.Contains("StatusConnectedTo", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task Connect_WithAutoReconnect_StartsAutoReconnect()
    {
        var device = CreateBluetoothDevice("Test Device", "test-id-1");
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;
        _viewModel.AutoReconnect = true;

        _btMock.Setup(x => x.ConnectAsync(device.Id))
            .ReturnsAsync(true);

        await _viewModel.ConnectCommand.ExecuteAsync(null);

        _btMock.Verify(x => x.StartAutoReconnectAsync(device.Id), Times.Once);
    }

    [Fact]
    public async Task Disconnect_CallsDisconnectOnService()
    {
        _btMock.Raise(x => x.StateChanged += null, AudioPlaybackConnectionState.Opened);

        _viewModel.DisconnectCommand.Execute(null);

        _btMock.Verify(x => x.Disconnect(), Times.Once);
        _btMock.Verify(x => x.StopAutoReconnect(), Times.Once);
    }

    [Fact]
    public async Task Disconnect_UpdatesConnectionState()
    {
        _btMock.Raise(x => x.StateChanged += null, AudioPlaybackConnectionState.Opened);

        _viewModel.DisconnectCommand.Execute(null);

        Assert.False(_viewModel.IsConnected);
        Assert.Equal("StatusDisconnected", _viewModel.ConnectionStateText);
        Assert.Contains("StatusDisconnectedMsg", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task ToggleMute_TogglesIsMuted()
    {
        var initial = _viewModel.IsMuted;

        _viewModel.ToggleMuteCommand.Execute(null);

        Assert.NotEqual(initial, _viewModel.IsMuted);
        _volumeMock.VerifySet(x => x.IsMuted = !initial);
    }

    [Fact]
    public async Task ToggleMute_WhenCalledTwice_ReturnsToOriginal()
    {
        var initial = _viewModel.IsMuted;

        _viewModel.ToggleMuteCommand.Execute(null);
        _viewModel.ToggleMuteCommand.Execute(null);

        Assert.Equal(initial, _viewModel.IsMuted);
    }

    [Fact]
    public void ErrorOccurred_UpdatesStatusMessage()
    {
        _btMock.Raise(x => x.ErrorOccurred += null, "Test error");

        Assert.Contains("StatusErrorMsg", _viewModel.StatusMessage);
    }

    [Fact]
    public void BtDevicesUpdated_ClearsAndPopulatesDevices()
    {
        // Pre-populate with some devices using the test-friendly constructor
        _viewModel.Devices.Add(CreateBluetoothDevice("Existing", "existing-id"));

        // Raise DevicesUpdated with empty list — DeviceInformation has no public constructor
        // in WinRT projection, so we verify the clear-and-reset behavior rather than wrapping.
        _btMock.Raise(x => x.DevicesUpdated += null, (IReadOnlyList<DeviceInformation>)new List<DeviceInformation>());

        Assert.Empty(_viewModel.Devices);
        Assert.False(_viewModel.HasDevices);
    }

    [Fact]
    public void SelectedDevice_WhenSet_CanConnectBecomesTrue()
    {
        _viewModel.SelectedDevice = CreateBluetoothDevice("Test Device", "test-id-1");

        Assert.True(_viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void AutoReconnect_WhenEnabled_AndConnected_StartsAutoReconnect()
    {
        var device = CreateBluetoothDevice("Test Device", "test-id-1");
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;
        _btMock.Raise(x => x.StateChanged += null, AudioPlaybackConnectionState.Opened);

        _viewModel.AutoReconnect = true;

        _btMock.Verify(x => x.StartAutoReconnectAsync(device.Id), Times.Once);
    }

    [Fact]
    public void AutoReconnect_WhenDisabled_StopsAutoReconnect()
    {
        _viewModel.AutoReconnect = true;
        _viewModel.AutoReconnect = false;

        _btMock.Verify(x => x.StopAutoReconnect(), Times.Once);
    }

    [Fact]
    public async Task RefreshDevices_AfterScan_IsScanningIsFalse()
    {
        _btMock.Setup(x => x.ScanDevicesAsync())
            .ReturnsAsync(Array.Empty<DeviceInformation>().AsReadOnly());

        await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);

        Assert.False(_viewModel.IsScanning);
    }

    [Fact]
    public async Task RefreshDevices_WhenNoDevices_SetsStatusMessage()
    {
        _btMock.Setup(x => x.ScanDevicesAsync())
            .ReturnsAsync(new List<DeviceInformation>().AsReadOnly());

        await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);

        Assert.Empty(_viewModel.Devices);
        Assert.False(_viewModel.HasDevices);
        Assert.Contains("StatusNoDevices", _viewModel.StatusMessage);
    }

    [Fact]
    public void VolumeServiceMuteChanged_DoesNotCreateInfiniteLoop()
    {
        // Simulate service raising MuteChanged -> should not cause re-entrant call
        _volumeMock.Raise(x => x.MuteChanged += null, true);

        Assert.True(_viewModel.IsMuted);
        // Should only have set IsMuted once (the guard prevents the loop)
        _volumeMock.VerifySet(x => x.IsMuted = true, Times.Never);
    }

    [Fact]
    public void VolumeServiceVolumeChanged_DoesNotCreateInfiniteLoop()
    {
        // Simulate service raising VolumeChanged -> should not cause re-entrant call
        _volumeMock.Raise(x => x.VolumeChanged += null, 0.5f);

        Assert.Equal(0.5f, _viewModel.Volume);
        // Should NOT have set Volume on the service again (guard prevents loop)
        _volumeMock.VerifySet(x => x.Volume = 0.5f, Times.Never);
    }

    [Fact]
    public async Task Connect_WhenSelectedDeviceIsNull_DoesNotCallService()
    {
        _viewModel.SelectedDevice = null;

        await _viewModel.ConnectCommand.ExecuteAsync(null);

        _btMock.Verify(x => x.ConnectAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Connect_WhenNotSuccessful_DoesNotChangeState()
    {
        var device = CreateBluetoothDevice("Test Device", "test-id-1");
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;

        _btMock.Setup(x => x.ConnectAsync(device.Id))
            .ReturnsAsync(false);

        await _viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.False(_viewModel.IsConnected);
        Assert.Equal("Disconnected", _viewModel.ConnectionStateText);
    }

    // ─── History Commands (RED tests — fail before implementation) ──────

    [Fact]
    public void ClearHistory_ShowsConfirmation_BeforeClearing()
    {
        // Arrange
        _viewModel.ConnectionHistory.Insert(0, new ConnectionHistoryEntry(DateTime.Now, "Device-A", ConnectionEventType.Connected));
        _viewModel.HasHistory = true;

        _dialogMock.Setup(x => x.ShowConfirm(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        // Act
        _viewModel.ClearHistoryCommand.Execute(null);

        // Assert — RED: ClearHistory currently ignores the dialog and clears anyway.
        // After fix: ClearHistory should check dialog and abort when user cancels.
        Assert.NotEmpty(_viewModel.ConnectionHistory);
        Assert.True(_viewModel.HasHistory);
    }

    [Fact]
    public void ClearHistory_WhenConfirmed_ClearsAllEntries()
    {
        // Arrange
        _viewModel.ConnectionHistory.Insert(0, new ConnectionHistoryEntry(DateTime.Now, "Device-B", ConnectionEventType.Connected));
        _viewModel.HasHistory = true;

        _dialogMock.Setup(x => x.ShowConfirm(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        // Act
        _viewModel.ClearHistoryCommand.Execute(null);

        // Assert — verifies the happy path once dialog integration lands.
        Assert.Empty(_viewModel.ConnectionHistory);
        Assert.False(_viewModel.HasHistory);
    }

    [Fact]
    public void DeleteHistoryEntry_RemovesSpecificEntry()
    {
        // Arrange
        var entry1 = new ConnectionHistoryEntry(DateTime.Now, "Alpha", ConnectionEventType.Connected);
        var entry2 = new ConnectionHistoryEntry(DateTime.Now, "Bravo", ConnectionEventType.Connected);
        var entry3 = new ConnectionHistoryEntry(DateTime.Now, "Charlie", ConnectionEventType.Connected);
        _viewModel.ConnectionHistory.Insert(0, entry1);
        _viewModel.ConnectionHistory.Insert(0, entry2);
        _viewModel.ConnectionHistory.Insert(0, entry3);
        // HasHistory is still false because AddHistoryEntry is what sets it

        // Act
        _viewModel.ConnectionHistory.Remove(entry2);

        // Assert
        Assert.Equal(2, _viewModel.ConnectionHistory.Count);
        Assert.DoesNotContain(entry2, _viewModel.ConnectionHistory);
        // RED: HasHistory should be true (2 items remain) but it's false because
        // removing from the collection doesn't update HasHistory.
        Assert.True(_viewModel.HasHistory);
    }

    [Fact]
    public void HasHistory_AfterDeletingLastEntry_ReturnsFalse()
    {
        // Arrange
        var entry = new ConnectionHistoryEntry(DateTime.Now, "Solo", ConnectionEventType.Connected);
        _viewModel.ConnectionHistory.Insert(0, entry);
        _viewModel.HasHistory = true; // Simulate state after AddHistoryEntry was called

        // Act
        _viewModel.ConnectionHistory.Remove(entry);

        // Assert
        Assert.Empty(_viewModel.ConnectionHistory);
        // RED: HasHistory stays true because removing from the collection
        // does not trigger a HasHistory recalculation.
        Assert.False(_viewModel.HasHistory);
    }

    /// <summary>
    /// Creates a BluetoothDeviceInfo for testing using the test-friendly constructor.
    /// </summary>
    private static BluetoothDeviceInfo CreateBluetoothDevice(string name, string id)
    {
        return new BluetoothDeviceInfo(id, name);
    }
}
