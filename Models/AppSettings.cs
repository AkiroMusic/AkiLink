using System.Text.Json.Serialization;

namespace AkiLink.Models;

/// <summary>
/// Persisted application settings. Serialized as JSON via SettingsService.
/// </summary>
public class AppSettings
{
    // ── Codec ──
    public string Codec { get; set; } = "Auto";
    public string Bitrate { get; set; } = "Auto";
    public int SampleRate { get; set; } = 44100;
    public string TransmissionMode { get; set; } = "Balanced";

    // ── Audio ──
    public float Volume { get; set; } = 0.75f;
    public bool IsMuted { get; set; }

    // ── Behaviour ──
    public bool AutoReconnect { get; set; }
    public bool AutoConnectOnStartup { get; set; }
    public string? LastDeviceId { get; set; }
    public bool CloseToTray { get; set; }

    // ── UI ──
    public string Language { get; set; } = "en-US";
}
