using System.IO;
using System.Text.Json;
using AkiLink.Models;

namespace AkiLink.Services;

/// <summary>
/// JSON-file-backed settings persistence in %APPDATA%\AkiLink\settings.json.
/// Save() updates the in-memory cache synchronously (so Load() always returns
/// fresh data) but coalesces the actual disk write onto a single background
/// flush loop — rapid changes (volume slider drags) no longer block the UI
/// thread with synchronous file I/O. Dispose() synchronously flushes any
/// pending write so the final save survives process exit.
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
    private readonly ReaderWriterLockSlim _cacheLock = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _pendingLock = new();
    private AppSettings? _cached;
    private AppSettings? _pendingWrite;
    private bool _flushScheduled;
    private Task? _flushTask;
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
        _cacheLock.EnterUpgradeableReadLock();
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
            _cacheLock.ExitUpgradeableReadLock();
        }
    }

    public void Save(AppSettings settings)
    {
        // Once disposed (app exit), drop saves instead of throwing on the
        // disposed locks — a late volume notification can otherwise fire an
        // ObjectDisposedException out of an async-void handler during shutdown.
        if (_disposed)
            return;

        // Cache is updated synchronously so Load() sees the freshest value even
        // before the background flush reaches the disk.
        _cacheLock.EnterWriteLock();
        try
        {
            _cached = settings;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        bool startFlush = false;
        lock (_pendingLock)
        {
            _pendingWrite = settings;
            if (!_flushScheduled)
            {
                _flushScheduled = true;
                startFlush = true;
            }
        }

        if (startFlush)
        {
            _flushTask = Task.Run(FlushLoopAsync);
        }
    }

    /// <summary>
    /// Drains the pending-write queue to disk, coalescing rapid saves into a
    /// single latest-value write per iteration.
    /// </summary>
    private async Task FlushLoopAsync()
    {
        while (true)
        {
            // If Dispose() already ran (app exiting), stop flushing to avoid
            // touching the disposed _writeGate from a background thread.
            if (_disposed)
                return;

            AppSettings? snapshot;
            lock (_pendingLock)
            {
                snapshot = _pendingWrite;
                if (snapshot is null)
                {
                    // Nothing pending — clear the scheduled flag and re-check under
                    // the same lock so a Save() that enqueued between the null-check
                    // and here can never be lost.
                    _flushScheduled = false;
                    if (_pendingWrite is not null)
                    {
                        _flushScheduled = true;
                        snapshot = _pendingWrite;
                    }
                }
                if (snapshot is not null)
                    _pendingWrite = null; // claim this value for writing
            }

            if (snapshot is null)
                return;

            await _writeGate.WaitAsync();
            try
            {
                WriteToDisk(snapshot);
            }
            catch
            {
                // Best-effort — a failed write should not crash the app.
            }
            finally
            {
                _writeGate.Release();
            }
        }
    }

    private void WriteToDisk(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        // Atomic write: write to temp then rename
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Let the background flush drain, then synchronously write anything
        // still pending so the final SaveSettings() on exit is not lost.
        Task? task;
        lock (_pendingLock)
        {
            task = _flushTask;
        }
        try { task?.Wait(TimeSpan.FromSeconds(5)); } catch { /* best-effort */ }

        AppSettings? pending;
        lock (_pendingLock)
        {
            pending = _pendingWrite;
            _pendingWrite = null;
        }
        if (pending is not null)
        {
            // Bounded wait: if the flush task is still mid-write after the 5s
            // drain above, do not block app shutdown indefinitely on a stuck
            // disk. Best-effort — a lost final write is preferable to a hang.
            if (!_writeGate.Wait(TimeSpan.FromSeconds(2)))
            {
                _pendingWrite = pending; // hand it back so a later flush retries
                _writeGate.Dispose();
                _cacheLock.Dispose();
                return;
            }
            try { WriteToDisk(pending); } catch { /* best-effort */ }
            finally { _writeGate.Release(); }
        }

        _writeGate.Dispose();
        _cacheLock.Dispose();
    }
}
