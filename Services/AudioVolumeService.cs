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

    private static readonly Guid IID_IAudioEndpointVolume =
        new("5CDF2C82-841E-4546-9722-0CF74078229A");

    // ────────────────────────────── COM Types ──────────────────────────────

    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDevice devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IAudioEndpointVolume endpointVolume);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        int GetMasterVolumeLevel(out float levelDB);
        int SetMasterVolumeLevel(float levelDB, in Guid eventContext);
        int GetMasterVolumeLevelScalar(out float level);
        int SetMasterVolumeLevelScalar(float level, in Guid eventContext);
        int GetMute(out bool muted);
        int SetMute(bool muted, in Guid eventContext);
        int GetVolumeRange(out float min, out float max, out float step);
    }

    [Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
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
        private Action<float, bool>? _onNotification;

        public AudioEndpointVolumeCallbackImpl(Action<float, bool> onNotification)
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
                handler(data.fMasterVolume, data.bMuted != 0);
            }
            catch
            {
                // Never let exceptions propagate across the COM boundary
            }

            return S_OK;
        }
    }

    // ────────────────────────────── State ──────────────────────────────

    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMDevice? _device;
    private IAudioEndpointVolume? _endpointVolume;
    private AudioEndpointVolumeCallbackImpl? _callback;
    private bool _disposed;
    private bool _initialized;

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
                System.Diagnostics.Debug.WriteLine("[AkiLink] AudioVolumeService: Failed to resolve MMDeviceEnumerator COM class");
                return;
            }
            _deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(comType)!;

            // 2. Get the default audio render endpoint (speakers/headphones)
            var hr = _deviceEnumerator.GetDefaultAudioEndpoint(E_RENDER, E_CONSOLE, out _device);
            if (hr < 0 || _device == null)
            {
                System.Diagnostics.Debug.WriteLine($"[AkiLink] AudioVolumeService: GetDefaultAudioEndpoint failed (hr=0x{hr:X8})");
                return;
            }

            // 3. Activate the IAudioEndpointVolume interface on the device
            var iid = IID_IAudioEndpointVolume; // local copy to avoid CS0199
            hr = _device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out _endpointVolume);
            if (hr < 0 || _endpointVolume == null)
            {
                System.Diagnostics.Debug.WriteLine($"[AkiLink] AudioVolumeService: Activate failed (hr=0x{hr:X8})");
                return;
            }

            // 4. Subscribe to volume-change notifications
            _callback = new AudioEndpointVolumeCallbackImpl(OnVolumeNotification);
            hr = _endpointVolume!.RegisterControlChangeNotify(_callback);
            if (hr < 0)
            {
                System.Diagnostics.Debug.WriteLine($"[AkiLink] AudioVolumeService: RegisterControlChangeNotify failed (hr=0x{hr:X8})");
            }

            _initialized = true;
            System.Diagnostics.Debug.WriteLine("[AkiLink] AudioVolumeService initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AkiLink] AudioVolumeService initialization failed: {ex.Message}");
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
            _endpointVolume.SetMasterVolumeLevelScalar(level, in Guid.Empty);
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
            return hr >= 0 && muted;
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
            _endpointVolume.SetMute(muted, in Guid.Empty);
        }
        catch
        {
            // Silently fail — mute is non-critical
        }
    }

    private void OnVolumeNotification(float volume, bool muted)
    {
        // Dispatch to the WPF UI thread so consumers can update bindings safely
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.Invoke(() => OnVolumeNotification(volume, muted));
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
