namespace AkiLink.Services;

/// <summary>
/// Abstraction over platform dialog APIs (e.g., MessageBox) to enable
/// unit testing of ViewModel dialog interactions.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog with the given title and message.
    /// Returns true if the user confirmed, false otherwise.
    /// </summary>
    bool ShowConfirm(string title, string message);
}
