using Microsoft.Extensions.Logging;
using System.Windows;
using AkiLink.Models;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AkiLink.Services;

public sealed class BluetoothAudioService : IBluetoothAudioService
{
    // ───────────────────────────── Events ─────────────────────────────

    public event Action<IReadOnlyList<DeviceInformation>>? DevicesUpdated;
    public event Action<AudioPlaybackConnectionState>? StateChanged;
    public event Action<string>? ErrorOccurred;
    public event Action<string>? LogMessage;
    public event Action<ConnectionQuality>? QualityUpdated;

    // ────────────────────────── Properties ────────────────────────────

    public AudioPlaybackConnectionState CurrentState => _currentState;

    public bool IsConnected => _currentState == AudioPlaybackConnectionState.Opened;

    public AudioCodecSettings? CodecPreferences { get; private set; }

    public ConnectionQuality CurrentQuality => _currentQuality;
    private ConnectionQuality _currentQuality = ConnectionQuality.Disconnected;

    // ─────────────────────────── Fields ───────────────────────────────

    private AudioPlaybackConnection? _activeConnection;
    private AudioPlaybackConnectionState _currentState = AudioPlaybackConnectionState.Closed;

    private readonly object _lock = new();

    private CancellationTokenSource? _autoReconnectCts;
    private string? _autoReconnectDeviceId;
    private Task? _pendingDisposeTask;
    private bool _disposed;

    private readonly ILogger<BluetoothAudioService> _logger;
    private readonly IBluetoothPlatform _platform;

    private readonly Random _rng = new();

    // ───────────────────────── Constructor ────────────────────────────

    public BluetoothAudioService(ILogger<BluetoothAudioService> logger, IBluetoothPlatform platform)
    {
        _logger = logger;
        _platform = platform;
    }

    // ─────────────────────── ScanDevicesAsync ─────────────────────────

    public async Task<IReadOnlyList<DeviceInformation>> ScanDevicesAsync()
    {
        try
        {
            var selector = _platform.GetDeviceSelector();
            FireLog($"Scanning for audio playback devices with AQS: {selector}");

            var devices = await _platform.FindAllAudioDevicesAsync(selector);

            var list = devices is not null
                ? (IReadOnlyList<DeviceInformation>)devices
                : Array.Empty<DeviceInformation>();
            FireLog($"Found {list.Count} audio playback device(s).");

            FireOnUiThread(() => DevicesUpdated?.Invoke(list));

            return list;
        }
        catch (Exception ex)
        {
            FireError($"Device scan failed: {ex.Message}");
            FireLog($"ScanDevicesAsync threw: {ex}");
            return Array.Empty<DeviceInformation>();
        }
    }

    // ───────────────────────── ConnectAsync ───────────────────────────

