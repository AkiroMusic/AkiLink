using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace AkiLink.Services;

/// <summary>
/// Samples the real-time peak level of the default render endpoint via the
/// Windows CoreAudio IAudioMeterInformation COM interface and raises
/// <see cref="LevelChanged"/> periodically so the UI can render a live VU meter.
///
/// Why IAudioMeterInformation instead of WASAPI loopback capture: the meter
/// interface already computes the instantaneous peak (0–1) of the render
/// stream, so no buffer management, WAVEFORMATEX parsing, or audio client
/// lifecycle is needed — ~100 lines of COM vs a full loopback pipeline. It
/// meters exactly what the user hears through the default playback device,
/// which for a receiver is the audio flowing from the connected Bluetooth
/// device.
/// </summary>
public sealed class AudioLevelMeterService : IAudioLevelMeterService
{
    // ────────────────────────────── COM Constants ──────────────────────────────

    private const int CLSCTX_ALL = 0x17;
    private const int E_RENDER = 0;
    private const int E_CONSOLE = 0;

    /// <summary>IAudioMeterInformation interface GUID.</summary>
    private static readonly Guid IID_IAudioMeterInformation =
        new("C02216F6-8C67-4B5B-9D00-D008E73E0064");

    // ────────────────────────────── Smoothing ──────────────────────────────
    // Attack is instant (peaks jump immediately), release decays exponentially
    // so the bar falls smoothly instead of strobing at the sample rate.

    /// <summary>Exponential release multiplier applied per tick (~30 ms).</summary>
    internal const float ReleasePerTick = 0.82f;

    /// <summary>
    /// Computes the next smoothed display level. Pure function (no COM/state
    /// dependencies) so the smoothing behavior is unit-testable.
    /// </summary>
    /// <param name="current">Previously displayed level in [0, 1].</param>
    /// <param name="raw">New raw sample from the meter in [0, 1].</param>
    public static float NextLevel(float current, float raw)
    {
        if (raw >= current) return raw;                      // instant attack
        return Math.Max(raw, current * ReleasePerTick);      // exponential release
    }

    // ────────────────────────────── COM Types ──────────────────────────────
    // NOTE: identical interop conventions as AudioVolumeService — [ComImport] +
    // [InterfaceType(InterfaceIsIUnknown)] + [PreserveSig], methods declared in
    // native vtable order (slot 3+). Declaration order == vtable slot.

    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDevice devices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IAudioMeterInformation meter);
    }

    // IAudioMeterInformation vtable (slots 3+):
    //   3 GetPeakValue          — instantaneous peak of the render stream, 0–1
    //   4 GetMeteringChannelCount
    //   5 GetChannelsPeakValues
    //   6 QueryHardwareSupport
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig]
        int GetPeakValue(out float pfPeak);                            // slot 3
        [PreserveSig]
        int GetMeteringChannelCount(out uint pnChannelCount);           // slot 4
        [PreserveSig]
        int GetChannelsPeakValues(uint u32ChannelCount, IntPtr afPeakValues); // slot 5
        [PreserveSig]
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);      // slot 6
    }

    // ────────────────────────────── State ──────────────────────────────

    private readonly ILogger<AudioLevelMeterService>? _logger;

    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMDevice? _device;
    private IAudioMeterInformation? _meter;
    private DispatcherTimer? _timer;
    private float _currentLevel;
    private bool _initialized;
    private bool _disposed;

    public AudioLevelMeterService(ILogger<AudioLevelMeterService>? logger = null)
    {
        _logger = logger;
    }

    public event Action<float>? LevelChanged;

    // ────────────────────────────── Initialization ──────────────────────────────

    /// <summary>
    /// Resolves the default render endpoint and activates IAudioMeterInformation.
    /// Must be called from the UI thread (the COM activation and the polling
    /// timer both live on the dispatcher). No-op after first success.
    /// </summary>
    public void Initialize()
    {
        if (_initialized || _disposed) return;

        try
        {
            var clsid = typeof(MMDeviceEnumerator).GUID;
            var comType = Type.GetTypeFromCLSID(clsid);
            if (comType == null)
            {
                _logger?.LogWarning("[AkiLink] AudioLevelMeter: failed to resolve MMDeviceEnumerator COM class");
                return;
            }
            _deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(comType)!;

            var hr = _deviceEnumerator.GetDefaultAudioEndpoint(E_RENDER, E_CONSOLE, out _device);
            if (hr < 0 || _device == null)
            {
                _logger?.LogWarning($"[AkiLink] AudioLevelMeter: GetDefaultAudioEndpoint failed (hr=0x{hr:X8})");
                CleanupCom();
                return;
            }

            var iid = IID_IAudioMeterInformation;
            hr = _device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out _meter);
            if (hr < 0 || _meter == null)
            {
                _logger?.LogWarning($"[AkiLink] AudioLevelMeter: Activate failed (hr=0x{hr:X8})");
                CleanupCom();
                return;
            }

            _initialized = true;
            _logger?.LogInformation("[AkiLink] AudioLevelMeter initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[AkiLink] AudioLevelMeter initialization failed: {Message}", ex.Message);
            CleanupCom();
        }
    }

    // ────────────────────────────── Start / Stop ──────────────────────────────

    /// <summary>
    /// Starts polling the meter on the UI thread at ~33 Hz. Initializes first if
    /// needed. If the meter could not be initialized, runs silently with zero
    /// output (the UI simply shows an idle bar).
    /// </summary>
    public void Start()
    {
        if (_disposed) return;

        Initialize();

        if (_timer != null || !_initialized) return;

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(30),
            DispatcherPriority.Background,
            OnTick,
            System.Windows.Application.Current?.Dispatcher
                ?? System.Windows.Threading.Dispatcher.CurrentDispatcher);
        _timer.Start();
        _logger?.LogInformation("[AkiLink] AudioLevelMeter started");
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
        _currentLevel = 0f;
        // Fire a final zero so the UI settles to idle.
        LevelChanged?.Invoke(0f);
        _logger?.LogInformation("[AkiLink] AudioLevelMeter stopped");
    }

    // ────────────────────────────── Polling ──────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_meter == null) return;

        float raw;
        try
        {
            var hr = _meter.GetPeakValue(out raw);
            if (hr < 0) return;
        }
        catch
        {
            return; // Silently skip transient COM failures
        }

        if (raw < 0f) raw = 0f;
        if (raw > 1f) raw = 1f;

        _currentLevel = NextLevel(_currentLevel, raw);
        LevelChanged?.Invoke(_currentLevel);
    }

    // ────────────────────────────── Cleanup / IDisposable ──────────────────────────────

    private void CleanupCom()
    {
        if (_meter != null)
        {
            try { Marshal.ReleaseComObject(_meter); } catch { }
            _meter = null;
        }
        if (_device != null)
        {
            try { Marshal.ReleaseComObject(_device); } catch { }
            _device = null;
        }
        if (_deviceEnumerator != null)
        {
            try { Marshal.ReleaseComObject(_deviceEnumerator); } catch { }
            _deviceEnumerator = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
        CleanupCom();
    }
}
