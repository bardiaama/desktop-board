using DesktopBoard.App.ViewModels;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopBoard.App;

/// <summary>
/// The single borderless window. It sizes itself to the primary monitor and hands its HWND
/// to <see cref="IDesktopHostService"/>, which places it in the desktop layer.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly IDesktopHostService _desktopHost;
    private readonly ISettingsService _settings;
    private readonly ScaleTransform _scale = new();
    private bool _attached;
    private bool _loaded;

    public MainWindow()
    {
        InitializeComponent();
        _desktopHost = App.Current.Services.GetRequiredService<IDesktopHostService>();
        _settings = App.Current.Services.GetRequiredService<ISettingsService>();

        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ApplyInsetSetting();
        _settings.SettingChanged += (_, key) =>
        {
            if (key is SettingKeys.BoardInsetLeft or SettingKeys.DockEnabled)
                DispatcherQueue.TryEnqueue(() => { ApplyInsetSetting(); if (_desktopHost.RefreshIfChanged()) ApplyScale(); });
        };
        ConfigureWindow();

        Board.RenderTransform = _scale;
        Board.ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.UiScale)) ApplyScale();
        };

        Activated += OnActivated;
        Closed += OnClosed;

        // A WS_CHILD window never receives WM_DISPLAYCHANGE, so dock/undock and resolution
        // changes are detected by comparing monitor geometry every few seconds (a single
        // GetMonitorInfo call; no work when nothing changed).
        _displayWatch = DispatcherQueue.CreateTimer();
        _displayWatch.Interval = TimeSpan.FromSeconds(1.5);
        _displayWatch.IsRepeating = true;
        _displayWatch.Tick += (_, _) => { if (_attached && _desktopHost.RefreshIfChanged()) ApplyScale(); };
        _displayWatch.Start();
    }

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _displayWatch;

    public nint Hwnd { get; }

    /// <summary>Physical height of the area the board covers; drives the automatic UI scale.</summary>
    public int TargetHeightPx { get; private set; }

    private bool ForcedNormalMode { get; set; }

    /// <summary>
    /// With the dock on, the board owns the whole work area and the shell icons are hidden.
    /// Otherwise the icon strip is measured automatically and the setting adds extra pixels.
    /// </summary>
    private void ApplyInsetSetting()
    {
        var dock = _settings.GetBool(SettingKeys.DockEnabled, true);
        var extraLogical = Math.Clamp(_settings.GetDouble(SettingKeys.BoardInsetLeft, 0), 0, 400);
        _desktopHost.SetLeftInset(auto: !dock, extraLogicalPx: dock ? 0 : (int)Math.Round(extraLogical));
        ApplyShellIcons(dock);
    }

    private bool _shellIconsHiddenByUs;

    private void ApplyShellIcons(bool dock)
    {
        try
        {
            var shell = App.Current.Services.GetRequiredService<IShellIconsService>();
            if (dock)
            {
                if (shell.AreVisible())
                {
                    shell.SetVisible(false);
                    _shellIconsHiddenByUs = true;
                    _ = _settings.SetAsync(SettingKeys.ShellIconsWereVisible, true);
                }
                else if (_settings.GetBool(SettingKeys.ShellIconsWereVisible))
                {
                    _shellIconsHiddenByUs = true; // hidden by a previous run that did not exit cleanly
                }
            }
            else if (_shellIconsHiddenByUs || _settings.GetBool(SettingKeys.ShellIconsWereVisible))
            {
                shell.SetVisible(true);
                _shellIconsHiddenByUs = false;
                _ = _settings.SetAsync(SettingKeys.ShellIconsWereVisible, false);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Shell icon toggle failed: " + ex.Message);
        }
    }

    private void ConfigureWindow()
    {
        var appWindow = AppWindow;
        appWindow.Title = "Desktop Board";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "DesktopBoard.ico"));

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = false;
        appWindow.SetPresenter(presenter);

        var (x, y, w, h) = _desktopHost.GetTargetBounds();

        // "--size=1920x1080" (debugging aid) previews the layout at another resolution in a normal window.
        var sizeArg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--size=", StringComparison.OrdinalIgnoreCase));
        if (sizeArg is not null)
        {
            var parts = sizeArg["--size=".Length..].Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var pw) && int.TryParse(parts[1], out var ph))
            {
                x = 0;
                y = 0;
                w = pw;
                h = ph;
                ForcedNormalMode = true;
            }
        }

        appWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(x, y, w, h));
        TargetHeightPx = h;
        Logger.Info($"Window sized to {w}x{h} at ({x},{y})");
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_attached) return;
        _attached = true;
        AttachToDesktop();
    }

    private void AttachToDesktop()
    {
        // "--host=normal|bottommost|embedded|auto" on the command line overrides the setting (debugging aid).
        var arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--host=", StringComparison.OrdinalIgnoreCase));
        var modeText = ForcedNormalMode ? "normal" : arg?["--host=".Length..] ?? _settings.GetString(SettingKeys.DesktopHostMode, "auto") ?? "auto";
        var mode = modeText.ToLowerInvariant() switch
        {
            "embedded" => DesktopHostMode.Embedded,
            "bottommost" => DesktopHostMode.BottomMost,
            "normal" => DesktopHostMode.Normal,
            _ => DesktopHostMode.Auto
        };

        try
        {
            // AppWindow stops being reachable once the HWND is a child of the desktop, so
            // decide the switcher visibility before attaching.
            AppWindow.IsShownInSwitchers = mode == DesktopHostMode.Normal;
            var result = _desktopHost.Attach(Hwnd, mode);
            Logger.Info($"Desktop host: requested {mode}, effective {result.EffectiveMode}. {result.Detail}");
        }
        catch (Exception ex)
        {
            Logger.Error("Desktop host attach failed; continuing as a normal window", ex);
        }
    }

    private async void Board_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        ApplyScale();
        try
        {
            await Board.ViewModel.LoadAsync();
            Logger.Info("Board loaded");
            _ = Board.ViewModel.Dock.LoadAsync(Content?.XamlRoot?.RasterizationScale ?? 1.0);
            await RunOnboardingAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Board load failed", ex);
        }
    }

    /// <summary>First run only: ask whether to start with Windows.</summary>
    private async Task RunOnboardingAsync()
    {
        if (_settings.GetBool(SettingKeys.OnboardingDone)) return;
        var strings = Board.ViewModel.Strings;
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = strings.OnboardingTitle,
            Content = new TextBlock { Text = strings.OnboardingBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 },
            PrimaryButtonText = strings.Yes,
            CloseButtonText = strings.NotNow,
            DefaultButton = ContentDialogButton.Primary,
            FlowDirection = strings.Language == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                App.Current.Services.GetRequiredService<IStartupService>().SetEnabled(true);
                await _settings.SetAsync(SettingKeys.StartWithWindows, true);
            }
            catch (Exception ex)
            {
                Logger.Error("Enabling startup failed", ex);
            }
        }
        await _settings.SetAsync(SettingKeys.OnboardingDone, true);
    }

    private void Host_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyScale();

    /// <summary>
    /// UI scale: the board is laid out at (size / scale) and rendered scaled up. When the user
    /// has not chosen a scale, it follows the screen height so a 1440p or 4K desktop does not
    /// get the 1080p layout with tiny text.
    /// </summary>
    private void ApplyScale()
    {
        if (Host.ActualWidth <= 0 || Host.ActualHeight <= 0) return;
        // Automatic scale: the 1920x1080 design must fit both ways, so the smaller ratio wins.
        var chosen = Board.ViewModel.UiScale;
        var auto = Math.Min(Host.ActualWidth / 1920.0, Host.ActualHeight / 1080.0);
        var s = chosen > 0 ? Math.Clamp(chosen, 0.5, 2.0) : Math.Clamp(auto, 0.6, 1.6);
        Board.ViewModel.EffectiveUiScale = s;
        _scale.ScaleX = s;
        _scale.ScaleY = s;
        Board.Width = Host.ActualWidth / s;
        Board.Height = Host.ActualHeight / s;
        AlignWallpaper(s);
    }

    /// <summary>
    /// Lays the wallpaper copy out over the whole monitor (in board units) so the image behind
    /// the cards continues the real desktop around the window instead of being re-scaled to it.
    /// </summary>
    private void AlignWallpaper(double scale)
    {
        try
        {
            var dpi = Content?.XamlRoot?.RasterizationScale ?? 1.0;
            if (dpi <= 0) dpi = 1.0;
            var mon = _desktopHost.GetPrimaryMonitorBounds();
            var win = _desktopHost.CurrentMode == DesktopHostMode.Normal
                ? (X: mon.X, Y: mon.Y, Width: mon.Width, Height: mon.Height)
                : _desktopHost.GetTargetBounds();
            var units = dpi * scale; // physical px per board unit
            Board.ViewModel.WallpaperWidth = mon.Width / units;
            Board.ViewModel.WallpaperHeight = mon.Height / units;
            Board.ViewModel.WallpaperMargin = new Thickness(-(win.X - mon.X) / units, -(win.Y - mon.Y) / units, 0, 0);
        }
        catch (Exception ex)
        {
            Logger.Warn("Wallpaper alignment failed: " + ex.Message);
        }
    }

    private bool _flushed;

    /// <summary>Cancels the first close, writes pending edits, then really closes.</summary>
    private async void OnClosed(object sender, WindowEventArgs args)
    {
        if (_flushed)
        {
            // Give the desktop its icons back when the board goes away.
            if (_shellIconsHiddenByUs) ApplyShellIcons(dock: false);
            _desktopHost.Detach(Hwnd);
            Logger.Info("---- session end ----");
            return;
        }

        args.Handled = true;
        try
        {
            await Board.ViewModel.FlushAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Flush on close failed", ex);
        }
        _flushed = true;
        Close();
    }
}
