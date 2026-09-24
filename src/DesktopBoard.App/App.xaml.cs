using DesktopBoard.App.Services;
using DesktopBoard.App.ViewModels;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Services;
using DesktopBoard.Data;
using DesktopBoard.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DesktopBoard.App;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Error("AppDomain unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Logger.Error("Unobserved task exception", e.Exception); e.SetObserved(); };

        Services = ConfigureServices();
    }

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; }

    /// <summary>
    /// %LOCALAPPDATA%\DesktopBoard - database, log and settings live here. The
    /// DESKTOPBOARD_DATA_DIR environment variable overrides it (portable use, tests, demos).
    /// </summary>
    public static string DataDirectory { get; } =
        Environment.GetEnvironmentVariable("DESKTOPBOARD_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopBoard");

    public static string DatabasePath => Path.Combine(DataDirectory, "board.db");

    public MainWindow? MainWindow => _window;

    private static IServiceProvider ConfigureServices()
    {
        Directory.CreateDirectory(DataDirectory);
        Logger.Initialize(DataDirectory);

        var services = new ServiceCollection();
        services.AddDesktopBoardData(DatabasePath);
        services.AddDesktopBoardWindows();

        services.AddSingleton<WallpaperImageService>();
        services.AddSingleton<UiStrings>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Run by the uninstaller (and usable by hand): give the desktop its icons back and
        // remove the Run key, then exit without showing the board.
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--uninstall-cleanup", StringComparison.OrdinalIgnoreCase)))
        {
            try { Services.GetRequiredService<IShellIconsService>().SetVisible(true); } catch (Exception ex) { Logger.Warn("cleanup: icons " + ex.Message); }
            try { Services.GetRequiredService<IStartupService>().SetEnabled(false); } catch (Exception ex) { Logger.Warn("cleanup: startup " + ex.Message); }
            Logger.Info("Uninstall cleanup done.");
            Exit();
            return;
        }

        try
        {
            await Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            await Services.GetRequiredService<ISettingsService>().LoadAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Database initialization failed", ex);
        }

        _window = new MainWindow();
        _window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled XAML exception: " + e.Message, e.Exception);
        // Keep the board alive for non-fatal UI errors.
        e.Handled = true;
    }
}
