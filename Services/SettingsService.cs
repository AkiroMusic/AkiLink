using System.IO;
using System.Text.Json;
using AkiLink.Models;

namespace AkiLink.Services;

/// <summary>
/// JSON-file-backed settings persistence in %APPDATA%\AkiLink\settings.json.
/// Thread-safe for concurrent reads/writes from background saves.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly string _directory;
    private readonly ReaderWriterLockSlim _lock = new();
    private AppSettings? _cached;
    private bool _disposed;

    public SettingsService()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AkiLink");
        _filePath = Path.Combine(_directory, "settings.json");
    }

    /// <summary>
    /// Internal constructor for testing with a custom path.
    /// </summary>
    internal SettingsService(string filePath)
    {
        _filePath = filePath;
        _directory = Path.GetDirectoryName(filePath)!;
    }

    public AppSettings Load()
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_cached is not null)
                return _cached;

            if (!File.Exists(_filePath))
                return _cached = new AppSettings();

            try
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                _cached = loaded ?? new AppSettings();
            }
            catch
            {
                // Corrupt file → return defaults
                _cached = new AppSettings();
            }

            return _cached;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public void Save(AppSettings settings)
    {
        _lock.EnterWriteLock();
        try
        {
            _cached = settings;

            Directory.CreateDirectory(_directory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            // Atomic write: write to temp then rename
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }
}
