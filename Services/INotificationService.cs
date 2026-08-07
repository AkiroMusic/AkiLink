namespace AkiLink.Services;

/// <summary>
/// Abstraction over desktop notifications (system tray balloon tips) so the
/// ViewModel can surface background events without depending on a concrete
/// tray implementation, keeping the behavior unit-testable.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows an informational desktop notification with the given title and body.
    /// No-op safe: implementations must never throw.
    /// </summary>
    void ShowNotification(string title, string message);
}
