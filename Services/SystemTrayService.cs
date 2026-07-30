using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;

namespace AkiLink.Services;

public class SystemTrayService : IDisposable
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

        // Double-click to restore
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

        // Last resort: extract from the assembly (if the .ico is the ApplicationIcon)
        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (icon is not null) return icon;
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

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false; // Reset after activation
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
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
        Application.Current.Shutdown();
    }
}
