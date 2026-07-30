using System.Windows;
using System.Windows.Threading;
using AkiLink.Services;
using AkiLink.ViewModels;

namespace AkiLink;

public partial class App : Application
{
    private IBluetoothAudioService? _btService;
    private IAudioVolumeService? _volumeService;
    private MainViewModel? _viewModel;
    private SystemTrayService? _trayService;

    public App()
    {
        // Global exception handlers for diagnostics
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskUnobservedException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Initialize localization (loads en-US by default)
            _ = LocalizationService.Instance;

            // Build dependency graph
            _volumeService = new AudioVolumeService();
            _btService = new BluetoothAudioService();
            _viewModel = new MainViewModel(_btService, _volumeService);

            // Create main window
            var mainWindow = new MainWindow
            {
                DataContext = _viewModel
            };

            // System tray
            _trayService = new SystemTrayService(mainWindow);
            _trayService.Initialize();

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AkiLink] Startup failed: {ex}");

            var fullMessage = ex.Message;
            if (ex.InnerException != null)
            {
                fullMessage += $"\n\n── Inner Exception ──\n{ex.InnerException.Message}";
                if (ex.InnerException.InnerException != null)
                    fullMessage += $"\n\n── Inner Inner Exception ──\n{ex.InnerException.InnerException.Message}";
            }

            MessageBox.Show(
                $"Application failed to start:\n\n{fullMessage}",
                "AkiLink - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        (_btService as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[AkiLink] Dispatcher exception: {e.Exception}");
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[AkiLink] AppDomain unhandled: {e.ExceptionObject}");
    }

    private void OnTaskUnobservedException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[AkiLink] Task unobserved exception: {e.Exception?.InnerException}");
        e.SetObserved();
    }
}
