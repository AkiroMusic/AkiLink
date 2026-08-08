using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using AkiLink.Services;
using AkiLink.ViewModels;

namespace AkiLink;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IBluetoothAudioService? _btService;
    private IAudioVolumeService? _volumeService;
    private ISettingsService? _settingsService;
    private MainViewModel? _viewModel;
    private SystemTrayService? _trayService;

    // Held for the entire process lifetime so a second launched instance can
    // detect us. The OS releases the kernel handle when the process exits, so
    // there is no leak even though we never dispose it explicitly.
    private System.Threading.Mutex? _singleInstanceMutex;

    // Mutex identifier for single-instance enforcement. Versioned so a future
    // incompatible release can force a fresh lock namespace if needed.
    private const string SingleInstanceMutexName = "AkiLink_SingleInstance_v1";

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskUnobservedException;
    }

    /// <summary>
    /// Activates the existing AkiLink main window when a duplicate instance is
    /// launched. Finds the already-running process, restores + foregrounds its
    /// main window, then returns so the new instance can shut itself down.
    /// Best-effort: if activation fails the duplicate instance still exits.
    /// </summary>
    private static void ActivateExistingInstance()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("AkiLink");
            foreach (var p in processes)
            {
                if (p.MainWindowHandle == IntPtr.Zero) continue;

                NativeMethods.ShowWindow(p.MainWindowHandle, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(p.MainWindowHandle);
                break;
            }
        }
        catch
        {
            // Best-effort activation — if this fails the duplicate instance still exits.
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ─── Single-instance guard ────────────────────────────────────────
        // A named Mutex is held for the lifetime of the process (stored in a
        // field, NOT disposed here — `using` would release it as soon as
        // OnStartup returns and defeat the whole guard). When a second instance
        // starts and cannot acquire it, we activate the existing window and exit
        // immediately — no second process, no second tray icon, no conflicting
        // AudioPlaybackConnection on the same adapter.
        _singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }
        // ──────────────────────────────────────────────────────────────────

        try
        {
            // Initialize localization (loads en-US by default)
            _ = LocalizationService.Instance;

            // Build DI container
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Resolve services from DI
            _volumeService = _serviceProvider.GetRequiredService<IAudioVolumeService>();
            _btService = _serviceProvider.GetRequiredService<IBluetoothAudioService>();
            _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();

            _viewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            // Create main window
            var mainWindow = new MainWindow
            {
                DataContext = _viewModel
            };

            // System tray
            _trayService = new SystemTrayService(
                mainWindow,
                _serviceProvider.GetService<ILogger<SystemTrayService>>());
            _trayService.Initialize();

            // Surface background connection events (connected / unexpected drop)
            // as tray balloon notifications. Attached here, not via DI, because
            // the tray service needs the Window which needs this ViewModel.
            _viewModel.AttachNotificationService(_trayService);

            mainWindow.Show();

            // Auto-connect to the last device on startup (if enabled).
            // Fire-and-forget: TryAutoConnectAsync is best-effort and swallows
            // its own exceptions, so a stale/offline device can't break startup.
            _ = _viewModel.TryAutoConnectAsync();
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

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IBluetoothPlatform, WinRtBluetoothPlatform>();
        services.AddSingleton<IBluetoothAudioService, BluetoothAudioService>();
        services.AddSingleton<IAudioVolumeService, AudioVolumeService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDialogService, MessageBoxDialogService>();
        services.AddTransient<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Save any pending settings before teardown
        _viewModel?.SaveSettings();

        _trayService?.Dispose();
        (_btService as IDisposable)?.Dispose();
        (_settingsService as IDisposable)?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider?.GetService<ILogger<App>>();
        if (logger is not null)
            logger.LogError(e.Exception, "Dispatcher exception");
        else
            System.Diagnostics.Debug.WriteLine($"[AkiLink] Dispatcher exception: {e.Exception}");
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider?.GetService<ILogger<App>>();
        if (logger is not null)
            logger.LogError("AppDomain unhandled: {ExceptionObject}", e.ExceptionObject);
        else
            System.Diagnostics.Debug.WriteLine($"[AkiLink] AppDomain unhandled: {e.ExceptionObject}");
    }

    private void OnTaskUnobservedException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var logger = _serviceProvider?.GetService<ILogger<App>>();
        if (logger is not null)
            logger.LogError(e.Exception?.InnerException, "Task unobserved exception");
        else
            System.Diagnostics.Debug.WriteLine($"[AkiLink] Task unobserved exception: {e.Exception?.InnerException}");
        e.SetObserved();
    }
}

/// <summary>
/// Win32 interop helpers used for single-instance window activation.
/// </summary>
internal static class NativeMethods
{
    /// <summary>Restores a minimized window and brings it to the foreground.</summary>
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Shows a window using the specified command (SW_RESTORE / SW_SHOW).</summary>
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public const int SW_RESTORE = 9;
}
