using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AkiLink;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Icon = LoadIconFromResource();
    }

    private static ImageSource? LoadIconFromResource()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/AkiLink.png", UriKind.Absolute);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage(uri);
            bitmap.Freeze(); // Cross-thread safe
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        // Minimize to tray instead of taskbar
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        // Minimize to tray on close instead of quitting
        // (user must use "Quit" from tray context menu to exit)
        e.Cancel = true;
        WindowState = WindowState.Minimized;
        Hide();
    }
}
