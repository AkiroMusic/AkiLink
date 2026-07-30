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
    private bool _isUpdatingVolume;
    private bool _isUpdatingMuted;
    private float _savedVolume = 0.75f;
    private bool _savedIsMuted;

    public MainViewModel(IBluetoothAudioService btService, IAudioVolumeService volumeService, ISettingsService settingsService, IDialogService? dialogService = null)
    {
        _btService = btService;
        _volumeService = volumeService;
        _settingsService = settingsService;
        _dialogService = dialogService;

        Devices = new ObservableCollection<BluetoothDeviceInfo>();
        ConnectionHistory = new ObservableCollection<ConnectionHistoryEntry>();
        ConnectionHistory.CollectionChanged += (_, _) => HasHistory = ConnectionHistory.Count > 0;
        CodecSettings = new AudioCodecSettings();
        CodecSettings.PropertyChanged += (_, e) =>
        {
            // Real-time sync: when any codec sub-property changes (codec, bitrate,
            // sample rate, transmission mode), push to the service and persist.
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
                }));
        }
        else
        {
            // Unit test path — safe because mocks don't invoke real COM
            _volumeService.Initialize();
            Volume = _savedVolume;
            IsMuted = _savedIsMuted;
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

    [ObservableProperty]
    private bool _autoReconnect;

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

        _btService.Disconnect();
        _btService.StopAutoReconnect();

        IsConnected = false;
        ConnectionStateText = T("StatusDisconnected");
        StatusMessage = T("StatusDisconnectedMsg");
    }

    private bool CanDisconnect() => IsConnected;

    [RelayCommand]
    private void ToggleMute()
    {
        _isUpdatingMuted = true;
        IsMuted = !IsMuted;
        _isUpdatingMuted = false;
        _volumeService.IsMuted = IsMuted;
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
        SaveSettings();
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_isUpdatingMuted) return;
        _volumeService.IsMuted = value;
        SaveSettings();
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
        SaveSettings();
    }

    partial void OnSelectedDeviceChanged(BluetoothDeviceInfo? value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        SaveSettings();
    }

    // ─── Settings Persistence ───────────────────────────

    private void LoadSettings()
    {
        var s = _settingsService.Load();

        if (Enum.TryParse<PreferredCodec>(s.Codec, ignoreCase: true, out var codec))
            CodecSettings.Codec = codec;
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
                    AddHistoryEntry(deviceName, ConnectionEventType.Connected);
                else if (state == AudioPlaybackConnectionState.Closed && previousState == AudioPlaybackConnectionState.Opened)
                    AddHistoryEntry(deviceName, ConnectionEventType.Disconnected);
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
