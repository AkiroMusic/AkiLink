using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AkiLink;

public partial class MainWindow : Window
{
    /// <summary>
    /// Set by SystemTrayService.QuitApplication before Application.Shutdown so
    /// OnClosing does not intercept the real quit as a "close to tray" (which
    /// would cancel the shutdown and leave an invisible, icon-less process).
    /// </summary>
    internal bool AllowClose { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Icon = LoadIconFromResource();

        // Fix maximized window extending past the taskbar with WindowChrome
        SourceInitialized += OnSourceInitialized;
    }

    // ─── Maximize fix (WM_GETMINMAXINFO) ─────────────────

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        hwndSource?.AddHook(WndProc);
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            // Adjust maximized size to account for WindowChrome border offset.
            // Without this, the window extends ~8px beyond the screen edge on each side.
            // Use the work area of the monitor the window is actually on (secondary
            // monitors have their own work area; SystemParameters.WorkArea is only
            // the primary monitor).
            var workArea = GetMonitorWorkArea(hwnd);
            mmi.ptMaxPosition.x = (int)workArea.Left;
            mmi.ptMaxPosition.y = (int)workArea.Top;
            mmi.ptMaxSize.x = (int)workArea.Width;
            mmi.ptMaxSize.y = (int)workArea.Height;

            Marshal.StructureToPtr(mmi, lParam, false);
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Returns the work area (monitor minus taskbar) for the monitor the window
    /// currently resides on, falling back to the primary work area if the Win32
    /// query fails. Fixes maximize misbehavior on secondary monitors.
    /// </summary>
    private static System.Windows.Rect GetMonitorWorkArea(IntPtr hwnd)
    {
        var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (hMonitor != IntPtr.Zero && GetMonitorInfo(hMonitor, ref info))
        {
            var r = info.rcWork;
            return new System.Windows.Rect(r.left, r.top, r.right - r.left, r.bottom - r.top);
        }
        return SystemParameters.WorkArea;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private static ImageSource? LoadIconFromResource()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/AkiLink.png", UriKind.Absolute);
            var bitmap = new BitmapImage(uri);
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    // ─── Custom title bar ─────────────────────────────────

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                // Double-click on title bar toggles maximize/restore
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        // Update maximize/restore button visual
        if (MaximizeButton != null)
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        }

        // Minimize to tray instead of taskbar
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        var vm = DataContext as ViewModels.MainViewModel;

        // Intercept close as "minimize to tray" ONLY when the user actually chose
        // to close the window and close-to-tray is enabled. A real quit from the
        // system tray (SystemTrayService.QuitApplication) sets AllowClose so the
        // Application.Shutdown() is not cancelled here — otherwise the process
        // survives as an invisible, icon-less zombie.
        if (vm?.CloseToTray == true && !AllowClose)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
            return;
        }

        // When actually exiting, disconnect Bluetooth on the UI thread first —
        // before the WPF Dispatcher starts shutting down — so the AudioPlaybackConnection
        // teardown runs cleanly. App.OnExit handles the final Dispose() as a safety net.
        if (vm?.IsConnected == true)
        {
            vm.DisconnectCommand.Execute(null);
        }
    }
}
