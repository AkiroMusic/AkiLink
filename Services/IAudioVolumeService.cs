namespace AkiLink.Services;

public interface IAudioVolumeService : IDisposable
{
    event Action<float>? VolumeChanged;
    event Action<bool>? MuteChanged;

    float Volume { get; set; }
    bool IsMuted { get; set; }
    void Initialize();
}
