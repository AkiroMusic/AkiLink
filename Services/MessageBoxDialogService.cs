using System.Windows;

namespace AkiLink.Services;

/// <summary>
/// MessageBox-based implementation of <see cref="IDialogService"/> for production.
/// Registered in DI so the ViewModel's confirmation dialogs actually show.
/// </summary>
public sealed class MessageBoxDialogService : IDialogService
{
    public bool ShowConfirm(string title, string message)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}
