using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkiLink.Models;
using AkiLink.Services;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AkiLink.ViewModels;

/// <summary>
/// Available views in the sidebar navigation.
/// </summary>
public enum ViewType
{
    Devices,
    Audio,
    Settings
}

public partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothAudioService _btService;
    private readonly IAudioVolumeService _volumeService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService? _dialogService;
    private readonly IAudioLevelMeterService? _levelMeterService;
    private INotificationService? _notificationService;
    private bool _isUpdatingVolume;
    private bool _isUpdatingMuted;
    private bool _isLoadingSettings;
    private float _savedVolume = 0.75f;
    private bool _savedIsMuted;

    /// <summary>
    /// Set when the user explicitly disconnects (DisconnectCommand) so the
    /// resulting Closed state is NOT reported as an unexpected-drop toast.
    /// Auto-reconnect teardown and other internal transitions leave it false.
    /// </summary>
    private bool _userInitiatedDisconnect;

    /// <summary>
    /// Attaches the desktop notification sink (system tray balloon tip).
    /// Called once from App startup after the tray service is initialized —
    /// kept separate from the constructor to avoid a DI cycle (the tray needs
    /// the Window, the Window needs this ViewModel).
    /// </summary>
    public void AttachNotificationService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// The device instance that currently holds an open connection. Tracked separately
    /// from SelectedDevice so the per-device IsConnected flag can be cleared even if the
    /// user changes the selection between connect and disconnect (e.g. auto-reconnect).
    /// </summary>
    private BluetoothDeviceInfo? _connectedDevice;

    /// <summary>
    /// Id of the most recently connected device, persisted via AppSettings.LastDeviceId.
    /// Used by AutoConnectOnStartup to re-establish the last session on launch.
    /// </summary>
    private string? _lastDeviceId;

    public MainViewModel(
        IBluetoothAudioService btService,
        IAudioVolumeService volumeService,
        ISettingsService settingsService,
        IDialogService? dialogService = null,
        IAudioLevelMeterService? levelMeterService = null)
    {
        _btService = btService;
        _volumeService = volumeService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _levelMeterService = levelMeterService;

        // Suppress SaveSettings() while initializing and loading persisted settings.
        // Without this guard, property-change handlers fire during construction and
        // persist partially-loaded defaults (Volume=0, Language=en-US) over the
        // user's saved values — clobbering settings on every launch.
        _isLoadingSettings = true;

        Devices = new ObservableCollection<BluetoothDeviceInfo>();
        ConnectionHistory = new ObservableCollection<ConnectionHistoryEntry>();
        ConnectionHistory.CollectionChanged += (_, _) => HasHistory = ConnectionHistory.Count > 0;
        CodecSettings = new AudioCodecSettings();
        CodecSettings.PropertyChanged += (_, e) =>
        {
            // Real-time sync: when any codec sub-property changes (codec, bitrate,
            // sample rate, transmission mode), push to the service and persist.
            if (_isLoadingSettings) return;
            _btService.ConfigureCodec(CodecSettings);
            SaveSettings();
        };

        // Subscribe to service events
        _btService.DevicesUpdated += OnBtDevicesUpdated;
        _btService.StateChanged += OnBtStateChanged;
        _btService.ErrorOccurred += OnBtErrorOccurred;
        _btService.LogMessage += OnBtLogMessage;
        _btService.QualityUpdated += OnBtQualityUpdated;

        _volumeService.VolumeChanged += OnVolumeServiceVolumeChanged;
        _volumeService.MuteChanged += OnVolumeServiceMuteChanged;

        // Live VU meter: the level service raises LevelChanged on the UI thread
        // (DispatcherTimer), so the percent property can be updated directly.
        if (_levelMeterService != null)
        {
            _levelMeterService.LevelChanged += OnLevelMeterLevelChanged;
        }

        // Load persisted settings (used as defaults for Volume/IsMuted)
        LoadSettings();

        // Push initial codec preferences to the service
        _btService.ConfigureCodec(CodecSettings);

        // Defer AudioVolumeService COM init via Dispatcher to avoid a .NET JIT/GC-tracking
        // segfault when calling COM RCW methods during DI container startup.
        // In unit tests (no WPF Application), run synchronously instead.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null)
        {
            _ = dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    _volumeService.Initialize();
                    Volume = _savedVolume;
                    IsMuted = _savedIsMuted;
                    _levelMeterService?.Start();
                    _isLoadingSettings = false;
                }));
        }
        else
        {
            // Unit test path — safe because mocks don't invoke real COM
            _volumeService.Initialize();
            Volume = _savedVolume;
            IsMuted = _savedIsMuted;
            _levelMeterService?.Start();
            _isLoadingSettings = false;
        }
    }

    // ─── Observable Properties ───────────────────────────

    [ObservableProperty]
    private ObservableCollection<BluetoothDeviceInfo> _devices;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private BluetoothDeviceInfo? _selectedDevice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStateText = "Disconnected";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _hasDevices;

    [ObservableProperty]
    private float _volume;

    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Current audio level as an integer percentage (0–100), updated live by the
    /// level meter service. Backs the VU meter bar in the Devices view.
    /// </summary>
    [ObservableProperty]
    private int _audioLevelPercent;

    [ObservableProperty]
    private bool _autoReconnect;

    [ObservableProperty]
    private bool _autoConnectOnStartup;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private AudioPlaybackConnectionState _connectionState = AudioPlaybackConnectionState.Closed;

    // ─── Connection Quality ──────────────────────────────

    [ObservableProperty]
    private ConnectionQuality? _currentQuality;

    [ObservableProperty]
    private int _signalStrengthPercent;

    [ObservableProperty]
    private string _codecDisplayText = string.Empty;

    // ─── View Navigation ─────────────────────────────────

    [ObservableProperty]
    private ViewType _currentView = ViewType.Devices;

    [RelayCommand]
    private void NavigateToView(string viewName)
    {
        if (Enum.TryParse<ViewType>(viewName, ignoreCase: true, out var view))
        {
            CurrentView = view;
        }
    }

    [ObservableProperty]
    private AudioCodecSettings _codecSettings;

    partial void OnCodecSettingsChanged(AudioCodecSettings value)
    {
        if (_isLoadingSettings) return;
        _btService.ConfigureCodec(value);
        SaveSettings();
    }

    [RelayCommand]
    private void SelectCodec(PreferredCodec codec)
    {
        CodecSettings.Codec = codec;
    }

    [RelayCommand]
    private void SelectBitrate(string bitrate)
    {
        CodecSettings.Bitrate = bitrate;
    }

    [RelayCommand]
    private void SelectSampleRate(string sampleRateStr)
    {
        if (int.TryParse(sampleRateStr, out var rate))
        {
            CodecSettings.SampleRate = rate;
        }
    }

    [RelayCommand]
    private void SelectTransmissionMode(TransmissionMode mode)
    {
        CodecSettings.TransmissionMode = mode;
    }

    // ─── Connection History ──────────────────────────────

    [ObservableProperty]
    private ObservableCollection<ConnectionHistoryEntry> _connectionHistory;

    [ObservableProperty]
    private bool _hasHistory;

    [RelayCommand]
    private void ClearHistory()
    {
        var confirmed = _dialogService?.ShowConfirm(
            T("ConfirmClearHistory"),
            T("ConfirmClearHistoryMessage")) ?? true;
        if (!confirmed) return;

        ConnectionHistory.Clear();
        HasHistory = ConnectionHistory.Count > 0;
    }

    [RelayCommand]
    private void DeleteHistoryEntry(ConnectionHistoryEntry entry)
    {
        ConnectionHistory.Remove(entry);
        HasHistory = ConnectionHistory.Count > 0;
    }

    /// <summary>
    /// Add an entry to the connection history log.
    /// </summary>
    private void AddHistoryEntry(string deviceName, ConnectionEventType eventType, string? detail = null)
    {
        var entry = new ConnectionHistoryEntry(DateTime.Now, deviceName, eventType, detail);
        ConnectionHistory.Insert(0, entry);
        HasHistory = ConnectionHistory.Count > 0;
    }

    // ─── Commands ────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshDevices()
    {
        IsScanning = true;
        StatusMessage = T("StatusScanning");

        try
        {
            var devices = await _btService.ScanDevicesAsync();
            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(new BluetoothDeviceInfo(device));
            }

            HasDevices = Devices.Count > 0;
            StatusMessage = HasDevices
                ? T("StatusFound", Devices.Count)
                : T("StatusNoDevices");
        }
        catch (Exception ex)
        {
            StatusMessage = T("StatusErrorMsg", ex.Message);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task Connect()
    {
        if (SelectedDevice == null) return;

        var deviceName = SelectedDevice.Name;
        try
        {
            StatusMessage = T("StatusConnectingTo", deviceName);

            var success = await _btService.ConnectAsync(SelectedDevice.Id);

            if (success)
            {
                IsConnected = true;
                ConnectionStateText = T("StatusConnected");
                StatusMessage = T("StatusConnectedTo", deviceName);

                // Flag the selected device as connected in the device list.
                _connectedDevice = SelectedDevice;
                if (_connectedDevice != null) _connectedDevice.IsConnected = true;

                // Remember the last-connected device id so AutoConnectOnStartup
                // can re-establish this session on the next launch.
                _lastDeviceId = SelectedDevice.Id;
                if (!_isLoadingSettings) SaveSettings();

                if (AutoReconnect)
                {
                    await _btService.StartAutoReconnectAsync(SelectedDevice.Id);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = T("StatusErrorMsg", ex.Message);
            AddHistoryEntry(deviceName, ConnectionEventType.Error, ex.Message);
            System.Diagnostics.Debug.WriteLine($"[AkiLink] Connect threw: {ex}");
        }
    }

    private bool CanConnect() =>
        SelectedDevice != null && !IsConnected && !IsScanning;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private void Disconnect()
    {
        var deviceName = SelectedDevice?.Name ?? "Unknown";

        // Mark this as user-initiated so the Closed state event that follows
        // is not surfaced as an "unexpected drop" desktop notification.
        _userInitiatedDisconnect = true;

        _btService.Disconnect();
        _btService.StopAutoReconnect();

        IsConnected = false;
        ConnectionStateText = T("StatusDisconnected");
        StatusMessage = T("StatusDisconnectedMsg");

        // Clear the per-device connected flag on the device that actually held the
        // connection, even if the selection changed since it was opened.
        if (_connectedDevice != null)
        {
            _connectedDevice.IsConnected = false;
            _connectedDevice = null;
        }
    }

    private bool CanDisconnect() => IsConnected;

    /// <summary>
    /// Attempts to re-establish the last-connected device on startup.
    /// Scans for available devices, matches the persisted LastDeviceId, selects
    /// it, and connects. Best-effort: any failure is logged and swallowed so a
    /// stale/offline device can never block application startup.
    /// </summary>
    public async Task TryAutoConnectAsync()
    {
        if (!AutoConnectOnStartup || string.IsNullOrWhiteSpace(_lastDeviceId)) return;
        if (IsConnected) return;

        try
        {
            var devices = await _btService.ScanDevicesAsync();

            // Merge the scan results into the Devices collection so the match below
            // is deterministic — the service also raises DevicesUpdated asynchronously
            // (BeginInvoke on the UI thread), which would race this lookup.
            foreach (var device in devices)
            {
                if (!Devices.Any(d => string.Equals(d.Id, device.Id, StringComparison.OrdinalIgnoreCase)))
                    Devices.Add(new BluetoothDeviceInfo(device));
            }

            var match = Devices.FirstOrDefault(d =>
                string.Equals(d.Id, _lastDeviceId, StringComparison.OrdinalIgnoreCase));
            if (match is null) return;

            SelectedDevice = match;
            await Connect();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AkiLink] Auto-connect on startup failed: {ex}");
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        // OnIsMutedChanged pushes to the service AND persists. Setting the
        // _isUpdatingMuted guard here would skip BOTH — mute toggles never
        // reached SaveSettings, so the mute state was lost on restart.
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void ChangeLanguage(string culture)
    {
        LocalizationService.Instance.ChangeLanguage(culture);
        SaveSettings();
    }

    // ─── Partial Methods (from ObservableProperty) ──────

    partial void OnVolumeChanged(float value)
    {
        if (_isUpdatingVolume) return;
        _volumeService.Volume = value;
        if (!_isLoadingSettings) SaveSettings();
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_isUpdatingMuted) return;
        _volumeService.IsMuted = value;
        if (!_isLoadingSettings) SaveSettings();
    }

    partial void OnAutoReconnectChanged(bool value)
    {
        if (!value)
        {
            _btService.StopAutoReconnect();
        }
        else if (IsConnected && SelectedDevice != null)
        {
            _ = _btService.StartAutoReconnectAsync(SelectedDevice.Id);
        }
        if (!_isLoadingSettings) SaveSettings();
    }

    partial void OnAutoConnectOnStartupChanged(bool value)
    {
        if (!_isLoadingSettings) SaveSettings();
    }

    partial void OnSelectedDeviceChanged(BluetoothDeviceInfo? value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (!_isLoadingSettings) SaveSettings();
    }

    // ─── Settings Persistence ───────────────────────────

    private void LoadSettings()
    {
        var s = _settingsService.Load();

        if (Enum.TryParse<PreferredCodec>(s.Codec, ignoreCase: true, out var codec))
            CodecSettings.Codec = codec;
        if (!string.IsNullOrWhiteSpace(s.Bitrate))
            CodecSettings.Bitrate = s.Bitrate;
        if (s.SampleRate > 0)
            CodecSettings.SampleRate = s.SampleRate;
        if (Enum.TryParse<TransmissionMode>(s.TransmissionMode, ignoreCase: true, out var mode))
            CodecSettings.TransmissionMode = mode;

        // Don't set Volume/IsMuted here — those properties trigger OnVolumeChanged/
        // OnIsMutedChanged which call _volumeService setters that do COM interop.
        // That crashes (segfault) during DI container startup (.NET 10 JIT/GC issue
        // with COM RCW tracking). The deferred init in the constructor applies them.
        _savedVolume = Math.Clamp(s.Volume, 0f, 1f);
        _savedIsMuted = s.IsMuted;

        AutoReconnect = s.AutoReconnect;
        AutoConnectOnStartup = s.AutoConnectOnStartup;
        _lastDeviceId = s.LastDeviceId;
        CloseToTray = s.CloseToTray;

        try { LocalizationService.Instance.ChangeLanguage(s.Language); } catch { /* best effort */ }
    }

    internal void SaveSettings()
    {
        var s = new AppSettings
        {
            Codec = CodecSettings.Codec.ToString(),
            Bitrate = CodecSettings.Bitrate,
            SampleRate = CodecSettings.SampleRate,
            TransmissionMode = CodecSettings.TransmissionMode.ToString(),
            Volume = Volume,
            IsMuted = IsMuted,
            AutoReconnect = AutoReconnect,
            AutoConnectOnStartup = AutoConnectOnStartup,
            LastDeviceId = _lastDeviceId,
            CloseToTray = CloseToTray,
            Language = LocalizationService.Instance.CurrentCulture
        };
        _settingsService.Save(s);
    }

    // ─── Locale String Helper ────────────────────────────

    /// <summary>
    /// Resolve a localized string by key from the current locale ResourceDictionary.
    /// Falls back to the key itself if not found.
    /// </summary>
    private static string T(string key, params object[] args)
    {
        var template = System.Windows.Application.Current?.TryFindResource(key) as string;
        if (template == null) return key;
        return args.Length > 0 ? string.Format(template, args) : template;
    }

    // ─── Safe Dispatcher Helper ─────────────────────────

    private static async Task DispatchAsync(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    // ─── Event Handlers ─────────────────────────────────

    private async void OnBtDevicesUpdated(IReadOnlyList<DeviceInformation> devices)
    {
        try
        {
            await DispatchAsync(() =>
            {
                Devices.Clear();
                foreach (var device in devices)
                {
                    Devices.Add(new BluetoothDeviceInfo(device));
                }
                HasDevices = Devices.Count > 0;
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }

    private async void OnBtStateChanged(AudioPlaybackConnectionState state)
    {
        try
        {
            await DispatchAsync(() =>
            {
                var previousState = ConnectionState;
                ConnectionState = state;
                IsConnected = state == AudioPlaybackConnectionState.Opened;
                ConnectionStateText = state switch
                {
                    AudioPlaybackConnectionState.Closed => T("StatusDisconnected"),
                    AudioPlaybackConnectionState.Opened => T("StatusConnected"),
                    _ => "Unknown"
                };
                ConnectCommand.NotifyCanExecuteChanged();
                DisconnectCommand.NotifyCanExecuteChanged();

                // Log state changes with the selected device name
                var deviceName = SelectedDevice?.Name ?? "Unknown";
                if (state == AudioPlaybackConnectionState.Opened)
                {
                    // Track the device that owns the connection and flag it in the
                    // device list. On auto-reconnect SelectedDevice is still the same
                    // instance, so this re-flags the same entry.
                    _connectedDevice = SelectedDevice;
                    if (_connectedDevice != null) _connectedDevice.IsConnected = true;
                    AddHistoryEntry(deviceName, ConnectionEventType.Connected);

                    // Desktop notification: connection established (manual connect,
                    // auto-reconnect, or startup auto-connect).
                    _notificationService?.ShowNotification(
                        T("AppTitle"), T("NotificationConnected", deviceName));
                }
                else if (state == AudioPlaybackConnectionState.Closed)
                {
                    // Clear the flag on the tracked device even if no device is
                    // selected anymore, or if Connect() opened it without a prior
                    // Opened state event.
                    if (_connectedDevice != null)
                    {
                        _connectedDevice.IsConnected = false;
                        _connectedDevice = null;
                    }
                    if (previousState == AudioPlaybackConnectionState.Opened)
                    {
                        AddHistoryEntry(deviceName, ConnectionEventType.Disconnected);

                        // Desktop notification: unexpected drop (device out of range,
                        // link lost). User-initiated disconnects are suppressed via
                        // the flag set in Disconnect().
                        if (!_userInitiatedDisconnect)
                        {
                            _notificationService?.ShowNotification(
                                T("AppTitle"), T("NotificationDisconnected", deviceName));
                        }
                    }
                    _userInitiatedDisconnect = false;
                }
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }

    private async void OnBtErrorOccurred(string message)
    {
        try
        {
            await DispatchAsync(() =>
            {
                StatusMessage = T("StatusErrorMsg", message);
                var deviceName = SelectedDevice?.Name ?? "Unknown";
                AddHistoryEntry(deviceName, ConnectionEventType.Error, message);
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }

    private async void OnBtLogMessage(string message)
    {
        try
        {
            await DispatchAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[AkiLink] {message}");
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }

    private async void OnVolumeServiceVolumeChanged(float volume)
    {
        try
        {
            await DispatchAsync(() =>
            {
                _isUpdatingVolume = true;
                Volume = volume;
                _isUpdatingVolume = false;
                // Persist external changes (volume keys, other apps) so the slider
                // restores the real system value after restart. The guard above only
                // suppresses the push-back to the service (loop protection), not the save.
                if (!_isLoadingSettings) SaveSettings();
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }

    private async void OnVolumeServiceMuteChanged(bool muted)
    {
        try
        {
            await DispatchAsync(() =>
            {
                _isUpdatingMuted = true;
                IsMuted = muted;
                _isUpdatingMuted = false;
                // Persist external mute changes (media keys, other apps) so the
                // state survives restart.
                if (!_isLoadingSettings) SaveSettings();
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }

    // ─── Level Meter Event Handler ──────────────────────

    private async void OnLevelMeterLevelChanged(float level)
    {
        try
        {
            await DispatchAsync(() =>
            {
                // Clamp to 0–1 defensively (COM GetPeakValue is documented as 0–1
                // but never trust native code), then scale to an integer percent.
                AudioLevelPercent = (int)Math.Round(Math.Clamp(level, 0f, 1f) * 100f);
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }

    // ─── Quality Event Handler ──────────────────────────

    private async void OnBtQualityUpdated(ConnectionQuality quality)
    {
        try
        {
            await DispatchAsync(() =>
            {
                CurrentQuality = quality;
                SignalStrengthPercent = quality.SignalStrength ?? 0;

                // Build a compact display string: "AAC · 48.0 kHz · ~80 ms"
                var parts = new List<string>(3);
                if (quality.CodecInUse is { Length: > 0 } codec && !string.Equals(codec, "Auto", StringComparison.OrdinalIgnoreCase))
                    parts.Add(codec);
                if (quality.SampleRate is { Length: > 0 } sr)
                    parts.Add(sr);
                if (quality.Latency is { Length: > 0 } lat)
                    parts.Add(lat);

                CodecDisplayText = parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
            });
        }
        catch { /* Suppress exceptions from async void event handlers */ }
    }
}
