using CommunityToolkit.Mvvm.ComponentModel;

namespace AkiLink.Models;

/// <summary>
/// Represents the Bluetooth audio codec selection.
/// The actual codec used at runtime depends on both the source device and receiver support.
/// </summary>
public enum PreferredCodec
{
    Auto,
    SBC,
    AAC,
    AptX,
    LDAC
}

/// <summary>
/// Transmission mode balances between audio quality and latency.
/// </summary>
public enum TransmissionMode
{
    Balanced,
    BestQuality,
    LowLatency
}

/// <summary>
/// Audio codec and quality settings for Bluetooth audio transmission.
/// </summary>
public partial class AudioCodecSettings : ObservableObject
{
    [ObservableProperty]
    private PreferredCodec _codec = PreferredCodec.Auto;

    [ObservableProperty]
    private string _bitrate = "Auto";

    [ObservableProperty]
    private int _sampleRate = 44100;

    [ObservableProperty]
    private TransmissionMode _transmissionMode = TransmissionMode.Balanced;

    // Available options for UI binding
    public static PreferredCodec[] CodecOptions { get; } = [
        PreferredCodec.Auto,
        PreferredCodec.SBC,
        PreferredCodec.AAC,
        PreferredCodec.AptX,
        PreferredCodec.LDAC
    ];

    public static string[] BitrateOptions { get; } = [
        "Auto", "64k", "96k", "128k", "192k", "256k", "320k", "512k", "990k"
    ];

    public static int[] SampleRateOptions { get; } = [
        44100, 48000
    ];

    public static TransmissionMode[] TransmissionOptions { get; } = [
        TransmissionMode.Balanced,
        TransmissionMode.BestQuality,
        TransmissionMode.LowLatency
    ];
}
