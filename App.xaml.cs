using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    public App()
    {
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
