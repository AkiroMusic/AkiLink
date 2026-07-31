using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Windows;

namespace AkiLink.Services;

    /// <summary>
    /// Controls system audio endpoint volume and mute state via the Windows CoreAudio API
    /// (IMMDeviceEnumerator / IAudioEndpointVolume COM interfaces).
    /// All COM operations are wrapped in try/catch to gracefully degrade when unavailable.
    /// </summary>
    public sealed class AudioVolumeService : IAudioVolumeService
    {
        // ────────────────────────────── COM Constants ──────────────────────────────

        private const int CLSCTX_ALL = 0x17;
        private const int E_RENDER = 0;
        private const int E_CONSOLE = 0;
        private const int S_OK = 0;
        private const float DefaultVolume = 0.75f;

    /// <summary>
    /// Event context GUID passed on every Set* call we make. Windows echoes it back in
    /// AUDIO_VOLUME_NOTIFICATION_DATA.guidEventContext, letting us distinguish changes
    /// WE caused from changes made elsewhere (volume keys, other apps).
    /// Without this, our own SetVolume/SetMute calls trigger OnNotify, whose async
    /// callback re-writes the slider with a stale/intermediate value and fights the
    /// user's drag (slider appears to "not respond").
    /// </summary>
    private static readonly Guid AppEventContext = new("A41B6E8C-2D3F-4A5B-9C7D-8E1F0A2B3C4D");

    private static readonly Guid IID_IAudioEndpointVolume =
        new("5CDF2C82-841E-4546-9722-0CF74078229A");

    // ────────────────────────────── COM Types ──────────────────────────────

    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    // NOTE: All COM interfaces must use [ComImport] + [InterfaceType(InterfaceIsIUnknown)] +
    // [PreserveSig]. Without [ComImport], the runtime treats the interface as InterfaceIsDual
    // and builds the CCW (callback) vtable from the managed class method table instead of the
    // interface declaration order — audioses.dll then dispatches to wrong/null vtable slots,
    // crashing natively AFTER OnNotify returns (dotnet/runtime#127512 bug family).
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
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IAudioEndpointVolume endpointVolume);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        // Methods MUST be declared in native vtable order (slots 3+).
        // Declaration order == vtable slot, so any shuffle dispatches to the WRONG native method.
        [PreserveSig]
        int RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);     // slot 3
        [PreserveSig]
        int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);   // slot 4
        [PreserveSig]
        int GetChannelCount(out uint channelCount);                                 // slot 5
        [PreserveSig]
        int SetMasterVolumeLevel(float levelDB, in Guid eventContext);              // slot 6
        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, in Guid eventContext);          // slot 7
        [PreserveSig]
        int GetMasterVolumeLevel(out float levelDB);                                // slot 8
        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);                            // slot 9
        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float levelDB, in Guid eventContext);     // slot 10
        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, in Guid eventContext); // slot 11
        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float levelDB);                 // slot 12
        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);             // slot 13
        [PreserveSig]
        int SetMute(int muted, in Guid eventContext);                               // slot 14
        [PreserveSig]
        int GetMute(out int muted);                                                 // slot 15
        [PreserveSig]
        int SetVolumeStep(uint stepDirection);                                      // slot 16
        [PreserveSig]
        int GetVolumeStepInfo(out uint step, out uint stepCount);                   // slot 17
        [PreserveSig]
        int QueryHardwareSupport(out uint hardwareSupportMask);                     // slot 18
        [PreserveSig]
        int GetVolumeRange(out float minDB, out float maxDB, out float incrementDB); // slot 19
    }

    [Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig]
        int OnNotify(IntPtr notifyData);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        public int bMuted;          // BOOL is 4 bytes
        public float fMasterVolume;
        public uint cbChannels;
        public float afChannelVolumes;
    }

    // ────────────────────────────── Callback Implementation ──────────────────────────────

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class AudioEndpointVolumeCallbackImpl : IAudioEndpointVolumeCallback
    {
        private Action<float, bool, Guid>? _onNotification;

        public AudioEndpointVolumeCallbackImpl(Action<float, bool, Guid> onNotification)
        {
            _onNotification = onNotification;
        }

        public void ReleaseHandler()
        {
            _onNotification = null;
        }

        public int OnNotify(IntPtr notifyData)
        {
            var handler = _onNotification;
            if (handler == null)
                return S_OK;

            try
            {
                var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(notifyData);
                handler(data.fMasterVolume, data.bMuted != 0, data.guidEventContext);
            }
            catch
            {
                // Never let exceptions propagate across the COM boundary
            }

            return S_OK;
        }
    }

    // ────────────────────────────── State ──────────────────────────────

    private readonly ILogger<AudioVolumeService>? _logger;

    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMDevice? _device;
    private IAudioEndpointVolume? _endpointVolume;
    private AudioEndpointVolumeCallbackImpl? _callback;
    private bool _disposed;
    private bool _initialized;

    public AudioVolumeService(ILogger<AudioVolumeService>? logger = null)
    {
        _logger = logger;
    }

    public event Action<float>? VolumeChanged;
    public event Action<bool>? MuteChanged;

    public float Volume
    {
        get => GetVolume();
        set => SetVolume(value);
    }

    public bool IsMuted
    {
        get => GetMute();
        set => SetMute(value);
    }

    // ────────────────────────────── Initialization ──────────────────────────────

    public void Initialize()
    {
        if (_initialized)
            return;

        try
        {
            // 1. Create MMDeviceEnumerator via COM activation
            var clsid = typeof(MMDeviceEnumerator).GUID;
            var comType = Type.GetTypeFromCLSID(clsid);
            if (comType == null)
            {
                _logger?.LogWarning("[AkiLink] AudioVolumeService: Failed to resolve MMDeviceEnumerator COM class");
                return;
            }
            _deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(comType)!;

            // 2. Get the default audio render endpoint (speakers/headphones)
            var hr = _deviceEnumerator.GetDefaultAudioEndpoint(E_RENDER, E_CONSOLE, out _device);
            if (hr < 0 || _device == null)
            {
                _logger?.LogWarning($"[AkiLink] AudioVolumeService: GetDefaultAudioEndpoint failed (hr=0x{hr:X8})");
                CleanupCom();
                return;
            }

            // 3. Activate the IAudioEndpointVolume interface on the device
            var iid = IID_IAudioEndpointVolume; // local copy to avoid CS0199
            hr = _device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out _endpointVolume);
            if (hr < 0 || _endpointVolume == null)
            {
                _logger?.LogWarning($"[AkiLink] AudioVolumeService: Activate failed (hr=0x{hr:X8})");
                CleanupCom();
                return;
            }

            // 4. Subscribe to volume-change notifications
            _callback = new AudioEndpointVolumeCallbackImpl(OnVolumeNotification);
            hr = _endpointVolume!.RegisterControlChangeNotify(_callback);
            if (hr < 0)
            {
                _logger?.LogWarning($"[AkiLink] AudioVolumeService: RegisterControlChangeNotify failed (hr=0x{hr:X8})");
            }

            _initialized = true;
            _logger?.LogInformation("[AkiLink] AudioVolumeService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[AkiLink] AudioVolumeService initialization failed: {Message}", ex.Message);
            CleanupCom();
        }
    }

    // ────────────────────────────── Private Helpers ──────────────────────────────

    private float GetVolume()
    {
        if (!_initialized || _endpointVolume == null)
            return DefaultVolume;
        try
        {
            var hr = _endpointVolume.GetMasterVolumeLevelScalar(out var level);
            return hr >= 0 ? level : DefaultVolume;
        }
        catch
        {
            return DefaultVolume;
        }
    }

    private void SetVolume(float level)
    {
        if (!_initialized || _endpointVolume == null)
            return;
        try
        {
            level = Math.Clamp(level, 0f, 1f);
            var ctx = AppEventContext; // local copy: `in` can't reference a static field safely
            _endpointVolume.SetMasterVolumeLevelScalar(level, in ctx);
        }
        catch
        {
            // Silently fail — volume control is non-critical
        }
    }

    private bool GetMute()
    {
        if (!_initialized || _endpointVolume == null)
            return false;
        try
        {
            var hr = _endpointVolume.GetMute(out var muted);
            return hr >= 0 && muted != 0;
        }
        catch
        {
            return false;
        }
    }

    private void SetMute(bool muted)
    {
        if (!_initialized || _endpointVolume == null)
            return;
        try
        {
            // BOOL is a 4-byte int in the native API; passing `muted ? 1 : 0`
            // avoids any ambiguity in the interop layer.
            var ctx = AppEventContext; // local copy: `in` can't reference a static field safely
            _endpointVolume.SetMute(muted ? 1 : 0, in ctx);
        }
        catch
        {
            // Silently fail — mute is non-critical
        }
    }

    private void OnVolumeNotification(float volume, bool muted, Guid eventContext)
    {
        // Skip notifications caused by OUR OWN SetVolume/SetMute calls. The eventContext
        // we pass on every Set* call is echoed back by Windows in the notification data,
        // so we can tell our own changes apart from external ones (volume keys, other apps).
        // Without this filter the async callback re-writes Volume with a stale/intermediate
        // value while the user is dragging the slider — the slider appears unresponsive.
        if (eventContext == AppEventContext)
            return;

        // Dispatch to the WPF UI thread so consumers can update bindings safely
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.BeginInvoke(() => OnVolumeNotification(volume, muted, eventContext));
                return;
            }
            catch
            {
                // Application may be shutting down – fall through to fire inline
            }
        }

        VolumeChanged?.Invoke(volume);
        MuteChanged?.Invoke(muted);
    }

    // ────────────────────────────── Cleanup / IDisposable ──────────────────────────────

    private void CleanupCom()
    {
        _callback?.ReleaseHandler();

        if (_endpointVolume != null)
        {
            try { Marshal.ReleaseComObject(_endpointVolume); } catch { }
            _endpointVolume = null;
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
        _callback = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unregister the callback before releasing COM
        if (_endpointVolume != null && _callback != null)
        {
            try { _endpointVolume.UnregisterControlChangeNotify(_callback); } catch { }
        }

        CleanupCom();
    }
}
