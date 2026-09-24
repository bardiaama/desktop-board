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
    /// <summary>Try Embedded first, then BottomMost.</summary>
    Auto = 0,
    /// <summary>Window is re-parented into the desktop (WorkerW) layer, behind desktop icons.</summary>
    Embedded = 1,
    /// <summary>Normal top-level window that is kept at the bottom of the Z-order.</summary>
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
    /// <summary>Full bounds (in physical pixels) of the monitor the board should cover.</summary>
    (int X, int Y, int Width, int Height) GetTargetBounds();
    DesktopHostMode CurrentMode { get; }
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
