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

            // Adjust maximized size to account for WindowChrome border offset
            // Without this, the window extends ~8px beyond the screen edge on each side
            var screenArea = SystemParameters.WorkArea;
            mmi.ptMaxPosition.x = (int)screenArea.Left;
            mmi.ptMaxPosition.y = (int)screenArea.Top;
            mmi.ptMaxSize.x = (int)screenArea.Width;
            mmi.ptMaxSize.y = (int)screenArea.Height;

            Marshal.StructureToPtr(mmi, lParam, false);
            handled = true;
        }

        return IntPtr.Zero;
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

        if (vm?.CloseToTray == true)
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
