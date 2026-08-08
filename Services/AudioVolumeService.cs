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
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDevice devices);                       // slot 3
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);                        // slot 4
        [PreserveSig]
        int RegisterEndpointNotificationCallback(IMMNotificationClient client);                           // slot 5
        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IMMNotificationClient client);                         // slot 6
    }

    /// <summary>
    /// IMMNotificationClient — receives device-change callbacks from the MMDevice
    /// enumerator. We only act on OnDefaultDeviceChanged (render flow): when the
    /// user switches the default playback device (plugs in headphones, changes the
    /// output in Settings), the volume slider/mute must re-bind to the new endpoint.
    /// All other methods are required for vtable completeness and are no-ops.
    /// </summary>
    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig]
        int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);          // slot 3
        [PreserveSig]
        int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);                              // slot 4
        [PreserveSig]
        int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);                            // slot 5
        [PreserveSig]
        int OnDefaultDeviceChanged(int dataFlow, int deviceRole, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId); // slot 6
        [PreserveSig]
        int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr propertyKey); // slot 7
        [PreserveSig]
        int OnQueryRemove([MarshalAs(UnmanagedType.LPWStr)] string deviceId);                              // slot 8
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

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
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

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class DeviceNotificationClientImpl : IMMNotificationClient
    {
        private AudioVolumeService? _owner;

        public DeviceNotificationClientImpl(AudioVolumeService owner)
        {
            _owner = owner;
        }

        public void ReleaseHandler()
        {
            _owner = null;
        }

        // All methods MUST be declared in native vtable order (slots 3+);
        // only OnDefaultDeviceChanged is acted upon, the rest are required
        // for a complete vtable and are no-ops.

        public int OnDeviceStateChanged(string deviceId, int newState) => S_OK;

        public int OnDeviceAdded(string deviceId) => S_OK;

        public int OnDeviceRemoved(string deviceId) => S_OK;

        public int OnDefaultDeviceChanged(int dataFlow, int deviceRole, string defaultDeviceId)
        {
            // Only the render (playback) flow matters to us — capture changes
            // (microphones etc.) must not trigger a re-bind of the volume endpoint.
            if (dataFlow != E_RENDER)
                return S_OK;

            var owner = _owner;
            if (owner == null)
                return S_OK;

            try
            {
                owner.HandleDefaultDeviceChanged();
            }
            catch
            {
                // Never let exceptions cross the COM boundary
            }

            return S_OK;
        }

        public int OnPropertyValueChanged(string deviceId, IntPtr propertyKey) => S_OK;

        public int OnQueryRemove(string deviceId) => S_OK;
    }

    // ────────────────────────────── State ──────────────────────────────

    private readonly ILogger<AudioVolumeService>? _logger;

    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMDevice? _device;
    private IAudioEndpointVolume? _endpointVolume;
    private AudioEndpointVolumeCallbackImpl? _callback;
    private DeviceNotificationClientImpl? _deviceNotificationClient;
    private bool _disposed;
    private bool _initialized;

    // Cached last-published values so VolumeChanged/MuteChanged only fire when
    // the underlying value actually changes (avoid duplicate event storms from
    // repeated CoreAudio notifications with the same value).
    private float _lastPublishedVolume = float.NaN;
    private bool? _lastPublishedMuted;

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

            // 5. Subscribe to device-change notifications so we can re-bind when
            //    the user switches the default playback device (headphones plugged
            //    in, output changed in Settings) without restarting the app.
            _deviceNotificationClient = new DeviceNotificationClientImpl(this);
            hr = _deviceEnumerator.RegisterEndpointNotificationCallback(_deviceNotificationClient);
            if (hr < 0)
            {
                _logger?.LogWarning($"[AkiLink] AudioVolumeService: RegisterEndpointNotificationCallback failed (hr=0x{hr:X8})");
                _deviceNotificationClient.ReleaseHandler();
                _deviceNotificationClient = null;
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

        // #9: only raise an event when the value actually changed. CoreAudio can
        // deliver repeated notifications with the same volume/mute value (e.g.
        // Windows echoing state during a device switch); firing on every one of
        // them would cause a redundant binding-update storm in the UI.
        var volumeChanged = float.IsNaN(_lastPublishedVolume) || Math.Abs(volume - _lastPublishedVolume) > 0.0001f;
        if (volumeChanged)
        {
            _lastPublishedVolume = volume;
            VolumeChanged?.Invoke(volume);
        }

        var mutedChanged = _lastPublishedMuted is null || muted != _lastPublishedMuted;
        if (mutedChanged)
        {
            _lastPublishedMuted = muted;
            MuteChanged?.Invoke(muted);
        }
    }

    // ────────────────────────────── Default Device Change ──────────────────────────────

    private bool _handlingDeviceChange;

    /// <summary>
    /// Re-binds the volume endpoint when the OS default playback device changes
    /// (headphones plugged in, output switched in Windows Settings). Called from
    /// the COM notification thread; marshalled onto the UI thread because the
    /// COM objects were activated on the main STA thread and event consumers
    /// expect UI-thread notifications.
    /// </summary>
    private void HandleDefaultDeviceChanged()
    {
        if (!_initialized || _deviceEnumerator == null)
            return;

        if (_handlingDeviceChange)
            return; // re-entrancy guard: a burst of device-change events
        _handlingDeviceChange = true;
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                try
                {
                    dispatcher.BeginInvoke(HandleDefaultDeviceChanged);
                    return;
                }
                catch
                {
                    // App shutting down — fall through to run inline
                }
            }

            _logger?.LogInformation("[AkiLink] AudioVolumeService: default audio device changed, re-binding endpoint.");

            // Unregister the control-change notify on the OLD endpoint first so
            // we stop receiving stale notifications while swapping.
            if (_endpointVolume != null && _callback != null)
            {
                try { _endpointVolume.UnregisterControlChangeNotify(_callback); } catch { }
            }
            _callback?.ReleaseHandler();

            // Release the old endpoint + device
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

            // Re-acquire the default render endpoint and re-activate
            var hr = _deviceEnumerator.GetDefaultAudioEndpoint(E_RENDER, E_CONSOLE, out _device);
            if (hr < 0 || _device == null)
            {
                _logger?.LogWarning($"[AkiLink] AudioVolumeService: re-bind GetDefaultAudioEndpoint failed (hr=0x{hr:X8})");
                return;
            }

            var iid = IID_IAudioEndpointVolume; // local copy to avoid CS0199
            hr = _device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out _endpointVolume);
            if (hr < 0 || _endpointVolume == null)
            {
                _logger?.LogWarning($"[AkiLink] AudioVolumeService: re-bind Activate failed (hr=0x{hr:X8})");
                return;
            }

            // Re-subscribe on the new endpoint
            _callback = new AudioEndpointVolumeCallbackImpl(OnVolumeNotification);
            hr = _endpointVolume.RegisterControlChangeNotify(_callback);
            if (hr < 0)
            {
                _logger?.LogWarning($"[AkiLink] AudioVolumeService: re-bind RegisterControlChangeNotify failed (hr=0x{hr:X8})");
            }

            // Push fresh values so the UI reflects the new device's state
            VolumeChanged?.Invoke(GetVolume());
            MuteChanged?.Invoke(GetMute());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[AkiLink] AudioVolumeService: default device re-bind failed: {Message}", ex.Message);
        }
        finally
        {
            _handlingDeviceChange = false;
        }
    }

    // ────────────────────────────── Cleanup / IDisposable ──────────────────────────────

    private void CleanupCom()
    {
        _callback?.ReleaseHandler();

        if (_deviceNotificationClient != null)
        {
            try { _deviceEnumerator?.UnregisterEndpointNotificationCallback(_deviceNotificationClient); } catch { }
            _deviceNotificationClient.ReleaseHandler();
            _deviceNotificationClient = null;
        }

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
