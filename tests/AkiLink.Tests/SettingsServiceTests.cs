using System.IO;
using AkiLink.Models;
using AkiLink.Services;

namespace AkiLink.Tests;

/// <summary>
/// Tests for SettingsService's async-coalesced persistence. Uses the internal
/// (string filePath) constructor so tests write to a unique temp file and never
/// touch the real %APPDATA%\AkiLink\settings.json.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AkiLinkTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var service = new SettingsService(_filePath);

        var settings = service.Load();

        Assert.NotNull(settings);
        Assert.Equal(0.75f, settings.Volume);
        Assert.Equal("Auto", settings.Bitrate);
        Assert.Equal("en-US", settings.Language);
    }

    [Fact]
    public void Save_ThenLoad_SameInstance_ReturnsUpdatedCache()
    {
        var service = new SettingsService(_filePath);
        var settings = new AppSettings { Volume = 0.4f, IsMuted = true, Bitrate = "320k" };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(0.4f, loaded.Volume);
        Assert.True(loaded.IsMuted);
        Assert.Equal("320k", loaded.Bitrate);
    }

    [Fact]
    public void Save_ThenDispose_FlushesToDisk()
    {
        var service = new SettingsService(_filePath);
        var settings = new AppSettings { Volume = 0.4f, IsMuted = true, Language = "zh-CN" };

        service.Save(settings);
        service.Dispose(); // synchronous flush must write the pending value

        Assert.True(File.Exists(_filePath));
        var onDisk = File.ReadAllText(_filePath);
        Assert.Contains("0.4", onDisk);
        Assert.Contains("zh-CN", onDisk);
    }

    [Fact]
    public void Save_MultipleRapidWrites_CoalescesToLatestValue()
    {
        var service = new SettingsService(_filePath);
        // Simulate a slider drag: many rapid saves, only the last value matters.
        for (int i = 1; i <= 50; i++)
        {
            service.Save(new AppSettings { Volume = i / 100f });
        }

        service.Dispose();

        var onDisk = File.ReadAllText(_filePath);
        // Latest value (50/100 = 0.5) must be what ended up on disk.
        Assert.Contains("0.5", onDisk);
        Assert.DoesNotContain("0.1", onDisk);
    }

    [Fact]
    public void Load_WhenFileExists_ReadsPersistedSettings()
    {
        var json = """{"volume":0.35,"isMuted":true,"language":"zh-CN","codec":"AAC"}""";
        File.WriteAllText(_filePath, json);

        var service = new SettingsService(_filePath);
        var settings = service.Load();

        Assert.Equal(0.35f, settings.Volume);
        Assert.True(settings.IsMuted);
        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal("AAC", settings.Codec);
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsDefaults()
    {
        File.WriteAllText(_filePath, "{ not valid json !!!");

        var service = new SettingsService(_filePath);
        var settings = service.Load();

        Assert.Equal(0.75f, settings.Volume);
        Assert.Equal("Auto", settings.Bitrate);
    }
}