    public async Task<bool> ConnectAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            FireError("Cannot connect: service has been disposed.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            FireError("Device ID cannot be null or empty.");
            return false;
        }

        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                FireLog("ConnectAsync cancelled.");
                return false;
            }

            // Only tear down the previous connection on the first attempt;
            // subsequent attempts start from a clean slate.
            if (attempt == 1)
            {
                DisconnectInternal();
                // Wait for the previous connection to be fully released before
                // creating a new one. Overlapping AudioPlaybackConnection
                // objects for the same device confuse the Bluetooth stack and
                // trigger reconnect loops.
                await WaitForPendingDisposeAsync();
            }

            AudioPlaybackConnection? connection;

            try
            {
                // TryCreateFromId must be called from an MTA thread to avoid
                // known AccessViolationException crashes in the WinRT interop
                // when called from the WPF STA thread.
                connection = await Task.Run(() => _platform.TryCreateAudioPlaybackConnection(deviceId));
            }
            catch (Exception ex)
            {
                FireError($"Failed to create audio connection: {ex.Message}");
                FireLog($"TryCreateFromId threw: {ex}");
                if (attempt < maxRetries)
                {
                    FireLog($"ConnectAsync attempt {attempt}/{maxRetries} — will retry after TryCreateFromId failure.");
                    try { await Task.Delay(1000, cancellationToken); } catch (OperationCanceledException) { return false; }
                    continue;
                }
                return false;
            }

            if (connection is null)
            {
                FireError("Device not available. Check Bluetooth driver and device pairing.");
                if (attempt < maxRetries)
                {
                    FireLog($"ConnectAsync attempt {attempt}/{maxRetries} — will retry after null connection.");
                    try { await Task.Delay(1000, cancellationToken); } catch (OperationCanceledException) { return false; }
                    continue;
                }
                return false;
            }

            // Subscribe before opening so we never miss a transition.
            connection.StateChanged += OnConnectionStateChanged;

            // StartAsync configures the system to receive audio input.
            // Must be awaited before OpenAsync per WinRT docs.
            try
            {
                await connection.StartAsync();
            }
            catch (Exception ex)
            {
                // Non-fatal: proceed with OpenAsync even if StartAsync fails.
                FireLog($"AudioPlaybackConnection.StartAsync failed (non-fatal): {ex.Message}");
            }

            AudioPlaybackConnectionOpenResult result;
            try
            {
                result = await connection.OpenAsync();
            }
            catch (Exception ex)
            {
                connection.StateChanged -= OnConnectionStateChanged;
                connection.Dispose();
                FireError($"Failed to open audio connection: {ex.Message}");
                FireLog($"OpenAsync threw: {ex}");
                if (attempt < maxRetries)
                {
                    FireLog($"ConnectAsync attempt {attempt}/{maxRetries} — will retry after OpenAsync exception.");
                    try { await Task.Delay(1000, cancellationToken); } catch (OperationCanceledException) { return false; }
                    continue;
                }
                return false;
            }

            if (result.Status != AudioPlaybackConnectionOpenResultStatus.Success)
            {
                connection.StateChanged -= OnConnectionStateChanged;
                connection.Dispose();

                var message = result.Status switch
                {
                    AudioPlaybackConnectionOpenResultStatus.DeniedBySystem
                        => "Connection request denied by the system.",
                    AudioPlaybackConnectionOpenResultStatus.RequestTimedOut
                        => "Connection request timed out.",
                    AudioPlaybackConnectionOpenResultStatus.UnknownFailure
                        => "Failed to open audio connection. Unknown error.",
                    _ => $"Failed to open audio connection. Unexpected status: {result.Status}.",
                };

                FireError(message);

                // Retry only on transient statuses; DeniedBySystem is permanent.
                if (attempt < maxRetries
                    && (result.Status == AudioPlaybackConnectionOpenResultStatus.RequestTimedOut
                        || result.Status == AudioPlaybackConnectionOpenResultStatus.UnknownFailure))
                {
                    FireLog($"ConnectAsync attempt {attempt}/{maxRetries} — will retry after transient status: {result.Status}.");
                    try { await Task.Delay(1000, cancellationToken); } catch (OperationCanceledException) { return false; }
                    continue;
                }
                return false;
            }

            // Success — assign connection and exit.
            lock (_lock)
            {
                _activeConnection = connection;
                SetState(AudioPlaybackConnectionState.Opened);
            }

            RefreshQuality();
            FireLog("Audio connection established successfully.");
            return true;
        }

        // Unreachable: every path in the loop either returns or continues.
        return false;
    }

    // ────────────────────────── Disconnect ────────────────────────────

    public void Disconnect()
    {
        DisconnectInternal();
        SetState(AudioPlaybackConnectionState.Closed);
    }

    // ────────────────────── StartAutoReconnectAsync ───────────────────

    public async Task StartAutoReconnectAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            FireError("Device ID cannot be null or empty for auto-reconnect.");
            return;
        }

        StopAutoReconnect();

        _autoReconnectDeviceId = deviceId;
        _autoReconnectCts = new CancellationTokenSource();
        var token = _autoReconnectCts.Token;

        FireLog($"Starting auto-reconnect monitoring for device: {deviceId}");

        await Task.Run(async () =>
        {
            var consecutiveFailures = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Skip scanning if we're already connected to this device.
                    bool alreadyConnected;
                    lock (_lock)
                    {
                        alreadyConnected = _activeConnection is not null
                            && _currentState == AudioPlaybackConnectionState.Opened;
                    }

                    if (!alreadyConnected)
                    {
                        var selector = _platform.GetDeviceSelector();
                        var devices = await _platform.FindAllAudioDevicesAsync(selector);

                        if (devices.Any(d => d.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase)))
                        {
                            FireLog("Auto-reconnect: target device detected, attempting connection…");
                            var connected = await ConnectAsync(deviceId, token);
                            consecutiveFailures = connected ? 0 : consecutiveFailures + 1;
                        }
                        else
                        {
                            consecutiveFailures = 0;
                        }
                    }
                    else
                    {
                        consecutiveFailures = 0;
                    }

                    // Exponential backoff (5s → 10s → 20s → 40s → 60s cap) so a
                    // detected-but-unconnectable device is not hammered: repeated
                    // StartAsync calls are a known Windows 11 fastfail crash.
                    var delayMs = (int)Math.Min(5000 * Math.Pow(2, consecutiveFailures), 60000);
                    await Task.Delay(delayMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    FireLog($"Auto-reconnect loop swallowed: {ex.Message}");
                    consecutiveFailures++;
                    // Use CancellationToken.None so we don't skip the delay
                    // when cancellation is requested during error recovery.
                    await Task.Delay(5000, CancellationToken.None);
                }
            }
        }, token);
    }

    // ─────────────────────── StopAutoReconnect ────────────────────────

    public void StopAutoReconnect()
    {
        _autoReconnectCts?.Cancel();
        _autoReconnectCts?.Dispose();
        _autoReconnectCts = null;
        _autoReconnectDeviceId = null;
        FireLog("Auto-reconnect stopped.");
    }

    // ─────────────────────── ConfigureCodec ──────────────────────────

    public void ConfigureCodec(AudioCodecSettings settings)
    {
        CodecPreferences = settings;
        FireLog($"Codec preferences updated: {settings.Codec}, {settings.Bitrate}, {settings.SampleRate} Hz, {settings.TransmissionMode}");

        // If currently connected, refresh quality with new configured values.
        if (IsConnected)
        {
            RefreshQuality();
        }
    }

    // ─────────────────────── Quality Tracking ─────────────────────────

    /// <summary>
    /// Refresh connection quality metrics.
    /// Called after connection state changes and periodically.
    /// Signal strength estimation uses a heuristic based on connection
    /// health since WinRT does not expose RSSI directly on
    /// AudioPlaybackConnection.
    /// </summary>
    private void RefreshQuality()
    {
        if (!IsConnected)
        {
            _currentQuality = ConnectionQuality.Disconnected;
            FireOnUiThread(() => QualityUpdated?.Invoke(_currentQuality));
            return;
        }

        // Map the preferred codec label for display.
        var codecLabel = CodecPreferences?.Codec switch
        {
            PreferredCodec.SBC => "SBC",
            PreferredCodec.AAC => "AAC",
            PreferredCodec.AptX => "aptX",
            PreferredCodec.LDAC => "LDAC",
            _ => "Auto"
        };

        var bitrateLabel = CodecPreferences?.Bitrate ?? "Auto";
        var sampleRateLabel = CodecPreferences?.SampleRate is int sr
            ? $"{sr / 1000.0:F1} kHz"
            : "—";

        // Signal strength: WinRT AudioPlaybackConnection does not expose RSSI,
        // so we leave SignalStrength null (signal bars in status bar simply
        // indicate connection state, not actual signal quality).

        // Latency: estimated from transmission mode preference, not measured.
        var latency = CodecPreferences?.TransmissionMode switch
        {
            TransmissionMode.LowLatency => "~40 ms (est.)",
            TransmissionMode.BestQuality => "~150 ms (est.)",
            _ => "~80 ms (est.)"
        };

        var quality = new ConnectionQuality
        {
            CodecInUse = codecLabel,
            Latency = latency,
            Bitrate = bitrateLabel,
            SampleRate = sampleRateLabel
        };

        _currentQuality = quality;
        FireOnUiThread(() => QualityUpdated?.Invoke(quality));
    }

    // ────────────────────────── Dispose ───────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAutoReconnect();

        // Block until in-flight teardown completes so the WinRT connection is
        // actually released before the process exits. Without this, the OS can
        // keep the A2DP audio link half-open, causing reconnect loops and
        // device corruption (device renamed / constant connect-disconnect).
        DisconnectInternal();
        WaitForPendingDisposeShutdown();
    }

    /// <summary>
    /// Synchronously waits (bounded) for the pending connection teardown so the
    /// process does not exit with a live WinRT connection. Called from the UI
    /// thread during application shutdown; the teardown itself runs on a
    /// threadpool thread, so this cannot deadlock the dispatcher.
    /// </summary>
    private void WaitForPendingDisposeShutdown()
    {
        Task? pending;
        lock (_lock)
        {
            pending = _pendingDisposeTask;
        }

        if (pending is null) return;

        try
        {
            if (!pending.Wait(TimeSpan.FromSeconds(5)))
            {
                FireLog("Timed out waiting for connection teardown during shutdown.");
            }
        }
        catch { /* best-effort — process may be shutting down */ }
    }

    // ───────────────────── Private helpers ────────────────────────────

    /// <summary>
    /// Tears down the active connection without firing the Closed event
    /// (the caller owns the state transition).  Safe to call multiple times.
    /// The teardown runs on a background thread and is tracked so a
    /// subsequent ConnectAsync can wait for it to fully complete.
    /// </summary>
    private void DisconnectInternal()
    {
        AudioPlaybackConnection? connection;

        lock (_lock)
        {
            connection = _activeConnection;
            _activeConnection = null;
        }

        if (connection is not null)
        {
            connection.StateChanged -= OnConnectionStateChanged;
            TrackDispose(connection);
            FireLog("Active audio connection torn down.");
        }
    }

    /// <summary>
    /// Disposes a connection on a background (MTA) thread and records the
    /// operation so callers can await its completion. AudioPlaybackConnection
    /// is created on the MTA via Task.Run in ConnectAsync; disposing on the
    /// WPF STA thread during shutdown can leave the Bluetooth adapter in an
    /// inconsistent state (intermittent connectivity until reboot).
    /// </summary>
    private void TrackDispose(AudioPlaybackConnection connection)
    {
        var disposeTask = Task.Run(() =>
        {
            try { connection.Dispose(); }
            catch { /* best-effort — process may be shutting down */ }
        });

        lock (_lock)
        {
            _pendingDisposeTask = disposeTask;
        }
    }

    /// <summary>
    /// Waits for any in-flight connection teardown to complete so a new
    /// connection for the same device is never created while the previous
    /// one is still alive on the Bluetooth stack. Bounded by a timeout so a
    /// hung dispose cannot stall a reconnect forever.
    /// </summary>
    private async Task WaitForPendingDisposeAsync()
    {
        Task? pending;
        lock (_lock)
        {
            pending = _pendingDisposeTask;
        }

        if (pending is null) return;

        try
        {
            var completed = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5)));
            if (!ReferenceEquals(completed, pending))
            {
                FireLog("Timed out waiting for connection teardown before reconnect.");
            }
        }
        catch { /* best-effort */ }

        lock (_lock)
        {
            if (ReferenceEquals(_pendingDisposeTask, pending))
                _pendingDisposeTask = null;
        }
    }

    private void OnConnectionStateChanged(AudioPlaybackConnection sender, object args)
    {
        try
        {
            var newState = sender.State;
            FireLog($"Connection state changed to: {newState}");

            var droppedConnection = false;

            lock (_lock)
            {
                // If the connection dropped unexpectedly, clean up our reference.
                if (newState == AudioPlaybackConnectionState.Closed
                    && ReferenceEquals(_activeConnection, sender))
                {
                    _activeConnection!.StateChanged -= OnConnectionStateChanged;
                    _activeConnection = null;
                    droppedConnection = true;
                }

                SetState(newState);
            }

            // Dispose the dropped connection off the event thread through the
            // tracked teardown path so a subsequent ConnectAsync waits for it.
            if (droppedConnection)
            {
                TrackDispose(sender);
            }
        }
        catch (Exception ex)
        {
            // The connection may have been disposed out from under us mid-event.
            // Never let an exception escape the WinRT event handler.
            FireLog($"OnConnectionStateChanged swallowed: {ex.Message}");
        }
    }

    private void SetState(AudioPlaybackConnectionState state)
    {
        if (_currentState == state) return;
        _currentState = state;
        FireOnUiThread(() => StateChanged?.Invoke(state));
        RefreshQuality();
    }

    private void FireOnUiThread(Action action)
    {
        try
        {
            if (Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
            {
                // Use BeginInvoke (fire-and-forget) instead of Invoke to avoid
                // nested dispatcher frames and potential deadlocks when called
                // from a lock-holding thread or from the UI thread itself.
                dispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch
        {
            try { action(); } catch { /* best effort */ }
        }
    }

    private void FireError(string message)
    {
        _logger.LogError("{AkiLink} {Message}", "AkiLink", message);
        FireOnUiThread(() => ErrorOccurred?.Invoke(message));
        FireLog(message);
    }

    private void FireLog(string message)
    {
        _logger.LogInformation("{AkiLink} {Message}", "AkiLink", message);
        FireOnUiThread(() => LogMessage?.Invoke(message));
    }
}
