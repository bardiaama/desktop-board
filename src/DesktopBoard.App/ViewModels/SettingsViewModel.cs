using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopBoard.App.Services;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;

namespace DesktopBoard.App.ViewModels;

/// <summary>Bound to the Settings dialog. Every change is written immediately (no Save button).</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IStartupService _startup;
    private readonly IBackupService _backup;
    private readonly MainViewModel _main;
    private bool _initializing = true;

    public SettingsViewModel(ISettingsService settings, IStartupService startup, IBackupService backup, UiStrings strings, MainViewModel main)
    {
        _settings = settings;
        _startup = startup;
        _backup = backup;
        _main = main;
        Strings = strings;

        StartWithWindows = SafeIsStartupEnabled();
        BackgroundImagePath = settings.GetString(SettingKeys.BackgroundImagePath) ?? string.Empty;
        Darkness = settings.GetDouble(SettingKeys.BackgroundDarkness, 0.45);
        Blur = settings.GetDouble(SettingKeys.BackgroundBlur, 0.35);
        var chosenScale = settings.GetDouble(SettingKeys.UiScale, 0);
        UiScale = chosenScale > 0 ? chosenScale : Math.Round(main.EffectiveUiScale, 2);
        LanguageIndex = settings.GetString(SettingKeys.Language, "bilingual") switch { "fa" => 1, "en" => 2, _ => 0 };
        DefaultLocked = settings.GetBool(SettingKeys.DefaultLocked, true);
        HoldToUnlock = settings.GetBool(SettingKeys.HoldToUnlock, true);
        GlassEnabled = settings.GetBool(SettingKeys.GlassEnabled, true);
        InsetLeft = settings.GetDouble(SettingKeys.BoardInsetLeft, 0);
        DesktopModeIndex = settings.GetString(SettingKeys.DesktopHostMode, "auto") switch
        {
            "embedded" => 1, "bottommost" => 2, "normal" => 3, _ => 0
        };
        _initializing = false;

        UseWindowsWallpaperCommand = new AsyncRelayCommand(async () => { BackgroundImagePath = string.Empty; await Task.CompletedTask; });
        ResetLayoutCommand = new AsyncRelayCommand(async () =>
        {
            await _main.ResetLayoutAsync();
            _initializing = true;
            Darkness = 0.45; Blur = 0.35; UiScale = Math.Round(_main.EffectiveUiScale, 2);
            _initializing = false;
            Status = strings.ResetLayout + " ✓";
        });
    }

    public UiStrings Strings { get; }
    public string DataFolderPath => App.DataDirectory;

    public IAsyncRelayCommand UseWindowsWallpaperCommand { get; }
    public IAsyncRelayCommand ResetLayoutCommand { get; }

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _backgroundImagePath = string.Empty;
    [ObservableProperty] private double _darkness;
    [ObservableProperty] private double _blur;
    [ObservableProperty] private double _uiScale;
    [ObservableProperty] private int _languageIndex;
    [ObservableProperty] private bool _defaultLocked;
    [ObservableProperty] private bool _holdToUnlock;
    [ObservableProperty] private bool _glassEnabled;
    [ObservableProperty] private double _insetLeft;
    [ObservableProperty] private int _desktopModeIndex;
    [ObservableProperty] private string _status = string.Empty;

    public string BackgroundImageDisplay => string.IsNullOrEmpty(BackgroundImagePath) ? Strings.UseWindowsWallpaper : Path.GetFileName(BackgroundImagePath);

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_initializing) return;
        try
        {
            _startup.SetEnabled(value);
            _ = _settings.SetAsync(SettingKeys.StartWithWindows, value);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to update startup registration", ex);
            Status = Strings.Error + ": " + ex.Message;
        }
    }

    partial void OnBackgroundImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(BackgroundImageDisplay));
        if (_initializing) return;
        _ = _settings.SetAsync(SettingKeys.BackgroundImagePath, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    partial void OnDarknessChanged(double value) { if (!_initializing) _ = _settings.SetAsync(SettingKeys.BackgroundDarkness, Math.Round(value, 2)); }
    partial void OnBlurChanged(double value) { if (!_initializing) _ = _settings.SetAsync(SettingKeys.BackgroundBlur, Math.Round(value, 2)); }
    partial void OnUiScaleChanged(double value) { if (!_initializing) _ = _settings.SetAsync(SettingKeys.UiScale, Math.Round(value, 2)); }
    partial void OnDefaultLockedChanged(bool value) { if (!_initializing) _ = _settings.SetAsync(SettingKeys.DefaultLocked, value); }
    partial void OnHoldToUnlockChanged(bool value) { if (!_initializing) _ = _settings.SetAsync(SettingKeys.HoldToUnlock, value); }
    partial void OnGlassEnabledChanged(bool value) { if (!_initializing) _ = _settings.SetAsync(SettingKeys.GlassEnabled, value); }
    partial void OnInsetLeftChanged(double value) { if (!_initializing) _ = _settings.SetAsync(SettingKeys.BoardInsetLeft, Math.Round(value)); }

    partial void OnLanguageIndexChanged(int value)
    {
        if (_initializing) return;
        _ = _settings.SetAsync(SettingKeys.Language, value switch { 1 => "fa", 2 => "en", _ => "bilingual" });
    }

    partial void OnDesktopModeIndexChanged(int value)
    {
        if (_initializing) return;
        _ = _settings.SetAsync(SettingKeys.DesktopHostMode, value switch { 1 => "embedded", 2 => "bottommost", 3 => "normal", _ => "auto" });
        Status = Strings.RestartHint;
    }

    public async Task ExportAsync(string path)
    {
        try
        {
            await _main.FlushAsync();
            await _backup.ExportAsync(path);
            Status = Strings.BackupDone + " " + path;
        }
        catch (Exception ex)
        {
            Logger.Error("Export failed", ex);
            Status = Strings.Error + ": " + ex.Message;
        }
    }

    public async Task ImportAsync(string path)
    {
        try
        {
            await _backup.ImportAsync(path);
            await _main.ReloadAllAsync();
            Status = Strings.RestoreDone;
        }
        catch (Exception ex)
        {
            Logger.Error("Import failed", ex);
            Status = Strings.Error + ": " + ex.Message;
        }
    }

    private bool SafeIsStartupEnabled()
    {
        try { return _startup.IsEnabled(); }
        catch (Exception ex) { Logger.Warn("Startup check failed: " + ex.Message); return false; }
    }
}
