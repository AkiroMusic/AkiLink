using AkiLink.Models;

namespace AkiLink.Services;

/// <summary>
/// Abstraction over persisted app settings for testability.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Load settings from persistent storage. Returns defaults on first run or error.
    /// </summary>
    AppSettings Load();

    /// <summary>
    /// Save settings to persistent storage.
    /// </summary>
    void Save(AppSettings settings);
}
