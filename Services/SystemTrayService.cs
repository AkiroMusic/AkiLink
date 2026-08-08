using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;

namespace AkiLink.Services;

public class SystemTrayService : INotificationService, IDisposable
{
    private readonly ILogger<SystemTrayService>? _logger;
    private TaskbarIcon? _taskbarIcon;
    private Window _mainWindow;
    private bool _disposed;

    public SystemTrayService(Window mainWindow, ILogger<SystemTrayService>? logger = null)
    {
        _mainWindow = mainWindow;
        _logger = logger;
    }

    public void Initialize()
    {
        // Load embedded icon for the tray
        var icon = LoadIconFromResource();

        // Create TaskbarIcon
        _taskbarIcon = new TaskbarIcon
        {
            Icon = icon,
            ToolTipText = "AkiLink - Bluetooth Audio Receiver",
            Visibility = Visibility.Visible
        };

        // Create context menu
        var contextMenu = new ContextMenu();

        var showMenuItem = new MenuItem
        {
            Header = "Show Window (_Show)",
            IsCheckable = false
        };
        showMenuItem.Click += (_, _) => ShowWindow();
        contextMenu.Items.Add(showMenuItem);

        contextMenu.Items.Add(new Separator());

        var quitMenuItem = new MenuItem
        {
            Header = "Quit (_Quit)"
        };
        quitMenuItem.Click += (_, _) => QuitApplication();
        contextMenu.Items.Add(quitMenuItem);

        _taskbarIcon.ContextMenu = contextMenu;

        // Left-click to show/restore the window
        _taskbarIcon.TrayLeftMouseUp += (_, _) => ShowWindow();
        _taskbarIcon.TrayBalloonTipClicked += (_, _) => ShowWindow();
        _taskbarIcon.PreviewTrayContextMenuOpen += (_, _) => { /* keep focus */ };
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Drawing.Icon LoadIconFromResource()
    {
        try
        {
            var stream = Application.GetResourceStream(
                new Uri("Resources/AkiLink.ico", UriKind.Relative))?.Stream;
            if (stream is not null)
            {
                using (stream)
                    return new System.Drawing.Icon(stream);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load icon from embedded resource");
        }

        // Last resort: extract from the app executable (if the .ico is the ApplicationIcon).
        // Use Environment.ProcessPath — Assembly.Location is empty in single-file publish.
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null) return icon;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract icon from assembly");
        }

        // Absolute last resort — 16×16 transparent icon
        using var bmp = new System.Drawing.Bitmap(16, 16);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void ShowWindow()
    {
        if (_mainWindow == null) return;

        // Only force the window to front when restoring from the tray.
        // When the window is already visible and active (e.g. user clicked
        // the tray icon while the app is on screen), keep the Topmost
        // toggling out of the way to avoid an unwanted flash on top.
        var wasHidden = !_mainWindow.IsVisible;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;

        if (wasHidden)
        {
            _mainWindow.Activate();
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false; // Reset after activation
        }
        else
        {
            _mainWindow.Activate();
        }
    }

    public void MinimizeToTray()
    {
        if (_mainWindow == null) return;

        _mainWindow.Hide();
        _taskbarIcon?.ShowBalloonTip(
            "AkiLink",
            "Application minimized to tray. Audio connection continues.",
            BalloonIcon.Info);
    }

    /// <summary>
    /// Shows an informational balloon notification via the tray icon.
    /// Best-effort: silently no-ops if the icon is unavailable or disposed,
    /// so callers (e.g. background connection events) never observe a throw.
    /// </summary>
    public void ShowNotification(string title, string message)
    {
        try
        {
            _taskbarIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }
        catch
        {
            // Notifications are non-critical; never let a toast failure
            // propagate into the connection state handling path.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_taskbarIcon != null)
        {
            _taskbarIcon.Dispose();
            _taskbarIcon = null;
        }

        _mainWindow = null!;
    }

    private void QuitApplication()
    {
        // Mark the window so MainWindow.OnClosing does not intercept the real quit
        // as a "close to tray" (which would cancel Shutdown and leave an invisible,
        // icon-less zombie process). The tray icon is disposed by App.OnExit via
        // SystemTrayService.Dispose().
        if (_mainWindow is MainWindow mainWindow)
        {
            mainWindow.AllowClose = true;
        }

        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
        Application.Current.Shutdown();
    }
}
