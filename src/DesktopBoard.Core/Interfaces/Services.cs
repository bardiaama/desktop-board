using DesktopBoard.Core.Models;

namespace DesktopBoard.Core.Interfaces;

/// <summary>Runs schema migrations and first-run seeding. Called once at startup.</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync();
}

/// <summary>Typed, cached access to AppSettings. Changes are persisted immediately.</summary>
public interface ISettingsService
{
    Task LoadAsync();
    string? GetString(string key, string? defaultValue = null);
    bool GetBool(string key, bool defaultValue = false);
    double GetDouble(string key, double defaultValue = 0);
    Task SetAsync(string key, string? value);
    Task SetAsync(string key, bool value);
    Task SetAsync(string key, double value);
    event EventHandler<string>? SettingChanged;
}

/// <summary>The LOCKED / EDIT state of the board.</summary>
public interface IBoardStateService
{
    bool IsLocked { get; }
    bool IsEditMode => !IsLocked;
    void Lock();
    void Unlock();
    void Toggle();
    event EventHandler<bool>? LockStateChanged;
}

/// <summary>Export / import of the whole board as a JSON package.</summary>
public interface IBackupService
{
    Task<BoardSnapshot> CreateSnapshotAsync();
    Task ExportAsync(string filePath);
    /// <summary>Replaces all board data with the snapshot in the file. Settings are merged.</summary>
    Task ImportAsync(string filePath);
    Task RestoreAsync(BoardSnapshot snapshot);
}

public enum DesktopHostMode
{
    /// <summary>BottomMost (interactive) with Normal as the fallback.</summary>
    Auto = 0,
    /// <summary>
    /// Window is re-parented into the desktop (WorkerW) layer, behind desktop icons.
    /// Display-only: Windows delivers no mouse or keyboard input to windows behind the
    /// icon layer, so the board can be looked at but not edited in this mode.
    /// </summary>
    Embedded = 1,
    /// <summary>Top-level window kept at the bottom of the Z-order, directly above the wallpaper. Fully interactive.</summary>
    BottomMost = 2,
    /// <summary>A plain borderless window. No shell integration.</summary>
    Normal = 3
}

public sealed record DesktopHostResult(DesktopHostMode EffectiveMode, string Detail);

/// <summary>
/// Places the main window "inside" the Windows desktop. Isolated so it can be replaced
/// or disabled without touching the rest of the app.
/// </summary>
public interface IDesktopHostService
{
    DesktopHostResult Attach(nint hwnd, DesktopHostMode requestedMode);
    void Detach(nint hwnd);
    /// <summary>
    /// Bounds (physical pixels) the board should cover: the primary monitor's work area
    /// (taskbar excluded) minus the left strip reserved for desktop icons.
    /// </summary>
    (int X, int Y, int Width, int Height) GetTargetBounds();
    /// <summary>
    /// Left strip to leave free for desktop icons: measured from the icon grid when
    /// <paramref name="auto"/> is true, plus <paramref name="extraLogicalPx"/> (DPI-scaled).
    /// </summary>
    void SetLeftInset(bool auto, int extraLogicalPx);
    /// <summary>Full bounds (physical pixels) of the primary monitor, used to align the wallpaper copy.</summary>
    (int X, int Y, int Width, int Height) GetPrimaryMonitorBounds();
    DesktopHostMode CurrentMode { get; }
    /// <summary>
    /// Re-fits the window if the monitor geometry changed or the shell replaced its host
    /// window. Cheap when nothing changed; safe to call from a timer on the UI thread.
    /// </summary>
    bool RefreshIfChanged();
}

/// <summary>Reads the items on the Windows desktop (user + public) and launches them.</summary>
public interface IDesktopItemsProvider : IDisposable
{
    /// <param name="iconSizePx">Requested icon size in physical pixels.</param>
    Task<IReadOnlyList<DesktopItem>> GetItemsAsync(int iconSizePx);
    /// <summary>Raised (on a background thread) when the desktop folders change.</summary>
    event EventHandler? Changed;
    void Launch(DesktopItem item);
    void ShowInFolder(DesktopItem item);
}

/// <summary>Shows or hides the shell's own desktop icons (the "Show desktop icons" toggle).</summary>
public interface IShellIconsService
{
    bool AreVisible();
    void SetVisible(bool visible);
}

/// <summary>"Start Desktop Board with Windows" via the per-user Run key.</summary>
public interface IStartupService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <summary>Reads the wallpaper the user currently has set in Windows.</summary>
public interface ISystemWallpaperService
{
    string? GetCurrentWallpaperPath();
}
