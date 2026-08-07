namespace AkiLink.Services;

/// <summary>
/// Exposes the real-time peak level (0–1) of the current default render
/// endpoint via Windows CoreAudio's IAudioMeterInformation interface.
/// Used to render a live VU meter proving that audio is actually flowing.
/// </summary>
public interface IAudioLevelMeterService : IDisposable
{
    /// <summary>
    /// Raised periodically (while running) with the smoothed peak level,
    /// a value in [0, 1]. Always raised on the UI thread so consumers can
    /// update bindings safely.
    /// </summary>
    event Action<float>? LevelChanged;

    /// <summary>
    /// Initializes the CoreAudio meter on the default render endpoint.
    /// Must be called from the UI thread. No-op if already initialized.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Starts polling the meter. Safe to call before Initialize (it will
    /// initialize first). No-op if already running.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops polling and fires a final zero level so the UI settles.
    /// </summary>
    void Stop();
}
